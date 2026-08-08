using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Reflection;
using System.Text;
using MapleLib.MapleCryptoLib;
using MapleLib.PacketLib;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class PacketLibAdversarialTests
{
    [Fact]
    public void PacketReader_UsesRequestedEncodingAndRejectsTruncatedString()
    {
        using var stream = new MemoryStream([2, 0, 0xC3, 0xA5]);
        using var reader = new PacketReader(stream, Encoding.UTF8, leaveOpen: true);

        Assert.Equal("å", reader.ReadMapleString());
        Assert.Throws<EndOfStreamException>(() => reader.ReadString(1));
    }

    [Fact]
    public void PacketWriter_StreamConstructorWritesToProvidedStream()
    {
        using var stream = new MemoryStream();
        using (var writer = new PacketWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.WriteMapleString("å");
            writer.Flush();
        }

        Assert.Equal([2, 0, 0xC3, 0xA5], stream.ToArray());
    }

    [Fact]
    public void PacketReader_NonMemoryStreamHonorsLeaveOpenOwnership()
    {
        string path = Path.GetTempFileName();
        var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
        try
        {
            stream.Write([0x2A]);
            stream.Position = 0;
            using (var reader = new PacketReader(stream, Encoding.ASCII, leaveOpen: false))
                Assert.Equal(0x2A, reader.ReadByte());

            Assert.False(stream.CanRead);
        }
        finally
        {
            stream.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void PacketWriter_RejectsEncodedMapleStringLengthOverflow()
    {
        using var stream = new MemoryStream();
        using var writer = new PacketWriter(stream, Encoding.UTF8, leaveOpen: true);
        Assert.Throws<ArgumentOutOfRangeException>(() => writer.WriteMapleString(new string('å', short.MaxValue)));
    }

    [Fact]
    public void Session_SendPacketDoesNotMutateCallerBuffer()
    {
        using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(1);
        using var sender = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        sender.Connect((IPEndPoint)listener.LocalEndPoint!);
        using Socket receiver = listener.Accept();

        var session = new Session(sender, SessionType.CLIENT_TO_SERVER)
        {
            SIV = new MapleCrypto([1, 2, 3, 4], 95)
        };
        byte[] packet = [1, 2, 3, 4, 5];
        byte[] expected = (byte[])packet.Clone();

        session.SendPacket(packet);

        Assert.Equal(expected, packet);
    }

    [Fact]
    public void PacketLengthProperties_RejectValuesThatWouldWrapShort()
    {
        using var writer = new PacketWriter();
        writer.WriteBytes(new byte[short.MaxValue + 1]);
        Assert.Throws<OverflowException>(() => _ = writer.Length);

        using var reader = new PacketReader(new byte[short.MaxValue + 1]);
        Assert.Throws<OverflowException>(() => _ = reader.Length);
    }

    [Fact]
    public void Session_ValidatesSocketHandshakeAndPacketArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new Session(null!, SessionType.CLIENT_TO_SERVER));
        using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(1);
        using var sender = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        sender.Connect((IPEndPoint)listener.LocalEndPoint!);
        using Socket receiver = listener.Accept();
        var session = new Session(sender, SessionType.CLIENT_TO_SERVER);

        Assert.Throws<ArgumentNullException>(() => session.SendInitialPacket(95, "", null!, [1, 2, 3, 4], 0));
        Assert.Throws<ArgumentException>(() => session.SendInitialPacket(95, "", [1, 2, 3], [1, 2, 3, 4], 0));
        Assert.Throws<ArgumentNullException>(() => session.SendPacket((byte[])null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => session.SendPacket(new byte[ushort.MaxValue + 1]));
    }

    [Fact]
    public void HexEncoding_RejectsNullAsciiInput()
    {
        Assert.Throws<ArgumentNullException>(() => HexEncoding.ToStringFromAscii(null!));
    }

    [Fact]
    public async Task Session_SerializesConcurrentEncryptedSends()
    {
        using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(1);
        using var sender = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        sender.Connect((IPEndPoint)listener.LocalEndPoint!);
        using Socket receiver = listener.Accept();

        byte[] iv = [1, 2, 3, 4];
        var session = new Session(sender, SessionType.CLIENT_TO_SERVER)
        {
            SIV = new MapleCrypto(iv, 95)
        };
        byte[][] payloads = Enumerable.Range(0, 24)
            .Select(index => Enumerable.Repeat((byte)index, 32).ToArray())
            .ToArray();

        await Task.WhenAll(payloads.Select(payload => Task.Run(() => session.SendPacket(payload))));

        byte[] received = new byte[payloads.Length * (4 + 32)];
        int offset = 0;
        while (offset < received.Length)
        {
            int count = receiver.Receive(received, offset, received.Length - offset, SocketFlags.None);
            Assert.True(count > 0, "The receiver closed before all packets arrived.");
            offset += count;
        }

        var decryptor = new MapleCrypto(iv, 95);
        HashSet<byte> observed = [];
        offset = 0;
        while (offset < received.Length)
        {
            int length = MapleCrypto.GetPacketLength(received.AsSpan(offset, 4).ToArray());
            Assert.Equal(32, length);
            byte[] body = received.AsSpan(offset + 4, length).ToArray();
            decryptor.Crypt(body);
            MapleCustomEncryption.Decrypt(body);
            Assert.All(body, value => Assert.Equal(body[0], value));
            Assert.True(observed.Add(body[0]), "A payload was duplicated or a send IV raced.");
            offset += 4 + length;
        }

        Assert.Equal(payloads.Length, observed.Count);
    }

    [Fact]
    public void Session_RejectsNonPositivePacketLength()
    {
        MethodInfo method = typeof(Session).GetMethod("ValidatePacketLength", BindingFlags.NonPublic | BindingFlags.Static)!;
        var exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, [0]));

        Assert.IsType<InvalidDataException>(exception.InnerException);
    }

    [Fact]
    public async Task RoleSessionProxy_ConcurrentClientsShareOneUpstreamPair()
    {
        using var remoteListener = new TcpListener(IPAddress.Loopback, 0);
        remoteListener.Start();
        int remotePort = ((IPEndPoint)remoteListener.LocalEndpoint).Port;
        var remoteClients = new ConcurrentBag<TcpClient>();
        var firstRemote = new TaskCompletionSource<TcpClient>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var remoteCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        async Task AcceptRemoteClientsAsync()
        {
            try
            {
                while (!remoteCancellation.IsCancellationRequested)
                {
                    TcpClient remoteClient = await remoteListener.AcceptTcpClientAsync(remoteCancellation.Token);
                    remoteClients.Add(remoteClient);
                    firstRemote.TrySetResult(remoteClient);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        Task remoteAcceptLoop = AcceptRemoteClientsAsync();

        using var proxy = new MapleRoleSessionProxy(MapleServerRole.Channel);
        Assert.True(proxy.Start(0, IPAddress.Loopback.ToString(), remotePort, out string status), status);

        TcpClient[] clients = Enumerable.Range(0, 8).Select(static _ => new TcpClient()).ToArray();
        try
        {
            await Task.WhenAll(clients.Select(client => client.ConnectAsync(IPAddress.Loopback, proxy.ListenPort)));

            await firstRemote.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Single(remoteClients);
            Assert.True(await WaitUntilAsync(() => proxy.ActiveSessionCount == 1, TimeSpan.FromSeconds(5)),
                "The accepted upstream connection was not published as the active proxy pair.");
        }
        finally
        {
            proxy.Stop();
            foreach (TcpClient client in clients)
                client.Dispose();

            remoteCancellation.Cancel();
            remoteListener.Stop();
            try
            {
                await remoteAcceptLoop;
            }
            catch (OperationCanceledException)
            {
            }

            foreach (TcpClient remoteClient in remoteClients)
                remoteClient.Dispose();
        }
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (!condition() && stopwatch.Elapsed < timeout)
            await Task.Delay(10);
        return condition();
    }
}

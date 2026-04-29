using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace MapleLib.PacketLib
{
    public sealed class MapleRoleSessionProxy : IDisposable
    {
        private sealed class BridgePair
        {
            public BridgePair(TcpClient clientTcpClient, TcpClient serverTcpClient, Session clientSession, Session serverSession)
            {
                ClientTcpClient = clientTcpClient;
                ServerTcpClient = serverTcpClient;
                ClientSession = clientSession;
                ServerSession = serverSession;
                RemoteEndpoint = serverTcpClient.Client.RemoteEndPoint?.ToString() ?? "unknown-remote";
                ClientEndpoint = clientTcpClient.Client.RemoteEndPoint?.ToString() ?? "unknown-client";
            }

            public TcpClient ClientTcpClient { get; }
            public TcpClient ServerTcpClient { get; }
            public Session ClientSession { get; }
            public Session ServerSession { get; }
            public string RemoteEndpoint { get; }
            public string ClientEndpoint { get; }
            public short Version { get; set; }
            public bool InitCompleted { get; set; }

            public void Close()
            {
                try
                {
                    ClientTcpClient.Close();
                }
                catch
                {
                }

                try
                {
                    ServerTcpClient.Close();
                }
                catch
                {
                }
            }
        }

        private readonly object _sync = new();
        private readonly MapleHandshakePolicy _handshakePolicy;
        private readonly MapleServerRole _role;

        private TcpListener _listener;
        private CancellationTokenSource _listenerCancellation;
        private Task _listenerTask;
        private BridgePair _activePair;

        public MapleRoleSessionProxy(MapleServerRole role, MapleHandshakePolicy handshakePolicy = null)
        {
            _role = role;
            _handshakePolicy = handshakePolicy ?? MapleHandshakePolicy.GlobalV95;
            ListenPort = 0;
            RemoteHost = IPAddress.Loopback.ToString();
            LastStatus = $"{_role} role-session proxy inactive.";
        }

        public event EventHandler<MapleSessionPacketEventArgs> ServerPacketReceived;
        public event EventHandler<MapleSessionPacketEventArgs> ClientPacketReceived;

        public int ListenPort { get; private set; }
        public string RemoteHost { get; private set; }
        public int RemotePort { get; private set; }
        public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;
        public bool HasAttachedClient => _activePair != null;
        public bool HasConnectedSession => _activePair?.InitCompleted == true;
        public int ReceivedCount { get; private set; }
        public int ClientReceivedCount { get; private set; }
        public int SentCount { get; private set; }
        public int ActiveSessionCount => _activePair == null ? 0 : 1;
        public DateTime? LastPacketUtc { get; private set; }
        public string LastStatus { get; private set; }

        public bool Start(int listenPort, string remoteHost, int remotePort, out string status)
        {
            lock (_sync)
            {
                string resolvedRemoteHost = string.IsNullOrWhiteSpace(remoteHost) ? IPAddress.Loopback.ToString() : remoteHost.Trim();
                if (IsRunning)
                {
                    if (ListenPort == listenPort
                        && RemotePort == remotePort
                        && string.Equals(RemoteHost, resolvedRemoteHost, StringComparison.OrdinalIgnoreCase))
                    {
                        status = $"{_role} role-session proxy already listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}.";
                        LastStatus = status;
                        return true;
                    }

                    status = $"{_role} role-session proxy is already authoritative for 127.0.0.1:{ListenPort} -> {RemoteHost}:{RemotePort}; rejected incompatible start request for 127.0.0.1:{listenPort} -> {resolvedRemoteHost}:{remotePort}.";
                    LastStatus = status;
                    return false;
                }

                StopInternal(resetCounters: true);

                try
                {
                    RemoteHost = resolvedRemoteHost;
                    RemotePort = remotePort;
                    _listenerCancellation = new CancellationTokenSource();
                    _listener = new TcpListener(IPAddress.Loopback, listenPort);
                    _listener.Start();
                    ListenPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
                    _listenerTask = Task.Run(() => ListenLoopAsync(_listenerCancellation.Token));
                    status = $"{_role} role-session proxy listening on 127.0.0.1:{ListenPort} and proxying to {RemoteHost}:{RemotePort}.";
                    LastStatus = status;
                    return true;
                }
                catch (Exception ex)
                {
                    StopInternal(resetCounters: true);
                    status = $"{_role} role-session proxy failed to start: {ex.Message}";
                    LastStatus = status;
                    return false;
                }
            }
        }

        public void Stop(bool resetCounters = true)
        {
            lock (_sync)
            {
                StopInternal(resetCounters);
                LastStatus = $"{_role} role-session proxy stopped.";
            }
        }

        public bool TrySendToServer(byte[] payload, out string status)
        {
            BridgePair pair = _activePair;
            if (pair == null || !pair.InitCompleted)
            {
                status = $"{_role} role-session proxy has no active Maple session.";
                LastStatus = status;
                return false;
            }

            try
            {
                pair.ServerSession.SendPacket((byte[])payload.Clone());
                SentCount++;
                status = $"Injected {payload?.Length ?? 0} byte(s) into {_role} role session {pair.RemoteEndpoint}.";
                LastStatus = status;
                return true;
            }
            catch (Exception ex)
            {
                status = $"{_role} role-session proxy injection failed: {ex.Message}";
                LastStatus = status;
                ClearActivePair(pair, status);
                return false;
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                StopInternal(resetCounters: true);
            }
        }

        private async Task ListenLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && _listener != null)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                    _ = Task.Run(() => AcceptClientAsync(client, cancellationToken), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                LastStatus = $"{_role} role-session proxy error: {ex.Message}";
            }
        }

        private async Task AcceptClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            BridgePair pair = null;
            try
            {
                lock (_sync)
                {
                    if (_activePair != null)
                    {
                        LastStatus = $"Rejected {_role} role-session client because a live Maple session is already attached.";
                        client.Close();
                        return;
                    }
                }

                TcpClient server = new();
                await server.ConnectAsync(RemoteHost, RemotePort, cancellationToken).ConfigureAwait(false);

                Session clientSession = new(client.Client, SessionType.SERVER_TO_CLIENT);
                Session serverSession = new(server.Client, SessionType.CLIENT_TO_SERVER);
                pair = new BridgePair(client, server, clientSession, serverSession);

                clientSession.OnPacketReceived += (packet, isInit) => HandleClientPacket(pair, packet, isInit);
                clientSession.OnClientDisconnected += _ => ClearActivePair(pair, $"{_role} role-session client disconnected: {pair.ClientEndpoint}.");
                serverSession.OnPacketReceived += (packet, isInit) => HandleServerPacket(pair, packet, isInit);
                serverSession.OnClientDisconnected += _ => ClearActivePair(pair, $"{_role} role-session server disconnected: {pair.RemoteEndpoint}.");

                lock (_sync)
                {
                    _activePair = pair;
                }

                LastStatus = $"{_role} role-session proxy connected {pair.ClientEndpoint} -> {pair.RemoteEndpoint}. Waiting for Maple init packet.";
                serverSession.WaitForDataNoEncryption();
            }
            catch (Exception ex)
            {
                client.Close();
                pair?.Close();
                LastStatus = $"{_role} role-session proxy connect failed: {ex.Message}";
            }
        }

        private void HandleServerPacket(BridgePair pair, PacketReader packet, bool isInit)
        {
            try
            {
                byte[] raw = packet.ToArray();
                if (isInit)
                {
                    PacketReader initReader = new(raw);
                    initReader.ReadShort();
                    short advertisedVersion = initReader.ReadShort();
                    string patchLocation = initReader.ReadMapleString();
                    byte[] clientSendIv = initReader.ReadBytes(4);
                    byte[] clientReceiveIv = initReader.ReadBytes(4);
                    byte serverType = initReader.ReadByte();

                    if (!_handshakePolicy.TryResolveSessionVersion(advertisedVersion, out short sessionVersion, out string versionError))
                    {
                        ClearActivePair(pair, $"{_role} role-session rejected init packet. {versionError}");
                        return;
                    }

                    pair.Version = sessionVersion;
                    pair.ClientSession.SIV = _handshakePolicy.CreateCrypto(clientReceiveIv, sessionVersion);
                    pair.ClientSession.RIV = _handshakePolicy.CreateCrypto(clientSendIv, sessionVersion);
                    pair.ClientSession.SendInitialPacket(sessionVersion, patchLocation, clientSendIv, clientReceiveIv, serverType);
                    pair.InitCompleted = true;
                    LastStatus = $"{_role} role-session initialized Maple crypto with version {sessionVersion} for {pair.ClientEndpoint} <-> {pair.RemoteEndpoint}.";
                    pair.ClientSession.WaitForData();
                    RaiseServerPacket(pair.RemoteEndpoint, raw, true, pair.Version);
                    return;
                }

                pair.ClientSession.SendPacket((byte[])raw.Clone());
                RaiseServerPacket(pair.RemoteEndpoint, raw, false, pair.Version);
                ReceivedCount++;
                LastPacketUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"{_role} role-session server handling failed: {ex.Message}");
            }
        }

        private void HandleClientPacket(BridgePair pair, PacketReader packet, bool isInit)
        {
            try
            {
                if (isInit)
                {
                    return;
                }

                byte[] raw = packet.ToArray();
                pair.ServerSession.SendPacket(raw);
                RaiseClientPacket(pair.ClientEndpoint, raw, false, pair.Version);
                ClientReceivedCount++;
                LastPacketUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                ClearActivePair(pair, $"{_role} role-session client handling failed: {ex.Message}");
            }
        }

        private void RaiseServerPacket(string sourceEndpoint, byte[] rawPacket, bool isInit, short? sessionVersion)
        {
            int opcode = TryDecodeOpcode(rawPacket, isInit);
            ServerPacketReceived?.Invoke(
                this,
                new MapleSessionPacketEventArgs(_role, sourceEndpoint, rawPacket, isInit, opcode, sessionVersion));
        }

        private void RaiseClientPacket(string sourceEndpoint, byte[] rawPacket, bool isInit, short? sessionVersion)
        {
            int opcode = TryDecodeOpcode(rawPacket, isInit);
            ClientPacketReceived?.Invoke(
                this,
                new MapleSessionPacketEventArgs(_role, sourceEndpoint, rawPacket, isInit, opcode, sessionVersion));
        }

        private static int TryDecodeOpcode(byte[] rawPacket, bool isInit)
        {
            if (isInit || rawPacket == null || rawPacket.Length < sizeof(ushort))
            {
                return -1;
            }

            return BitConverter.ToUInt16(rawPacket, 0);
        }

        private void ClearActivePair(BridgePair pair, string status)
        {
            lock (_sync)
            {
                if (_activePair == pair)
                {
                    _activePair = null;
                }
            }

            pair?.Close();
            LastStatus = status;
        }

        private void StopInternal(bool resetCounters)
        {
            try
            {
                _listenerCancellation?.Cancel();
            }
            catch
            {
            }

            try
            {
                _listener?.Stop();
            }
            catch
            {
            }

            try
            {
                _listenerTask?.Wait(100);
            }
            catch
            {
            }

            _activePair?.Close();
            _activePair = null;
            _listener = null;
            _listenerCancellation?.Dispose();
            _listenerCancellation = null;
            _listenerTask = null;

            if (resetCounters)
            {
                ReceivedCount = 0;
                ClientReceivedCount = 0;
                SentCount = 0;
                LastPacketUtc = null;
            }
        }
    }
}

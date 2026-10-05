using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace MapleLib.PacketLib
{
    /// <summary>
    /// Raised for each decrypted inbound packet on the active direct session.
    /// Events fire on socket completion threads; consumers must marshal state
    /// application onto their own game thread.
    /// </summary>
    public sealed class MapleDirectSessionPacketEventArgs : EventArgs
    {
        public MapleDirectSessionPacketEventArgs(
            MapleServerRole role,
            long generation,
            byte[] rawPacket,
            int opcode,
            string remoteEndpoint)
        {
            Role = role;
            Generation = generation;
            RawPacket = rawPacket ?? Array.Empty<byte>();
            Opcode = opcode;
            RemoteEndpoint = remoteEndpoint ?? string.Empty;
        }

        public MapleServerRole Role { get; }
        public long Generation { get; }
        public byte[] RawPacket { get; }
        public int Opcode { get; }
        public string RemoteEndpoint { get; }
    }

    /// <summary>Raised after the v95 handshake validated and armed crypto.</summary>
    public sealed class MapleDirectSessionHandshakeEventArgs : EventArgs
    {
        public MapleDirectSessionHandshakeEventArgs(
            MapleServerRole role,
            long generation,
            short sessionVersion,
            byte serverType,
            byte[] rawInitPacket,
            string remoteEndpoint)
        {
            Role = role;
            Generation = generation;
            SessionVersion = sessionVersion;
            ServerType = serverType;
            RawInitPacket = rawInitPacket ?? Array.Empty<byte>();
            RemoteEndpoint = remoteEndpoint ?? string.Empty;
        }

        public MapleServerRole Role { get; }
        public long Generation { get; }
        public short SessionVersion { get; }
        public byte ServerType { get; }
        public byte[] RawInitPacket { get; }
        public string RemoteEndpoint { get; }
    }

    /// <summary>Raised when an active direct session is retired.</summary>
    public sealed class MapleDirectSessionDisconnectedEventArgs : EventArgs
    {
        public MapleDirectSessionDisconnectedEventArgs(
            MapleServerRole role,
            long generation,
            string reason,
            bool expected)
        {
            Role = role;
            Generation = generation;
            Reason = reason ?? string.Empty;
            Expected = expected;
        }

        public MapleServerRole Role { get; }
        public long Generation { get; }
        public string Reason { get; }
        /// <summary>True when the local owner closed the session deliberately.</summary>
        public bool Expected { get; }
    }

    /// <summary>Connection or handshake failed for a direct session.</summary>
    public class MapleDirectSessionException : InvalidOperationException
    {
        public MapleDirectSessionException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Client-owned direct Maple session. Connects straight to a server without
    /// an external relay client attached and owns its own handshake, framing
    /// and crypto through <see cref="Session"/> in the client role.
    ///
    /// Migration follows the native close/connect contract observed at
    /// CWvsContext::IssueConnect (0x9e0300) and CClientSocket::Close
    /// (0x4ae990): calling <see cref="ConnectAsync"/> retires the current
    /// connection (socket + crypto context) before dialing the next endpoint,
    /// and every connection carries a unique monotonically increasing
    /// generation so stale packets from retired sessions can be rejected.
    /// </summary>
    public sealed class MapleClientDirectSession : IDisposable
    {
        private static readonly TimeSpan DefaultHandshakeTimeout = TimeSpan.FromSeconds(10);
        private static long s_nextGeneration;

        private readonly object _sync = new();
        private readonly MapleServerRole _role;
        private readonly MapleHandshakePolicy _handshakePolicy;

        private TcpClient _tcpClient;
        private Session _session;
        private long _generation;
        private bool _handshakeComplete;
        private bool _disconnected;
        private short _sessionVersion;
        private string _remoteEndpoint = string.Empty;
        private string _lastStatus = "Direct session inactive.";
        private TaskCompletionSource<MapleDirectSessionHandshakeEventArgs> _handshakeCompletion;
        private bool _connectInProgress;
        private bool _disposed;

        public MapleClientDirectSession(MapleServerRole role, MapleHandshakePolicy handshakePolicy = null)
        {
            _role = role;
            _handshakePolicy = handshakePolicy ?? MapleHandshakePolicy.GlobalV95;
        }

        public event EventHandler<MapleDirectSessionPacketEventArgs> PacketReceived;
        public event EventHandler<MapleDirectSessionHandshakeEventArgs> HandshakeCompleted;
        public event EventHandler<MapleDirectSessionDisconnectedEventArgs> Disconnected;

        public MapleServerRole Role => _role;
        public long? Generation
        {
            get
            {
                lock (_sync)
                    return _session != null ? _generation : null;
            }
        }
        public bool IsConnected
        {
            get
            {
                lock (_sync)
                    return _session != null && _handshakeComplete && !_disconnected;
            }
        }
        public short? SessionVersion
        {
            get
            {
                lock (_sync)
                    return _handshakeComplete ? _sessionVersion : null;
            }
        }
        public string RemoteEndpoint
        {
            get
            {
                lock (_sync)
                    return _remoteEndpoint;
            }
        }
        public string LastStatus
        {
            get
            {
                lock (_sync)
                    return _lastStatus;
            }
            private set
            {
                lock (_sync)
                    _lastStatus = value;
            }
        }

        /// <summary>
        /// Connects directly to the server. Any current connection is retired
        /// first (native IssueConnect contract). Throws
        /// <see cref="MapleDirectSessionException"/> for version mismatches and
        /// handshake failures, <see cref="OperationCanceledException"/> when
        /// <paramref name="cancellationToken"/> fires.
        /// </summary>
        public async Task ConnectAsync(
            string host,
            int port,
            CancellationToken cancellationToken = default,
            TimeSpan? handshakeTimeout = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(host);
            ArgumentOutOfRangeException.ThrowIfNegative(port);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(port, ushort.MaxValue);
            ThrowIfDisposed();

            lock (_sync)
            {
                if (_connectInProgress)
                    throw new InvalidOperationException($"{_role} direct session already has a connect attempt in progress.");
                RetireCurrentConnectionLocked("Retired before reconnect.");
                _connectInProgress = true;
            }

            // Generations are globally unique so a router can never mistake a
            // retired packet stream for the active one, even across instances.
            long generation = Interlocked.Increment(ref s_nextGeneration);
            TcpClient client = null;
            try
            {
                client = new TcpClient();
                client.NoDelay = true;
                await client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);

                string endpoint = client.Client.RemoteEndPoint?.ToString() ?? $"{host}:{port}";
                TaskCompletionSource<MapleDirectSessionHandshakeEventArgs> handshakeCompletion =
                    new(TaskCreationOptions.RunContinuationsAsynchronously);
                Session session;
                lock (_sync)
                {
                    ThrowIfDisposed();
                    _tcpClient = client;
                    _generation = generation;
                    _handshakeComplete = false;
                    _disconnected = false;
                    _sessionVersion = 0;
                    _remoteEndpoint = endpoint;
                    _handshakeCompletion = handshakeCompletion;
                    session = new Session(client.Client, SessionType.CLIENT_TO_SERVER);
                    _session = session;
                    session.OnPacketReceived += (packet, isInit) => OnSessionPacket(generation, packet, isInit);
                    session.OnClientDisconnected += _ => OnSessionDisconnected(generation);
                }

                LastStatus = $"{_role} direct session {generation} connecting to {endpoint}; awaiting v95 handshake.";
                session.WaitForDataNoEncryption();

                MapleDirectSessionHandshakeEventArgs handshake =
                    await handshakeCompletion.Task.WaitAsync(
                        handshakeTimeout ?? DefaultHandshakeTimeout,
                        cancellationToken).ConfigureAwait(false);

                LastStatus = $"{_role} direct session {generation} connected to {endpoint} with v{handshake.SessionVersion}.";
            }
            catch
            {
                lock (_sync)
                {
                    if (_generation == generation)
                        RetireCurrentConnectionLocked("Connect failed.");
                    _connectInProgress = false;
                }
                try
                {
                    client?.Close();
                }
                catch
                {
                }
                throw;
            }
            finally
            {
                lock (_sync)
                    _connectInProgress = false;
            }
        }

        /// <summary>
        /// Retires the current connection. Safe to call repeatedly; a
        /// deliberate close raises exactly one expected-disconnect event for
        /// the retired generation.
        /// </summary>
        public void Close(string reason = null)
        {
            ThrowIfDisposed();
            MapleDirectSessionDisconnectedEventArgs args;
            lock (_sync)
            {
                args = RetireCurrentConnectionLocked(reason ?? "Closed by owner.");
            }

            if (args != null)
                Disconnected?.Invoke(this, args);
        }

        /// <summary>
        /// Sends an encrypted packet body (opcode prefixed). Fails while the
        /// handshake is incomplete or the session is retired.
        /// </summary>
        public bool TrySendPacket(byte[] payload, out string error)
        {
            error = null;
            ArgumentNullException.ThrowIfNull(payload);
            ThrowIfDisposed();

            Session session;
            lock (_sync)
            {
                if (_session == null || !_handshakeComplete || _disconnected)
                {
                    error = $"{_role} direct session has no active v95 session to send on.";
                    return false;
                }
                session = _session;
            }

            try
            {
                session.SendPacket(payload);
                return true;
            }
            catch (Exception ex)
            {
                error = $"{_role} direct session send failed: {ex.Message}";
                Close(error);
                return false;
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                    return;
                _disposed = true;
                RetireCurrentConnectionLocked("Session disposed.");
            }
        }

        private void OnSessionPacket(long generation, PacketReader packet, bool isInit)
        {
            byte[] raw;
            try
            {
                raw = packet.ToArray();
            }
            catch (Exception ex)
            {
                LastStatus = $"{_role} direct session {generation} dropped malformed inbound data: {ex.Message}";
                return;
            }

            if (isInit)
            {
                HandleHandshakePacket(generation, raw);
                return;
            }

            EventHandler<MapleDirectSessionPacketEventArgs> handler;
            string endpoint;
            lock (_sync)
            {
                if (_session == null || _generation != generation || !_handshakeComplete)
                    return; // stale generation or retired connection
                handler = PacketReceived;
                endpoint = _remoteEndpoint;
            }

            handler?.Invoke(
                this,
                new MapleDirectSessionPacketEventArgs(_role, generation, raw, DecodeOpcode(raw), endpoint));
        }

        private void HandleHandshakePacket(long generation, byte[] raw)
        {
            short advertisedVersion;
            byte serverType;
            try
            {
                PacketReader reader = new(raw);
                advertisedVersion = reader.ReadShort();
                reader.ReadMapleString();
                reader.ReadBytes(4);
                reader.ReadBytes(4);
                serverType = reader.ReadByte();
            }
            catch (Exception ex)
            {
                FailHandshake(generation, $"Malformed v95 handshake: {ex.Message}");
                return;
            }

            if (!_handshakePolicy.TryResolveSessionVersion(advertisedVersion, out short sessionVersion, out string versionError))
            {
                FailHandshake(generation, versionError);
                return;
            }

            TaskCompletionSource<MapleDirectSessionHandshakeEventArgs> completion;
            lock (_sync)
            {
                if (_session == null || _generation != generation)
                    return; // retired mid-handshake
                _handshakeComplete = true;
                _sessionVersion = sessionVersion;
                completion = _handshakeCompletion;
            }

            var args = new MapleDirectSessionHandshakeEventArgs(_role, generation, sessionVersion, serverType, raw, RemoteEndpoint);
            completion?.TrySetResult(args);
            HandshakeCompleted?.Invoke(this, args);
        }

        private void FailHandshake(long generation, string error)
        {
            TaskCompletionSource<MapleDirectSessionHandshakeEventArgs> completion;
            lock (_sync)
            {
                if (_session == null || _generation != generation)
                    return;
                completion = _handshakeCompletion;
            }

            completion?.TrySetException(new MapleDirectSessionException(error));
            // Complete with the real failure first; the retirement below only
            // reports "closed before handshake" when no cause was recorded yet.
            Close(error);
        }

        private void OnSessionDisconnected(long generation)
        {
            MapleDirectSessionDisconnectedEventArgs args;
            lock (_sync)
            {
                if (_session == null || _generation != generation)
                    return; // already retired locally

                _disconnected = true;
                if (!_handshakeComplete)
                {
                    _handshakeCompletion?.TrySetException(
                        new MapleDirectSessionException($"{_role} direct session disconnected before the v95 handshake completed."));
                    return; // ConnectAsync performs the retirement
                }

                args = RetireCurrentConnectionLocked("Server closed the connection.", expected: false);
            }

            if (args != null)
                Disconnected?.Invoke(this, args);
        }

        /// <summary>Teardown only; caller holds <see cref="_sync"/>.</summary>
        private MapleDirectSessionDisconnectedEventArgs RetireCurrentConnectionLocked(string reason, bool expected = true)
        {
            if (_session == null)
                return null;

            TcpClient client = _tcpClient;
            long generation = _generation;
            _session = null;
            _tcpClient = null;
            _handshakeComplete = false;
            _handshakeCompletion?.TrySetException(
                new MapleDirectSessionException($"{_role} direct session closed before the handshake completed."));
            _handshakeCompletion = null;
            _lastStatus = $"{_role} direct session {generation} retired: {reason}";

            try
            {
                client?.Close();
            }
            catch
            {
            }

            // A locally initiated close owns the disconnect notification so the
            // Session's socket callback cannot double-report the same generation.
            return new MapleDirectSessionDisconnectedEventArgs(_role, generation, reason, expected);
        }

        private static int DecodeOpcode(byte[] rawPacket)
        {
            if (rawPacket == null || rawPacket.Length < sizeof(ushort))
                return -1;
            return BitConverter.ToUInt16(rawPacket, 0);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MapleClientDirectSession));
        }
    }
}

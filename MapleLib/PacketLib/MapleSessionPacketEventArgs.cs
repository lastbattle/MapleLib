using System;

namespace MapleLib.PacketLib
{
    public sealed class MapleSessionPacketEventArgs : EventArgs
    {
        public MapleSessionPacketEventArgs(
            MapleServerRole role,
            string sourceEndpoint,
            byte[] rawPacket,
            bool isInit,
            int opcode,
            short? sessionVersion = null,
            long? proxySessionId = null)
        {
            Role = role;
            SourceEndpoint = sourceEndpoint ?? string.Empty;
            RawPacket = rawPacket ?? Array.Empty<byte>();
            IsInit = isInit;
            Opcode = opcode;
            SessionVersion = sessionVersion;
            ProxySessionId = proxySessionId;
        }

        public MapleServerRole Role { get; }
        public string SourceEndpoint { get; }
        public byte[] RawPacket { get; }
        public bool IsInit { get; }
        public int Opcode { get; }
        public short? SessionVersion { get; }
        public long? ProxySessionId { get; }
    }
}

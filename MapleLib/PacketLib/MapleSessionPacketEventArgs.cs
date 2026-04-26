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
            int opcode)
        {
            Role = role;
            SourceEndpoint = sourceEndpoint ?? string.Empty;
            RawPacket = rawPacket ?? Array.Empty<byte>();
            IsInit = isInit;
            Opcode = opcode;
        }

        public MapleServerRole Role { get; }
        public string SourceEndpoint { get; }
        public byte[] RawPacket { get; }
        public bool IsInit { get; }
        public int Opcode { get; }
    }
}

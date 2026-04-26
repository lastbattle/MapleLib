using System;

namespace MapleLib.PacketLib
{
    using MapleLib.MapleCryptoLib;

    public sealed class MapleHandshakePolicy
    {
        public static readonly MapleHandshakePolicy GlobalV95 = new MapleHandshakePolicy(95, true);

        public MapleHandshakePolicy(short requiredVersion, bool rejectMismatchedVersions)
        {
            RequiredVersion = requiredVersion;
            RejectMismatchedVersions = rejectMismatchedVersions;
        }

        public short RequiredVersion { get; }
        public bool RejectMismatchedVersions { get; }

        public bool TryResolveSessionVersion(short advertisedVersion, out short sessionVersion, out string error)
        {
            error = null;
            if (advertisedVersion == RequiredVersion)
            {
                sessionVersion = RequiredVersion;
                return true;
            }

            if (RejectMismatchedVersions)
            {
                sessionVersion = advertisedVersion;
                error = $"Handshake version mismatch. Expected {RequiredVersion} but received {advertisedVersion}.";
                return false;
            }

            sessionVersion = RequiredVersion;
            return true;
        }

        public MapleCrypto CreateCrypto(byte[] iv, short sessionVersion)
        {
            return new MapleCrypto((byte[])iv.Clone(), sessionVersion);
        }
    }
}

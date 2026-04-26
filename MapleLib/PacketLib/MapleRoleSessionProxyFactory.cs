using System.Collections.Generic;
using System.Text;

namespace MapleLib.PacketLib
{
    /// <summary>
    /// Centralized creation path for role-session proxies and shared per-role authority.
    /// </summary>
    public sealed class MapleRoleSessionProxyFactory
    {
        public static readonly MapleRoleSessionProxyFactory GlobalV95 = new MapleRoleSessionProxyFactory(MapleHandshakePolicy.GlobalV95);

        private readonly MapleHandshakePolicy _handshakePolicy;
        private readonly bool _shareRoleSessionProxyPerRole;
        private readonly Dictionary<MapleServerRole, MapleRoleSessionProxy> _sharedRoleProxies = new();
        private readonly object _sharedRoleProxyLock = new();

        public MapleRoleSessionProxyFactory(
            MapleHandshakePolicy handshakePolicy = null,
            bool shareRoleSessionProxyPerRole = false)
        {
            _handshakePolicy = handshakePolicy ?? MapleHandshakePolicy.GlobalV95;
            _shareRoleSessionProxyPerRole = shareRoleSessionProxyPerRole;
        }

        public MapleRoleSessionProxy Create(MapleServerRole role)
        {
            if (_shareRoleSessionProxyPerRole)
            {
                lock (_sharedRoleProxyLock)
                {
                    if (_sharedRoleProxies.TryGetValue(role, out MapleRoleSessionProxy existingProxy))
                    {
                        return existingProxy;
                    }

                    MapleRoleSessionProxy createdProxy = new MapleRoleSessionProxy(role, _handshakePolicy);
                    _sharedRoleProxies[role] = createdProxy;
                    return createdProxy;
                }
            }

            return new MapleRoleSessionProxy(role, _handshakePolicy);
        }

        public string DescribeAuthorityStatus()
        {
            if (!_shareRoleSessionProxyPerRole)
            {
                return "Role-session proxy authority mode: independent per-request proxies.";
            }

            lock (_sharedRoleProxyLock)
            {
                if (_sharedRoleProxies.Count == 0)
                {
                    return "Role-session proxy authority mode: shared per-role proxies (none created).";
                }

                StringBuilder builder = new StringBuilder("Role-session proxy authority mode: shared per-role proxies [");
                bool isFirst = true;
                foreach (KeyValuePair<MapleServerRole, MapleRoleSessionProxy> entry in _sharedRoleProxies)
                {
                    if (!isFirst)
                    {
                        builder.Append(", ");
                    }

                    isFirst = false;
                    MapleRoleSessionProxy proxy = entry.Value;
                    builder.Append(entry.Key);
                    builder.Append(":");
                    builder.Append(proxy.IsRunning ? "running" : "stopped");
                    builder.Append("/sessions=");
                    builder.Append(proxy.ActiveSessionCount);
                    builder.Append("/server=");
                    builder.Append(proxy.ReceivedCount);
                    builder.Append("/client=");
                    builder.Append(proxy.ClientReceivedCount);
                    builder.Append("/sent=");
                    builder.Append(proxy.SentCount);
                    builder.Append("/last=");
                    builder.Append(proxy.LastPacketUtc.HasValue ? proxy.LastPacketUtc.Value.ToString("O") : "never");
                }

                builder.Append(']');
                return builder.ToString();
            }
        }

        public MapleRoleSessionProxy CreateLogin()
        {
            return Create(MapleServerRole.Login);
        }

        public MapleRoleSessionProxy CreateChannel()
        {
            return Create(MapleServerRole.Channel);
        }

        public MapleRoleSessionProxy CreateCashShop()
        {
            return Create(MapleServerRole.CashShop);
        }

        public MapleRoleSessionProxy CreateMts()
        {
            return Create(MapleServerRole.Mts);
        }
    }
}

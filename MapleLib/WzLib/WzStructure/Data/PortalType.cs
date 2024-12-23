using System;
using System.Collections.Generic;
using System.Linq;

namespace MapleLib.WzLib.WzStructure.Data
{
    public enum PortalType
    {
        /// <summary>Start point portal</summary>
        StartPoint,

        /// <summary>Invisible portal</summary>
        Invisible,

        /// <summary>Visible portal</summary>
        Visible,

        /// <summary>Collision portal</summary>
        Collision,

        /// <summary>Changeable portal</summary>
        Changeable,

        /// <summary>Changeable invisible portal</summary>
        ChangeableInvisible,

        /// <summary>Town portal point</summary>
        TownPortalPoint,

        /// <summary>Script portal</summary>
        Script,

        /// <summary>Invisible script portal</summary>
        ScriptInvisible,

        /// <summary>Collision script portal</summary>
        CollisionScript,

        /// <summary>Hidden portal that only appears when user is nearby</summary>
        Hidden,

        /// <summary>Hidden script portal</summary>
        ScriptHidden,

        /// <summary>Collision vertical jump portal</summary>
        CollisionVerticalJump,

        /// <summary>Collision custom impact portal</summary>
        CollisionCustomImpact,

        /// <summary>Unknown collision portal type PCIG</summary>
        CollisionUnknownPcig,

        /// <summary>Hidden script portal UNG type</summary>
        ScriptHiddenUng
    }

    // Extension method to convert enum values to original string codes
    public static class PortalTypeExtensions
    {
        private static readonly IReadOnlyDictionary<PortalType, string> _portalTypeToCodes = new Dictionary<PortalType, string>
        {
            { PortalType.StartPoint, "sp" },
            { PortalType.Invisible, "pi" },
            { PortalType.Visible, "pv" },
            { PortalType.Collision, "pc" },
            { PortalType.Changeable, "pg" },
            { PortalType.ChangeableInvisible, "pgi" },
            { PortalType.TownPortalPoint, "tp" },
            { PortalType.Script, "ps" },
            { PortalType.ScriptInvisible, "psi" },
            { PortalType.CollisionScript, "pcs" },
            { PortalType.Hidden, "ph" },
            { PortalType.ScriptHidden, "psh" },
            { PortalType.CollisionVerticalJump, "pcj" },
            { PortalType.CollisionCustomImpact, "pci" },
            { PortalType.CollisionUnknownPcig, "pcig" },
            { PortalType.ScriptHiddenUng, "pshg" }
        };

        private static readonly IReadOnlyDictionary<string, PortalType> _codeToPortalTypes =
            _portalTypeToCodes.ToDictionary(x => x.Value, x => x.Key, StringComparer.OrdinalIgnoreCase);

        public static string ToCode(this PortalType portalType)
        {
            return _portalTypeToCodes.TryGetValue(portalType, out var code)
                ? code
                : throw new ArgumentOutOfRangeException(nameof(portalType));
        }

        public static PortalType FromCode(string code)
        {
            if (code == null)
                throw new ArgumentNullException(nameof(code));

            return _codeToPortalTypes.TryGetValue(code, out var portalType)
                ? portalType
                : throw new ArgumentException($"Invalid portal type code: {code}", nameof(code));
        }


        private static readonly IReadOnlyDictionary<PortalType, string> _portalTypeToNames = new Dictionary<PortalType, string>
        {
            { PortalType.StartPoint, "Start Point" },
            { PortalType.Invisible, "Invisible" },
            { PortalType.Visible, "Visible" },
            { PortalType.Collision, "Collision" },
            { PortalType.Changeable, "Changable" },
            { PortalType.ChangeableInvisible, "Changable Invisible" },
            { PortalType.TownPortalPoint, "Town Portal" },
            { PortalType.Script, "Script" },
            { PortalType.ScriptInvisible, "Script Invisible" },
            { PortalType.CollisionScript, "Script Collision" },
            { PortalType.Hidden, "Hidden" },
            { PortalType.ScriptHidden, "Script Hidden" },
            { PortalType.CollisionVerticalJump, "Vertical Spring" },
            { PortalType.CollisionCustomImpact, "Custom Impact Spring" },
            { PortalType.CollisionUnknownPcig, "Unknown (PCIG)" },
            { PortalType.ScriptHiddenUng, "Script Hidden Ung" }  // Added this one since it was in the enum
        };

        public static string GetFriendlyName(this PortalType portalType)
        {
            return _portalTypeToNames.TryGetValue(portalType, out var name)
                ? name
                : throw new ArgumentOutOfRangeException(nameof(portalType));
        }
    }
}
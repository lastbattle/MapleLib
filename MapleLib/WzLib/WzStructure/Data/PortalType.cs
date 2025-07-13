using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
        Default,

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

        /// <summary>Collision custom impact 2 portal</summary>
        CollisionCustomImpact2,

        /// <summary>Unknown collision portal type PCIG</summary>
        CollisionUnknownPcig,

        /// <summary>Hidden script portal UNG type</summary>
        ScriptHiddenUng,

        /// <summary>Unknown for now, pcc (green square?) using 'pc' image</summary>
        UNKNOWN_PCC,
        /// <summary>Unknown for now, pcir. (red z?)</summary>
        UNKNOWN_PCIR
    }

    // Extension method to convert enum values to original string codes
    public static class PortalTypeExtensions
    {
        private static readonly IReadOnlyDictionary<PortalType, string> _portalTypeToCodes = new Dictionary<PortalType, string>
        {
            { PortalType.StartPoint, "sp" },
            { PortalType.Invisible, "pi" },
            { PortalType.Visible, "pv" },
            { PortalType.Default, "default" }, // Equivalent to 'pv' (Visible)
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
            { PortalType.CollisionCustomImpact2, "pci2" },
            { PortalType.CollisionUnknownPcig, "pcig" },
            { PortalType.ScriptHiddenUng, "pshg" },

            { PortalType.UNKNOWN_PCC, "pcc" }, // unknown for now
            { PortalType.UNKNOWN_PCIR, "pcir" }, // unknown for now
        };

        private static readonly IReadOnlyDictionary<PortalType, string> _portalTypeToNames = new Dictionary<PortalType, string>
        {
            { PortalType.StartPoint, "Start Point" },
            { PortalType.Invisible, "Invisible" },
            { PortalType.Visible, "Visible" },
            { PortalType.Default, "Visible (Default)" },
            { PortalType.Collision, "Collision" },
            { PortalType.Changeable, "Changeable" },
            { PortalType.ChangeableInvisible, "Changeable Invisible" },
            { PortalType.TownPortalPoint, "Town Portal" },
            { PortalType.Script, "Script" },
            { PortalType.ScriptInvisible, "Script Invisible" },
            { PortalType.CollisionScript, "Script Collision" },
            { PortalType.Hidden, "Hidden" },
            { PortalType.ScriptHidden, "Script Hidden" },
            { PortalType.CollisionVerticalJump, "Collision Vertical Jump" },
            { PortalType.CollisionCustomImpact, "Collision Custom Impact Spring" },
            { PortalType.CollisionCustomImpact2, "Collision Custom Impact Spring 2" },
            { PortalType.CollisionUnknownPcig, "Unknown (PCIG)" },
            { PortalType.ScriptHiddenUng, "Script Hidden Ung" },  // Added this one since it was in the enum

            { PortalType.UNKNOWN_PCC, "Unknown pcc" },
            { PortalType.UNKNOWN_PCIR, "Unknown pcir" },
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


        public static string GetFriendlyName(this PortalType portalType)
        {
            return _portalTypeToNames.TryGetValue(portalType, out var name)
                ? name
                : throw new ArgumentOutOfRangeException(nameof(portalType));
        }
    }
}

/* 660 */
/*enum $6A2D6ECC51E909C6B55B51DC9641E1F1
{
  PORTALTYPE_NONE = 0xFFFFFFFF,
  PORTALTYPE_STARTPOINT = 0x0,
  PORTALTYPE_INVISIBLE = 0x1,
  PORTALTYPE_VISIBLE = 0x2,
  PORTALTYPE_COLLISION = 0x3,
  PORTALTYPE_CHANGABLE = 0x4,
  PORTALTYPE_CHANGABLE_INVISIBLE = 0x5,
  PORTALTYPE_TOWNPORTAL_POINT = 0x6,
  PORTALTYPE_SCRIPT = 0x7,
  PORTALTYPE_SCRIPT_INVISIBLE = 0x8,
  PORTALTYPE_COLLISION_SCRIPT = 0x9,
  PORTALTYPE_HIDDEN = 0xA,
  PORTALTYPE_SCRIPT_HIDDEN = 0xB,
  PORTALTYPE_COLLISION_VERTICAL_JUMP = 0xC,
  PORTALTYPE_COLLISION_CUSTOM_IMPACT = 0xD,
  PORTALTYPE_SCRIPT_INVISIBLE_CHANGEABLE = 0xE,
  PORTALTYPE_COLLISION_CUSTOM_IMPACT2 = 0xF,
}*/
#nullable enable

using System.Globalization;

namespace MapleLib.WzLib.Util
{
    /// <summary>
    /// Culture-invariant numeric parsing for content taken from WZ archives
    /// and serialized dumps. Every writer emits invariant numbers, so content
    /// parsing must not depend on the host locale. The ambient-culture
    /// fallback in the XML importer for hand edited dumps is the documented
    /// exception. See docs/safety-contracts.md.
    /// </summary>
    public static class WzParse
    {
        public static bool TryInt(string text, out int value) =>
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        public static bool TryShort(string text, out short value) =>
            short.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        public static bool TryLong(string text, out long value) =>
            long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        public static bool TryFloat(string text, out float value) =>
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        public static bool TryDouble(string text, out double value) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}

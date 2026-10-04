using MapleLib.Helpers;
using System.Globalization;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class ByteUtilsTests
{
    [Fact]
    public void HexToBytes_ParsesCanonicalFormsAndWildcards()
    {
        Assert.Equal(new byte[] { 0x01, 0xAF, 0x00, 0xFF },
            ByteUtils.HexToBytes("0x01:AF, 00-ff"));

        byte[] wildcard = ByteUtils.HexToBytes("**");
        Assert.Single(wildcard);
        // The wildcard denotes any complete byte, including 0xFF.
        Assert.InRange(wildcard[0], byte.MinValue, byte.MaxValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0x")]
    [InlineData("A")]
    [InlineData("0x0")]
    [InlineData("GG")]
    [InlineData("0x12Z4")]
    [InlineData("*0")]
    [InlineData("0*")]
    public void HexToBytes_RejectsMalformedInput(string? value)
    {
        if (value is null)
        {
            Assert.Throws<ArgumentNullException>(() => ByteUtils.HexToBytes(value!));
        }
        else
        {
            Assert.Throws<FormatException>(() => ByteUtils.HexToBytes(value));
        }
    }

    [Fact]
    public void BytesToHex_RejectsNullAndFormatsBytes()
    {
        Assert.Throws<ArgumentNullException>(() => ByteUtils.BytesToHex(null!));
        Assert.Equal("prefix01 AF FF ", ByteUtils.BytesToHex([0x01, 0xAF, 0xFF], "prefix"));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("00", "00")]
    [InlineData("00 01", "0001")]
    [InlineData("0x 00", "00")]
    [InlineData("0x00:0Xab,CD-ef", "00ABCDEF")]
    [InlineData("\t00\r\nFF ", "00FF")]
    public void HexToBytes_PreservesAcceptedSeparatorsAndPrefixes(string input, string expectedHex)
    {
        Assert.Equal(expectedHex, Convert.ToHexString(ByteUtils.HexToBytes(input)));
    }

    [Theory]
    [InlineData("0x")]
    [InlineData("12x34")]
    [InlineData("12;")]
    [InlineData("123")]
    [InlineData("***")]
    [InlineData("A*")]
    public void HexToBytes_PreservesMalformedInputFailures(string input)
    {
        Assert.Throws<FormatException>(() => ByteUtils.HexToBytes(input));
    }

    [Fact]
    public void HexToBytes_WildcardProducesOneBytePerPair()
    {
        for (int iteration = 0; iteration < 32; iteration++)
            Assert.Equal(3, ByteUtils.HexToBytes("**:** **").Length);
    }

    [Theory]
    [InlineData("0x-0x00", "00")]
    [InlineData("0x-\u20030Xff", "FF")]
    [InlineData("--::,,00--FF", "00FF")]
    public void HexToBytes_PreservesInterleavedPrefixesSeparatorsAndUnicodeWhitespace(string input, string expectedHex)
    {
        Assert.Equal(expectedHex, Convert.ToHexString(ByteUtils.HexToBytes(input)));
    }

    [Theory]
    [InlineData("G", "Invalid hexadecimal character 'G'.")]
    [InlineData("0G0", "Invalid hexadecimal character 'G'.")]
    [InlineData("00?", "Invalid hexadecimal character '?'.")]
    [InlineData("**Z", "Invalid hexadecimal character 'Z'.")]
    [InlineData("0*", "A wildcard byte must be written as '**'.")]
    public void HexToBytes_PreservesValidationPrecedenceAndMessages(string input, string expectedMessage)
    {
        FormatException exception = Assert.Throws<FormatException>(() => ByteUtils.HexToBytes(input));
        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public void HexToBytes_MatchesFrozenBaselineAcrossSeededInputs()
    {
        const string alphabet = "0123456789abcdefABCDEFxXG? -:,\t\r\n\u2003\u00a0";
        var random = new Random(0x5EED);
        for (int sample = 0; sample < 2_000; sample++)
        {
            int length = random.Next(0, 65);
            string input = string.Create(length, random, static (destination, source) =>
            {
                const string chars = alphabet;
                for (int index = 0; index < destination.Length; index++)
                    destination[index] = chars[source.Next(chars.Length)];
            });

            byte[]? expected = null;
            byte[]? actual = null;
            Exception? expectedException = Record.Exception(() => expected = FrozenBaselineHexToBytes(input));
            Exception? actualException = Record.Exception(() => actual = ByteUtils.HexToBytes(input));

            AssertExceptionEquivalent(expectedException, actualException);
            if (expectedException is null)
                Assert.Equal(expected, actual);
        }
    }

    private static byte[] FrozenBaselineHexToBytes(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length == 0)
            return Array.Empty<byte>();

        List<char> digits = new(value.Length);
        bool atTokenStart = true;
        bool sawHexDigit = false;
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (char.IsWhiteSpace(current) || current is '-' or ':' or ',')
            {
                atTokenStart = true;
                continue;
            }

            if (atTokenStart && current == '0' && index + 1 < value.Length &&
                (value[index + 1] == 'x' || value[index + 1] == 'X'))
            {
                index++;
                atTokenStart = false;
                continue;
            }

            if (!ByteUtils.IsHexDigit(current))
                throw new FormatException($"Invalid hexadecimal character '{current}'.");

            digits.Add(char.ToUpperInvariant(current));
            atTokenStart = false;
            sawHexDigit = true;
        }

        if (!sawHexDigit || digits.Count % 2 != 0)
            throw new FormatException("The hexadecimal string must contain an even number of digits.");

        byte[] result = new byte[digits.Count / 2];
        for (int index = 0, digitIndex = 0; index < result.Length; index++, digitIndex += 2)
        {
            char high = digits[digitIndex];
            char low = digits[digitIndex + 1];
            if (high == '*' || low == '*')
            {
                if (high != '*' || low != '*')
                    throw new FormatException("A wildcard byte must be written as '**'.");
                result[index] = 0;
            }
            else
            {
                result[index] = byte.Parse(new string([high, low]), NumberStyles.HexNumber);
            }
        }

        return result;
    }

    private static void AssertExceptionEquivalent(Exception? expected, Exception? actual)
    {
        if (expected is null)
        {
            Assert.Null(actual);
            return;
        }

        Assert.NotNull(actual);
        Assert.Equal(expected.GetType(), actual.GetType());
        Assert.Equal(expected.Message, actual.Message);
        if (expected is ArgumentException expectedArgument && actual is ArgumentException actualArgument)
            Assert.Equal(expectedArgument.ParamName, actualArgument.ParamName);
    }

    [Fact]
    public void BytesToHex_TreatsNullHeaderAsEmpty()
    {
        Assert.Equal("AF ", ByteUtils.BytesToHex([0xAF], null!));
    }

    [Fact]
    public void CompareBytearrays_RejectsNullInputs()
    {
        Assert.Equal("a", Assert.Throws<ArgumentNullException>(() => ByteUtils.CompareBytearrays(null!, [])).ParamName);
        Assert.Equal("b", Assert.Throws<ArgumentNullException>(() => ByteUtils.CompareBytearrays([], null!)).ParamName);
        Assert.Equal("a", Assert.Throws<ArgumentNullException>(() => ByteUtils.CompareBytearrays(null!, null!)).ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(4096)]
    public void CompareBytearrays_RequiresEqualLengthsAndEveryByte(int length)
    {
        byte[] left = Enumerable.Range(0, length).Select(i => (byte)i).ToArray();
        byte[] right = (byte[])left.Clone();
        Assert.True(ByteUtils.CompareBytearrays(left, right));
        Assert.True(ByteUtils.CompareBytearrays(left, left));
        Assert.False(ByteUtils.CompareBytearrays(left, new byte[length + 1]));
        Assert.False(ByteUtils.CompareBytearrays(new byte[length + 1], left));
        if (length == 0) return;
        foreach (int index in new[] { 0, length / 2, length - 1 }.Distinct())
        {
            right[index] ^= 1;
            Assert.False(ByteUtils.CompareBytearrays(left, right));
            right[index] ^= 1;
        }
    }
}

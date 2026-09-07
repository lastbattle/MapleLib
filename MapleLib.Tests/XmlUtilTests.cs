using MapleLib.WzLib.Util;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class XmlUtilTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("ordinary Maple text 123", "ordinary Maple text 123")]
    [InlineData("\"'&<>", "&quot;&apos;&amp;&lt;&gt;")]
    [InlineData("before & after <tag attr=\"value\">'", "before &amp; after &lt;tag attr=&quot;value&quot;&gt;&apos;")]
    [InlineData("메이플스토리 日本語 é", "메이플스토리 日本語 é")]
    [InlineData("line\ncontrol\tcharacters", "line\ncontrol\tcharacters")]
    public void SanitizeText_PreservesCurrentEscaping(string input, string expected)
    {
        Assert.Equal(expected, XmlUtil.SanitizeText(input));
    }

    [Fact]
    public void SanitizeText_PreservesNullFailure()
    {
        Assert.Throws<NullReferenceException>(() => XmlUtil.SanitizeText(null!));
    }
}

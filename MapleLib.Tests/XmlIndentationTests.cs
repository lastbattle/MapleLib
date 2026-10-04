using System;
using MapleLib.WzLib.Util;
using Xunit;
using XunitAssert = Xunit.Assert;

namespace MapleLib.Tests;

public class XmlIndentationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(64)]
    [InlineData(4096)]
    public void IndentationContainsExactlyRequestedTabs(int level)
    {
        string result = XmlUtil.Indentation(level);
        XunitAssert.Equal(level, result.Length);
        XunitAssert.All(result, c => XunitAssert.Equal('\t', c));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NegativeLengthPreservesArrayAllocationException(int level)
    {
        XunitAssert.Throws<OverflowException>(() => new char[level]);
        XunitAssert.Throws<OverflowException>(() => XmlUtil.Indentation(level));
    }
}

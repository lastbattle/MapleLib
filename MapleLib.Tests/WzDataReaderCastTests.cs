using MapleLib;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Runtime.Versioning;

namespace UnitTest_WzFile;

[TestClass]
[SupportedOSPlatform("windows")]
public class WzDataReaderCastTests
{
    [TestMethod]
    public void ReadString_FallsBackOnHostilePropertyTypes()
    {
        WzImageProperty absent = null!;

        Assert.AreEqual("fallback", new WzCanvasProperty("mapName").ReadString("fallback"));
        Assert.AreEqual("fallback", new WzVectorProperty("origin", 1, 2).ReadString("fallback"));
        Assert.AreEqual("fallback", new WzConvexProperty("shape").ReadString("fallback"));
        Assert.AreEqual("fallback", new WzShortProperty("short", 3).ReadString("fallback"));
        Assert.AreEqual("fallback", new WzFloatProperty("single", 1.5f).ReadString("fallback"));
        Assert.AreEqual("fallback", new WzLongProperty("long", 8L).ReadString("fallback"));
        Assert.AreEqual("fallback", new WzNullProperty("nothing").ReadString("fallback"));
        Assert.AreEqual("fallback", absent.ReadString("fallback"));
    }

    [TestMethod]
    public void ReadString_KeepsStringIntAndNullBehavior()
    {
        Assert.AreEqual("hello", new WzStringProperty("name", "hello").ReadString("fb"));
        Assert.AreEqual("5", new WzIntProperty("id", 5).ReadString("fb"));
    }

    [TestMethod]
    public void ReadVector_TreatsHostileNodesAsAbsent()
    {
        var vector = new WzVectorProperty("origin", 3, 4);

        Assert.AreSame(vector, vector.ReadVector());
        Assert.IsNull(new WzIntProperty("scalar", 5).ReadVector());
        Assert.IsNull(new WzCanvasProperty("canvas").ReadVector());
    }

    [TestMethod]
    public void ReadValueWithoutDefault_TotalOverHostileTrees()
    {
        Assert.AreEqual(0, new WzCanvasProperty("canvas").ReadValue());
        Assert.AreEqual(0, new WzStringProperty("text", "not-a-number").ReadValue());
        Assert.AreEqual(10, new WzStringProperty("percent", "10%").ReadValue());
        Assert.AreEqual(42, new WzStringProperty("numeric", "42").ReadValue());
        Assert.AreEqual(7, new WzIntProperty("value", 7).ReadValue());
        WzImageProperty absent = null!;
        Assert.AreEqual(0, absent.ReadValue());
    }

    [TestMethod]
    public void ReadValueWithDefault_FallsBackOnHostileTrees()
    {
        Assert.AreEqual(5, new WzCanvasProperty("canvas").ReadValue(5));
        Assert.AreEqual(-1, new WzStringProperty("text", "nope").ReadValue(-1));
        Assert.AreEqual(10, new WzStringProperty("percent", "10%").ReadValue(-1));
        WzImageProperty absent = null!;
        Assert.AreEqual(9, absent.ReadValue(9));
    }
}

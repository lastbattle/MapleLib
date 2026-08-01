using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;

namespace UnitTest_WzFile;

[TestClass]
[SupportedOSPlatform("windows")]
public class WzFileSearchTests
{
    [TestMethod]
    public void WildcardTraversalReturnsDirectObjectsInPathOrder()
    {
        using WzFile file = CreateSearchFixture(
            out WzImage firstImage,
            out WzIntProperty firstValue,
            out WzSubProperty group,
            out WzVectorProperty position,
            out WzImage secondImage,
            out WzDirectory childDirectory,
            out WzImage childImage,
            out WzIntProperty childValue);

        List<WzObject> results = file.GetObjectsFromWildcardPath("Synthetic.wz/**");

        CollectionAssert.AreEqual(
            new WzObject[]
            {
                firstValue,
                group,
                position,
                position.X,
                position.Y,
                childDirectory,
                childImage,
                childValue
            },
            results);
        Assert.DoesNotContain(firstImage, results);
        Assert.DoesNotContain(secondImage, results);
        Assert.IsTrue(results.All(result => result != null));
    }

    [TestMethod]
    public void RegexTraversalReturnsVectorTerminalObjectsDirectly()
    {
        using WzFile file = CreateSearchFixture(
            out _,
            out _,
            out _,
            out WzVectorProperty position,
            out _,
            out _,
            out _,
            out _);

        List<WzObject> results = file.GetObjectsFromRegexPath(
            @"^Synthetic\.wz/First\.img/Position/[XY]$");

        CollectionAssert.AreEqual(new WzObject[] { position.X, position.Y }, results);
    }

    [TestMethod]
    public void SearchMatchingRemainsCaseSensitive()
    {
        using WzFile file = CreateSearchFixture(
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _);

        Assert.IsEmpty(file.GetObjectsFromWildcardPath("synthetic.wz/**"));
        Assert.IsEmpty(file.GetObjectsFromRegexPath(
            @"^synthetic\.wz/First\.img/Position/[XY]$"));
    }

    private static WzFile CreateSearchFixture(
        out WzImage firstImage,
        out WzIntProperty firstValue,
        out WzSubProperty group,
        out WzVectorProperty position,
        out WzImage secondImage,
        out WzDirectory childDirectory,
        out WzImage childImage,
        out WzIntProperty childValue)
    {
        WzFile file = new WzFile(1, WzMapleVersion.BMS) { Name = "Synthetic.wz" };
        file.WzDirectory.Name = file.Name;

        firstImage = new WzImage("First.img");
        firstValue = new WzIntProperty("Value", 1);
        firstImage.AddProperty(firstValue);

        group = new WzSubProperty("Group");
        group.AddProperty(new WzIntProperty("Nested", 2));
        firstImage.AddProperty(group);

        position = new WzVectorProperty(
            "Position",
            new WzIntProperty("XValue", 10),
            new WzIntProperty("YValue", 20));
        firstImage.AddProperty(position);
        file.WzDirectory.AddImage(firstImage);

        secondImage = new WzImage("Second.img");
        file.WzDirectory.AddImage(secondImage);

        childDirectory = new WzDirectory("Child");
        childImage = new WzImage("Child.img");
        childValue = new WzIntProperty("ChildValue", 3);
        childImage.AddProperty(childValue);
        childDirectory.AddImage(childImage);
        file.WzDirectory.AddDirectory(childDirectory);

        return file;
    }
}

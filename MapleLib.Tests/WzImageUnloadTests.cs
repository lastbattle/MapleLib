using System.Collections.Generic;
using System.Reflection;
using MapleLib.WzLib;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class WzImageUnloadTests
{
    [Fact]
    public void Unload_RemovesFirstMatchingEntryAndUpdatedStateAndDisposesImage()
    {
        using var manager = new WzFileManager();
        var image = new WzImage("target.img");
        var other = new WzImage("other.img");
        Dictionary<string, WzImage> images = GetDictionary<string, WzImage>(manager, "_wzImages");
        Dictionary<WzImage, bool> updated = GetDictionary<WzImage, bool>(manager, "_wzImagesUpdated");
        images.Add("other", other);
        images.Add("first", image);
        images.Add("second", image);
        updated.Add(image, true);

        manager.UnloadWzImgFile(image);

        Assert.False(images.ContainsKey("first"));
        Assert.Same(image, images["second"]);
        Assert.Same(other, images["other"]);
        Assert.False(updated.ContainsKey(image));
        Assert.Null(image.Name);
        Assert.Equal("other.img", other.Name);
    }

    [Fact]
    public void Unload_UnknownImageLeavesItAndRegistriesUnchanged()
    {
        using var manager = new WzFileManager();
        using var image = new WzImage("unknown.img");
        manager.UnloadWzImgFile(image);
        Assert.Equal("unknown.img", image.Name);
        Assert.Empty(manager.WzImagesList);
    }

    private static Dictionary<TKey, TValue> GetDictionary<TKey, TValue>(WzFileManager manager, string name)
        where TKey : notnull
    {
        return (Dictionary<TKey, TValue>)typeof(WzFileManager)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(manager)!;
    }
}

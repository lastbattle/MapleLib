using System.IO;
using MapleLib.Img;
using MapleLib.WzLib;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class LazyWzImageDictionaryTests
{
    [Fact]
    public void TryGetValueUsesLoadedCacheAndCaseInsensitiveNames()
    {
        using WzImage image = new("Image.img");
        int loads = 0;
        var dictionary = new LazyWzImageDictionary(_ =>
        {
            loads++;
            return image;
        });
        dictionary.RegisterName("Image.img");

        Assert.True(dictionary.TryGetValue("image.IMG", out WzImage? first));
        Assert.Same(image, first);
        Assert.True(dictionary.TryGetValue("IMAGE.img", out WzImage? second));
        Assert.Same(image, second);
        Assert.Equal(1, loads);
    }

    [Fact]
    public void TryGetValueReturnsTrueForRegisteredNullOrFailingLoads()
    {
        var nullDictionary = new LazyWzImageDictionary(_ => null!);
        nullDictionary.RegisterName("Null.img");
        Assert.True(nullDictionary.TryGetValue("Null.img", out WzImage? nullValue));
        Assert.Null(nullValue);

        var failingDictionary = new LazyWzImageDictionary(_ => throw new InvalidDataException());
        failingDictionary.RegisterName("Fail.img");
        Assert.True(failingDictionary.TryGetValue("Fail.img", out WzImage? failedValue));
        Assert.Null(failedValue);
    }

    [Fact]
    public void TryGetValueRejectsUnregisteredAndEmptyKeys()
    {
        var dictionary = new LazyWzImageDictionary(_ => new WzImage("unused.img"));
        Assert.False(dictionary.TryGetValue("Missing.img", out WzImage? missing));
        Assert.Null(missing);
        Assert.False(dictionary.TryGetValue(string.Empty, out WzImage? empty));
        Assert.Null(empty);
    }

    [Fact]
    public void CopyToLoadsRegisteredImagesIntoRequestedArrayOffset()
    {
        using WzImage first = new("First.img");
        using WzImage second = new("Second.img");
        var dictionary = new LazyWzImageDictionary(name =>
            name.Equals("First.img", StringComparison.OrdinalIgnoreCase) ? first : second);
        dictionary.RegisterName("First.img");
        dictionary.RegisterName("Second.img");

        var target = new KeyValuePair<string, WzImage>[3];
        dictionary.CopyTo(target, 1);

        Assert.Null(target[0].Key);
        Assert.Equal(2, target.Skip(1).Count(item => item.Value != null));
        Assert.Equal(dictionary.Keys.OrderBy(name => name), target.Skip(1).Select(item => item.Key).OrderBy(name => name));
        Assert.All(target.Skip(1), item => Assert.NotNull(item.Value));
    }
}

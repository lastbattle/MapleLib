using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure.Data.MapStructure;
using Xunit;
using Assert = Xunit.Assert;

namespace MapleLib.Tests;

public sealed class MapDirectionInfoTests
{
    [Fact]
    public void DirectionInfo_RoundTripsKnownAndUnknownFields()
    {
        WzSubProperty root = new("directionInfo");
        WzSubProperty sourceEvent = new("7");
        sourceEvent.AddProperty(new WzIntProperty("x", 1827));
        sourceEvent.AddProperty(new WzIntProperty("y", -153));
        sourceEvent.AddProperty(new WzIntProperty("forcedInput", 0));
        sourceEvent.AddProperty(new WzStringProperty("futureClientField", "preserve-me"));
        WzSubProperty eventQueue = new("EventQ");
        eventQueue.AddProperty(new WzStringProperty("0", "cannon_tuto_02"));
        eventQueue.AddProperty(new WzIntProperty("1", 77));
        eventQueue.AddProperty(new WzIntProperty("futureQueueField", 99));
        sourceEvent.AddProperty(eventQueue);
        root.AddProperty(sourceEvent);
        root.AddProperty(new WzIntProperty("rootMetadata", 3));

        MapDirectionInfo model = MapDirectionInfo.FromProperty(root)!;

        Assert.Single(model.Events);
        Assert.Equal(1827, model.Events[0].X);
        Assert.Equal(-153, model.Events[0].Y);
        Assert.Equal("cannon_tuto_02", Assert.Single(model.Events[0].EventQueue));

        model.Events[0].X = 1900;
        WzSubProperty saved = model.ToProperty();
        WzSubProperty savedEvent = Assert.IsType<WzSubProperty>(saved["7"]);

        Assert.Equal(1900, Assert.IsType<WzIntProperty>(savedEvent["x"]).Value);
        Assert.Equal("preserve-me", Assert.IsType<WzStringProperty>(savedEvent["futureClientField"]).Value);
        Assert.Equal(99, Assert.IsType<WzIntProperty>(savedEvent["EventQ"]["futureQueueField"]).Value);
        Assert.Equal(77, Assert.IsType<WzIntProperty>(savedEvent["EventQ"]["1"]).Value);
        Assert.Equal(3, Assert.IsType<WzIntProperty>(saved["rootMetadata"]).Value);
    }
}

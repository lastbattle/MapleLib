using MapleLib.Helpers;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MapleLib.WzLib.WzStructure.Data.MapStructure
{
    /// <summary>
    /// A spatial direction trigger stored under a map's top-level directionInfo node.
    /// Unknown fields are retained so older and newer client variants round-trip safely.
    /// </summary>
    public sealed class MapDirectionEvent
    {
        public string Name { get; set; } = "0";
        public int X { get; set; }
        public int Y { get; set; }
        public int ForcedInput { get; set; }
        public List<string> EventQueue { get; } = new();
        public List<WzImageProperty> UnknownProperties { get; } = new();
        public List<WzImageProperty> UnknownEventQueueProperties { get; } = new();
        private readonly List<string> _eventQueueNames = new();

        internal static MapDirectionEvent FromProperty(WzImageProperty property)
        {
            MapDirectionEvent result = new() { Name = property.Name };
            foreach (WzImageProperty child in property.WzProperties)
            {
                switch (child.Name)
                {
                    case "x":
                        result.X = InfoTool.GetInt(child);
                        break;
                    case "y":
                        result.Y = InfoTool.GetInt(child);
                        break;
                    case "forcedInput":
                        result.ForcedInput = InfoTool.GetInt(child);
                        break;
                    case "EventQ":
                        foreach (WzImageProperty queueItem in child.WzProperties.OrderBy(p => ParseIndex(p.Name)))
                        {
                            if (queueItem is WzStringProperty stringProperty)
                            {
                                result.EventQueue.Add(stringProperty.Value);
                                result._eventQueueNames.Add(queueItem.Name);
                            }
                            else
                                result.UnknownEventQueueProperties.Add(queueItem.DeepClone());
                        }
                        break;
                    default:
                        result.UnknownProperties.Add(child.DeepClone());
                        break;
                }
            }
            return result;
        }

        internal WzSubProperty ToProperty()
        {
            WzSubProperty result = new(Name);
            result["x"] = InfoTool.SetInt(X);
            result["y"] = InfoTool.SetInt(Y);
            result["forcedInput"] = InfoTool.SetInt(ForcedInput);

            if (EventQueue.Count > 0 || UnknownEventQueueProperties.Count > 0)
            {
                WzSubProperty eventQueue = new("EventQ");
                HashSet<string> usedNames = UnknownEventQueueProperties.Select(property => property.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < EventQueue.Count; i++)
                {
                    string name = i < _eventQueueNames.Count && !usedNames.Contains(_eventQueueNames[i])
                        ? _eventQueueNames[i]
                        : NextQueueName(usedNames);
                    usedNames.Add(name);
                    eventQueue.AddProperty(new WzStringProperty(name, EventQueue[i]));
                }
                foreach (WzImageProperty property in UnknownEventQueueProperties)
                    eventQueue.AddProperty(property.DeepClone());
                result.AddProperty(eventQueue);
            }

            foreach (WzImageProperty property in UnknownProperties)
                result.AddProperty(property.DeepClone());
            return result;
        }

        private static int ParseIndex(string value) => int.TryParse(value, out int index) ? index : int.MaxValue;
        private static string NextQueueName(ISet<string> usedNames)
        {
            for (int index = 0; ; index++)
                if (!usedNames.Contains(index.ToString()))
                    return index.ToString();
        }
    }

    public sealed class MapDirectionInfo
    {
        public List<MapDirectionEvent> Events { get; } = new();
        public List<WzImageProperty> UnknownProperties { get; } = new();

        public static MapDirectionInfo FromProperty(WzImageProperty property)
        {
            if (property == null)
                return null;

            MapDirectionInfo result = new();
            foreach (WzImageProperty child in property.WzProperties)
            {
                if (child is WzSubProperty && int.TryParse(child.Name, out _))
                    result.Events.Add(MapDirectionEvent.FromProperty(child));
                else
                    result.UnknownProperties.Add(child.DeepClone());
            }
            return result;
        }

        public WzSubProperty ToProperty()
        {
            WzSubProperty result = new("directionInfo");
            foreach (MapDirectionEvent directionEvent in Events)
                result.AddProperty(directionEvent.ToProperty());
            foreach (WzImageProperty property in UnknownProperties)
                result.AddProperty(property.DeepClone());
            return result;
        }
    }
}

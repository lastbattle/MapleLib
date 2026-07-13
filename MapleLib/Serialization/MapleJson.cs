using MapleLib.Configuration;
using MapleLib.Img;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MapleLib
{
    internal static class MapleJson
    {
        public static readonly JsonSerializerOptions IndentedOptions = new()
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
    }

    internal sealed class MapleJsonObjectConverter : JsonConverter<object>
    {
        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using JsonDocument document = JsonDocument.ParseValue(ref reader);
            return document.RootElement.Clone();
        }

        public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
        {
            WriteValue(writer, value);
        }

        private static void WriteValue(Utf8JsonWriter writer, object? value)
        {
            switch (value)
            {
                case null:
                    writer.WriteNullValue();
                    return;
                case string stringValue:
                    writer.WriteStringValue(stringValue);
                    return;
                case bool boolValue:
                    writer.WriteBooleanValue(boolValue);
                    return;
                case byte[] bytes:
                    writer.WriteBase64StringValue(bytes);
                    return;
                case byte byteValue:
                    writer.WriteNumberValue(byteValue);
                    return;
                case sbyte sbyteValue:
                    writer.WriteNumberValue(sbyteValue);
                    return;
                case short shortValue:
                    writer.WriteNumberValue(shortValue);
                    return;
                case ushort ushortValue:
                    writer.WriteNumberValue(ushortValue);
                    return;
                case int intValue:
                    writer.WriteNumberValue(intValue);
                    return;
                case uint uintValue:
                    writer.WriteNumberValue(uintValue);
                    return;
                case long longValue:
                    writer.WriteNumberValue(longValue);
                    return;
                case ulong ulongValue:
                    writer.WriteNumberValue(ulongValue);
                    return;
                case float floatValue:
                    writer.WriteNumberValue(floatValue);
                    return;
                case double doubleValue:
                    writer.WriteNumberValue(doubleValue);
                    return;
                case decimal decimalValue:
                    writer.WriteNumberValue(decimalValue);
                    return;
                case char charValue:
                    writer.WriteStringValue(charValue.ToString());
                    return;
                case DateTime dateTimeValue:
                    writer.WriteStringValue(dateTimeValue);
                    return;
                case DateTimeOffset dateTimeOffsetValue:
                    writer.WriteStringValue(dateTimeOffsetValue);
                    return;
                case Guid guidValue:
                    writer.WriteStringValue(guidValue);
                    return;
                case JsonElement jsonElement:
                    jsonElement.WriteTo(writer);
                    return;
                case JsonNode jsonNode:
                    jsonNode.WriteTo(writer);
                    return;
                case IDictionary<string, object?> dictionary:
                    writer.WriteStartObject();
                    foreach (KeyValuePair<string, object?> entry in dictionary)
                    {
                        writer.WritePropertyName(entry.Key);
                        WriteValue(writer, entry.Value);
                    }
                    writer.WriteEndObject();
                    return;
                case IEnumerable enumerable:
                    writer.WriteStartArray();
                    foreach (object? item in enumerable)
                    {
                        WriteValue(writer, item);
                    }
                    writer.WriteEndArray();
                    return;
                default:
                    writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture));
                    return;
            }
        }
    }

    [JsonSourceGenerationOptions(
        GenerationMode = JsonSourceGenerationMode.Metadata,
        Converters = new[] { typeof(MapleJsonObjectConverter) },
        IncludeFields = true,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true)]
    [JsonSerializable(typeof(UserSettings))]
    [JsonSerializable(typeof(ApplicationSettings))]
    [JsonSerializable(typeof(List<EncryptionKey>))]
    [JsonSerializable(typeof(CategoryIndex))]
    [JsonSerializable(typeof(HaCreatorConfig))]
    [JsonSerializable(typeof(VersionInfo))]
    [JsonSerializable(typeof(ListWzJsonFormat))]
    [JsonSerializable(typeof(ImageCaseMapData))]
    [JsonSerializable(typeof(Dictionary<string, object>))]
    internal partial class MapleJsonContext : JsonSerializerContext
    {
    }
}

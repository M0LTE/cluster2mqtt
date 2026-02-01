using System.Text.Json.Serialization;

namespace Cluster2Mqtt.Models;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DxSpot))]
[JsonSerializable(typeof(WeatherData))]
[JsonSerializable(typeof(Heartbeat))]
public partial class JsonContext : JsonSerializerContext
{
}

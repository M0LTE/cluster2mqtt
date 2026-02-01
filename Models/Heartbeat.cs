namespace Cluster2Mqtt.Models;

public sealed record Heartbeat
{
    public required DateTimeOffset Timestamp { get; init; }
    public required string Status { get; init; }
    public bool? ClusterConnected { get; init; }
    public bool? MqttConnected { get; init; }
    public long? SpotsPublished { get; init; }
    public long? WeatherPublished { get; init; }
}

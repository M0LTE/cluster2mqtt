namespace Cluster2Mqtt.Configuration;

public sealed class MqttOptions
{
    public const string SectionName = "Mqtt";

    public string Host { get; set; } = "mqtt.lan";
    public int Port { get; set; } = 1883;
    public string SpotTopic { get; set; } = "dxcluster/spots";
    public string WeatherTopic { get; set; } = "dxcluster/weather";
    public string HeartbeatTopic { get; set; } = "dxcluster/heartbeat";
    public int HeartbeatIntervalSeconds { get; set; } = 60;
    public string ClientId { get; set; } = "cluster2mqtt";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool UseTls { get; set; } = false;
}

namespace Cluster2Mqtt.Configuration;

public sealed class DxClusterOptions
{
    public const string SectionName = "DxCluster";

    public string Host { get; set; } = "g4bfg.net";
    public int Port { get; set; } = 7300;
    public string Callsign { get; set; } = "m0lte";
    public int ReconnectDelaySeconds { get; set; } = 5;
    public int ConnectionTimeoutSeconds { get; set; } = 30;
}

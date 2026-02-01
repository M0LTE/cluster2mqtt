namespace Cluster2Mqtt.Models;

public sealed record DxSpot
{
    /// <summary>
    /// The callsign of the station reporting the spot (spotter)
    /// </summary>
    public required string Spotter { get; init; }

    /// <summary>
    /// Frequency in kHz (e.g., 21142.3)
    /// </summary>
    public required decimal FrequencyKhz { get; init; }

    /// <summary>
    /// The callsign of the DX station being spotted
    /// </summary>
    public required string DxCallsign { get; init; }

    /// <summary>
    /// Optional comment/info about the spot
    /// </summary>
    public string? Comment { get; init; }

    /// <summary>
    /// UTC timestamp of the spot (ISO 8601 format)
    /// </summary>
    public required DateTimeOffset Time { get; init; }

    /// <summary>
    /// Full ISO 8601 timestamp when the spot was received by this service
    /// </summary>
    public required DateTimeOffset ReceivedAt { get; init; }
}

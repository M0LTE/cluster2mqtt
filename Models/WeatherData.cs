namespace Cluster2Mqtt.Models;

/// <summary>
/// Solar and geomagnetic weather data from WCY or WWV broadcasts
/// </summary>
public sealed record WeatherData
{
    /// <summary>
    /// Source of the data (e.g., "DK0WCY-2")
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Hour of the report (UTC)
    /// </summary>
    public int? Hour { get; init; }

    /// <summary>
    /// K-index (0-9, measure of geomagnetic activity)
    /// </summary>
    public int? KIndex { get; init; }

    /// <summary>
    /// Expected K-index
    /// </summary>
    public int? ExpectedKIndex { get; init; }

    /// <summary>
    /// A-index (daily average of geomagnetic activity)
    /// </summary>
    public int? AIndex { get; init; }

    /// <summary>
    /// R value (sunspot number proxy)
    /// </summary>
    public int? R { get; init; }

    /// <summary>
    /// Solar Flux Index (10.7cm radio flux)
    /// </summary>
    public int? Sfi { get; init; }

    /// <summary>
    /// Solar activity level (e.g., "eru" for eruptive)
    /// </summary>
    public string? SolarActivity { get; init; }

    /// <summary>
    /// Geomagnetic field condition (e.g., "qui" for quiet)
    /// </summary>
    public string? GeomagneticField { get; init; }

    /// <summary>
    /// Aurora indicator (e.g., "no", "yes")
    /// </summary>
    public string? Aurora { get; init; }

    /// <summary>
    /// Full ISO 8601 timestamp when the data was received
    /// </summary>
    public required DateTimeOffset ReceivedAt { get; init; }

    /// <summary>
    /// Raw line from the DX Cluster
    /// </summary>
    public string? RawLine { get; init; }
}

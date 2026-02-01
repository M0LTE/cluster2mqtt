using System.Globalization;
using System.Text.RegularExpressions;
using Cluster2Mqtt.Models;

namespace Cluster2Mqtt.Services;

public partial class SpotParser
{
    // Regex pattern for DX spots:
    // "DX de K4VTE:     21142.3  VE6KIX       comment text        1829Z"
    // Groups: 1=spotter, 2=frequency, 3=dx callsign, 4=comment, 5=time
    [GeneratedRegex(
        @"^DX\s+de\s+([A-Z0-9/]+):\s+(\d+\.?\d*)\s+([A-Z0-9/]+)\s*(.*?)\s+(\d{4})Z?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DxSpotPattern();

    public DxSpot? TryParse(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        // Strip control characters (like bell \x07) that some clusters append
        var trimmed = StripControlCharacters(line).Trim();

        // Quick check before regex
        if (!trimmed.StartsWith("DX de ", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("DX  de ", StringComparison.OrdinalIgnoreCase))
            return null;

        var match = DxSpotPattern().Match(trimmed);
        if (!match.Success)
            return null;

        var frequencyStr = match.Groups[2].Value;
        if (!decimal.TryParse(frequencyStr, CultureInfo.InvariantCulture, out var frequency))
            return null;

        var comment = match.Groups[4].Value.Trim();
        var timeHhmm = match.Groups[5].Value;
        var spotTime = ParseSpotTime(timeHhmm);

        return new DxSpot
        {
            Spotter = match.Groups[1].Value.ToUpperInvariant(),
            FrequencyKhz = frequency,
            DxCallsign = match.Groups[3].Value.ToUpperInvariant(),
            Comment = string.IsNullOrEmpty(comment) ? null : comment,
            Time = spotTime,
            ReceivedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Converts HHMM time string to a DateTimeOffset.
    /// Uses today's date in UTC, but if the time is in the future (due to timezone edge cases),
    /// uses yesterday's date.
    /// </summary>
    private static DateTimeOffset ParseSpotTime(string hhmm)
    {
        if (hhmm.Length != 4 ||
            !int.TryParse(hhmm.AsSpan(0, 2), out var hour) ||
            !int.TryParse(hhmm.AsSpan(2, 2), out var minute))
        {
            // Fallback to current time if parsing fails
            return DateTimeOffset.UtcNow;
        }

        // Clamp to valid ranges
        hour = Math.Clamp(hour, 0, 23);
        minute = Math.Clamp(minute, 0, 59);

        var now = DateTimeOffset.UtcNow;
        var today = now.Date;
        var spotTime = new DateTimeOffset(today.Year, today.Month, today.Day, hour, minute, 0, TimeSpan.Zero);

        // If the spot time is more than 1 hour in the future, assume it's from yesterday
        // (handles edge cases around midnight UTC)
        if (spotTime > now.AddHours(1))
        {
            spotTime = spotTime.AddDays(-1);
        }

        return spotTime;
    }

    private static string StripControlCharacters(string input)
    {
        // Remove control characters (0x00-0x1F except tab, CR, LF) and 0x7F
        var sb = new System.Text.StringBuilder(input.Length);
        foreach (var c in input)
        {
            if (c >= 32 || c == '\t' || c == '\r' || c == '\n')
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}

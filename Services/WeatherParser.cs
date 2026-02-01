using System.Globalization;
using System.Text.RegularExpressions;
using Cluster2Mqtt.Models;

namespace Cluster2Mqtt.Services;

public partial class WeatherParser
{
    // WCY format: "WCY de DK0WCY-2 <19> : K=2 expK=0 A=5 R=126 SFI=141 SA=eru GMF=qui Au=no"
    [GeneratedRegex(
        @"^WCY\s+de\s+([A-Z0-9-]+)\s+<(\d+)>\s*:\s*(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex WcyPattern();

    // Individual field patterns
    [GeneratedRegex(@"K=(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex KIndexPattern();

    [GeneratedRegex(@"expK=(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex ExpKPattern();

    [GeneratedRegex(@"A=(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex AIndexPattern();

    [GeneratedRegex(@"R=(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex RPattern();

    [GeneratedRegex(@"SFI=(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex SfiPattern();

    [GeneratedRegex(@"SA=(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex SaPattern();

    [GeneratedRegex(@"GMF=(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex GmfPattern();

    [GeneratedRegex(@"Au=(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex AuroraPattern();

    public WeatherData? TryParse(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        var trimmed = line.Trim();

        // Check for WCY prefix
        if (!trimmed.StartsWith("WCY de ", StringComparison.OrdinalIgnoreCase))
            return null;

        var match = WcyPattern().Match(trimmed);
        if (!match.Success)
            return null;

        var source = match.Groups[1].Value;
        var hourStr = match.Groups[2].Value;
        var dataSection = match.Groups[3].Value;

        int? hour = int.TryParse(hourStr, out var h) ? h : null;

        return new WeatherData
        {
            Source = source,
            Hour = hour,
            KIndex = ExtractInt(KIndexPattern(), dataSection),
            ExpectedKIndex = ExtractInt(ExpKPattern(), dataSection),
            AIndex = ExtractInt(AIndexPattern(), dataSection),
            R = ExtractInt(RPattern(), dataSection),
            Sfi = ExtractInt(SfiPattern(), dataSection),
            SolarActivity = ExtractString(SaPattern(), dataSection),
            GeomagneticField = ExtractString(GmfPattern(), dataSection),
            Aurora = ExtractString(AuroraPattern(), dataSection),
            ReceivedAt = DateTimeOffset.UtcNow,
            RawLine = line
        };
    }

    private static int? ExtractInt(Regex pattern, string input)
    {
        var match = pattern.Match(input);
        if (match.Success && int.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out var value))
            return value;
        return null;
    }

    private static string? ExtractString(Regex pattern, string input)
    {
        var match = pattern.Match(input);
        return match.Success ? match.Groups[1].Value : null;
    }
}

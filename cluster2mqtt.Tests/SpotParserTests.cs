using Cluster2Mqtt.Services;

namespace Cluster2Mqtt.Tests;

public class SpotParserTests
{
    private readonly SpotParser _parser = new();

    [Fact]
    public void TryParse_BasicSpot_ReturnsSpot()
    {
        var line = "DX de K4VTE:     21142.3  VE6KIX                                      1829Z";

        var result = _parser.TryParse(line);

        Assert.NotNull(result);
        Assert.Equal("K4VTE", result.Spotter);
        Assert.Equal(21142.3m, result.FrequencyKhz);
        Assert.Equal("VE6KIX", result.DxCallsign);
        Assert.Null(result.Comment);
        Assert.Equal(18, result.Time.Hour);
        Assert.Equal(29, result.Time.Minute);
    }

    [Fact]
    public void TryParse_SpotWithComment_ReturnsSpotWithComment()
    {
        var line = "DX de OH0M:      21044.0  K5OHY        WWFF KFF-2989                  1830Z";

        var result = _parser.TryParse(line);

        Assert.NotNull(result);
        Assert.Equal("OH0M", result.Spotter);
        Assert.Equal(21044.0m, result.FrequencyKhz);
        Assert.Equal("K5OHY", result.DxCallsign);
        Assert.Equal("WWFF KFF-2989", result.Comment);
        Assert.Equal(18, result.Time.Hour);
        Assert.Equal(30, result.Time.Minute);
    }

    [Fact]
    public void TryParse_SpotWithFT8Comment_ReturnsSpotWithComment()
    {
        var line = "DX de PY8JL:     21074.9  IK1EZX       FT8 -16                        1830Z";

        var result = _parser.TryParse(line);

        Assert.NotNull(result);
        Assert.Equal("PY8JL", result.Spotter);
        Assert.Equal(21074.9m, result.FrequencyKhz);
        Assert.Equal("IK1EZX", result.DxCallsign);
        Assert.Equal("FT8 -16", result.Comment);
        Assert.Equal(18, result.Time.Hour);
        Assert.Equal(30, result.Time.Minute);
    }

    [Fact]
    public void TryParse_SpotWithVhfFrequency_ReturnsSpot()
    {
        var line = "DX de II4FMOB:  144350.0  IU4JJJ       50 Diploma RADIO FLASH MOB AWA 1830Z";

        var result = _parser.TryParse(line);

        Assert.NotNull(result);
        Assert.Equal("II4FMOB", result.Spotter);
        Assert.Equal(144350.0m, result.FrequencyKhz);
        Assert.Equal("IU4JJJ", result.DxCallsign);
        Assert.Equal("50 Diploma RADIO FLASH MOB AWA", result.Comment);
    }

    [Fact]
    public void TryParse_SpotWithGridSquares_ReturnsSpotWithComment()
    {
        var line = "DX de CT7BOD:    28461.0  K1NF         USB IM57uo -> FN53bw           1831Z";

        var result = _parser.TryParse(line);

        Assert.NotNull(result);
        Assert.Equal("CT7BOD", result.Spotter);
        Assert.Equal(28461.0m, result.FrequencyKhz);
        Assert.Equal("K1NF", result.DxCallsign);
        Assert.Equal("USB IM57uo -> FN53bw", result.Comment);
    }

    [Fact]
    public void TryParse_SpotWithNumericCallsignPrefix_ReturnsSpot()
    {
        var line = "DX de JR6RRD:    10136.0  5Z4VJ        FT8 CQ                         1845Z";

        var result = _parser.TryParse(line);

        Assert.NotNull(result);
        Assert.Equal("JR6RRD", result.Spotter);
        Assert.Equal(10136.0m, result.FrequencyKhz);
        Assert.Equal("5Z4VJ", result.DxCallsign);
        Assert.Equal("FT8 CQ", result.Comment);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_NullOrEmpty_ReturnsNull(string? line)
    {
        var result = _parser.TryParse(line);
        Assert.Null(result);
    }

    [Fact]
    public void TryParse_WcyLine_ReturnsNull()
    {
        var line = "WCY de DK0WCY-2 <19> : K=2 expK=0 A=5 R=126 SFI=141 SA=eru GMF=qui Au=no";

        var result = _parser.TryParse(line);

        Assert.Null(result);
    }

    [Fact]
    public void TryParse_LoginPrompt_ReturnsNull()
    {
        var line = "login: m0lte";

        var result = _parser.TryParse(line);

        Assert.Null(result);
    }

    [Fact]
    public void TryParse_PromptLine_ReturnsNull()
    {
        var line = "M0LTE de G4BFG-9  1-Feb-2026 1829Z dxspider >";

        var result = _parser.TryParse(line);

        Assert.Null(result);
    }

    [Fact]
    public void TryParse_WelcomeMessage_ReturnsNull()
    {
        var line = "Hello Tom, this is G4BFG-9 in Warminster, Wiltshire";

        var result = _parser.TryParse(line);

        Assert.Null(result);
    }

    [Fact]
    public void TryParse_CallsignsAreUppercased()
    {
        var line = "DX de k4vte:     21142.3  ve6kix                                      1829Z";

        var result = _parser.TryParse(line);

        Assert.NotNull(result);
        Assert.Equal("K4VTE", result.Spotter);
        Assert.Equal("VE6KIX", result.DxCallsign);
    }

    [Fact]
    public void TryParse_SpotWithSlashCallsign_ReturnsSpot()
    {
        var line = "DX de W1AW/1:    28472.0  VE2/K1ABC    portable                       1829Z";

        var result = _parser.TryParse(line);

        Assert.NotNull(result);
        Assert.Equal("W1AW/1", result.Spotter);
        Assert.Equal("VE2/K1ABC", result.DxCallsign);
    }

    [Fact]
    public void TryParse_SpotWithBellCharacters_ReturnsSpot()
    {
        // Real-world format from G4BFG cluster includes bell characters (0x07) after timestamp
        var line = "DX de N2TA:      7047.0  RI0SP        GRID IR37                      2050Z\x07\x07";

        var result = _parser.TryParse(line);

        Assert.NotNull(result);
        Assert.Equal("N2TA", result.Spotter);
        Assert.Equal(7047.0m, result.FrequencyKhz);
        Assert.Equal("RI0SP", result.DxCallsign);
        Assert.Equal("GRID IR37", result.Comment);
        Assert.Equal(20, result.Time.Hour);
        Assert.Equal(50, result.Time.Minute);
    }

    [Fact]
    public void TryParse_SpotWithTrailingControlChars_ReturnsSpot()
    {
        // Another format with multiple control characters
        var line = "DX de WJ1T:      28457.0  VE8DAV                                      2050Z\x07\x07";

        var result = _parser.TryParse(line);

        Assert.NotNull(result);
        Assert.Equal("WJ1T", result.Spotter);
        Assert.Equal(28457.0m, result.FrequencyKhz);
        Assert.Equal("VE8DAV", result.DxCallsign);
        Assert.Null(result.Comment);
        Assert.Equal(20, result.Time.Hour);
        Assert.Equal(50, result.Time.Minute);
    }

    [Fact]
    public void TryParse_Time_IsUtc()
    {
        var line = "DX de K4VTE:     21142.3  VE6KIX                                      1829Z";

        var result = _parser.TryParse(line);

        Assert.NotNull(result);
        Assert.Equal(TimeSpan.Zero, result.Time.Offset);
    }
}

using Cluster2Mqtt.Services;

namespace Cluster2Mqtt.Tests;

public class WeatherParserTests
{
    private readonly WeatherParser _parser = new();

    [Fact]
    public void TryParse_ValidWcyLine_ReturnsWeatherData()
    {
        var line = "WCY de DK0WCY-2 <19> : K=2 expK=0 A=5 R=126 SFI=141 SA=eru GMF=qui Au=no";

        var result = _parser.TryParse(line);

        Assert.NotNull(result);
        Assert.Equal("DK0WCY-2", result.Source);
        Assert.Equal(19, result.Hour);
        Assert.Equal(2, result.KIndex);
        Assert.Equal(0, result.ExpectedKIndex);
        Assert.Equal(5, result.AIndex);
        Assert.Equal(126, result.R);
        Assert.Equal(141, result.Sfi);
        Assert.Equal("eru", result.SolarActivity);
        Assert.Equal("qui", result.GeomagneticField);
        Assert.Equal("no", result.Aurora);
        Assert.Equal(line, result.RawLine);
    }

    [Fact]
    public void TryParse_WcyWithDifferentHour_ReturnsCorrectHour()
    {
        var line = "WCY de DK0WCY-2 <06> : K=1 expK=1 A=3 R=100 SFI=135 SA=qui GMF=qui Au=no";

        var result = _parser.TryParse(line);

        Assert.NotNull(result);
        Assert.Equal(6, result.Hour);
        Assert.Equal(1, result.KIndex);
        Assert.Equal(135, result.Sfi);
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
    public void TryParse_DxSpotLine_ReturnsNull()
    {
        var line = "DX de K4VTE:     21142.3  VE6KIX                                      1829Z";

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
    public void TryParse_LoginPrompt_ReturnsNull()
    {
        var line = "login: m0lte";

        var result = _parser.TryParse(line);

        Assert.Null(result);
    }

    [Fact]
    public void TryParse_PartialData_ReturnsAvailableFields()
    {
        // If only some fields are present, we should still parse what we can
        var line = "WCY de DK0WCY-2 <12> : K=3 SFI=150";

        var result = _parser.TryParse(line);

        Assert.NotNull(result);
        Assert.Equal("DK0WCY-2", result.Source);
        Assert.Equal(12, result.Hour);
        Assert.Equal(3, result.KIndex);
        Assert.Equal(150, result.Sfi);
        Assert.Null(result.ExpectedKIndex);
        Assert.Null(result.AIndex);
        Assert.Null(result.Aurora);
    }
}

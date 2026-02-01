using Cluster2Mqtt.Configuration;
using Cluster2Mqtt.Models;
using Cluster2Mqtt.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cluster2Mqtt.Tests;

public class EndToEndTests : IAsyncLifetime
{
    private MockTcpServer? _server;

    // Sample data from the real PuTTY log
    private static readonly string[] SampleClusterOutput = [
        "DX de K4VTE:     21142.3  VE6KIX                                      1829Z",
        "DX de OH0M:      21044.0  K5OHY        WWFF KFF-2989                  1830Z",
        "DX de PY8JL:     21074.9  IK1EZX       FT8 -16                        1830Z",
        "DX de II4FMOB:  144350.0  IU4JJJ       50 Diploma RADIO FLASH MOB AWA 1830Z",
        "DX de AB9YC:     14018.6  OR2M         CW                             1830Z",
        "DX de EB1CU:     21350.0  EA1FOX       LLOTA POTA                     1830Z",
        "DX de AA8CS:     24915.4  CX2SA                                       1831Z",
        "DX de CT7BOD:    28461.0  K1NF         USB IM57uo -> FN53bw           1831Z",
        "DX de IU4QQD:    14200.0  IU2SLI       vediamo un p? per un paio di c 1831Z",
        "DX de W3LPL:     14037.4  TZ4AM        Heard in PA                    1832Z",
        "WCY de DK0WCY-2 <19> : K=2 expK=0 A=5 R=126 SFI=141 SA=eru GMF=qui Au=no",
        "DX de N2TA:      28180.0  RI0SP        GRID IR37                      1837Z",
        "DX de JR6RRD:    10136.0  5Z4VJ        FT8 CQ                         1845Z",
    ];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_server != null)
            await _server.DisposeAsync();
    }

    [Fact]
    public async Task FullPipeline_ParsesSpots_FromMockCluster()
    {
        // Arrange
        _server = new MockTcpServer(SampleClusterOutput);
        _server.Start();

        var clusterOptions = Options.Create(new DxClusterOptions
        {
            Host = "127.0.0.1",
            Port = _server.Port,
            Callsign = "TEST1ABC",
            ConnectionTimeoutSeconds = 5
        });

        await using var client = new DxClusterClient(clusterOptions, NullLogger<DxClusterClient>.Instance);
        var spotParser = new SpotParser();
        var weatherParser = new WeatherParser();

        var receivedSpots = new List<DxSpot>();
        var receivedWeather = new List<WeatherData>();

        client.LineReceived += line =>
        {
            var spot = spotParser.TryParse(line);
            if (spot != null)
            {
                receivedSpots.Add(spot);
                return;
            }

            var weather = weatherParser.TryParse(line);
            if (weather != null)
            {
                receivedWeather.Add(weather);
            }
        };

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await client.ConnectAsync(cts.Token);

        // Wait for all lines to be processed
        await Task.Delay(2000);

        // Assert - verify spots
        Assert.Equal(12, receivedSpots.Count); // 12 DX spots in sample data

        var firstSpot = receivedSpots.First();
        Assert.Equal("K4VTE", firstSpot.Spotter);
        Assert.Equal(21142.3m, firstSpot.FrequencyKhz);
        Assert.Equal("VE6KIX", firstSpot.DxCallsign);

        var spotWithComment = receivedSpots.First(s => s.Spotter == "OH0M");
        Assert.Equal("WWFF KFF-2989", spotWithComment.Comment);

        var vhfSpot = receivedSpots.First(s => s.Spotter == "II4FMOB");
        Assert.Equal(144350.0m, vhfSpot.FrequencyKhz);

        // Assert - verify weather
        Assert.Single(receivedWeather);
        var weather = receivedWeather[0];
        Assert.Equal("DK0WCY-2", weather.Source);
        Assert.Equal(19, weather.Hour);
        Assert.Equal(2, weather.KIndex);
        Assert.Equal(141, weather.Sfi);
        Assert.Equal("eru", weather.SolarActivity);
        Assert.Equal("qui", weather.GeomagneticField);
        Assert.Equal("no", weather.Aurora);
    }

    [Fact]
    public async Task FullPipeline_HandlesLogin_AndReceivesData()
    {
        // Arrange
        _server = new MockTcpServer(SampleClusterOutput);
        _server.Start();

        var clusterOptions = Options.Create(new DxClusterOptions
        {
            Host = "127.0.0.1",
            Port = _server.Port,
            Callsign = "M0LTE",
            ConnectionTimeoutSeconds = 5
        });

        await using var client = new DxClusterClient(clusterOptions, NullLogger<DxClusterClient>.Instance);
        var receivedLines = new List<string>();
        client.LineReceived += line => receivedLines.Add(line);

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await client.ConnectAsync(cts.Token);
        await Task.Delay(2000);

        // Assert
        Assert.Equal("M0LTE", _server.ReceivedCallsign);
        Assert.True(receivedLines.Count >= 10, $"Expected at least 10 lines, got {receivedLines.Count}");
    }

    [Fact]
    public async Task SpotParser_HandlesAllSampleFormats()
    {
        // This test validates our parser against all the real-world formats
        var parser = new SpotParser();

        // Test various formats from the sample
        var testCases = new (string line, string expectedSpotter, decimal expectedFreq, string expectedDx)[]
        {
            ("DX de K4VTE:     21142.3  VE6KIX                                      1829Z", "K4VTE", 21142.3m, "VE6KIX"),
            ("DX de OH0M:      21044.0  K5OHY        WWFF KFF-2989                  1830Z", "OH0M", 21044.0m, "K5OHY"),
            ("DX de PY8JL:     21074.9  IK1EZX       FT8 -16                        1830Z", "PY8JL", 21074.9m, "IK1EZX"),
            ("DX de II4FMOB:  144350.0  IU4JJJ       50 Diploma RADIO FLASH MOB AWA 1830Z", "II4FMOB", 144350.0m, "IU4JJJ"),
            ("DX de CT7BOD:    28461.0  K1NF         USB IM57uo -> FN53bw           1831Z", "CT7BOD", 28461.0m, "K1NF"),
            ("DX de JR6RRD:    10136.0  5Z4VJ        FT8 CQ                         1845Z", "JR6RRD", 10136.0m, "5Z4VJ"),
        };

        foreach (var (line, expectedSpotter, expectedFreq, expectedDx) in testCases)
        {
            var result = parser.TryParse(line);

            Assert.NotNull(result);
            Assert.Equal(expectedSpotter, result.Spotter);
            Assert.Equal(expectedFreq, result.FrequencyKhz);
            Assert.Equal(expectedDx, result.DxCallsign);
        }
    }

    [Fact]
    public void WeatherParser_ParsesRealWcyFormat()
    {
        var parser = new WeatherParser();
        var line = "WCY de DK0WCY-2 <19> : K=2 expK=0 A=5 R=126 SFI=141 SA=eru GMF=qui Au=no";

        var result = parser.TryParse(line);

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
    }
}

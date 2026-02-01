using Cluster2Mqtt.Configuration;
using Cluster2Mqtt.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cluster2Mqtt.Tests;

public class DxClusterClientTests : IAsyncLifetime
{
    private MockTcpServer? _server;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_server != null)
            await _server.DisposeAsync();
    }

    [Fact]
    public async Task ConnectAsync_SendsCallsignOnLoginPrompt()
    {
        // Arrange
        var testLines = new[]
        {
            "DX de K4VTE:     21142.3  VE6KIX                                      1829Z"
        };
        _server = new MockTcpServer(testLines);
        _server.Start();

        var options = Options.Create(new DxClusterOptions
        {
            Host = "127.0.0.1",
            Port = _server.Port,
            Callsign = "TEST1ABC",
            ConnectionTimeoutSeconds = 5
        });

        await using var client = new DxClusterClient(options, NullLogger<DxClusterClient>.Instance);

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await client.ConnectAsync(cts.Token);

        // Give time for login exchange
        await Task.Delay(500);

        // Assert
        Assert.True(client.IsConnected);
        Assert.Equal("TEST1ABC", _server.ReceivedCallsign);
    }

    [Fact]
    public async Task ConnectAsync_RaisesLineReceivedEvent()
    {
        // Arrange
        var testLines = new[]
        {
            "DX de K4VTE:     21142.3  VE6KIX                                      1829Z",
            "DX de OH0M:      21044.0  K5OHY        WWFF KFF-2989                  1830Z"
        };
        _server = new MockTcpServer(testLines);
        _server.Start();

        var options = Options.Create(new DxClusterOptions
        {
            Host = "127.0.0.1",
            Port = _server.Port,
            Callsign = "TEST1ABC",
            ConnectionTimeoutSeconds = 5
        });

        await using var client = new DxClusterClient(options, NullLogger<DxClusterClient>.Instance);
        var receivedLines = new List<string>();
        client.LineReceived += line => receivedLines.Add(line);

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await client.ConnectAsync(cts.Token);

        // Wait for lines to be received
        await Task.Delay(1000);

        // Assert
        Assert.Contains(receivedLines, l => l.Contains("K4VTE"));
        Assert.Contains(receivedLines, l => l.Contains("OH0M"));
    }

    [Fact]
    public async Task DisconnectAsync_ClosesConnection()
    {
        // Arrange
        _server = new MockTcpServer();
        _server.Start();

        var options = Options.Create(new DxClusterOptions
        {
            Host = "127.0.0.1",
            Port = _server.Port,
            Callsign = "TEST1ABC",
            ConnectionTimeoutSeconds = 5
        });

        await using var client = new DxClusterClient(options, NullLogger<DxClusterClient>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await client.ConnectAsync(cts.Token);
        await Task.Delay(200);

        // Act
        await client.DisconnectAsync();

        // Assert
        Assert.False(client.IsConnected);
    }
}

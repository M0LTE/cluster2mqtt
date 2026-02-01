using Cluster2Mqtt.Configuration;
using Cluster2Mqtt.Models;
using Cluster2Mqtt.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cluster2Mqtt.Workers;

public sealed class DxClusterWorker : BackgroundService
{
    private readonly IDxClusterClient _clusterClient;
    private readonly IMqttPublisher _mqttPublisher;
    private readonly SpotParser _spotParser;
    private readonly WeatherParser _weatherParser;
    private readonly DxClusterOptions _clusterOptions;
    private readonly MqttOptions _mqttOptions;
    private readonly ILogger<DxClusterWorker> _logger;
    private volatile bool _isStopping;

    public DxClusterWorker(
        IDxClusterClient clusterClient,
        IMqttPublisher mqttPublisher,
        SpotParser spotParser,
        WeatherParser weatherParser,
        IOptions<DxClusterOptions> clusterOptions,
        IOptions<MqttOptions> mqttOptions,
        ILogger<DxClusterWorker> logger)
    {
        _clusterClient = clusterClient;
        _mqttPublisher = mqttPublisher;
        _spotParser = spotParser;
        _weatherParser = weatherParser;
        _clusterOptions = clusterOptions.Value;
        _mqttOptions = mqttOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DxClusterWorker starting");

        // Wire up line received handler
        _clusterClient.LineReceived += OnLineReceived;
        _clusterClient.ErrorOccurred += ex => _logger.LogError(ex, "Cluster client error");
        _clusterClient.Connected += OnClusterConnected;
        _clusterClient.Disconnected += OnClusterDisconnected;

        // Wire up MQTT connection state handlers
        _mqttPublisher.Connected += OnMqttConnected;
        _mqttPublisher.Disconnected += OnMqttDisconnected;

        // Start MQTT client (ManagedClient handles reconnection internally)
        await _mqttPublisher.StartAsync(stoppingToken);

        // Start heartbeat task
        var heartbeatTask = HeartbeatLoopAsync(stoppingToken);

        // Main connection loop with reconnection
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var disconnectTcs = new TaskCompletionSource();
                void OnDisconnect() => disconnectTcs.TrySetResult();

                _clusterClient.Disconnected += OnDisconnect;

                try
                {
                    await _clusterClient.ConnectAsync(stoppingToken);

                    // Wait until disconnected or cancelled
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    var cancelTask = Task.Delay(Timeout.Infinite, cts.Token);

                    await Task.WhenAny(disconnectTcs.Task, cancelTask);
                    await cts.CancelAsync();
                }
                finally
                {
                    _clusterClient.Disconnected -= OnDisconnect;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection failed");
            }

            if (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "Reconnecting in {Delay} seconds",
                    _clusterOptions.ReconnectDelaySeconds);

                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(_clusterOptions.ReconnectDelaySeconds),
                        stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        // Wait for heartbeat task to complete
        try
        {
            await heartbeatTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        _logger.LogInformation("DxClusterWorker stopping");
    }

    private async Task HeartbeatLoopAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(_mqttOptions.HeartbeatIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var heartbeat = new Heartbeat
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Status = "running",
                    ClusterConnected = _clusterClient.IsConnected,
                    MqttConnected = _mqttPublisher.IsConnected,
                    SpotsPublished = _mqttPublisher.SpotsPublished,
                    WeatherPublished = _mqttPublisher.WeatherPublished
                };

                await _mqttPublisher.PublishHeartbeatAsync(heartbeat, stoppingToken);
                _logger.LogDebug("Heartbeat published: Cluster={ClusterConnected}, MQTT={MqttConnected}, Spots={Spots}",
                    heartbeat.ClusterConnected, heartbeat.MqttConnected, heartbeat.SpotsPublished);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to publish heartbeat");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async void OnClusterConnected()
    {
        await PublishHeartbeatOnStateChangeAsync("Cluster connected");
    }

    private async void OnClusterDisconnected()
    {
        await PublishHeartbeatOnStateChangeAsync("Cluster disconnected");
    }

    private async void OnMqttConnected()
    {
        await PublishHeartbeatOnStateChangeAsync("MQTT connected");
    }

    private async void OnMqttDisconnected()
    {
        // Don't try to publish if MQTT just disconnected - it won't work
        _logger.LogDebug("MQTT disconnected - heartbeat will be published on reconnect");
    }

    private async Task PublishHeartbeatOnStateChangeAsync(string reason)
    {
        if (_isStopping)
        {
            _logger.LogDebug("Skipping heartbeat during shutdown: {Reason}", reason);
            return;
        }

        try
        {
            var heartbeat = new Heartbeat
            {
                Timestamp = DateTimeOffset.UtcNow,
                Status = "running",
                ClusterConnected = _clusterClient.IsConnected,
                MqttConnected = _mqttPublisher.IsConnected,
                SpotsPublished = _mqttPublisher.SpotsPublished,
                WeatherPublished = _mqttPublisher.WeatherPublished
            };

            await _mqttPublisher.PublishHeartbeatAsync(heartbeat, CancellationToken.None);
            _logger.LogInformation("Heartbeat published: {Reason}", reason);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish heartbeat on state change: {Reason}", reason);
        }
    }

    private async void OnLineReceived(string line)
    {
        try
        {
            // Try to parse as DX spot
            var spot = _spotParser.TryParse(line);
            if (spot != null)
            {
                _logger.LogInformation(
                    "Spot: {Spotter} -> {DxCall} on {Freq} kHz",
                    spot.Spotter, spot.DxCallsign, spot.FrequencyKhz);

                await _mqttPublisher.PublishSpotAsync(spot, CancellationToken.None);
                return;
            }

            // Try to parse as weather data
            var weather = _weatherParser.TryParse(line);
            if (weather != null)
            {
                _logger.LogInformation(
                    "Weather: SFI={Sfi} K={K} A={A} Aurora={Aurora}",
                    weather.Sfi, weather.KIndex, weather.AIndex, weather.Aurora);

                await _mqttPublisher.PublishWeatherAsync(weather, CancellationToken.None);
                return;
            }

            // Line was not a spot or weather - that's fine, ignore it
            _logger.LogTrace("Ignored line: {Line}", line);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing line: {Line}", line);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping DxClusterWorker");
        _isStopping = true;

        await _clusterClient.DisconnectAsync();

        // Publish final heartbeat with stopped status before disconnecting MQTT
        try
        {
            var stoppedHeartbeat = new Heartbeat
            {
                Timestamp = DateTimeOffset.UtcNow,
                Status = "stopped"
            };

            await _mqttPublisher.PublishHeartbeatAsync(stoppedHeartbeat, cancellationToken);
            _logger.LogInformation("Published stopped heartbeat");

            // Give MQTT client time to send the message before stopping
            await Task.Delay(500, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish stopped heartbeat");
        }

        await _mqttPublisher.StopAsync();

        await base.StopAsync(cancellationToken);
    }
}

using System.Text.Json;
using Cluster2Mqtt.Configuration;
using Cluster2Mqtt.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;

namespace Cluster2Mqtt.Services;

public interface IMqttPublisher : IAsyncDisposable
{
    bool IsConnected { get; }
    long SpotsPublished { get; }
    long WeatherPublished { get; }
    event Action? Connected;
    event Action? Disconnected;
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync();
    Task PublishSpotAsync(DxSpot spot, CancellationToken cancellationToken);
    Task PublishWeatherAsync(WeatherData weather, CancellationToken cancellationToken);
    Task PublishHeartbeatAsync(Heartbeat heartbeat, CancellationToken cancellationToken);
}

public sealed class MqttPublisher : IMqttPublisher
{
    private readonly MqttOptions _options;
    private readonly ILogger<MqttPublisher> _logger;
    private readonly IManagedMqttClient _mqttClient;
    private bool _disposed;
    private long _spotsPublished;
    private long _weatherPublished;

    public bool IsConnected => _mqttClient.IsConnected;
    public long SpotsPublished => Interlocked.Read(ref _spotsPublished);
    public long WeatherPublished => Interlocked.Read(ref _weatherPublished);
    public event Action? Connected;
    public event Action? Disconnected;

    public MqttPublisher(
        IOptions<MqttOptions> options,
        ILogger<MqttPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
        _mqttClient = new MqttFactory().CreateManagedMqttClient();

        _mqttClient.ConnectedAsync += e =>
        {
            _logger.LogInformation("Connected to MQTT broker");
            Connected?.Invoke();
            return Task.CompletedTask;
        };

        _mqttClient.DisconnectedAsync += e =>
        {
            if (e.Exception != null)
            {
                _logger.LogWarning(e.Exception, "Disconnected from MQTT broker");
            }
            else
            {
                _logger.LogInformation("Disconnected from MQTT broker");
            }
            Disconnected?.Invoke();
            return Task.CompletedTask;
        };

        _mqttClient.ConnectingFailedAsync += e =>
        {
            _logger.LogError(e.Exception, "Failed to connect to MQTT broker");
            return Task.CompletedTask;
        };
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var clientOptionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(_options.Host, _options.Port)
            .WithClientId(_options.ClientId)
            .WithCleanSession(true);

        if (!string.IsNullOrEmpty(_options.Username))
        {
            clientOptionsBuilder.WithCredentials(_options.Username, _options.Password);
        }

        if (_options.UseTls)
        {
            clientOptionsBuilder.WithTlsOptions(o => o.UseTls());
        }

        var managedOptions = new ManagedMqttClientOptionsBuilder()
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
            .WithClientOptions(clientOptionsBuilder.Build())
            .Build();

        _logger.LogInformation(
            "Starting MQTT client, connecting to {Host}:{Port}",
            _options.Host, _options.Port);

        await _mqttClient.StartAsync(managedOptions);
    }

    public async Task PublishSpotAsync(DxSpot spot, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(spot, JsonContext.Default.DxSpot);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(_options.SpotTopic)
            .WithPayload(json)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(false)
            .Build();

        await _mqttClient.EnqueueAsync(message);
        Interlocked.Increment(ref _spotsPublished);
        _logger.LogDebug("Enqueued spot to {Topic}: {Spotter} -> {DxCall}",
            _options.SpotTopic, spot.Spotter, spot.DxCallsign);
    }

    public async Task PublishWeatherAsync(WeatherData weather, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(weather, JsonContext.Default.WeatherData);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(_options.WeatherTopic)
            .WithPayload(json)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(true) // Retain weather data so new subscribers get latest
            .Build();

        await _mqttClient.EnqueueAsync(message);
        Interlocked.Increment(ref _weatherPublished);
        _logger.LogDebug("Enqueued weather to {Topic}: SFI={Sfi} K={K}",
            _options.WeatherTopic, weather.Sfi, weather.KIndex);
    }

    public async Task PublishHeartbeatAsync(Heartbeat heartbeat, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(heartbeat, JsonContext.Default.Heartbeat);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(_options.HeartbeatTopic)
            .WithPayload(json)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(true) // Retain so subscribers get latest heartbeat immediately
            .Build();

        await _mqttClient.EnqueueAsync(message);
        _logger.LogDebug("Published heartbeat to {Topic}", _options.HeartbeatTopic);
    }

    public async Task StopAsync()
    {
        _logger.LogInformation("Stopping MQTT client");
        await _mqttClient.StopAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await StopAsync();
        _mqttClient.Dispose();
        _logger.LogDebug("MqttPublisher disposed");
    }
}

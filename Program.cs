using Cluster2Mqtt.Configuration;
using Cluster2Mqtt.Services;
using Cluster2Mqtt.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Services.Configure<DxClusterOptions>(
    builder.Configuration.GetSection(DxClusterOptions.SectionName));
builder.Services.Configure<MqttOptions>(
    builder.Configuration.GetSection(MqttOptions.SectionName));

// Services
builder.Services.AddSingleton<SpotParser>();
builder.Services.AddSingleton<WeatherParser>();
builder.Services.AddSingleton<IDxClusterClient, DxClusterClient>();
builder.Services.AddSingleton<IMqttPublisher, MqttPublisher>();

// Worker
builder.Services.AddHostedService<DxClusterWorker>();

// systemd integration (no-op on Windows)
builder.Services.AddSystemd();

var host = builder.Build();
await host.RunAsync();

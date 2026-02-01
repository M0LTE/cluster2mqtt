# cluster2mqtt

A .NET 10 service that connects to a DX Cluster via telnet, parses spot and weather data, and publishes to MQTT.

## Features

- Connects to DX Cluster nodes via TCP/telnet
- Parses DX spots and WCY weather data
- Publishes JSON to configurable MQTT topics
- Automatic reconnection for both cluster and MQTT connections
- Periodic heartbeat with connection status and message counts
- Runs as a systemd service on Linux

## Configuration

Edit `appsettings.json` or `appsettings.Production.json`:

```json
{
  "DxCluster": {
    "Host": "g4bfg.net",
    "Port": 7300,
    "Callsign": "YOUR_CALLSIGN",
    "ReconnectDelaySeconds": 5,
    "ConnectionTimeoutSeconds": 30
  },
  "Mqtt": {
    "Host": "localhost",
    "Port": 1883,
    "SpotTopic": "dxcluster/spots",
    "WeatherTopic": "dxcluster/weather",
    "HeartbeatTopic": "dxcluster/heartbeat",
    "HeartbeatIntervalSeconds": 60,
    "ClientId": "cluster2mqtt",
    "Username": null,
    "Password": null,
    "UseTls": false
  }
}
```

## MQTT Output

### Spots (`dxcluster/spots`)

```json
{
  "spotter": "G4ABC",
  "dxCallsign": "VK2XYZ",
  "frequencyKhz": 14025.0,
  "comment": "CQ DX",
  "time": "2025-02-01T20:57:00+00:00",
  "receivedAt": "2025-02-01T20:57:01.234+00:00"
}
```

### Weather (`dxcluster/weather`)

```json
{
  "sfi": 150,
  "aIndex": 10,
  "kIndex": 2,
  "expK": 3,
  "r": 0,
  "sa": "act",
  "gmf": "qui",
  "aurora": false,
  "receivedAt": "2025-02-01T18:00:00+00:00"
}
```

### Heartbeat (`dxcluster/heartbeat`)

```json
{
  "timestamp": "2025-02-01T20:57:00+00:00",
  "status": "running",
  "clusterConnected": true,
  "mqttConnected": true,
  "spotsPublished": 1234,
  "weatherPublished": 5
}
```

## Building

```bash
# Build for local development
dotnet build

# Run locally
dotnet run

# Run tests
dotnet test

# Publish for Linux deployment
dotnet publish -c Release -r linux-x64 --self-contained -o publish
```

## Deployment

Use the included PowerShell script to deploy to a Linux server:

```powershell
.\deploy.ps1
```

Or manually:

```bash
# Copy files to server
scp -r publish/* user@server:/opt/cluster2mqtt/

# On the server
sudo useradd -r -s /sbin/nologin cluster2mqtt
sudo chown -R cluster2mqtt:cluster2mqtt /opt/cluster2mqtt
sudo chmod +x /opt/cluster2mqtt/cluster2mqtt
sudo cp cluster2mqtt.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable cluster2mqtt
sudo systemctl start cluster2mqtt
```

## Monitoring

```bash
# View logs
sudo journalctl -u cluster2mqtt -f

# Check status
sudo systemctl status cluster2mqtt

# Subscribe to spots
mosquitto_sub -h localhost -t "dxcluster/spots"
```

## License

MIT

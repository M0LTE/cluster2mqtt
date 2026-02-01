using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Cluster2Mqtt.Tests;

/// <summary>
/// A simple mock TCP server that simulates a DX Cluster for testing purposes.
/// </summary>
public sealed class MockTcpServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly List<string> _linesToSend;
    private readonly CancellationTokenSource _cts = new();
    private Task? _serverTask;
    private TcpClient? _connectedClient;

    public int Port { get; }
    public string ReceivedCallsign { get; private set; } = "";
    public bool ClientConnected => _connectedClient?.Connected ?? false;

    public MockTcpServer(IEnumerable<string>? linesToSend = null)
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _linesToSend = linesToSend?.ToList() ?? [];
    }

    public void Start()
    {
        _serverTask = AcceptAndServeAsync(_cts.Token);
    }

    private async Task AcceptAndServeAsync(CancellationToken cancellationToken)
    {
        try
        {
            _connectedClient = await _listener.AcceptTcpClientAsync(cancellationToken);
            var stream = _connectedClient.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);

            // Send login prompt WITHOUT trailing newline (like the real cluster)
            var loginPrompt = Encoding.ASCII.GetBytes("login: ");
            await stream.WriteAsync(loginPrompt, cancellationToken);
            await stream.FlushAsync(cancellationToken);

            // Wait for callsign
            var callsign = await reader.ReadLineAsync(cancellationToken);
            ReceivedCallsign = callsign ?? "";

            // Send welcome message (with newlines, like the real cluster)
            await using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true) { AutoFlush = true };
            await writer.WriteLineAsync($"Hello, this is MockCluster");
            await writer.WriteLineAsync($"{callsign} de MockCluster >");

            // Send the configured lines
            foreach (var line in _linesToSend)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                await writer.WriteLineAsync(line);
                await Task.Delay(10, cancellationToken); // Small delay between lines
            }

            // Keep connection open until cancelled
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (Exception)
        {
            // Ignore errors during test
        }
    }

    /// <summary>
    /// Sends an additional line to the connected client.
    /// </summary>
    public async Task SendLineAsync(string line)
    {
        if (_connectedClient?.Connected == true)
        {
            var stream = _connectedClient.GetStream();
            await using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true) { AutoFlush = true };
            await writer.WriteLineAsync(line);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _listener.Stop();

        if (_serverTask != null)
        {
            try
            {
                await _serverTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // Ignore timeout
            }
        }

        _connectedClient?.Dispose();
        _cts.Dispose();
    }
}

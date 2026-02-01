using System.Net.Sockets;
using System.Text;
using Cluster2Mqtt.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cluster2Mqtt.Services;

public interface IDxClusterClient : IAsyncDisposable
{
    event Action<string>? LineReceived;
    event Action<Exception>? ErrorOccurred;
    event Action? Connected;
    event Action? Disconnected;

    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken cancellationToken);
    Task DisconnectAsync();
}

public sealed class DxClusterClient : IDxClusterClient
{
    private readonly DxClusterOptions _options;
    private readonly ILogger<DxClusterClient> _logger;
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private StreamWriter? _writer;
    private CancellationTokenSource? _readCts;
    private bool _disposed;

    public event Action<string>? LineReceived;
    public event Action<Exception>? ErrorOccurred;
    public event Action? Connected;
    public event Action? Disconnected;

    public bool IsConnected => _tcpClient?.Connected ?? false;

    public DxClusterClient(
        IOptions<DxClusterOptions> options,
        ILogger<DxClusterClient> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Connecting to DX Cluster {Host}:{Port}",
            _options.Host, _options.Port);

        _tcpClient = new TcpClient();

        using var timeoutCts = new CancellationTokenSource(
            TimeSpan.FromSeconds(_options.ConnectionTimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCts.Token);

        await _tcpClient.ConnectAsync(_options.Host, _options.Port, linkedCts.Token);

        _stream = _tcpClient.GetStream();
        _writer = new StreamWriter(_stream, Encoding.ASCII) { AutoFlush = true };

        _logger.LogInformation("Connected to DX Cluster");

        // Handle login handshake - the prompt "login: " doesn't end with newline
        await HandleLoginAsync(linkedCts.Token);

        Connected?.Invoke();

        // Start read loop
        _readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = ReadLoopAsync(_readCts.Token);
    }

    private async Task HandleLoginAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];
        var received = new StringBuilder();

        // Wait for login prompt (with timeout)
        var loginTimeout = TimeSpan.FromSeconds(10);
        using var loginCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        loginCts.CancelAfter(loginTimeout);

        while (!loginCts.Token.IsCancellationRequested)
        {
            // Check if data is available
            if (_stream!.DataAvailable)
            {
                var bytesRead = await _stream.ReadAsync(buffer, loginCts.Token);
                if (bytesRead > 0)
                {
                    var text = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    received.Append(text);
                    _logger.LogDebug("Login handshake received: {Text}", text.Replace("\r", "\\r").Replace("\n", "\\n"));

                    // Check for login prompt
                    var content = received.ToString();
                    if (content.Contains("login:", StringComparison.OrdinalIgnoreCase) ||
                        content.Contains("call:", StringComparison.OrdinalIgnoreCase) ||
                        content.Contains("Please enter your call", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Login prompt detected, sending callsign: {Callsign}", _options.Callsign);
                        await _writer!.WriteLineAsync(_options.Callsign);
                        return;
                    }
                }
            }
            else
            {
                // Small delay before checking again
                await Task.Delay(50, loginCts.Token);
            }
        }

        _logger.LogWarning("Login prompt not detected within timeout, proceeding anyway");
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var lineBuffer = new StringBuilder();

        try
        {
            while (!cancellationToken.IsCancellationRequested && _stream != null)
            {
                int bytesRead;
                try
                {
                    bytesRead = await _stream.ReadAsync(buffer, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (bytesRead == 0)
                {
                    _logger.LogWarning("Connection closed by server");
                    break;
                }

                var text = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                lineBuffer.Append(text);

                // Process complete lines
                var content = lineBuffer.ToString();
                var lines = content.Split('\n');

                // Process all complete lines (all except possibly the last one)
                for (int i = 0; i < lines.Length - 1; i++)
                {
                    var line = lines[i].TrimEnd('\r');
                    ProcessLine(line);
                }

                // Keep the incomplete line (or empty if last char was newline)
                lineBuffer.Clear();
                lineBuffer.Append(lines[^1]);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error in read loop");
            ErrorOccurred?.Invoke(ex);
        }
        finally
        {
            _logger.LogDebug("Read loop ended");
            Disconnected?.Invoke();
        }
    }

    private void ProcessLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        _logger.LogDebug("Received: {Line}", line);

        // Notify subscribers
        try
        {
            LineReceived?.Invoke(line);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in LineReceived handler");
        }
    }

    public async Task DisconnectAsync()
    {
        _logger.LogDebug("Disconnecting from DX Cluster");

        if (_readCts != null)
        {
            await _readCts.CancelAsync();
            _readCts.Dispose();
            _readCts = null;
        }

        if (_writer != null)
        {
            try
            {
                await _writer.WriteLineAsync("bye");
            }
            catch
            {
                // Ignore errors during disconnect
            }
        }

        Cleanup();
    }

    private void Cleanup()
    {
        _writer?.Dispose();
        _writer = null;

        _stream?.Dispose();
        _stream = null;

        _tcpClient?.Dispose();
        _tcpClient = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await DisconnectAsync();
        _logger.LogDebug("DxClusterClient disposed");
    }
}

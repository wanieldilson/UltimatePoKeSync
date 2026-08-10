using System.Net;
using System.Net.Sockets;
using System.Text;

namespace UltimatePoKeSync.Providers.MGba.Tests;

/// <summary>
/// Stands in for the Lua script: listens on an ephemeral port and sends lines. Lets us
/// test the provider and the protocol without mGBA and without a ROM.
/// </summary>
internal sealed class FakeBridge : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly List<TcpClient> _clients = [];
    private readonly CancellationTokenSource _cancellation = new();
    private readonly TaskCompletionSource _firstClient =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private FakeBridge(TcpListener listener)
    {
        _listener = listener;
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        _ = AcceptLoopAsync();
    }

    public int Port { get; }

    /// <summary>Completes when the first client has connected.</summary>
    public Task FirstClientConnected => _firstClient.Task;

    public static FakeBridge Start(int port = 0)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        return new FakeBridge(listener);
    }

    private async Task AcceptLoopAsync()
    {
        try
        {
            while (!_cancellation.IsCancellationRequested)
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(_cancellation.Token);
                lock (_clients)
                {
                    _clients.Add(client);
                }

                _firstClient.TrySetResult();
            }
        }
        catch (Exception)
        {
            // Listener stopped: end of test.
        }
    }

    public async Task SendLineAsync(string line)
    {
        byte[] payload = Encoding.UTF8.GetBytes(line + "\n");

        TcpClient[] snapshot;
        lock (_clients)
        {
            snapshot = [.. _clients];
        }

        foreach (TcpClient client in snapshot)
        {
            await client.GetStream().WriteAsync(payload);
            await client.GetStream().FlushAsync();
        }
    }

    /// <summary>Simulates mGBA being closed abruptly, to exercise reconnection.</summary>
    public void DropAllClients()
    {
        lock (_clients)
        {
            foreach (TcpClient client in _clients)
            {
                client.Dispose();
            }

            _clients.Clear();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cancellation.CancelAsync();
        DropAllClients();
        _listener.Stop();
        _cancellation.Dispose();
    }
}

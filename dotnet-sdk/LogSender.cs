using System.Net.Http.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;

namespace HttpLogger;

internal sealed class LogSender : BackgroundService
{
    internal const string ClientName = "HttpLogger.Sink";
    private const int MaxBatchSize = 64;
    private static readonly TimeSpan FlushDelay = TimeSpan.FromMilliseconds(200);
    private readonly Channel<LogEntry> _channel;
    private readonly HttpClient _client;

    public LogSender(IHttpClientFactory factory)
    {
        _client = factory.CreateClient(ClientName);
        _channel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(4096)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    public void Ship(LogEntry entry)
    {
        _channel.Writer.TryWrite(entry);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (await _channel.Reader.WaitToReadAsync(stoppingToken))
            {
                var batch = new List<LogEntry>(MaxBatchSize);

                while (batch.Count < MaxBatchSize && _channel.Reader.TryRead(out var entry))
                    batch.Add(entry);

                if (batch.Count == 0)
                    continue;

                await Task.Delay(FlushDelay, stoppingToken);

                while (batch.Count < MaxBatchSize && _channel.Reader.TryRead(out var more))
                    batch.Add(more);

                await ShipBatchAsync(batch, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }

        await DrainRemainingAsync();
    }

    private async Task DrainRemainingAsync()
    {
        var batch = new List<LogEntry>(MaxBatchSize);

        while (_channel.Reader.TryRead(out var entry))
        {
            batch.Add(entry);

            if (batch.Count == MaxBatchSize)
            {
                await ShipBatchAsync(batch, CancellationToken.None);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
            await ShipBatchAsync(batch, CancellationToken.None);
    }

    private async Task ShipBatchAsync(IReadOnlyCollection<LogEntry> batch, CancellationToken ct)
    {
        try
        {
            await _client.PostAsJsonAsync("/", batch, ct);
        }
        catch
        {
            // Sink unavailable — silently discard
        }
    }
}

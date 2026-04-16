using System.Net.Http.Json;

namespace HttpLogger;

internal sealed class LogSender(IHttpClientFactory factory)
{
    internal const string ClientName = "HttpLogger.Sink";

    public void Ship(LogEntry entry) => _ = ShipAsync(entry);

    private async Task ShipAsync(LogEntry entry)
    {
        try
        {
            using var client = factory.CreateClient(ClientName);
            await client.PostAsJsonAsync("/", entry);
        }
        catch
        {
            // Sink unavailable — silently discard
        }
    }
}

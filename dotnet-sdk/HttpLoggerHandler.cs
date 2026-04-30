using System.Diagnostics;

namespace HttpLogger;

internal sealed class HttpLoggerHandler(LogSender sender) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var start = Stopwatch.GetTimestamp();

        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            sender.Ship(HttpLoggerLog.CreateOutbound(
                request,
                response: null,
                Stopwatch.GetElapsedTime(start).TotalMilliseconds,
                ex));
            throw;
        }

        var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        if ((int)response.StatusCode >= 400)
        {
            var responseBody = string.Empty;
            if (response.Content is not null)
            {
                await response.Content.LoadIntoBufferAsync(ct);
                responseBody = await response.Content.ReadAsStringAsync(ct);
            }

            sender.Ship(HttpLoggerLog.CreateOutbound(request, response, elapsed, null, responseBody));
        }

        return response;
    }
}

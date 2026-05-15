using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace HttpLogger;

internal sealed class HttpLoggerHandler(LogSender sender, IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var requestId = HttpLoggerContext.GetCurrentRequestId(httpContextAccessor);
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
                requestId,
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

            sender.Ship(HttpLoggerLog.CreateOutbound(request, requestId, response, elapsed, null, responseBody));
        }

        return response;
    }
}

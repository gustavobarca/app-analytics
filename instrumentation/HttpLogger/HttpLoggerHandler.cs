using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;

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
            sender.Ship(new LogEntry(
                DateTimeOffset.UtcNow,
                "Outbound",
                request.Method.Method,
                request.RequestUri?.ToString() ?? string.Empty,
                0,
                Stopwatch.GetElapsedTime(start).TotalMilliseconds,
                Request: ex.ToString()));
            throw;
        }

        var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        var statusCode = (int)response.StatusCode;
        var url = request.RequestUri?.ToString() ?? string.Empty;

        if (statusCode >= 400)
        {
            string requestBody;
            try
            {
                requestBody = request.Content is not null
                    ? await request.Content.ReadAsStringAsync(ct)
                    : string.Empty;
            }
            catch
            {
                requestBody = "<unreadable>";
            }

            await response.Content.LoadIntoBufferAsync(ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            sender.Ship(new LogEntry(
                DateTimeOffset.UtcNow,
                "Outbound",
                request.Method.Method,
                url,
                statusCode,
                elapsed,
                FormatRequest(request, requestBody),
                FormatResponse(response, responseBody)));
        }
        else
        {
            sender.Ship(new LogEntry(
                DateTimeOffset.UtcNow,
                "Outbound",
                request.Method.Method,
                url,
                statusCode,
                elapsed));
        }

        return response;
    }

    private static string FormatRequest(HttpRequestMessage request, string body)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{request.Method} {request.RequestUri} HTTP/1.1");
        AppendHeaders(sb, request.Headers);
        if (request.Content is not null)
            AppendHeaders(sb, request.Content.Headers);
        if (!string.IsNullOrWhiteSpace(body))
        {
            sb.AppendLine();
            sb.Append(body);
        }
        return sb.ToString();
    }

    private static string FormatResponse(HttpResponseMessage response, string body)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"HTTP/1.1 {(int)response.StatusCode} {response.ReasonPhrase}");
        AppendHeaders(sb, response.Headers);
        AppendHeaders(sb, response.Content.Headers);
        if (!string.IsNullOrWhiteSpace(body))
        {
            sb.AppendLine();
            sb.Append(body);
        }
        return sb.ToString();
    }

    private static void AppendHeaders(StringBuilder sb, HttpHeaders headers)
    {
        foreach (var (key, values) in headers)
            sb.AppendLine($"{key}: {string.Join(", ", values)}");
    }
}

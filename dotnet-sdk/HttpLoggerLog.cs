using System.Text;
using Microsoft.AspNetCore.Http;

namespace HttpLogger;

internal static class HttpLoggerLog
{
    public static LogEntry CreateInbound(HttpContext context, string requestId, double elapsedMs, string? responseBody, Exception? error)
    {
        var request = context.Request;
        var response = context.Response;
        var statusCode = error is null
            ? response.StatusCode
            : response.StatusCode >= 400
                ? response.StatusCode
                : StatusCodes.Status500InternalServerError;

        return new LogEntry(
            DateTimeOffset.UtcNow,
            requestId,
            "Inbound",
            request.Method,
            $"{request.Scheme}://{request.Host}{request.Path}{request.QueryString}",
            statusCode,
            elapsedMs,
            responseBody is null ? null : FormatResponse(response, responseBody),
            error?.ToString());
    }

    public static LogEntry CreateOutbound(HttpRequestMessage request, string requestId, HttpResponseMessage? response, double elapsedMs, Exception? error, string? responseBody = null)
    {
        return new LogEntry(
            DateTimeOffset.UtcNow,
            requestId,
            "Outbound",
            request.Method.Method,
            request.RequestUri?.ToString() ?? string.Empty,
            response is null ? 0 : (int)response.StatusCode,
            elapsedMs,
            response is null ? null : FormatResponse(response, responseBody),
            error?.ToString());
    }

    public static string FormatResponse(HttpResponse response, string? body)
        => BuildResponse(
            $"HTTP/1.1 {response.StatusCode}",
            sb =>
            {
                foreach (var (key, value) in response.Headers)
                    sb.AppendLine($"{key}: {value}");
            },
            body);

    public static string FormatResponse(HttpResponseMessage response, string? body)
        => BuildResponse(
            $"HTTP/1.1 {(int)response.StatusCode} {response.ReasonPhrase}",
            sb =>
            {
                foreach (var (key, values) in response.Headers)
                    sb.AppendLine($"{key}: {string.Join(", ", values)}");

                if (response.Content is null)
                    return;

                foreach (var (key, values) in response.Content.Headers)
                    sb.AppendLine($"{key}: {string.Join(", ", values)}");
            },
            body);

    private static string BuildResponse(string statusLine, Action<StringBuilder> appendHeaders, string? body)
    {
        var sb = new StringBuilder();
        sb.AppendLine(statusLine);
        appendHeaders(sb);

        if (!string.IsNullOrWhiteSpace(body))
        {
            sb.AppendLine();
            sb.Append(body);
        }

        return sb.ToString();
    }
}

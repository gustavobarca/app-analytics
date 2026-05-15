using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace HttpLogger;

internal static class HttpLoggerContext
{
    private const string RequestIdKey = "__HttpLogger_RequestId";

    public static string GetOrCreateRequestId(HttpContext context)
    {
        if (context.Items.TryGetValue(RequestIdKey, out var value) && value is string requestId && !string.IsNullOrWhiteSpace(requestId))
            return requestId;

        var newRequestId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        context.Items[RequestIdKey] = newRequestId;
        return newRequestId;
    }

    public static string GetCurrentRequestId(IHttpContextAccessor accessor)
    {
        var context = accessor.HttpContext;
        if (context is null)
            return Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");

        return GetOrCreateRequestId(context);
    }
}

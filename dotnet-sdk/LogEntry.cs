namespace HttpLogger;

public sealed record LogEntry(
    DateTimeOffset Timestamp,
    string RequestId,
    string Direction,
    string Method,
    string Url,
    int StatusCode,
    double ElapsedMs,
    string? Response = null,
    string? Error = null
);

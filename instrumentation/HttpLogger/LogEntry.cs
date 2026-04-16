namespace HttpLogger;

internal sealed record LogEntry(
    DateTimeOffset Timestamp,
    string Direction,
    string Method,
    string Url,
    int StatusCode,
    double ElapsedMs,
    string? Request = null,
    string? Response = null
);

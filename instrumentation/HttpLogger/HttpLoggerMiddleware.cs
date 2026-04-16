using System.Buffers;
using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace HttpLogger;

internal sealed class HttpLoggerMiddleware(RequestDelegate next, LogSender sender)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Request.EnableBuffering();

        CapturingStream? capture = null;
        var originalBody = context.Response.Body;

        context.Response.OnStarting(() =>
        {
            if (context.Response.StatusCode >= 400)
            {
                capture = new CapturingStream(originalBody);
                context.Response.Body = capture;
            }
            return Task.CompletedTask;
        });

        var start = Stopwatch.GetTimestamp();

        await next(context);

        var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        var req = context.Request;
        var res = context.Response;
        var url = $"{req.Scheme}://{req.Host}{req.Path}{req.QueryString}";

        if (capture is not null)
        {
            context.Response.Body = originalBody;

            req.Body.Position = 0;
            var requestBody = await ReadBodyAsync(req.Body, context.RequestAborted);
            var responseBody = capture.GetBodyAsString();

            sender.Ship(new LogEntry(
                DateTimeOffset.UtcNow,
                "Inbound",
                req.Method,
                url,
                res.StatusCode,
                elapsed,
                FormatRequest(req, requestBody),
                FormatResponse(res, responseBody)));
        }
        else
        {
            sender.Ship(new LogEntry(
                DateTimeOffset.UtcNow,
                "Inbound",
                req.Method,
                url,
                res.StatusCode,
                elapsed));
        }
    }

    private static string FormatRequest(HttpRequest request, string body)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{request.Method} {request.Path}{request.QueryString} HTTP/1.1");
        sb.AppendLine($"Host: {request.Host}");
        foreach (var (key, value) in request.Headers)
            sb.AppendLine($"{key}: {value}");
        if (!string.IsNullOrWhiteSpace(body))
        {
            sb.AppendLine();
            sb.Append(body);
        }
        return sb.ToString();
    }

    private static string FormatResponse(HttpResponse response, string body)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"HTTP/1.1 {response.StatusCode}");
        foreach (var (key, value) in response.Headers)
            sb.AppendLine($"{key}: {value}");
        if (!string.IsNullOrWhiteSpace(body))
        {
            sb.AppendLine();
            sb.Append(body);
        }
        return sb.ToString();
    }

    private static async ValueTask<string> ReadBodyAsync(Stream body, CancellationToken ct)
    {
        if (body.Length == 0)
            return string.Empty;

        var length = (int)body.Length;
        var buffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            var read = await body.ReadAsync(buffer.AsMemory(0, length), ct);
            return Encoding.UTF8.GetString(buffer, 0, read);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private sealed class CapturingStream(Stream inner) : Stream
    {
        private readonly MemoryStream _buffer = new();

        public string GetBodyAsString() => Encoding.UTF8.GetString(_buffer.GetBuffer(), 0, (int)_buffer.Length);

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken ct) => inner.FlushAsync(ct);
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count)
        {
            inner.Write(buffer, offset, count);
            _buffer.Write(buffer, offset, count);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        {
            await inner.WriteAsync(buffer.AsMemory(offset, count), ct);
            await _buffer.WriteAsync(buffer.AsMemory(offset, count), ct);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
        {
            await inner.WriteAsync(buffer, ct);
            await _buffer.WriteAsync(buffer, ct);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _buffer.Dispose();
            base.Dispose(disposing);
        }
    }
}

using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace HttpLogger;

internal sealed class HttpLoggerMiddleware(RequestDelegate next, LogSender sender)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Request.EnableBuffering();
        var requestId = HttpLoggerContext.GetOrCreateRequestId(context);

        CapturingStream? capture = null;
        var originalBody = context.Response.Body;
        Exception? failure = null;

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

        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            if (capture is not null)
                context.Response.Body = originalBody;
        }

        var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        if (failure is null && context.Response.StatusCode < 400)
            return;

        var responseBody = capture?.GetBodyAsString();
        sender.Ship(HttpLoggerLog.CreateInbound(context, requestId, elapsed, responseBody, failure));

        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
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

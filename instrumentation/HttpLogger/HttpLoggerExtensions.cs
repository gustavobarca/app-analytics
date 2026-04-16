using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HttpLogger;

public static class HttpLoggerExtensions
{
    public static IServiceCollection AddHttpLoggerSink(this IServiceCollection services, Uri endpoint)
    {
        services.AddHttpClient(LogSender.ClientName, client =>
        {
            client.BaseAddress = endpoint;
            client.DefaultRequestVersion = HttpVersion.Version20;
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
        });

        services.TryAddSingleton<LogSender>();

        return services;
    }

    public static IApplicationBuilder UseCustomHttpLogger(this IApplicationBuilder app)
        => app.UseMiddleware<HttpLoggerMiddleware>();

    public static IHttpClientBuilder AddHttpLogger(this IHttpClientBuilder builder)
    {
        builder.Services.TryAddTransient<HttpLoggerHandler>();
        return builder.AddHttpMessageHandler<HttpLoggerHandler>();
    }
}

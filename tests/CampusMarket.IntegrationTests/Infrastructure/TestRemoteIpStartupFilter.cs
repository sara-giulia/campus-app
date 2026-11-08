using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace CampusMarket.IntegrationTests.Infrastructure;

// Sets the request's remote IP from the X-Test-Remote-Ip header, before any other middleware runs.
public sealed class TestRemoteIpStartupFilter : IStartupFilter
{
    public const string HeaderName = "X-Test-Remote-Ip";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            if (context.Request.Headers.TryGetValue(HeaderName, out var value))
                context.Connection.RemoteIpAddress = IPAddress.Parse(value.ToString());

            await nextMiddleware(context);
        });

        next(app);
    };
}

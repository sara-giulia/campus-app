using System.Text.Json;
using System.Text.Json.Serialization;
using CampusMarket.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CampusMarket.UnitTests.Api;

/// <summary>
/// Starts the real API (same setup as Program.cs) on a free port on this computer,
/// and gives the test an HttpClient that talks to it, like Postman does.
/// Each test starts its own API, so the in-memory data is empty at the start.
/// </summary>
public sealed class TestApi : IAsyncDisposable
{
    // Same JSON settings as the API: enums as text ("Textbook").
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly WebApplication _app;

    public HttpClient Client { get; }

    private TestApi(WebApplication app, HttpClient client)
    {
        _app = app;
        Client = client;
    }

    /// <param name="replaceServices">
    /// Optional: register fake services here. They are added after the real ones, so they win.
    /// </param>
    public static async Task<TestApi> StartAsync(Action<IServiceCollection>? replaceServices = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");   // port 0 = any free port
        builder.Logging.ClearProviders();

        builder.Services.AddCampusMarket();
        replaceServices?.Invoke(builder.Services);

        var app = builder.Build();
        app.MapCampusMarket();
        await app.StartAsync();

        // UseProxy = false: requests to 127.0.0.1 never go through a proxy.
        var client = new HttpClient(new HttpClientHandler { UseProxy = false })
        {
            BaseAddress = new Uri(app.Urls.First())
        };

        return new TestApi(app, client);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}


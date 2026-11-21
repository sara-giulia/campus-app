using CampusMarket.Api;
using CampusMarket.Api.Infrastructure.ErrorHandling;

var builder = WebApplication.CreateBuilder(args);

// Hosts like Render pass the public port through PORT; keep the launch-settings
// port in Development when PORT is not set.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// Services and endpoints are set up in CampusMarketSetup, so the tests can reuse them
builder.Services.AddCampusMarket(builder.Configuration);
builder.Services.AddCampusMarketErrorHandling();

var app = builder.Build();

app.ValidateCampusMarketProductionConfiguration();

app.MapHealthChecks("/health");

app.EnsureCampusMarketDatabase();

// Order matters: a middleware only sees what runs after it.
// Exceptions first, then the empty 401/403/404/429 answers, then auth and rate limiting.
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapCampusMarket();

await app.RunAsync();
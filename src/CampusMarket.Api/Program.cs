using CampusMarket.Api;
using CampusMarket.Api.Infrastructure.ErrorHandling;

var builder = WebApplication.CreateBuilder(args);

// Hosts like Render pass the public port through PORT; keep the launch-settings
// port in Development when PORT is not set.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}"); // NOSONAR: TLS is terminated by the platform proxy; the container listens on plain HTTP inside the private network
}

// Services and endpoints are set up in CampusMarketSetup, so the tests can reuse them
builder.Services.AddCampusMarket(builder.Configuration);
builder.Services.AddCampusMarketErrorHandling();

var app = builder.Build();

app.ValidateCampusMarketProductionConfiguration();

app.MapHealthChecks("/health");

app.EnsureCampusMarketDatabase();

// Order matters: a middleware only sees what runs after it.
// CORS first, so the frontend also gets the CORS headers on 401/404/500 answers.
// Then exceptions, the empty 401/403/404/429 answers, auth and rate limiting.
app.UseCors(CampusMarketSetup.FrontendCorsPolicy);
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapCampusMarket();

await app.RunAsync();
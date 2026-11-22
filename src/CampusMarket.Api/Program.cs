using CampusMarket.Api;
using CampusMarket.Api.Infrastructure.ErrorHandling;
using CampusMarket.Api.Infrastructure.Logging;
using CampusMarket.Api.Infrastructure.Observability;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;

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

app.UseMiddleware<CorrelationIdMiddleware>();

// Observability: times every request and records (route template, status,
// duration). Placed after CorrelationId so the finally block sees the final
// response status (even after UseExceptionHandler has rewritten a 500).
app.UseMiddleware<MetricsMiddleware>();

app.ValidateCampusMarketProductionConfiguration();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

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
app.UseOutputCache();

app.MapCampusMarket();

// Admin-only observability endpoint; mapped here (not from MapCampusMarket)
// because the architecture tests forbid Features from depending on Infrastructure.
app.MapAdminMetrics();

// API documentation, only in Development: the JSON at /openapi/v1.json, the web page at /scalar.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

await app.RunAsync();
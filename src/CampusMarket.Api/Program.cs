using CampusMarket.Api;
using CampusMarket.Api.Infrastructure.ErrorHandling;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Services and endpoints are set up in CampusMarketSetup, so the tests can reuse them
builder.Services.AddCampusMarket(builder.Configuration);
builder.Services.AddCampusMarketErrorHandling();

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

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
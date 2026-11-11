using CampusMarket.Api;
using CampusMarket.Api.Infrastructure.ErrorHandling;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Services and endpoints are set up in CampusMarketSetup, so the tests can reuse them
builder.Services.AddCampusMarket(builder.Configuration);
builder.Services.AddCampusMarketErrorHandling();

var app = builder.Build();

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

// API documentation, only in Development: the JSON at /openapi/v1.json, the web page at /scalar.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

await app.RunAsync();
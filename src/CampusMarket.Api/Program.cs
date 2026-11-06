using CampusMarket.Api;

var builder = WebApplication.CreateBuilder(args);

// Services and endpoints are set up in CampusMarketSetup, so the tests can reuse them
builder.Services.AddCampusMarket(builder.Configuration);

var app = builder.Build();

app.MapHealthChecks("/health");

app.EnsureCampusMarketDatabase();

app.UseRateLimiter();

app.MapCampusMarket();

await app.RunAsync();

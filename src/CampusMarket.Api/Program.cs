using CampusMarket.Api;

var builder = WebApplication.CreateBuilder(args);

// Services and endpoints are set up in CampusMarketSetup, so the tests can reuse them
builder.Services.AddCampusMarket();

var app = builder.Build();

app.MapCampusMarket();

await app.RunAsync();


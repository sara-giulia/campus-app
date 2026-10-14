using System.Text.Json.Serialization;
using CampusMarket.Api.Features.Listings;
using CampusMarket.Api.Features.Members;

var builder = WebApplication.CreateBuilder(args);

// Application clock: tests replace it with a fake clock
builder.Services.AddSingleton(TimeProvider.System);

// Enums are sent as text in JSON ("Active"), not as numbers (1)
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.MapGet("/", () => "CampusMarket API is running");

// All endpoints live under /api/v1; each group is created in its own *Endpoints class
var api = app.MapGroup("/api/v1");
api.MapListingsEndpoints();
api.MapMembersEndpoints();

app.Run();
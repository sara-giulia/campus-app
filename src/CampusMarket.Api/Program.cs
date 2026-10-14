using System.Text.Json.Serialization;
using CampusMarket.Api.Features.Listings;
using CampusMarket.Api.Features.Members;

var builder = WebApplication.CreateBuilder(args);

// Ceasul aplicatiei: in teste se inlocuieste cu un ceas fals
builder.Services.AddSingleton(TimeProvider.System);

// Enum-urile apar ca text in JSON ("Active"), nu ca numar (1)
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.MapGet("/", () => "CampusMarket API is running");

// Toate endpoint-urile stau sub /api/v1
var api = app.MapGroup("/api/v1");
api.MapListingsEndpoints();
api.MapMembersEndpoints();

app.Run();
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "CampusMarket API is running");

// Toate endpoint-urile stau sub /api/v1
var api = app.MapGroup("/api/v1");
var listings = api.MapGroup("/listings");   // => /api/v1/listings
var members = api.MapGroup("/members");     // => /api/v1/members

app.Run();
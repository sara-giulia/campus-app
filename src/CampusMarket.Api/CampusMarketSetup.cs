using System.Text.Json.Serialization;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Listings;
using CampusMarket.Api.Features.Members;
using CampusMarket.Api.Infrastructure.Background;
using CampusMarket.Api.Infrastructure.ML;
using CampusMarket.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CampusMarket.Api.Infrastructure.Auth;
using CampusMarket.Api.Features.Auth;
using System.Text;
using CampusMarket.Api.Common.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using CampusMarket.Api.Domain.Favorites;
using CampusMarket.Api.Features.Favorites;
using CampusMarket.Api.Domain.Reports;
using CampusMarket.Api.Features.Moderation;

namespace CampusMarket.Api;

/// The whole app setup in one place. Program.cs uses it to run the API,
/// and the API tests use it to start the same API on a free port.
public static class CampusMarketSetup
{
    public static IServiceCollection AddCampusMarket(this IServiceCollection services, IConfiguration configuration)
    {
        // Application clock: tests can replace it with a fake clock
        services.AddSingleton(TimeProvider.System);

        services.AddCampusMarketMl(configuration);

        services.AddDbContext<CampusMarketDbContext>((sp, options) =>
            options.UseSqlite(sp.GetRequiredService<IConfiguration>().GetConnectionString("CampusMarket")));

        services.AddScoped<IListingRepository, EfListingRepository>();
        services.AddScoped<IMemberRepository, EfMemberRepository>();
        services.AddScoped<IRefreshTokenRepository, EfRefreshTokenRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IFavoriteRepository, EfFavoriteRepository>();
        services.AddScoped<IReportRepository, EfReportRepository>();
        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
        services.AddOptions<JwtOptions>().BindConfiguration("Jwt");
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy("Admin", policy => policy.RequireRole("Admin"));
        
        // At most 5 login attempts per minute from the same IP address, then 429.
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("login", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1)
                    }));
        });

        services.AddHandlers(typeof(CampusMarketSetup).Assembly);

        // Background reservation expiry: registered only when enabled, so tests and
        // scenarios that need a quiet host can turn it off through configuration.
        services.AddOptions<ReservationExpiryOptions>()
            .Bind(configuration.GetSection(ReservationExpiryOptions.SectionName));

        var expiryOptions = configuration.GetSection(ReservationExpiryOptions.SectionName)
            .Get<ReservationExpiryOptions>() ?? new ReservationExpiryOptions();
        if (expiryOptions.Enabled)
            services.AddHostedService<ReservationExpiryService>();

        // Enums are sent as text in JSON ("Active"), not as numbers (1)
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        // GET /health: the API is up and can reach the database.
        services.AddHealthChecks().AddDbContextCheck<CampusMarketDbContext>();
        
        return services;
    }

    public static WebApplication EnsureCampusMarketDatabase(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusMarketDbContext>();
        db.Database.Migrate();

        SeedAdmin(scope.ServiceProvider, db, app.Configuration);

        return app;
    }

    // Creates a default Admin when Seed:AdminEmail is set (only in appsettings.Development.json).
    private static void SeedAdmin(IServiceProvider services, CampusMarketDbContext db, IConfiguration config)
    {
        var email = config["Seed:AdminEmail"];
        var password = config["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        // Same normalization as Register (trim + lower case), so the lookup matches the stored email.
        var adminEmail = Email.Create(email);
        if (adminEmail.IsFailure)
            return;

        if (db.Members.AsEnumerable().Any(m => m.Email == adminEmail.Value))
            return;

        var hasher = services.GetRequiredService<IPasswordHasher>();
        var clock = services.GetRequiredService<TimeProvider>();

        var admin = Member.Register("Admin", adminEmail.Value.Value, hasher.Hash(password), clock.GetUtcNow(), MemberRole.Admin);
        if (admin.IsFailure)
            return;

        db.Members.Add(admin.Value);
        db.SaveChanges();
    }

    public static IEndpointRouteBuilder MapCampusMarket(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", () => "CampusMarket API is running");

        // All endpoints live under /api/v1; each group is created in its own *Endpoints class
        var api = app.MapGroup("/api/v1");
        api.MapListingsEndpoints();
        api.MapMembersEndpoints();
        api.MapAuthEndpoints();
        api.MapFavoritesEndpoints();
        api.MapModerationEndpoints();

        return app;
    }
}

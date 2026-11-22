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
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Favorites;
using CampusMarket.Api.Features.Conversations;
using CampusMarket.Api.Features.Favorites;
using CampusMarket.Api.Domain.Reports;
using CampusMarket.Api.Features.Moderation;
using CampusMarket.Api.Domain.Reviews;
using CampusMarket.Api.Features.Reviews;
using CampusMarket.Api.Features.Admin;

namespace CampusMarket.Api;

/// The whole app setup in one place. Program.cs uses it to run the API,
/// and the API tests use it to start the same API on a free port.
public static class CampusMarketSetup
{
    public const string FrontendCorsPolicy = "Frontend";

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
        services.AddScoped<IReviewRepository, EfReviewRepository>();
        services.AddScoped<ISaleRecordRepository, EfSaleRecordRepository>();
        services.AddScoped<IConversationRepository, EfConversationRepository>();
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
        
        // At most 5 login attempts and 5 registrations per minute from the same IP address, then 429.
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

            // Same idea as login: a burst of sign-ups from one IP is blocked (429). The limit is
            // configurable so the integration tests, which register many members, can raise it.
            var registerLimit = configuration.GetValue("RateLimiting:RegisterPermitLimit", 5);
            options.AddPolicy("register", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = registerLimit,
                        Window = TimeSpan.FromMinutes(1)
                    }));
        });

        services.AddHandlers(typeof(CampusMarketSetup).Assembly);
        
        // API description for the documentation page (served only in Development, see Program.cs).
        services.AddOpenApi();

        // Background reservation expiry: registered only when enabled, so tests and
        // scenarios that need a quiet host can turn it off through configuration.
        services.AddOptions<ReservationExpiryOptions>()
            .Bind(configuration.GetSection(ReservationExpiryOptions.SectionName));

        // Same default as ReservationExpiryOptions.Enabled when the setting is missing.
        var expiryEnabled = configuration.GetValue(
            $"{ReservationExpiryOptions.SectionName}:{nameof(ReservationExpiryOptions.Enabled)}",
            new ReservationExpiryOptions().Enabled);
        if (expiryEnabled)
            services.AddHostedService<ReservationExpiryService>();

        // Enums are sent as text in JSON ("Active"), not as numbers (1)
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        // GET /health: the API is up and can reach the database.
        services.AddHealthChecks()
            .AddDbContextCheck<CampusMarketDbContext>(tags: ["ready"])
            .AddCheck<MlModelsHealthCheck>("ml-models", tags: ["ready"]);
        services.AddOutputCache();

        // The web frontend runs on another origin (Vite on :5173 in development),
        // so the browser needs CORS. Only the origins in Cors:AllowedOrigins are allowed;
        // the token goes in the Authorization header, so no cookies (no AllowCredentials).
        // GetChildren instead of Get<string[]>() ?? []: a missing section simply gives no origins.
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").GetChildren()
            .Select(origin => origin.Value!)
            .ToArray();
        services.AddCors(options => options.AddPolicy(FrontendCorsPolicy, policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("Location")));

        return services;
    }

    // Fails fast in Production when the JWT signing key is missing or too
    // short. Dev and Test keep their own keys (appsettings.Development.json /
    // the integration-test factory), so this check is production-only.
    public static WebApplication ValidateCampusMarketProductionConfiguration(this WebApplication app)
    {
        if (!app.Environment.IsProduction())
            return app;

        var key = app.Configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(key) || key.Length < MinJwtKeyLength)
        {
            throw new InvalidOperationException(
                $"Jwt:Key is missing or shorter than {MinJwtKeyLength} characters. " +
                "Set the Jwt__Key environment variable to a random value of at least " +
                $"{MinJwtKeyLength} characters before starting the API in Production.");
        }

        return app;
    }

    private const int MinJwtKeyLength = 32;

    public static WebApplication EnsureCampusMarketDatabase(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusMarketDbContext>();
        db.Database.Migrate();

        SeedAdmin(scope.ServiceProvider, db, app.Configuration);
        SeedDemoDataIfRequested(scope.ServiceProvider, db, app.Configuration);

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

    private const int MinDemoPasswordLength = 8;

    // Idempotent demo seed: only runs when Seed:DemoData is "true" AND the
    // database is empty (no members, no listings). Safe to leave wired across
    // restarts — a redeploy on Render loses the SQLite file, so the first
    // start after a redeploy seeds again, every later start is a no-op.
    // Off by default; tests leave Seed:DemoData unset.
    private static void SeedDemoDataIfRequested(IServiceProvider services, CampusMarketDbContext db, IConfiguration config)
    {
        if (!config.GetValue("Seed:DemoData", false))
            return;

        if (db.Members.Any() || db.Listings.Any())
            return;

        var clock = services.GetRequiredService<TimeProvider>();
        var now = clock.GetUtcNow();

        // Seed the demo listings even when the password env var is unusable, so
        // a fresh Render instance has something to browse; the listings then
        // belong to an orphan seller id, which is fine because nobody can log
        // in as that account.
        var sellerId = TrySeedDemoMember(services, db, config, now) ?? Guid.NewGuid();

        foreach (var draft in BuildDemoListings(sellerId, now))
        {
            draft.Publish();
            db.Listings.Add(draft);
        }

        db.SaveChanges();
    }

    // Returns the demo member's id when Seed:DemoPassword is at least
    // MinDemoPasswordLength characters; null (with a warning in the log)
    // when the env var is missing or too short. The demo member is ALWAYS
    // created with the Student role — never Admin.
    private static Guid? TrySeedDemoMember(IServiceProvider services, CampusMarketDbContext db, IConfiguration config, DateTimeOffset now)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("CampusMarket.DemoSeed");
        var password = config["Seed:DemoPassword"];

        if (string.IsNullOrEmpty(password) || password.Length < MinDemoPasswordLength)
        {
            logger.LogWarning(
                "{Variable} is missing or shorter than {MinLength} characters; seeding the demo listings without a demo member. " +
                "Set the Seed__DemoPassword environment variable to a value of at least that length to enable sign-in as the demo student.",
                "Seed:DemoPassword", MinDemoPasswordLength);
            return null;
        }

        var hasher = services.GetRequiredService<IPasswordHasher>();
        var member = Member.Register(
            "Demo Student",
            "demo@campusmarket.ro",
            hasher.Hash(password),
            now,
            MemberRole.Student);
        if (member.IsFailure)
            return null;

        db.Members.Add(member.Value);
        return member.Value.Id;
    }

    private static List<Listing> BuildDemoListings(Guid sellerId, DateTimeOffset now)
    {
        var drafts = new List<Listing>();
        TryAdd(drafts, Listing.Create(sellerId, "Manual Analiza Matematica anul I", Category.Textbook, Condition.Good, 40m, now, "Stare buna, notite in creion pe ultimele pagini."));
        TryAdd(drafts, Listing.Create(sellerId, "Casti wireless aproape noi", Category.Electronics, Condition.Good, 180m, now, "Autonomie buna, cutia originala."));
        TryAdd(drafts, Listing.Create(sellerId, "Notite curs structuri de date", Category.Notes, Condition.New, 20m, now, "Scrise de mana, sem 2, anul I."));
        return drafts;
    }

    private static void TryAdd(List<Listing> drafts, Result<Listing> result)
    {
        if (result.IsSuccess)
            drafts.Add(result.Value);
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
        api.MapReviewsEndpoints();
        api.MapAdminEndpoints();
        api.MapConversationsEndpoints();

        return app;
    }
}

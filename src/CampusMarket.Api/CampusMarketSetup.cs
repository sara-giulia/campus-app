using System.Text.Json.Serialization;
using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Listings;
using CampusMarket.Api.Features.Members;
using CampusMarket.Api.Infrastructure.Background;
using CampusMarket.Api.Infrastructure.ML;
using CampusMarket.Api.Infrastructure.Observability;
using CampusMarket.Api.Infrastructure.Persistence;
using CampusMarket.Api.Infrastructure.Persistence.Seed;
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
using CampusMarket.Api.Domain.Photos;
using CampusMarket.Api.Common.Storage;
using CampusMarket.Api.Infrastructure.Storage;
using CampusMarket.Api.Features.Photos;
using CampusMarket.Api.Features.Conversations;
using CampusMarket.Api.Features.Favorites;
using CampusMarket.Api.Domain.Reports;
using CampusMarket.Api.Features.Moderation;
using CampusMarket.Api.Domain.Reviews;
using CampusMarket.Api.Features.Reviews;
using CampusMarket.Api.Features.Admin;
using CampusMarket.Api.Common.Email;
using CampusMarket.Api.Infrastructure.Mailing;
using CampusMarket.Api.Domain.Notifications;
using CampusMarket.Api.Features.Notifications;
using CampusMarket.Api.Domain.Categories;
using CampusMarket.Api.Features.Categories;
using CampusMarket.Api.Common.Events;
using CampusMarket.Api.Infrastructure.Events;
using CampusMarket.Api.Common.Images;
using CampusMarket.Api.Infrastructure.Images;
using CampusMarket.Api.Common.Listings;
using CampusMarket.Api.Domain.Offers;
using CampusMarket.Api.Domain.Blocks;
using CampusMarket.Api.Features.Blocks;
using CampusMarket.Api.Features.Offers;

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

        // Observability: a named Meter (OpenTelemetry can read it later) and
        // an in-memory aggregator served at /api/v1/admin/metrics. Singletons
        // so counters survive across requests.
        services.AddSingleton<CampusMarketMeters>();
        services.AddSingleton<MetricsRegistry>();

        services.AddCampusMarketMl(configuration);

        services.AddDbContext<CampusMarketDbContext>((sp, options) =>
            options.UseNpgsql(sp.GetRequiredService<IConfiguration>().GetConnectionString("CampusMarket")));

        services.AddScoped<IListingRepository, EfListingRepository>();
        services.AddScoped<IMemberRepository, EfMemberRepository>();
        services.AddScoped<IRefreshTokenRepository, EfRefreshTokenRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IFavoriteRepository, EfFavoriteRepository>();
        services.AddScoped<IReportRepository, EfReportRepository>();
        services.AddScoped<IReviewRepository, EfReviewRepository>();
        services.AddScoped<ISaleRecordRepository, EfSaleRecordRepository>();
        services.AddScoped<IConversationRepository, EfConversationRepository>();
        services.AddScoped<IOfferRepository, EfOfferRepository>();
        services.AddScoped<IMemberBlockRepository, EfMemberBlockRepository>();
        services.AddScoped<IPasswordResetTokenRepository, EfPasswordResetTokenRepository>();
        services.AddScoped<IPhotoRepository, EfPhotoRepository>();
        services.AddOptions<PhotoStorageOptions>().Bind(configuration.GetSection(PhotoStorageOptions.SectionName));
        services.AddSingleton<IPhotoStorage, LocalPhotoStorage>();
        services.AddScoped<INotificationRepository, EfNotificationRepository>();
        services.AddOptions<LiveEventsOptions>()
            .Bind(configuration.GetSection(LiveEventsOptions.SectionName))
            .Validate(o => o.HeartbeatSeconds is >= 1 and <= 300, "LiveEvents:HeartbeatSeconds must be between 1 and 300.")
            .ValidateOnStart();
        // One instance for the whole app: every request and the unit of work see the same open connections.
        services.AddSingleton<ILiveEvents, InMemoryLiveEvents>();
        services.AddScoped<ICategoryRepository, EfCategoryRepository>();
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

        // Listing expiry: how long a listing stays published (Listings:ExpiryDays, used by publish and
        // renew) and the background service that expires them (ListingExpiry, off in the tests).
        services.AddOptions<ListingOptions>()
            .Bind(configuration.GetSection(ListingOptions.SectionName))
            .Validate(o => o.ExpiryDays >= 1, "Listings:ExpiryDays must be at least 1.")
            .ValidateOnStart();
        services.AddOptions<ListingExpiryOptions>()
            .Bind(configuration.GetSection(ListingExpiryOptions.SectionName));

        var listingExpiryEnabled = configuration.GetValue(
            $"{ListingExpiryOptions.SectionName}:{nameof(ListingExpiryOptions.Enabled)}",
            new ListingExpiryOptions().Enabled);
        if (listingExpiryEnabled)
            services.AddHostedService<ListingExpiryService>();

        AddImageGeneration(services, configuration);

        // Enums are sent as text in JSON ("Active"), not as numbers (1)
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        // Email sender: Brevo HTTP API when Email__ApiKey is set (used on Render free,
        // where outbound SMTP ports are blocked); SMTP when Email:Host is set
        // (e.g. Mailpit on docker compose); otherwise a logger-only sender so the API
        // runs without SMTP in dev and in tests. The Brevo API key and the SMTP
        // username/password are read from env vars by the senders themselves; they
        // must never be in appsettings.*.json.
        services.AddOptions<EmailOptions>().BindConfiguration(EmailOptions.SectionName);
        services.AddOptions<FrontendOptions>().BindConfiguration(FrontendOptions.SectionName);
        var brevoApiKey = Environment.GetEnvironmentVariable("Email__ApiKey");
        var emailHost = configuration[$"{EmailOptions.SectionName}:{nameof(EmailOptions.Host)}"];
        if (!string.IsNullOrWhiteSpace(brevoApiKey))
        {
            services.AddHttpClient<IEmailSender, BrevoEmailSender>(client =>
                client.BaseAddress = new Uri("https://api.brevo.com/"));
        }
        else if (!string.IsNullOrWhiteSpace(emailHost))
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        else
            services.AddSingleton<IEmailSender, LoggingEmailSender>();

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

    // Generated images for listings without photos: the SVG by default, Ollama when
    // ImageGeneration:Provider is "Ollama" (it falls back to the SVG). The background service only
    // runs when ImageGeneration:Enabled is true (the tests turn it off to keep the host quiet).
    private static void AddImageGeneration(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(ImageGenerationOptions.SectionName);
        services.AddOptions<ImageGenerationOptions>()
            .Bind(section)
            .Validate(o => o.Provider is ImageGenerationOptions.SvgProvider or ImageGenerationOptions.OllamaProvider,
                "ImageGeneration:Provider must be Svg or Ollama.")
            .ValidateOnStart();

        services.AddSingleton<IImageGenerationQueue, ImageGenerationQueue>();
        services.AddSingleton<SvgImageGenerator>();

        var options = new ImageGenerationOptions();
        section.Bind(options);
        if (options.Provider == ImageGenerationOptions.OllamaProvider)
            services.AddHttpClient<IImageGenerator, OllamaImageGenerator>(client =>
            {
                client.BaseAddress = new Uri(options.Ollama.BaseUrl.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromSeconds(options.Ollama.TimeoutSeconds);
            });
        else
            services.AddSingleton<IImageGenerator>(sp => sp.GetRequiredService<SvgImageGenerator>());

        if (options.Enabled)
            services.AddHostedService<ImageGenerationService>();
    }

    // Fails fast in Production when the JWT signing key is missing or too short,
    // when the PostgreSQL connection string (ConnectionStrings__CampusMarket) is missing,
    // or when the SMTP sender is used without TLS. Dev and Test keep their own
    // keys and connection strings (appsettings.Development.json / the integration-test
    // factory) and talk to Mailpit without TLS, so these checks are production-only.
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

        var connectionString = app.Configuration.GetConnectionString("CampusMarket");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:CampusMarket is missing. " +
                "Set the ConnectionStrings__CampusMarket environment variable to a PostgreSQL connection string " +
                "in key/value form (Host=...;Database=...;Username=...;Password=...;SSL Mode=Require), " +
                "not a postgresql:// URL, before starting the API in Production.");
        }

        var emailHost = app.Configuration[$"{EmailOptions.SectionName}:{nameof(EmailOptions.Host)}"];
        if (!string.IsNullOrWhiteSpace(emailHost))
        {
            var useSsl = app.Configuration.GetValue($"{EmailOptions.SectionName}:{nameof(EmailOptions.UseSsl)}", true);
            if (!useSsl)
            {
                throw new InvalidOperationException(
                    $"{EmailOptions.SectionName}:{nameof(EmailOptions.UseSsl)} must be true in Production when an SMTP host is configured. " +
                    $"Set the {EmailOptions.SectionName}__{nameof(EmailOptions.UseSsl)} environment variable to true (STARTTLS on port 587) " +
                    "before starting the API in Production.");
            }
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

    // Idempotent demo seed: only runs when Seed:DemoData is "true" AND no demo
    // listings have been seeded yet. The member table may already hold rows (an
    // Admin created by SeedAdmin, or a real sign-up) — the demo listings still
    // seed next to them. Safe to leave wired across restarts: a redeploy on
    // Render keeps the PostgreSQL data, so only the first start ever seeds,
    // and every later start is a no-op. Off by default; tests leave
    // Seed:DemoData unset.
    private static void SeedDemoDataIfRequested(IServiceProvider services, CampusMarketDbContext db, IConfiguration config)
    {
        if (!config.GetValue("Seed:DemoData", false))
            return;

        // Only the listing table drives idempotency: a stray member (Admin or
        // a real sign-up) must NOT block the demo catalog, which is the Render
        // regression this skip condition used to cause.
        if (db.Listings.Any())
            return;

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("CampusMarket.DemoSeed");
        var clock = services.GetRequiredService<TimeProvider>();
        var now = clock.GetUtcNow();

        // Seed the demo listings even when the password env var is unusable, so
        // a fresh Render instance has something to browse; the listings then
        // all belong to one orphan seller id, which is fine because nobody can
        // log in as that account.
        var sellerIds = TrySeedDemoMembers(logger, services, db, config, now);
        var seededMemberCount = sellerIds.Count;
        if (sellerIds.Count == 0)
            sellerIds.Add(Guid.NewGuid());

        var listings = DemoListings.BuildListings(sellerIds, now);
        foreach (var listing in listings)
            db.Listings.Add(listing);

        db.SaveChanges();
        
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Demo seed: {MemberCount} members, {ListingCount} listings", seededMemberCount, listings.Count);
        }
    }

    // Returns the ids of the demo members that were added for seeding when
    // Seed:DemoPassword is at least MinDemoPasswordLength characters; an empty
    // list (with a warning in the log) when the env var is missing or too
    // short. Demo members whose email already exists in the database are
    // skipped, so the seed stays idempotent when other startup code (SeedAdmin)
    // reserves an overlapping email. Demo members are ALWAYS created with the
    // Student role — never Admin.
    private static List<Guid> TrySeedDemoMembers(ILogger logger, IServiceProvider services, CampusMarketDbContext db, IConfiguration config, DateTimeOffset now)
    {
        var password = config["Seed:DemoPassword"];

        if (string.IsNullOrEmpty(password) || password.Length < MinDemoPasswordLength)
        {
            logger.LogWarning(
                "{Variable} is missing or shorter than {MinLength} characters; seeding the demo listings without any demo members. " +
                "Set the Seed__DemoPassword environment variable to a value of at least that length to enable sign-in as a demo student.",
                "Seed:DemoPassword", MinDemoPasswordLength);
            return [];
        }

        var hasher = services.GetRequiredService<IPasswordHasher>();
        var members = DemoListings.BuildMembers(hasher, password, now);

        // Pre-load existing emails so a pre-seeded Admin (or any other row)
        // with a colliding address does not fail the SaveChanges on the
        // unique email index.
        var existingEmails = new HashSet<string>(
            db.Members.AsEnumerable().Select(m => m.Email.Value),
            StringComparer.Ordinal);

        var ids = new List<Guid>(members.Count);
        foreach (var member in members)
        {
            if (!existingEmails.Add(member.Email.Value))
                continue;
            db.Members.Add(member);
            ids.Add(member.Id);
        }
        return ids;
    }

    public static IEndpointRouteBuilder MapCampusMarket(this IEndpointRouteBuilder app)
    {
        // GET / is NOT mapped here on purpose: UseDefaultFiles + UseStaticFiles
        // serve wwwroot/index.html for the root in Production (the React SPA on
        // Render). An endpoint on "/" would win over the static files middleware.
        // When wwwroot is missing (tests, dotnet run without a frontend build)
        // the SPA fallback in Program.cs returns 404 for "/", which is fine.

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
        api.MapOffersEndpoints();
        api.MapBlocksEndpoints();
        api.MapPhotosEndpoints();
        api.MapNotificationsEndpoints();
        api.MapCategoriesEndpoints();

        return app;
    }
}

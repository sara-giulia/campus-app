using CampusMarket.Api.Domain.Categories;
using CampusMarket.Api.Domain.Conversations;
using CampusMarket.Api.Domain.Favorites;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Photos;
using CampusMarket.Api.Domain.Notifications;
using CampusMarket.Api.Domain.Reports;
using CampusMarket.Api.Domain.Reviews;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.Api.Infrastructure.Persistence;

public sealed class CampusMarketDbContext : DbContext
{
    public CampusMarketDbContext(DbContextOptions<CampusMarketDbContext> options) : base(options)
    {
    }

    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<SaleRecord> SaleRecords => Set<SaleRecord>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ListingCategory> Categories => Set<ListingCategory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CampusMarketDbContext).Assembly);
    }
}

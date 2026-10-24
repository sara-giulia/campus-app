using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.Api.Infrastructure.Persistence;

public sealed class CampusMarketDbContext : DbContext
{
    public CampusMarketDbContext(DbContextOptions<CampusMarketDbContext> options) : base(options)
    {
    }

    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<Member> Members => Set<Member>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CampusMarketDbContext).Assembly);
    }
}

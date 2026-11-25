using CampusMarket.Api.Domain.Favorites;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Reports;
using CampusMarket.Api.Domain.Reviews;
using CampusMarket.Api.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CampusMarket.IntegrationTests.Infrastructure;

// AddAsync with an id that is already saved cannot be reached over HTTP (the domain always creates a new Guid),
// so these tests call the EF Core repositories directly on the test database.
public sealed class RepositoryTests : IntegrationTestBase
{
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private static Listing PublishedListing(Guid sellerId)
    {
        var listing = Listing.Create(sellerId, "Manual POO", Category.Textbook, Condition.Good, 50m, CreatedAt).Value;
        listing.Publish(DateTimeOffset.UtcNow.AddDays(30).ToMicroseconds());
        return listing;
    }

    private static Listing SoldListing(Member seller, Member buyer)
    {
        var listing = PublishedListing(seller.Id);
        var reservation = listing.Reserve(buyer.Id, CreatedAt).Value;
        listing.ConfirmSale(reservation.Id, CreatedAt);
        return listing;
    }

    private AsyncServiceScope NewScope(out CampusMarketDbContext db)
    {
        var scope = Factory.Services.CreateAsyncScope();
        db = scope.ServiceProvider.GetRequiredService<CampusMarketDbContext>();
        return scope;
    }

    [Fact]
    public async Task Given_ListingAlreadySaved_When_AddingItAgain_Then_ReturnsFalseAndKeepsOneRow()
    {
        // Arrange
        var listing = PublishedListing(SellerId);
        await SeedAsync(listing);
        await using var scope = NewScope(out var db);
        var repository = new EfListingRepository(db);
        var loaded = await db.Set<Listing>().SingleAsync(l => l.Id == listing.Id);

        // Act
        var added = await repository.AddAsync(loaded);

        // Assert
        added.Should().BeFalse();
        await db.SaveChangesAsync();
        var count = await QueryDbAsync(d => d.Set<Listing>().CountAsync(l => l.Id == listing.Id));
        count.Should().Be(1);
    }

    [Fact]
    public async Task Given_NewListing_When_Adding_Then_ReturnsTrueAndSavesIt()
    {
        // Arrange
        var listing = PublishedListing(SellerId);
        await using var scope = NewScope(out var db);
        var repository = new EfListingRepository(db);

        // Act
        var added = await repository.AddAsync(listing);

        // Assert
        added.Should().BeTrue();
        await db.SaveChangesAsync();
        var saved = await QueryDbAsync(d => d.Set<Listing>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(listing.Id);
        saved.Title.Should().Be("Manual POO");
    }

    [Fact]
    public async Task Given_MemberAlreadySaved_When_AddingItAgain_Then_ReturnsFalseAndKeepsOneRow()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        await SeedAsync(member);
        await using var scope = NewScope(out var db);
        var repository = new EfMemberRepository(db);
        var loaded = await db.Set<Member>().SingleAsync(m => m.Id == member.Id);

        // Act
        var added = await repository.AddAsync(loaded);

        // Assert
        added.Should().BeFalse();
        await db.SaveChangesAsync();
        var count = await QueryDbAsync(d => d.Set<Member>().CountAsync(m => m.Id == member.Id));
        count.Should().Be(1);
    }

    [Fact]
    public async Task Given_NewMember_When_Adding_Then_ReturnsTrueAndSavesIt()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        await using var scope = NewScope(out var db);
        var repository = new EfMemberRepository(db);

        // Act
        var added = await repository.AddAsync(member);

        // Assert
        added.Should().BeTrue();
        await db.SaveChangesAsync();
        var saved = await QueryDbAsync(d => d.Set<Member>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(member.Id);
        saved.Email.Value.Should().Be("ana@student.ro");
    }

    [Fact]
    public async Task Given_FavoriteAlreadySaved_When_AddingItAgain_Then_ReturnsFalseAndKeepsOneRow()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = PublishedListing(SellerId);
        var favorite = Favorite.Create(member.Id, listing, CreatedAt.AddHours(1)).Value;
        await SeedAsync(member, listing, favorite);
        await using var scope = NewScope(out var db);
        var repository = new EfFavoriteRepository(db);
        var loaded = await db.Set<Favorite>().SingleAsync(f => f.Id == favorite.Id);

        // Act
        var added = await repository.AddAsync(loaded);

        // Assert
        added.Should().BeFalse();
        await db.SaveChangesAsync();
        var count = await QueryDbAsync(d => d.Set<Favorite>().CountAsync(f => f.Id == favorite.Id));
        count.Should().Be(1);
    }

    [Fact]
    public async Task Given_NewFavorite_When_Adding_Then_ReturnsTrueAndSavesIt()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = PublishedListing(SellerId);
        await SeedAsync(member, listing);
        var favorite = Favorite.Create(member.Id, listing, CreatedAt.AddHours(1)).Value;
        await using var scope = NewScope(out var db);
        var repository = new EfFavoriteRepository(db);

        // Act
        var added = await repository.AddAsync(favorite);

        // Assert
        added.Should().BeTrue();
        await db.SaveChangesAsync();
        var saved = await QueryDbAsync(d => d.Set<Favorite>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(favorite.Id);
        saved.MemberId.Should().Be(member.Id);
        saved.ListingId.Should().Be(listing.Id);
    }

    [Fact]
    public async Task Given_ReportAlreadySaved_When_AddingItAgain_Then_ReturnsFalseAndKeepsOneRow()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = PublishedListing(SellerId);
        var report = Report.Create(member.Id, listing, "Pare o inselatorie", CreatedAt.AddHours(1)).Value;
        await SeedAsync(member, listing, report);
        await using var scope = NewScope(out var db);
        var repository = new EfReportRepository(db);
        var loaded = await db.Set<Report>().SingleAsync(r => r.Id == report.Id);

        // Act
        var added = await repository.AddAsync(loaded);

        // Assert
        added.Should().BeFalse();
        await db.SaveChangesAsync();
        var count = await QueryDbAsync(d => d.Set<Report>().CountAsync(r => r.Id == report.Id));
        count.Should().Be(1);
    }

    [Fact]
    public async Task Given_NewReport_When_Adding_Then_ReturnsTrueAndSavesIt()
    {
        // Arrange
        var member = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = PublishedListing(SellerId);
        await SeedAsync(member, listing);
        var report = Report.Create(member.Id, listing, "Pare o inselatorie", CreatedAt.AddHours(1)).Value;
        await using var scope = NewScope(out var db);
        var repository = new EfReportRepository(db);

        // Act
        var added = await repository.AddAsync(report);

        // Assert
        added.Should().BeTrue();
        await db.SaveChangesAsync();
        var saved = await QueryDbAsync(d => d.Set<Report>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(report.Id);
        saved.ReporterId.Should().Be(member.Id);
        saved.ListingId.Should().Be(listing.Id);
    }

    [Fact]
    public async Task Given_ReviewAlreadySaved_When_AddingItAgain_Then_ReturnsFalseAndKeepsOneRow()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = SoldListing(seller, buyer);
        var review = Review.Create(buyer.Id, listing, 5, "Vanzator corect", CreatedAt.AddHours(1)).Value;
        await SeedAsync(seller, buyer, listing, review);
        await using var scope = NewScope(out var db);
        var repository = new EfReviewRepository(db);
        var loaded = await db.Set<Review>().SingleAsync(r => r.Id == review.Id);

        // Act
        var added = await repository.AddAsync(loaded);

        // Assert
        added.Should().BeFalse();
        await db.SaveChangesAsync();
        var count = await QueryDbAsync(d => d.Set<Review>().CountAsync(r => r.Id == review.Id));
        count.Should().Be(1);
    }

    [Fact]
    public async Task Given_NewReview_When_Adding_Then_ReturnsTrueAndSavesIt()
    {
        // Arrange
        var seller = Member.Create("Ion Ionescu", "ion@student.ro", CreatedAt).Value;
        var buyer = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var listing = SoldListing(seller, buyer);
        await SeedAsync(seller, buyer, listing);
        var review = Review.Create(buyer.Id, listing, 5, "Vanzator corect", CreatedAt.AddHours(1)).Value;
        await using var scope = NewScope(out var db);
        var repository = new EfReviewRepository(db);

        // Act
        var added = await repository.AddAsync(review);

        // Assert
        added.Should().BeTrue();
        await db.SaveChangesAsync();
        var saved = await QueryDbAsync(d => d.Set<Review>().AsNoTracking().SingleAsync());
        saved.Id.Should().Be(review.Id);
        saved.ListingId.Should().Be(listing.Id);
        saved.ReviewerId.Should().Be(buyer.Id);
        saved.Rating.Should().Be(5);
    }
}

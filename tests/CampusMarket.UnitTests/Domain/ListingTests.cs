using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using Xunit;

namespace CampusMarket.UnitTests.Domain;

public class ListingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static Listing CreateDraft(Guid? sellerId = null) =>
        Listing.Create(sellerId ?? Guid.NewGuid(), "Java Book", Category.Textbook, Condition.Good, 50, Now).Value;

    private static Listing CreateActive()
    {
        var listing = CreateDraft();
        listing.Publish();
        return listing;
    }

    [Fact]
    public void Create_ValidData_ShouldCreateDraftWithVersion1()
    {
        var sellerId = Guid.NewGuid();

        var result = Listing.Create(sellerId, "  Java Book  ", Category.Textbook, Condition.Good, 50, Now);

        Assert.True(result.IsSuccess);
        var listing = result.Value;
        Assert.NotEqual(Guid.Empty, listing.Id);
        Assert.Equal(sellerId, listing.SellerId);
        Assert.Equal("Java Book", listing.Title);
        Assert.Equal(Category.Textbook, listing.Category);
        Assert.Equal(Condition.Good, listing.Condition);
        Assert.Equal(50m, listing.Price.Amount);
        Assert.Equal(ListingStatus.Draft, listing.Status);
        Assert.Equal(1, listing.Version);
        Assert.Equal(Now, listing.CreatedAt);
        Assert.Empty(listing.Reservations);
    }

    [Fact]
    public void Create_TitleWithMaxLength_ShouldSucceed()
    {
        var title = new string('a', Listing.MaxTitleLength);

        var result = Listing.Create(Guid.NewGuid(), title, Category.Notes, Condition.New, 1, Now);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Create_EmptySeller_ShouldFail()
    {
        var result = Listing.Create(Guid.Empty, "Java Book", Category.Textbook, Condition.Good, 50, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("Listing.SellerEmpty", result.Error.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Theory]
    [InlineData("Java Book", 99, 1, 50, "Listing.CategoryInvalid")]
    [InlineData("Java Book", 0, 99, 50, "Listing.ConditionInvalid")]
    [InlineData("", 0, 1, 50, "Listing.TitleEmpty")]
    [InlineData("   ", 0, 1, 50, "Listing.TitleEmpty")]
    [InlineData("Java Book", 0, 1, 0, "Money.Invalid")]
    [InlineData("Java Book", 0, 1, -5, "Money.Invalid")]
    public void Create_InvalidDetails_ShouldFailWithCode(
        string title, int category, int condition, int price, string expectedCode)
    {
        var result = Listing.Create(
            Guid.NewGuid(), title, (Category)category, (Condition)condition, price, Now);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error.Code);
    }

    [Fact]
    public void Create_TitleTooLong_ShouldFail()
    {
        var title = new string('a', Listing.MaxTitleLength + 1);

        var result = Listing.Create(Guid.NewGuid(), title, Category.Textbook, Condition.Good, 50, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("Listing.TitleTooLong", result.Error.Code);
    }

    [Fact]
    public void UpdateDetails_ValidData_ShouldChangeDetailsAndIncreaseVersion()
    {
        var listing = CreateDraft();

        var result = listing.UpdateDetails(" C# Notes ", Category.Notes, Condition.Worn, 20);

        Assert.True(result.IsSuccess);
        Assert.Equal("C# Notes", listing.Title);
        Assert.Equal(Category.Notes, listing.Category);
        Assert.Equal(Condition.Worn, listing.Condition);
        Assert.Equal(20m, listing.Price.Amount);
        Assert.Equal(2, listing.Version);
    }

    [Fact]
    public void UpdateDetails_InvalidData_ShouldNotChangeAnything()
    {
        var listing = CreateDraft();

        var result = listing.UpdateDetails("New title", Category.Notes, Condition.Worn, 0);

        Assert.True(result.IsFailure);
        Assert.Equal("Money.Invalid", result.Error.Code);
        Assert.Equal("Java Book", listing.Title);
        Assert.Equal(Category.Textbook, listing.Category);
        Assert.Equal(50m, listing.Price.Amount);
        Assert.Equal(1, listing.Version);
    }

    [Fact]
    public void Publish_Draft_ShouldBecomeActive()
    {
        var listing = CreateDraft();

        var result = listing.Publish();

        Assert.True(result.IsSuccess);
        Assert.Equal(ListingStatus.Active, listing.Status);
        Assert.Equal(2, listing.Version);
    }

    [Fact]
    public void Publish_NotDraft_ShouldFail()
    {
        var listing = CreateActive();

        var result = listing.Publish();

        Assert.True(result.IsFailure);
        Assert.Equal("Listing.NotDraft", result.Error.Code);
        Assert.Equal(2, listing.Version);
    }

    [Fact]
    public void Reserve_EmptyBuyer_ShouldFail()
    {
        var listing = CreateActive();

        var result = listing.Reserve(Guid.Empty, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("Reservation.InvalidBuyer", result.Error.Code);
        Assert.Equal(ListingStatus.Active, listing.Status);
    }

    [Fact]
    public void CancelReservation_UnknownId_ShouldReturnNotFound()
    {
        var listing = CreateActive();
        listing.Reserve(Guid.NewGuid(), Now);

        var result = listing.CancelReservation(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("Reservation.NotFound", result.Error.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal(ListingStatus.Reserved, listing.Status);
    }

    [Fact]
    public void Reservation_Cancelled_ShouldNeverBeExpired()
    {
        var listing = CreateActive();
        var reservation = listing.Reserve(Guid.NewGuid(), Now).Value;

        listing.CancelReservation(reservation.Id);

        Assert.False(reservation.IsExpired(Now.AddHours(100)));
        Assert.Equal(Now, reservation.CreatedAt);
        Assert.NotEqual(Guid.Empty, reservation.Id);
    }
}


using System.Net;
using System.Net.Http.Json;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.Views;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using static CampusMarket.IntegrationTests.Offers.OfferTestData;

namespace CampusMarket.IntegrationTests.Views;

public sealed class RecordViewTests : IntegrationTestBase
{
    private readonly Member _seller = NewMember("Ion Ionescu", "ion@student.ro");
    private readonly Member _buyer = NewMember("Ana Popescu", "ana@student.ro");

    private Task<HttpResponseMessage> PostView(Guid listingId, string? visitorId = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/listings/{listingId}/views");
        if (visitorId is not null)
            request.Headers.Add("X-Visitor-Id", visitorId);
        return Client.SendAsync(request);
    }

    private static string NewVisitor() => Guid.NewGuid().ToString();

    private static string VisitorKey(string visitorId) => $"v:{Guid.Parse(visitorId):N}";

    private Task<List<ListingView>> ViewsAsync() =>
        QueryDbAsync(db => db.Set<ListingView>().AsNoTracking().OrderBy(v => v.CreatedAt).ToListAsync());

    private async Task<Listing> SeedListingAsync(ListingStatus status)
    {
        var listing = ActiveListing(_seller.Id);
        if (status == ListingStatus.Draft)
            listing = Listing.Create(_seller.Id, "Manual POO", Category.Textbook, Condition.Good, 100m, CreatedAt).Value;

        switch (status)
        {
            case ListingStatus.Hidden:
                listing.Hide();
                break;
            case ListingStatus.Reserved:
                listing.Reserve(_buyer.Id, DateTimeOffset.UtcNow);
                break;
            case ListingStatus.Sold:
                var reservation = listing.Reserve(_buyer.Id, DateTimeOffset.UtcNow).Value;
                listing.ConfirmSale(reservation.Id, DateTimeOffset.UtcNow);
                break;
            case ListingStatus.Expired:
                listing.Expire(DateTimeOffset.UtcNow.AddDays(31));
                break;
        }

        listing.Status.Should().Be(status);
        await SeedAsync(_seller, _buyer, listing);
        return listing;
    }

    [Fact]
    public async Task Given_VisitorWithoutToken_When_ViewingActiveListing_Then_Returns204AndSavesOneView()
    {
        // Arrange
        var listing = await SeedListingAsync(ListingStatus.Active);
        var visitor = NewVisitor();
        var dayBefore = DateOnly.FromDateTime(DateTime.UtcNow);
        var before = DateTimeOffset.UtcNow.ToMicroseconds();

        // Act
        var response = await PostView(listing.Id, visitor);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
        var view = (await ViewsAsync()).Should().ContainSingle().Subject;
        view.ListingId.Should().Be(listing.Id);
        view.ViewerKey.Should().Be(VisitorKey(visitor));
        view.Day.Should().BeOneOf(dayBefore, DateOnly.FromDateTime(DateTime.UtcNow));
        view.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Given_SameVisitorTwiceOnTheSameDay_When_Viewing_Then_BothReturn204AndOneViewStays()
    {
        // Arrange
        var listing = await SeedListingAsync(ListingStatus.Active);
        var visitor = NewVisitor();
        (await PostView(listing.Id, visitor)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var first = (await ViewsAsync()).Single();

        // Act
        var response = await PostView(listing.Id, visitor);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var view = (await ViewsAsync()).Should().ContainSingle().Subject;
        view.Id.Should().Be(first.Id);
        view.CreatedAt.Should().Be(first.CreatedAt);
    }

    [Fact]
    public async Task Given_TwoVisitors_When_Viewing_Then_TwoViewsAreSaved()
    {
        // Arrange
        var listing = await SeedListingAsync(ListingStatus.Active);
        var first = NewVisitor();
        var second = NewVisitor();

        // Act
        await PostView(listing.Id, first);
        await PostView(listing.Id, second);

        // Assert
        (await ViewsAsync()).Select(v => v.ViewerKey).Should().BeEquivalentTo(VisitorKey(first), VisitorKey(second));
    }

    [Fact]
    public async Task Given_LoggedInMember_When_Viewing_Then_SavesTheViewWithTheMemberKey()
    {
        // Arrange
        var listing = await SeedListingAsync(ListingStatus.Active);
        AuthenticateAs(_buyer.Id);

        // Act
        var response = await PostView(listing.Id);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ViewsAsync()).Should().ContainSingle().Which.ViewerKey.Should().Be($"m:{_buyer.Id:N}");
    }

    [Fact]
    public async Task Given_MemberWithAndWithoutVisitorHeader_When_Viewing_Then_TheTokenWinsAndOneViewIsSaved()
    {
        // Arrange
        var listing = await SeedListingAsync(ListingStatus.Active);
        AuthenticateAs(_buyer.Id);

        // Act
        var withHeader = await PostView(listing.Id, NewVisitor());
        var withoutHeader = await PostView(listing.Id);

        // Assert
        withHeader.StatusCode.Should().Be(HttpStatusCode.NoContent);
        withoutHeader.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ViewsAsync()).Should().ContainSingle().Which.ViewerKey.Should().Be($"m:{_buyer.Id:N}");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_TheSeller_When_Viewing_Then_Returns204AndSavesNothing(bool withVisitorHeader)
    {
        // Arrange
        var listing = await SeedListingAsync(ListingStatus.Active);
        AuthenticateAs(_seller.Id);

        // Act
        var response = await PostView(listing.Id, withVisitorHeader ? NewVisitor() : null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ViewsAsync()).Should().BeEmpty();
    }

    [Theory]
    [InlineData(ListingStatus.Draft)]
    [InlineData(ListingStatus.Hidden)]
    public async Task Given_ListingThatIsNotPublic_When_Viewing_Then_Returns204AndSavesNothing(ListingStatus status)
    {
        // Arrange
        var listing = await SeedListingAsync(status);

        // Act
        var response = await PostView(listing.Id, NewVisitor());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ViewsAsync()).Should().BeEmpty();
    }

    [Theory]
    [InlineData(ListingStatus.Reserved)]
    [InlineData(ListingStatus.Sold)]
    [InlineData(ListingStatus.Expired)]
    public async Task Given_ReservedSoldOrExpiredListing_When_Viewing_Then_SavesTheView(ListingStatus status)
    {
        // Arrange
        var listing = await SeedListingAsync(status);

        // Act
        var response = await PostView(listing.Id, NewVisitor());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ViewsAsync()).Should().ContainSingle();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    [InlineData("")]
    public async Task Given_NoTokenAndNoValidVisitorId_When_Viewing_Then_Returns400ViewerMissingAndSavesNothing(string? header)
    {
        // Arrange
        var listing = await SeedListingAsync(ListingStatus.Active);

        // Act
        var response = await PostView(listing.Id, header);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("View.ViewerMissing");
        error.Type.Should().Be(ErrorType.Validation);
        (await ViewsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Given_UnknownListing_When_Viewing_Then_Returns404ListingNotFound()
    {
        // Act
        var response = await PostView(Guid.NewGuid(), NewVisitor());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<Error>(Json);
        error!.Code.Should().Be("Listing.NotFound");
        error.Type.Should().Be(ErrorType.NotFound);
        (await ViewsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Given_ViewOfYesterdayBySameVisitor_When_ViewingToday_Then_AddsASecondView()
    {
        // Arrange
        var listing = await SeedListingAsync(ListingStatus.Active);
        var visitor = NewVisitor();
        await SeedAsync(ListingView.Create(listing.Id, VisitorKey(visitor), DateTimeOffset.UtcNow.AddDays(-1)));

        // Act
        var response = await PostView(listing.Id, visitor);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var views = await ViewsAsync();
        views.Should().HaveCount(2);
        views.Select(v => v.Day).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_TenParallelCallsOfTheSameVisitor_When_Viewing_Then_AllReturn204AndOneRowExists()
    {
        // Arrange
        var listing = await SeedListingAsync(ListingStatus.Active);
        var visitor = NewVisitor();

        // Act (INSERT ... ON CONFLICT DO NOTHING: no duplicate, no unique-index failure)
        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => PostView(listing.Id, visitor)));

        // Assert
        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.NoContent);
        (await ViewsAsync()).Should().ContainSingle().Which.ViewerKey.Should().Be(VisitorKey(visitor));
    }

    [Fact]
    public async Task Given_ViewsOfAListing_When_TheListingIsDeleted_Then_TheViewsAreDeleted()
    {
        // Arrange
        var listing = await SeedListingAsync(ListingStatus.Active);
        await PostView(listing.Id, NewVisitor());
        AuthenticateAs(_seller.Id);

        // Act
        var response = await Client.DeleteAsync($"/api/v1/listings/{listing.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ViewsAsync()).Should().BeEmpty();
    }
}

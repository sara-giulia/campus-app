using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Reports;
using CampusMarket.Api.Features.Moderation.GetReportedListings;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Moderation.GetReportedListings;

public class GetReportedListingsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ReporterId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherReporterId = new("33333333-3333-3333-3333-333333333333");

    private static Listing BuildPublishedListing(string title = "Algebra book")
    {
        var listing = Listing.Create(SellerId, title, Category.Textbook, Condition.Good, 25m, Now).Value;
        listing.Publish();
        return listing;
    }

    [Fact]
    public async Task Given_NoReports_When_Handling_Then_ReturnsEmptyPage()
    {
        var reports = new FakeReportRepository();
        var listings = new FakeListingRepository();
        var handler = new GetReportedListingsHandler(reports, listings);
        var query = new GetReportedListingsQuery();

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.Count.Should().Be(0);
        result.Value.TotalCount.Should().Be(0);
        reports.Items.Should().BeEmpty();
        listings.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_ListingWithTwoUserReports_When_Handling_Then_ReturnsOneItemWithCountAndReasonsNewestFirst()
    {
        var listing = BuildPublishedListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var reports = new FakeReportRepository();
        reports.Items.Add(Report.Create(ReporterId, listing, "Older reason", Now.AddHours(-2)).Value);
        reports.Items.Add(Report.Create(OtherReporterId, listing, "Newer reason", Now.AddHours(-1)).Value);
        var handler = new GetReportedListingsHandler(reports, listings);
        var query = new GetReportedListingsQuery();

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        var item = result.Value.Items[0];
        item.ListingId.Should().Be(listing.Id);
        item.Title.Should().Be("Algebra book");
        item.SellerId.Should().Be(SellerId);
        item.Status.Should().Be(ListingStatus.Active);
        item.ReportCount.Should().Be(2);
        item.FlaggedAsSpam.Should().BeFalse();
        item.SpamProbability.Should().BeNull();
        item.LastReportedAt.Should().Be(Now.AddHours(-1));
        item.Reasons.Should().Equal("Newer reason", "Older reason");
        result.Value.Count.Should().Be(1);
        result.Value.TotalCount.Should().Be(1);
        reports.Items.Should().HaveCount(2);
        listings.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Given_ListingWithAutomaticReport_When_Handling_Then_ItemIsFlaggedAsSpamWithProbability()
    {
        var listing = BuildPublishedListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var reports = new FakeReportRepository();
        reports.Items.Add(Report.CreateAutomatic(listing.Id, 0.9, Now.AddHours(-1)));
        var handler = new GetReportedListingsHandler(reports, listings);
        var query = new GetReportedListingsQuery();

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].FlaggedAsSpam.Should().BeTrue();
        result.Value.Items[0].SpamProbability.Should().Be(0.9);
        result.Value.Items[0].Reasons.Should().Equal(Report.AutomaticReason);
        reports.Items.Should().HaveCount(1);
        listings.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Given_ListingsWithDifferentReportCounts_When_Handling_Then_MoreReportedComesFirst()
    {
        var once = BuildPublishedListing("Reported once");
        var twice = BuildPublishedListing("Reported twice");
        var listings = new FakeListingRepository();
        listings.Items[once.Id] = once;
        listings.Items[twice.Id] = twice;
        var reports = new FakeReportRepository();
        reports.Items.Add(Report.Create(ReporterId, once, "reason", Now.AddHours(-1)).Value);
        reports.Items.Add(Report.Create(ReporterId, twice, "reason", Now.AddHours(-3)).Value);
        reports.Items.Add(Report.Create(OtherReporterId, twice, "reason", Now.AddHours(-2)).Value);
        var handler = new GetReportedListingsHandler(reports, listings);
        var query = new GetReportedListingsQuery();

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.ListingId).Should().Equal(twice.Id, once.Id);
        reports.Items.Should().HaveCount(3);
        listings.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_ListingsWithSameReportCount_When_Handling_Then_NewestLastReportedAtComesFirst()
    {
        var older = BuildPublishedListing("Older");
        var newer = BuildPublishedListing("Newer");
        var listings = new FakeListingRepository();
        listings.Items[older.Id] = older;
        listings.Items[newer.Id] = newer;
        var reports = new FakeReportRepository();
        reports.Items.Add(Report.Create(ReporterId, older, "reason", Now.AddHours(-2)).Value);
        reports.Items.Add(Report.Create(ReporterId, newer, "reason", Now.AddHours(-1)).Value);
        var handler = new GetReportedListingsHandler(reports, listings);
        var query = new GetReportedListingsQuery();

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.ListingId).Should().Equal(newer.Id, older.Id);
        reports.Items.Should().HaveCount(2);
        listings.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_ListingsWithSameReportCountAndSameTime_When_Handling_Then_OrdersByListingId()
    {
        var first = BuildPublishedListing("First");
        var second = BuildPublishedListing("Second");
        var listings = new FakeListingRepository();
        listings.Items[first.Id] = first;
        listings.Items[second.Id] = second;
        var reports = new FakeReportRepository();
        reports.Items.Add(Report.Create(ReporterId, first, "reason", Now).Value);
        reports.Items.Add(Report.Create(ReporterId, second, "reason", Now).Value);
        var handler = new GetReportedListingsHandler(reports, listings);
        var query = new GetReportedListingsQuery();

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Should().BeInAscendingOrder(i => i.ListingId);
        reports.Items.Should().HaveCount(2);
        listings.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_ReportsOfHiddenListing_When_Handling_Then_AreNotReturnedNorCounted()
    {
        var visible = BuildPublishedListing("Visible");
        var hidden = BuildPublishedListing("Hidden");
        var listings = new FakeListingRepository();
        listings.Items[visible.Id] = visible;
        listings.Items[hidden.Id] = hidden;
        var reports = new FakeReportRepository();
        reports.Items.Add(Report.Create(ReporterId, visible, "reason", Now.AddHours(-2)).Value);
        reports.Items.Add(Report.Create(ReporterId, hidden, "reason", Now.AddHours(-1)).Value);
        hidden.Hide();
        var handler = new GetReportedListingsHandler(reports, listings);
        var query = new GetReportedListingsQuery();

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.ListingId).Should().Equal(visible.Id);
        result.Value.Count.Should().Be(1);
        result.Value.TotalCount.Should().Be(1);
        reports.Items.Should().HaveCount(2);
        listings.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Given_ReportWhoseListingIsMissing_When_Handling_Then_IsNotReturnedNorCounted()
    {
        var kept = BuildPublishedListing("Kept");
        var missing = BuildPublishedListing("Missing");
        var listings = new FakeListingRepository();
        listings.Items[kept.Id] = kept;
        var reports = new FakeReportRepository();
        reports.Items.Add(Report.Create(ReporterId, kept, "reason", Now.AddHours(-2)).Value);
        reports.Items.Add(Report.Create(ReporterId, missing, "reason", Now.AddHours(-1)).Value);
        var handler = new GetReportedListingsHandler(reports, listings);
        var query = new GetReportedListingsQuery();

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.ListingId).Should().Equal(kept.Id);
        result.Value.Count.Should().Be(1);
        result.Value.TotalCount.Should().Be(1);
        reports.Items.Should().HaveCount(2);
        listings.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Given_ThreeReportedListings_When_RequestingSecondPageOfTwo_Then_ReturnsOneItem()
    {
        var oldest = BuildPublishedListing("Oldest");
        var middle = BuildPublishedListing("Middle");
        var newest = BuildPublishedListing("Newest");
        var listings = new FakeListingRepository();
        listings.Items[oldest.Id] = oldest;
        listings.Items[middle.Id] = middle;
        listings.Items[newest.Id] = newest;
        var reports = new FakeReportRepository();
        reports.Items.Add(Report.Create(ReporterId, oldest, "reason", Now.AddHours(-3)).Value);
        reports.Items.Add(Report.Create(ReporterId, middle, "reason", Now.AddHours(-2)).Value);
        reports.Items.Add(Report.Create(ReporterId, newest, "reason", Now.AddHours(-1)).Value);
        var handler = new GetReportedListingsHandler(reports, listings);
        var query = new GetReportedListingsQuery(2, 2);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Select(i => i.ListingId).Should().Equal(oldest.Id);
        result.Value.Count.Should().Be(1);
        result.Value.TotalCount.Should().Be(3);
        result.Value.Page.Should().Be(2);
        result.Value.PageSize.Should().Be(2);
        reports.Items.Should().HaveCount(3);
        listings.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task Given_PageSizeOfMax_When_Handling_Then_Succeeds()
    {
        var listing = BuildPublishedListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var reports = new FakeReportRepository();
        reports.Items.Add(Report.Create(ReporterId, listing, "reason", Now).Value);
        var handler = new GetReportedListingsHandler(reports, listings);
        var query = new GetReportedListingsQuery(1, GetReportedListingsQuery.MaxPageSize);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PageSize.Should().Be(100);
        result.Value.Items.Should().ContainSingle();
        reports.Items.Should().HaveCount(1);
        listings.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Given_PageZero_When_Handling_Then_ReturnsInvalidPage()
    {
        var listing = BuildPublishedListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var reports = new FakeReportRepository();
        reports.Items.Add(Report.Create(ReporterId, listing, "reason", Now).Value);
        var handler = new GetReportedListingsHandler(reports, listings);
        var query = new GetReportedListingsQuery(0, 20);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Moderation.InvalidPage");
        result.Error.Message.Should().Be("Page must be at least 1.");
        reports.Items.Should().HaveCount(1);
        listings.Items.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Given_InvalidPageSize_When_Handling_Then_ReturnsInvalidPageSize(int pageSize)
    {
        var listing = BuildPublishedListing();
        var listings = new FakeListingRepository();
        listings.Items[listing.Id] = listing;
        var reports = new FakeReportRepository();
        reports.Items.Add(Report.Create(ReporterId, listing, "reason", Now).Value);
        var handler = new GetReportedListingsHandler(reports, listings);
        var query = new GetReportedListingsQuery(1, pageSize);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Moderation.InvalidPageSize");
        result.Error.Message.Should().Be($"Page size must be between 1 and {GetReportedListingsQuery.MaxPageSize}.");
        reports.Items.Should().HaveCount(1);
        listings.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Given_DefaultQuery_When_Handling_Then_UsesPageOneAndPageSizeTwenty()
    {
        var reports = new FakeReportRepository();
        var listings = new FakeListingRepository();
        var handler = new GetReportedListingsHandler(reports, listings);
        var query = new GetReportedListingsQuery();

        var result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(20);
        reports.Items.Should().BeEmpty();
        listings.Items.Should().BeEmpty();
    }
}

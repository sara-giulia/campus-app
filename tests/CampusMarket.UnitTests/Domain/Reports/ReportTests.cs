using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Reports;

namespace CampusMarket.UnitTests.Domain.Reports;

public class ReportTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ReporterId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherMemberId = new("33333333-3333-3333-3333-333333333333");

    private static Listing BuildDraftListing() =>
        Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;

    private static Listing BuildPublishedListing()
    {
        var listing = BuildDraftListing();
        listing.Publish(Now.AddDays(30));
        return listing;
    }

    [Fact]
    public void Given_PublishedListingAndValidReason_When_Creating_Then_ReturnsReportWithTrimmedReason()
    {
        var listing = BuildPublishedListing();
        var createdAt = Now.AddHours(1);

        var result = Report.Create(ReporterId, listing, "  Fake photos  ", createdAt);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBe(Guid.Empty);
        result.Value.ListingId.Should().Be(listing.Id);
        result.Value.ReporterId.Should().Be(ReporterId);
        result.Value.Reason.Should().Be("Fake photos");
        result.Value.SpamProbability.Should().BeNull();
        result.Value.IsAutomatic.Should().BeFalse();
        result.Value.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void Given_ReservedListing_When_Creating_Then_ReturnsReport()
    {
        var listing = BuildPublishedListing();
        listing.Reserve(OtherMemberId, Now);

        var result = Report.Create(ReporterId, listing, "Fake photos", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.ListingId.Should().Be(listing.Id);
    }

    [Fact]
    public void Given_EmptyReporterId_When_Creating_Then_ReturnsReporterEmptyError()
    {
        var listing = BuildPublishedListing();

        var result = Report.Create(Guid.Empty, listing, "Fake photos", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Report.ReporterEmpty");
        result.Error.Message.Should().Be("Reporter id must not be empty.");
    }

    [Fact]
    public void Given_SellerOfListing_When_Creating_Then_ReturnsOwnListingError()
    {
        var listing = BuildPublishedListing();

        var result = Report.Create(SellerId, listing, "Fake photos", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Report.OwnListing");
        result.Error.Message.Should().Be("A seller cannot report their own listing.");
    }

    [Fact]
    public void Given_DraftListing_When_Creating_Then_ReturnsListingNotAvailableError()
    {
        var listing = BuildDraftListing();

        var result = Report.Create(ReporterId, listing, "Fake photos", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Report.ListingNotAvailable");
        result.Error.Message.Should().Be("Only an active or reserved listing can be reported.");
    }

    [Fact]
    public void Given_SoldListing_When_Creating_Then_ReturnsListingNotAvailableError()
    {
        var listing = BuildPublishedListing();
        var reservation = listing.Reserve(OtherMemberId, Now).Value;
        listing.ConfirmSale(reservation.Id, Now);

        var result = Report.Create(ReporterId, listing, "Fake photos", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Report.ListingNotAvailable");
        result.Error.Message.Should().Be("Only an active or reserved listing can be reported.");
    }

    [Fact]
    public void Given_HiddenListing_When_Creating_Then_ReturnsListingNotAvailableError()
    {
        var listing = BuildPublishedListing();
        listing.Hide();

        var result = Report.Create(ReporterId, listing, "Fake photos", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Report.ListingNotAvailable");
        result.Error.Message.Should().Be("Only an active or reserved listing can be reported.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_EmptyReason_When_Creating_Then_ReturnsReasonEmptyError(string? reason)
    {
        var listing = BuildPublishedListing();

        var result = Report.Create(ReporterId, listing, reason, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Report.ReasonEmpty");
        result.Error.Message.Should().Be("Reason must not be empty.");
    }

    [Fact]
    public void Given_ReasonOfMaxLength_When_Creating_Then_ReturnsReport()
    {
        var listing = BuildPublishedListing();
        var reason = new string('a', Report.MaxReasonLength);

        var result = Report.Create(ReporterId, listing, reason, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Reason.Should().Be(reason);
    }

    [Fact]
    public void Given_ReasonLongerThanMax_When_Creating_Then_ReturnsReasonTooLongError()
    {
        var listing = BuildPublishedListing();
        var reason = new string('a', Report.MaxReasonLength + 1);

        var result = Report.Create(ReporterId, listing, reason, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Report.ReasonTooLong");
        result.Error.Message.Should().Be($"Reason must have at most {Report.MaxReasonLength} characters.");
    }

    [Fact]
    public void Given_ReasonOfMaxLengthWithSpacesAround_When_Creating_Then_ReturnsReportWithTrimmedReason()
    {
        var listing = BuildPublishedListing();
        var text = new string('a', Report.MaxReasonLength);

        var result = Report.Create(ReporterId, listing, "  " + text + "  ", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Reason.Should().Be(text);
    }

    [Fact]
    public void Given_EmptyReporterIdOnDraftListing_When_Creating_Then_ReturnsReporterEmptyFirst()
    {
        var listing = BuildDraftListing();

        var result = Report.Create(Guid.Empty, listing, "Fake photos", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Report.ReporterEmpty");
        result.Error.Message.Should().Be("Reporter id must not be empty.");
    }

    [Fact]
    public void Given_SellerWithEmptyReason_When_Creating_Then_ReturnsOwnListingFirst()
    {
        var listing = BuildPublishedListing();

        var result = Report.Create(SellerId, listing, "", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Report.OwnListing");
        result.Error.Message.Should().Be("A seller cannot report their own listing.");
    }

    [Fact]
    public void Given_ListingId_When_CreatingAutomatic_Then_ReturnsAutomaticReportWithProbability()
    {
        var listingId = Guid.NewGuid();
        var createdAt = Now.AddHours(1);

        var report = Report.CreateAutomatic(listingId, 0.9, createdAt);

        report.Id.Should().NotBe(Guid.Empty);
        report.ListingId.Should().Be(listingId);
        report.ReporterId.Should().BeNull();
        report.IsAutomatic.Should().BeTrue();
        report.Reason.Should().Be(Report.AutomaticReason);
        report.SpamProbability.Should().Be(0.9);
        report.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void Given_PublishedListing_When_CreatingTwice_Then_ReturnsTwoDifferentIds()
    {
        var listing = BuildPublishedListing();

        var first = Report.Create(ReporterId, listing, "Fake photos", Now);
        var second = Report.Create(ReporterId, listing, "Fake photos", Now);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        first.Value.Id.Should().NotBe(second.Value.Id);
    }

    [Fact]
    public void Given_ListingId_When_CreatingAutomaticTwice_Then_ReturnsTwoDifferentIds()
    {
        var listingId = Guid.NewGuid();

        var first = Report.CreateAutomatic(listingId, 0.9, Now);
        var second = Report.CreateAutomatic(listingId, 0.9, Now);

        first.Id.Should().NotBe(second.Id);
    }
}

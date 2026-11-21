using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;

namespace CampusMarket.UnitTests.Domain.Listings;

public class SaleRecordTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid ListingId = new("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Given_ValidData_When_Creating_Then_ReturnsRecordWithAllFields()
    {
        var result = SaleRecord.Create(ListingId, Category.Textbook, Condition.Good, 120m, 100m, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBe(Guid.Empty);
        result.Value.ListingId.Should().Be(ListingId);
        result.Value.Category.Should().Be(Category.Textbook);
        result.Value.Condition.Should().Be(Condition.Good);
        result.Value.SuggestedPrice.Should().Be(120m);
        result.Value.SoldPrice.Should().Be(100m);
        result.Value.SoldAt.Should().Be(Now);
    }

    [Fact]
    public void Given_EmptyListingId_When_Creating_Then_ReturnsValidationError()
    {
        var result = SaleRecord.Create(Guid.Empty, Category.Textbook, Condition.Good, 120m, 100m, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("SaleRecord.ListingEmpty");
        result.Error.Message.Should().Be("Listing id must not be empty.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Given_NonPositiveSuggestedPrice_When_Creating_Then_ReturnsValidationError(decimal suggestedPrice)
    {
        var result = SaleRecord.Create(ListingId, Category.Textbook, Condition.Good, suggestedPrice, 100m, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("SaleRecord.SuggestedPriceInvalid");
        result.Error.Message.Should().Be("Suggested price must be greater than 0.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Given_NonPositiveSoldPrice_When_Creating_Then_ReturnsValidationError(decimal soldPrice)
    {
        var result = SaleRecord.Create(ListingId, Category.Textbook, Condition.Good, 120m, soldPrice, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("SaleRecord.SoldPriceInvalid");
        result.Error.Message.Should().Be("Sold price must be greater than 0.");
    }

    [Fact]
    public void Given_TwoValidCalls_When_Creating_Then_ReturnsDifferentIds()
    {
        var a = SaleRecord.Create(ListingId, Category.Textbook, Condition.Good, 120m, 100m, Now).Value;
        var b = SaleRecord.Create(ListingId, Category.Textbook, Condition.Good, 120m, 100m, Now).Value;

        a.Id.Should().NotBe(b.Id);
    }
}

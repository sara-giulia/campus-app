using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Admin.PriceAccuracy;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Admin.PriceAccuracy;

public class PriceAccuracyHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid ListingA = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ListingB = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ListingC = new("33333333-3333-3333-3333-333333333333");

    private static SaleRecord BuildRecord(Guid listingId, Category category, Condition condition, decimal suggested, decimal sold, DateTimeOffset soldAt) =>
        SaleRecord.Create(listingId, category, condition, suggested, sold, soldAt).Value;

    [Fact]
    public async Task Given_NoRecords_When_Handling_Then_ReturnsZeroCountAndNullMetrics()
    {
        var saleRecords = new FakeSaleRecordRepository();
        var handler = new PriceAccuracyHandler(saleRecords);

        var result = await handler.HandleAsync(new PriceAccuracyQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(0);
        result.Value.Mae.Should().BeNull();
        result.Value.Mape.Should().BeNull();
        result.Value.PerCategory.Should().BeEmpty();
        result.Value.LastRecords.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_TwoRecords_When_Handling_Then_ComputesMaeAndMape()
    {
        var saleRecords = new FakeSaleRecordRepository();
        saleRecords.Items.Add(BuildRecord(ListingA, Category.Textbook, Condition.Good, 100m, 80m, Now));
        saleRecords.Items.Add(BuildRecord(ListingB, Category.Textbook, Condition.New, 50m, 50m, Now.AddHours(1)));
        var handler = new PriceAccuracyHandler(saleRecords);

        var result = await handler.HandleAsync(new PriceAccuracyQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(2);
        result.Value.Mae.Should().Be(10m);
        result.Value.Mape.Should().Be(12.5m);
    }

    [Fact]
    public async Task Given_RecordsAcrossCategories_When_Handling_Then_GroupsPerCategory()
    {
        var saleRecords = new FakeSaleRecordRepository();
        saleRecords.Items.Add(BuildRecord(ListingA, Category.Textbook, Condition.Good, 100m, 80m, Now));
        saleRecords.Items.Add(BuildRecord(ListingB, Category.Textbook, Condition.New, 50m, 50m, Now.AddHours(1)));
        saleRecords.Items.Add(BuildRecord(ListingC, Category.Electronics, Condition.Worn, 200m, 150m, Now.AddHours(2)));
        var handler = new PriceAccuracyHandler(saleRecords);

        var result = await handler.HandleAsync(new PriceAccuracyQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PerCategory.Should().HaveCount(2);
        var textbook = result.Value.PerCategory.Single(c => c.Category == Category.Textbook);
        textbook.Count.Should().Be(2);
        textbook.Mae.Should().Be(10m);
        var electronics = result.Value.PerCategory.Single(c => c.Category == Category.Electronics);
        electronics.Count.Should().Be(1);
        electronics.Mae.Should().Be(50m);
    }

    [Fact]
    public async Task Given_ThreeRecords_When_Handling_Then_LastRecordsAreNewestFirst()
    {
        var saleRecords = new FakeSaleRecordRepository();
        var oldest = BuildRecord(ListingA, Category.Textbook, Condition.Good, 100m, 80m, Now);
        var middle = BuildRecord(ListingB, Category.Notes, Condition.New, 20m, 20m, Now.AddHours(1));
        var newest = BuildRecord(ListingC, Category.Electronics, Condition.Worn, 300m, 250m, Now.AddHours(2));
        saleRecords.Items.Add(oldest);
        saleRecords.Items.Add(middle);
        saleRecords.Items.Add(newest);
        var handler = new PriceAccuracyHandler(saleRecords);

        var result = await handler.HandleAsync(new PriceAccuracyQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.LastRecords.Should().HaveCount(3);
        result.Value.LastRecords[0].ListingId.Should().Be(newest.ListingId);
        result.Value.LastRecords[0].Category.Should().Be(Category.Electronics);
        result.Value.LastRecords[0].Condition.Should().Be(Condition.Worn);
        result.Value.LastRecords[0].SuggestedPrice.Should().Be(300m);
        result.Value.LastRecords[0].SoldPrice.Should().Be(250m);
        result.Value.LastRecords[0].SoldAt.Should().Be(Now.AddHours(2));
        result.Value.LastRecords[1].ListingId.Should().Be(middle.ListingId);
        result.Value.LastRecords[2].ListingId.Should().Be(oldest.ListingId);
    }

    [Fact]
    public async Task Given_TwelveRecords_When_Handling_Then_LastRecordsHasAtMostTen()
    {
        var saleRecords = new FakeSaleRecordRepository();
        saleRecords.Items.AddRange(Enumerable.Range(0, 12)
            .Select(i => BuildRecord(Guid.NewGuid(), Category.Textbook, Condition.Good, 100m, 80m, Now.AddHours(i))));
        var handler = new PriceAccuracyHandler(saleRecords);

        var result = await handler.HandleAsync(new PriceAccuracyQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(12);
        result.Value.LastRecords.Should().HaveCount(10);
        result.Value.LastRecords[0].SoldAt.Should().Be(Now.AddHours(11));
        result.Value.LastRecords[9].SoldAt.Should().Be(Now.AddHours(2));
    }

    [Fact]
    public async Task Given_RecordsInTwoCategories_When_Handling_Then_PerCategoryIsOrderedByCategory()
    {
        var saleRecords = new FakeSaleRecordRepository();
        saleRecords.Items.Add(BuildRecord(ListingA, Category.Electronics, Condition.Worn, 200m, 150m, Now));
        saleRecords.Items.Add(BuildRecord(ListingB, Category.Textbook, Condition.Good, 100m, 80m, Now.AddHours(1)));
        var handler = new PriceAccuracyHandler(saleRecords);

        var result = await handler.HandleAsync(new PriceAccuracyQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PerCategory.Select(c => c.Category).Should().Equal(Category.Textbook, Category.Electronics);
    }

    [Fact]
    public async Task Given_TwoRecordsSoldAtSameTime_When_Handling_Then_LastRecordsAreOrderedByIdDescending()
    {
        var saleRecords = new FakeSaleRecordRepository();
        var first = BuildRecord(ListingA, Category.Textbook, Condition.Good, 100m, 80m, Now);
        var second = BuildRecord(ListingB, Category.Notes, Condition.New, 20m, 20m, Now);
        saleRecords.Items.Add(first);
        saleRecords.Items.Add(second);
        var expected = new[] { first, second }.OrderByDescending(r => r.Id).Select(r => r.ListingId).ToList();
        var handler = new PriceAccuracyHandler(saleRecords);

        var result = await handler.HandleAsync(new PriceAccuracyQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.LastRecords.Select(r => r.ListingId).Should().Equal(expected);
    }
}

using System.Globalization;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Features.Admin.PriceFeedback;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Admin.PriceFeedback;

public class PriceFeedbackHandlerTests
{
    private const string ExpectedHeader = "Title,Description,Category,Condition,Price,IsSpam,TemplateId";

    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DeletedListingId = new("99999999-9999-9999-9999-999999999999");

    private static Listing SeedListing(FakeListingRepository listings, string title, string? description, decimal price = 150m)
    {
        var listing = Listing.Create(SellerId, title, Category.Electronics, Condition.Good, price, Now, description).Value;
        listings.Items[listing.Id] = listing;
        return listing;
    }

    private static SaleRecord SeedRecord(FakeSaleRecordRepository records, Guid listingId, decimal sold, DateTimeOffset soldAt, Category category = Category.Electronics, Condition condition = Condition.Good)
    {
        var record = SaleRecord.Create(listingId, category, condition, 200m, sold, soldAt).Value;
        records.Items.Add(record);
        return record;
    }

    private static string[] Lines(string csv) => csv.Split('\n');

    [Fact]
    public async Task Given_NoSales_When_Handling_Then_ReturnsOnlyHeaderAndZeroRows()
    {
        var records = new FakeSaleRecordRepository();
        var listings = new FakeListingRepository();
        var handler = new PriceFeedbackHandler(records, listings);

        var result = await handler.HandleAsync(new PriceFeedbackQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Csv.Should().Be(ExpectedHeader + "\n");
        result.Value.RowCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_NoSales_When_Handling_Then_HeaderIsTheSameAsExternalCsv()
    {
        var handler = new PriceFeedbackHandler(new FakeSaleRecordRepository(), new FakeListingRepository());

        var result = await handler.HandleAsync(new PriceFeedbackQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        Lines(result.Value.Csv)[0].Should().Be(ExpectedHeader);
        PriceFeedbackQuery.Header.Should().Be(ExpectedHeader);
    }

    [Fact]
    public async Task Given_OneSale_When_Handling_Then_WritesOneRowFromListingTextAndRecordValues()
    {
        var records = new FakeSaleRecordRepository();
        var listings = new FakeListingRepository();
        var listing = SeedListing(listings, "Laptop Dell", "Stare buna", price: 150m);
        SeedRecord(records, listing.Id, 120m, Now, Category.LabEquipment, Condition.Worn);
        var handler = new PriceFeedbackHandler(records, listings);

        var result = await handler.HandleAsync(new PriceFeedbackQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Csv.Should().Be(
            ExpectedHeader + "\n" +
            "\"Laptop Dell\",\"Stare buna\",LabEquipment,Worn,120,false,\"feedback\"\n");
        result.Value.RowCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_ListingWithoutDescription_When_Handling_Then_WritesEmptyQuotedDescription()
    {
        var records = new FakeSaleRecordRepository();
        var listings = new FakeListingRepository();
        var listing = SeedListing(listings, "Calculator", null);
        SeedRecord(records, listing.Id, 80m, Now);
        var handler = new PriceFeedbackHandler(records, listings);

        var result = await handler.HandleAsync(new PriceFeedbackQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        Lines(result.Value.Csv)[1].Should().Be("\"Calculator\",\"\",Electronics,Good,80,false,\"feedback\"");
    }

    [Fact]
    public async Task Given_RecordWhoseListingWasDeleted_When_Handling_Then_SkipsItAndCountsOnlyTheOthers()
    {
        var records = new FakeSaleRecordRepository();
        var listings = new FakeListingRepository();
        var listing = SeedListing(listings, "Calculator", null);
        SeedRecord(records, DeletedListingId, 50m, Now);
        SeedRecord(records, listing.Id, 80m, Now.AddHours(1));
        var handler = new PriceFeedbackHandler(records, listings);

        var result = await handler.HandleAsync(new PriceFeedbackQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Csv.Should().Be(
            ExpectedHeader + "\n" +
            "\"Calculator\",\"\",Electronics,Good,80,false,\"feedback\"\n");
        result.Value.RowCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_OnlyRecordsWithDeletedListings_When_Handling_Then_ReturnsOnlyHeader()
    {
        var records = new FakeSaleRecordRepository();
        var listings = new FakeListingRepository();
        SeedRecord(records, DeletedListingId, 50m, Now);
        var handler = new PriceFeedbackHandler(records, listings);

        var result = await handler.HandleAsync(new PriceFeedbackQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Csv.Should().Be(ExpectedHeader + "\n");
        result.Value.RowCount.Should().Be(0);
    }

    [Fact]
    public async Task Given_TitleWithQuote_When_Handling_Then_DoublesTheQuote()
    {
        var records = new FakeSaleRecordRepository();
        var listings = new FakeListingRepository();
        var listing = SeedListing(listings, "Laptop 15\" Dell", null);
        SeedRecord(records, listing.Id, 80m, Now);
        var handler = new PriceFeedbackHandler(records, listings);

        var result = await handler.HandleAsync(new PriceFeedbackQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        Lines(result.Value.Csv)[1].Should().Be("\"Laptop 15\"\" Dell\",\"\",Electronics,Good,80,false,\"feedback\"");
    }

    [Fact]
    public async Task Given_TitleWithComma_When_Handling_Then_KeepsTheCommaInsideQuotes()
    {
        var records = new FakeSaleRecordRepository();
        var listings = new FakeListingRepository();
        var listing = SeedListing(listings, "Casti, sigilate", null);
        SeedRecord(records, listing.Id, 80m, Now);
        var handler = new PriceFeedbackHandler(records, listings);

        var result = await handler.HandleAsync(new PriceFeedbackQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        Lines(result.Value.Csv)[1].Should().Be("\"Casti, sigilate\",\"\",Electronics,Good,80,false,\"feedback\"");
    }

    [Fact]
    public async Task Given_DescriptionWithNewLine_When_Handling_Then_KeepsTheNewLineInsideQuotes()
    {
        var records = new FakeSaleRecordRepository();
        var listings = new FakeListingRepository();
        var listing = SeedListing(listings, "Manual", "Linia 1\nLinia 2");
        SeedRecord(records, listing.Id, 80m, Now);
        var handler = new PriceFeedbackHandler(records, listings);

        var result = await handler.HandleAsync(new PriceFeedbackQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Csv.Should().Be(
            ExpectedHeader + "\n" +
            "\"Manual\",\"Linia 1\nLinia 2\",Electronics,Good,80,false,\"feedback\"\n");
        result.Value.RowCount.Should().Be(1);
    }

    [Fact]
    public async Task Given_RecordsSeededOutOfOrder_When_Handling_Then_RowsAreOrderedBySoldAt()
    {
        var records = new FakeSaleRecordRepository();
        var listings = new FakeListingRepository();
        var later = SeedListing(listings, "Later", null);
        var earlier = SeedListing(listings, "Earlier", null);
        SeedRecord(records, later.Id, 80m, Now.AddHours(2));
        SeedRecord(records, earlier.Id, 80m, Now);
        var handler = new PriceFeedbackHandler(records, listings);

        var result = await handler.HandleAsync(new PriceFeedbackQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        Lines(result.Value.Csv)[1].Should().StartWith("\"Earlier\"");
        Lines(result.Value.Csv)[2].Should().StartWith("\"Later\"");
    }

    [Fact]
    public async Task Given_TwoRecordsWithSameSoldAt_When_Handling_Then_RowsAreOrderedByRecordId()
    {
        var records = new FakeSaleRecordRepository();
        var listings = new FakeListingRepository();
        var first = SeedListing(listings, "First", null);
        var second = SeedListing(listings, "Second", null);
        var firstRecord = SeedRecord(records, first.Id, 80m, Now);
        var secondRecord = SeedRecord(records, second.Id, 80m, Now);
        var titleByListing = new Dictionary<Guid, string> { [first.Id] = "First", [second.Id] = "Second" };
        var expectedTitles = new[] { firstRecord, secondRecord }
            .OrderBy(r => r.Id)
            .Select(r => "\"" + titleByListing[r.ListingId] + "\"")
            .ToList();
        var handler = new PriceFeedbackHandler(records, listings);

        var result = await handler.HandleAsync(new PriceFeedbackQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        Lines(result.Value.Csv).Skip(1).Take(2).Select(l => l.Split(',')[0]).Should().Equal(expectedTitles);
    }

    [Theory]
    [InlineData("99.5", "99.5")]
    [InlineData("1234.5", "1234.5")]
    [InlineData("100", "100")]
    public async Task Given_RomanianCulture_When_Handling_Then_WritesPriceWithDotAndNoThousandsSeparator(string soldPrice, string expected)
    {
        using var _ = new CultureScope("ro-RO");
        var records = new FakeSaleRecordRepository();
        var listings = new FakeListingRepository();
        var listing = SeedListing(listings, "Calculator", null);
        SeedRecord(records, listing.Id, decimal.Parse(soldPrice, CultureInfo.InvariantCulture), Now);
        var handler = new PriceFeedbackHandler(records, listings);

        var result = await handler.HandleAsync(new PriceFeedbackQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        Lines(result.Value.Csv)[1].Should().Be($"\"Calculator\",\"\",Electronics,Good,{expected},false,\"feedback\"");
    }

    [Fact]
    public async Task Given_Sales_When_Handling_Then_RepositoriesAreUnchanged()
    {
        var records = new FakeSaleRecordRepository();
        var listings = new FakeListingRepository();
        var listing = SeedListing(listings, "Calculator", null);
        var record = SeedRecord(records, listing.Id, 80m, Now);
        var handler = new PriceFeedbackHandler(records, listings);

        var result = await handler.HandleAsync(new PriceFeedbackQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        records.Items.Should().Equal(record);
        listings.Items.Values.Should().Equal(listing);
        listings.UpdateCount.Should().Be(0);
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

        public CultureScope(string name)
        {
            CultureInfo.CurrentCulture = new CultureInfo(name);
            CultureInfo.CurrentUICulture = new CultureInfo(name);
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _culture;
            CultureInfo.CurrentUICulture = _uiCulture;
        }
    }
}

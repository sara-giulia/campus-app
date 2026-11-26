using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.TrainingExamples;
using CampusMarket.Api.Features.Admin.TrainingExamples;
using CampusMarket.UnitTests.Fakes;

namespace CampusMarket.UnitTests.Features.Admin.TrainingExamples;

public class TrainingExamplesHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AdminId = new("22222222-2222-2222-2222-222222222222");

    private static Listing BuildListing(Category category = Category.Textbook) =>
        Listing.Create(SellerId, "Algebra book", category, Condition.Good, 25m, Now, "Second edition").Value;

    private static TrainingExample BuildSpamExample(bool isSpam, DateTimeOffset createdAt) =>
        TrainingExample.ForSpamDecision(BuildListing(), isSpam, AdminId, createdAt).Value;

    private static TrainingExample BuildCategoryExample(Category chosen, DateTimeOffset createdAt) =>
        TrainingExample.ForCategoryChoice(BuildListing(chosen), "Electronics", SellerId, createdAt).Value;

    [Fact]
    public async Task Given_NoExamples_When_Handling_Then_ReturnsZeroCountAndEmptyLists()
    {
        var repository = new FakeTrainingExampleRepository();
        var handler = new TrainingExamplesHandler(repository);

        var result = await handler.HandleAsync(new TrainingExamplesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(0);
        result.Value.PerLabel.Should().BeEmpty();
        result.Value.Latest.Should().BeEmpty();
        repository.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Given_ExamplesOfBothKinds_When_Handling_Then_CountsPerKindAndLabelInOrder()
    {
        var repository = new FakeTrainingExampleRepository();
        repository.Items.Add(BuildCategoryExample(Category.Textbook, Now));
        repository.Items.Add(BuildSpamExample(true, Now));
        repository.Items.Add(BuildCategoryExample(Category.Notes, Now));
        repository.Items.Add(BuildSpamExample(false, Now));
        repository.Items.Add(BuildSpamExample(true, Now));
        repository.Items.Add(BuildCategoryExample(Category.Textbook, Now));
        var handler = new TrainingExamplesHandler(repository);

        var result = await handler.HandleAsync(new TrainingExamplesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Count.Should().Be(6);
        result.Value.PerLabel.Should().Equal(
            new TrainingExampleCount(TrainingExampleKind.Spam, "not spam", 1),
            new TrainingExampleCount(TrainingExampleKind.Spam, "spam", 2),
            new TrainingExampleCount(TrainingExampleKind.Category, "Notes", 1),
            new TrainingExampleCount(TrainingExampleKind.Category, "Textbook", 2));
        repository.Items.Should().HaveCount(6);
    }

    [Fact]
    public async Task Given_OneExample_When_Handling_Then_LatestItemHasEveryField()
    {
        var example = BuildCategoryExample(Category.Notes, Now);
        var repository = new FakeTrainingExampleRepository();
        repository.Items.Add(example);
        var handler = new TrainingExamplesHandler(repository);

        var result = await handler.HandleAsync(new TrainingExamplesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Latest.Should().ContainSingle();
        var item = result.Value.Latest[0];
        item.Id.Should().Be(example.Id);
        item.ListingId.Should().Be(example.ListingId);
        item.Kind.Should().Be(TrainingExampleKind.Category);
        item.Title.Should().Be("Algebra book");
        item.Description.Should().Be("Second edition");
        item.Label.Should().Be("Notes");
        item.Source.Should().Be(TrainingExampleSource.SellerChoice);
        item.CreatedBy.Should().Be(SellerId);
        item.CreatedAt.Should().Be(Now);
        item.LastChangedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Given_RelabelledExample_When_Handling_Then_LastChangedAtIsUpdatedAtAndItComesFirst()
    {
        var listing = BuildListing();
        var relabelled = TrainingExample.ForSpamDecision(listing, true, AdminId, Now).Value;
        relabelled.RelabelSpam(listing, false, AdminId, Now.AddHours(5));
        var newerCreated = BuildSpamExample(true, Now.AddHours(1));
        var repository = new FakeTrainingExampleRepository();
        repository.Items.Add(newerCreated);
        repository.Items.Add(relabelled);
        var handler = new TrainingExamplesHandler(repository);

        var result = await handler.HandleAsync(new TrainingExamplesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Latest.Select(i => i.Id).Should().Equal(relabelled.Id, newerCreated.Id);
        result.Value.Latest[0].CreatedAt.Should().Be(Now);
        result.Value.Latest[0].LastChangedAt.Should().Be(Now.AddHours(5));
        result.Value.Latest[0].Label.Should().Be("not spam");
    }

    [Fact]
    public async Task Given_MoreThanLatestCountExamples_When_Handling_Then_ReturnsTheNewestTwentyNewestFirst()
    {
        var examples = Enumerable.Range(0, TrainingExamplesQuery.LatestCount + 1)
            .Select(i => BuildSpamExample(true, Now.AddMinutes(i)))
            .ToList();
        var repository = new FakeTrainingExampleRepository();
        repository.Items.AddRange(examples);
        var handler = new TrainingExamplesHandler(repository);
        var expected = examples.OrderByDescending(e => e.CreatedAt).Take(20).Select(e => e.Id).ToList();

        var result = await handler.HandleAsync(new TrainingExamplesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        TrainingExamplesQuery.LatestCount.Should().Be(20);
        result.Value.Count.Should().Be(21);
        result.Value.Latest.Should().HaveCount(20);
        result.Value.Latest.Select(i => i.Id).Should().Equal(expected);
        result.Value.Latest.Should().NotContain(i => i.Id == examples[0].Id);
        repository.Items.Should().HaveCount(21);
    }

    [Fact]
    public async Task Given_ExactlyLatestCountExamples_When_Handling_Then_ReturnsAllOfThem()
    {
        var examples = Enumerable.Range(0, TrainingExamplesQuery.LatestCount)
            .Select(i => BuildSpamExample(false, Now.AddMinutes(i)))
            .ToList();
        var repository = new FakeTrainingExampleRepository();
        repository.Items.AddRange(examples);
        var handler = new TrainingExamplesHandler(repository);

        var result = await handler.HandleAsync(new TrainingExamplesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Latest.Should().HaveCount(20);
    }

    [Fact]
    public async Task Given_TwoExamplesWithSameTime_When_Handling_Then_LatestIsOrderedById()
    {
        var first = BuildSpamExample(true, Now);
        var second = BuildSpamExample(true, Now);
        var repository = new FakeTrainingExampleRepository();
        repository.Items.Add(first);
        repository.Items.Add(second);
        var handler = new TrainingExamplesHandler(repository);
        var expected = new[] { first.Id, second.Id }.OrderBy(id => id).ToList();

        var result = await handler.HandleAsync(new TrainingExamplesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Latest.Select(i => i.Id).Should().Equal(expected);
        result.Value.Latest.Should().BeInAscendingOrder(i => i.Id);
    }
}

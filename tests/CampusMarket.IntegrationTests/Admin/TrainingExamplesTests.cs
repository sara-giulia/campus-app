using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Domain.TrainingExamples;
using CampusMarket.Api.Features.Admin.TrainingExamples;
using CampusMarket.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CampusMarket.IntegrationTests.Admin;

public sealed class TrainingExamplesTests : IntegrationTestBase
{
    private const string Url = "/api/v1/admin/ml/training-examples";
    private static readonly Guid SellerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherStudentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Given_ExamplesOfBothKinds_When_AdminGetsTrainingExamples_Then_Returns200WithCountsAndLatest()
    {
        // Arrange
        var ana = Member.Create("Ana Popescu", "ana@student.ro", CreatedAt).Value;
        var textbook = NewListing("Manual POO", Category.Textbook, "Putin folosit.");
        var spam = NewListing("BANI RAPIZI", Category.Other, null);
        var calculator = NewListing("Calculator stiintific", Category.Electronics, "Casio fx-991.");
        var notes = NewListing("Notite Analiza", Category.Notes, null);
        var laptop = NewListing("Laptop Lenovo", Category.Electronics, null);
        var spamExample = TrainingExample.ForSpamDecision(textbook, isSpam: true, AdminId, CreatedAt.AddHours(1)).Value;
        var notSpamExample = TrainingExample.ForSpamDecision(spam, isSpam: false, AdminId, CreatedAt.AddHours(2)).Value;
        var relabelled = TrainingExample.ForSpamDecision(calculator, isSpam: false, AdminId, CreatedAt.AddHours(3)).Value;
        relabelled.RelabelSpam(calculator, isSpam: true, AdminId, CreatedAt.AddHours(7));
        var notesExample = TrainingExample.ForCategoryChoice(notes, "Textbook", ana.Id, CreatedAt.AddHours(4)).Value;
        var laptopExample = TrainingExample.ForCategoryChoice(laptop, "Other", SellerId, CreatedAt.AddHours(5)).Value;
        await SeedAsync(ana, spamExample, notSpamExample, relabelled, notesExample, laptopExample);
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.GetAsync(Url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain("Ana Popescu").And.NotContain("ana@student.ro");

        var body = JsonSerializer.Deserialize<TrainingExamplesResponse>(raw, Json);
        body!.Count.Should().Be(5);
        body.PerLabel.Should().Equal(
            new TrainingExampleCount(TrainingExampleKind.Spam, TrainingExample.NotSpamLabel, 1),
            new TrainingExampleCount(TrainingExampleKind.Spam, TrainingExample.SpamLabel, 2),
            new TrainingExampleCount(TrainingExampleKind.Category, "Electronics", 1),
            new TrainingExampleCount(TrainingExampleKind.Category, "Notes", 1));
        body.Latest.Select(i => i.Id).Should().Equal(
            relabelled.Id, laptopExample.Id, notesExample.Id, notSpamExample.Id, spamExample.Id);
        body.Latest[0].Should().Be(new TrainingExampleItem(
            relabelled.Id, calculator.Id, TrainingExampleKind.Spam, "Calculator stiintific", "Casio fx-991.",
            TrainingExample.SpamLabel, TrainingExampleSource.Report, AdminId, CreatedAt.AddHours(3), CreatedAt.AddHours(7)));
        body.Latest[2].CreatedBy.Should().Be(ana.Id);
        body.Latest[2].Source.Should().Be(TrainingExampleSource.SellerChoice);

        var saved = await QueryDbAsync(db => db.Set<TrainingExample>().AsNoTracking().ToListAsync());
        saved.Should().BeEquivalentTo(new[] { spamExample, notSpamExample, relabelled, notesExample, laptopExample });
    }

    [Fact]
    public async Task Given_NoExamples_When_AdminGetsTrainingExamples_Then_Returns200WithEmptyLists()
    {
        // Arrange
        AuthenticateAs(AdminId, "Admin");

        // Act
        var response = await Client.GetAsync(Url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TrainingExamplesResponse>(Json);
        body!.Count.Should().Be(0);
        body.PerLabel.Should().BeEmpty();
        body.Latest.Should().BeEmpty();

        var count = await QueryDbAsync(db => db.Set<TrainingExample>().CountAsync());
        count.Should().Be(0);
    }

    [Fact]
    public async Task Given_Student_When_GettingTrainingExamples_Then_Returns403AndKeepsExamples()
    {
        // Arrange
        var example = await SeedOneExampleAsync();
        AuthenticateAs(OtherStudentId);

        // Act
        var response = await Client.GetAsync(Url);

        // Assert
        await ShouldBeProblemAsync(response, HttpStatusCode.Forbidden, "Auth.Forbidden");

        var saved = await QueryDbAsync(db => db.Set<TrainingExample>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(example);
    }

    [Fact]
    public async Task Given_NoToken_When_GettingTrainingExamples_Then_Returns401AndKeepsExamples()
    {
        // Arrange
        var example = await SeedOneExampleAsync();

        // Act
        var response = await Client.GetAsync(Url);

        // Assert
        await ShouldBeProblemAsync(response, HttpStatusCode.Unauthorized, "Auth.Unauthorized");

        var saved = await QueryDbAsync(db => db.Set<TrainingExample>().AsNoTracking().SingleAsync());
        saved.Should().BeEquivalentTo(example);
    }

    private static Listing NewListing(string title, Category category, string? description) =>
        Listing.Create(SellerId, title, category, Condition.Good, 50m, CreatedAt, description).Value;

    private async Task<TrainingExample> SeedOneExampleAsync()
    {
        var listing = NewListing("Manual POO", Category.Textbook, null);
        var example = TrainingExample.ForSpamDecision(listing, isSpam: true, AdminId, CreatedAt.AddHours(1)).Value;
        await SeedAsync(example);
        return example;
    }

    private static async Task ShouldBeProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(Json);
        problem!.Status.Should().Be((int)status);
        problem.Code.Should().Be(code);
        problem.Instance.Should().Be(Url);
        problem.TraceId.Should().NotBeEmpty();
    }
}

using System.Runtime.CompilerServices;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.TrainingExamples;

namespace CampusMarket.UnitTests.Domain.TrainingExamples;

public class TrainingExampleTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SellerId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AdminId = new("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherAdminId = new("33333333-3333-3333-3333-333333333333");
    private static readonly string[] PersonalDataProperties = ["Name", "Email", "SellerName", "SellerEmail", "PasswordHash"];

    private static Listing BuildListing(Category category = Category.Textbook) =>
        Listing.Create(SellerId, "Algebra book", category, Condition.Good, 25m, Now, "Second edition").Value;

    // Listing.Create always gives a new Id, so the only way to reach TrainingExample.ListingEmpty is a
    // listing object whose constructor never ran (Id is Guid.Empty, as for a row with no key).
    private static Listing BuildListingWithEmptyId() =>
        (Listing)RuntimeHelpers.GetUninitializedObject(typeof(Listing));

    [Fact]
    public void Given_ListingAndSpamDecision_When_CreatingForSpamDecision_Then_ReturnsSpamExampleWithListingText()
    {
        var listing = BuildListing();

        var result = TrainingExample.ForSpamDecision(listing, true, AdminId, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBe(Guid.Empty);
        result.Value.ListingId.Should().Be(listing.Id);
        result.Value.Kind.Should().Be(TrainingExampleKind.Spam);
        result.Value.Title.Should().Be("Algebra book");
        result.Value.Description.Should().Be("Second edition");
        result.Value.Label.Should().Be(TrainingExample.SpamLabel);
        result.Value.Label.Should().Be("spam");
        result.Value.Source.Should().Be(TrainingExampleSource.Report);
        result.Value.CreatedBy.Should().Be(AdminId);
        result.Value.CreatedAt.Should().Be(Now);
        result.Value.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Given_ListingAndNotSpamDecision_When_CreatingForSpamDecision_Then_ReturnsNotSpamLabel()
    {
        var listing = BuildListing();

        var result = TrainingExample.ForSpamDecision(listing, false, AdminId, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Label.Should().Be(TrainingExample.NotSpamLabel);
        result.Value.Label.Should().Be("not spam");
    }

    [Fact]
    public void Given_ListingWithoutDescription_When_CreatingForSpamDecision_Then_DescriptionIsNull()
    {
        var listing = Listing.Create(SellerId, "Algebra book", Category.Textbook, Condition.Good, 25m, Now).Value;

        var result = TrainingExample.ForSpamDecision(listing, true, AdminId, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Description.Should().BeNull();
    }

    [Fact]
    public void Given_ListingTwice_When_CreatingForSpamDecision_Then_ReturnsTwoDifferentIds()
    {
        var listing = BuildListing();

        var first = TrainingExample.ForSpamDecision(listing, true, AdminId, Now);
        var second = TrainingExample.ForSpamDecision(listing, true, AdminId, Now);

        first.Value.Id.Should().NotBe(second.Value.Id);
    }

    [Fact]
    public void Given_EmptyAdminId_When_CreatingForSpamDecision_Then_ReturnsCreatedByEmpty()
    {
        var listing = BuildListing();

        var result = TrainingExample.ForSpamDecision(listing, true, Guid.Empty, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("TrainingExample.CreatedByEmpty");
        result.Error.Message.Should().Be("Created by must not be empty.");
    }

    [Fact]
    public void Given_ListingWithEmptyId_When_CreatingForSpamDecision_Then_ReturnsListingEmpty()
    {
        var listing = BuildListingWithEmptyId();

        var result = TrainingExample.ForSpamDecision(listing, true, AdminId, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("TrainingExample.ListingEmpty");
        result.Error.Message.Should().Be("Listing id must not be empty.");
    }

    [Fact]
    public void Given_ListingWithEmptyIdAndEmptyAdmin_When_CreatingForSpamDecision_Then_ReturnsListingEmptyFirst()
    {
        var listing = BuildListingWithEmptyId();

        var result = TrainingExample.ForSpamDecision(listing, true, Guid.Empty, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("TrainingExample.ListingEmpty");
    }

    [Fact]
    public void Given_SellerPickedAnotherCategory_When_CreatingForCategoryChoice_Then_ReturnsCategoryExampleWithChosenCategory()
    {
        var listing = BuildListing(Category.Textbook);

        var result = TrainingExample.ForCategoryChoice(listing, "Notes", SellerId, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBe(Guid.Empty);
        result.Value.ListingId.Should().Be(listing.Id);
        result.Value.Kind.Should().Be(TrainingExampleKind.Category);
        result.Value.Title.Should().Be("Algebra book");
        result.Value.Description.Should().Be("Second edition");
        result.Value.Label.Should().Be("Textbook");
        result.Value.Source.Should().Be(TrainingExampleSource.SellerChoice);
        result.Value.CreatedBy.Should().Be(SellerId);
        result.Value.CreatedAt.Should().Be(Now);
        result.Value.UpdatedAt.Should().BeNull();
    }

    [Theory]
    [InlineData("notes")]
    [InlineData("NOTES")]
    [InlineData("  Notes  ")]
    public void Given_SuggestionWithOtherCaseOrSpaces_When_CreatingForCategoryChoice_Then_Succeeds(string suggested)
    {
        var listing = BuildListing(Category.Textbook);

        var result = TrainingExample.ForCategoryChoice(listing, suggested, SellerId, Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Label.Should().Be("Textbook");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Books")]
    [InlineData("3")]
    [InlineData("Textbook,Notes")]
    public void Given_MissingOrInvalidSuggestion_When_CreatingForCategoryChoice_Then_ReturnsSuggestionInvalid(string? suggested)
    {
        var listing = BuildListing(Category.Textbook);

        var result = TrainingExample.ForCategoryChoice(listing, suggested, SellerId, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("TrainingExample.SuggestionInvalid");
        result.Error.Message.Should().Be("No valid suggested category was sent.");
    }

    [Theory]
    [InlineData("Textbook")]
    [InlineData("textbook")]
    public void Given_SuggestionSameAsChosen_When_CreatingForCategoryChoice_Then_ReturnsSameAsSuggested(string suggested)
    {
        var listing = BuildListing(Category.Textbook);

        var result = TrainingExample.ForCategoryChoice(listing, suggested, SellerId, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("TrainingExample.SameAsSuggested");
        result.Error.Message.Should().Be("The chosen category is the suggested one.");
    }

    [Fact]
    public void Given_EmptySellerId_When_CreatingForCategoryChoice_Then_ReturnsCreatedByEmpty()
    {
        var listing = BuildListing(Category.Textbook);

        var result = TrainingExample.ForCategoryChoice(listing, "Notes", Guid.Empty, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("TrainingExample.CreatedByEmpty");
        result.Error.Message.Should().Be("Created by must not be empty.");
    }

    [Fact]
    public void Given_ListingWithEmptyId_When_CreatingForCategoryChoice_Then_ReturnsListingEmpty()
    {
        var listing = BuildListingWithEmptyId();

        var result = TrainingExample.ForCategoryChoice(listing, "Notes", SellerId, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("TrainingExample.ListingEmpty");
        result.Error.Message.Should().Be("Listing id must not be empty.");
    }

    [Fact]
    public void Given_InvalidSuggestionAndEmptySeller_When_CreatingForCategoryChoice_Then_ReturnsSuggestionInvalidFirst()
    {
        var listing = BuildListing(Category.Textbook);

        var result = TrainingExample.ForCategoryChoice(listing, "Books", Guid.Empty, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("TrainingExample.SuggestionInvalid");
    }

    [Fact]
    public void Given_SpamExample_When_RelabellingAsNotSpam_Then_ChangesLabelAndKeepsIdentity()
    {
        var listing = BuildListing();
        var example = TrainingExample.ForSpamDecision(listing, true, AdminId, Now).Value;
        var originalId = example.Id;
        listing.UpdateDetails("Linear algebra", Category.Textbook, Condition.Good, 25m, "Third edition");
        var updatedAt = Now.AddHours(2);

        var result = example.RelabelSpam(listing, false, OtherAdminId, updatedAt);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(example);
        example.Id.Should().Be(originalId);
        example.ListingId.Should().Be(listing.Id);
        example.Kind.Should().Be(TrainingExampleKind.Spam);
        example.Label.Should().Be("not spam");
        example.Title.Should().Be("Linear algebra");
        example.Description.Should().Be("Third edition");
        example.Source.Should().Be(TrainingExampleSource.Report);
        example.CreatedBy.Should().Be(OtherAdminId);
        example.CreatedAt.Should().Be(Now);
        example.UpdatedAt.Should().Be(updatedAt);
    }

    [Fact]
    public void Given_NotSpamExample_When_RelabellingAsSpam_Then_LabelIsSpam()
    {
        var listing = BuildListing();
        var example = TrainingExample.ForSpamDecision(listing, false, AdminId, Now).Value;

        var result = example.RelabelSpam(listing, true, AdminId, Now.AddHours(1));

        result.IsSuccess.Should().BeTrue();
        example.Label.Should().Be("spam");
    }

    [Fact]
    public void Given_CategoryExample_When_RelabellingSpam_Then_ReturnsKindMismatchAndChangesNothing()
    {
        var listing = BuildListing(Category.Textbook);
        var example = TrainingExample.ForCategoryChoice(listing, "Notes", SellerId, Now).Value;

        var result = example.RelabelSpam(listing, true, AdminId, Now.AddHours(1));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("TrainingExample.KindMismatch");
        result.Error.Message.Should().Be("This example is not the spam example of this listing.");
        example.Label.Should().Be("Textbook");
        example.CreatedBy.Should().Be(SellerId);
        example.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Given_SpamExampleOfAnotherListing_When_RelabellingSpam_Then_ReturnsKindMismatchAndChangesNothing()
    {
        var example = TrainingExample.ForSpamDecision(BuildListing(), true, AdminId, Now).Value;
        var otherListing = BuildListing();

        var result = example.RelabelSpam(otherListing, false, OtherAdminId, Now.AddHours(1));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("TrainingExample.KindMismatch");
        result.Error.Message.Should().Be("This example is not the spam example of this listing.");
        example.Label.Should().Be("spam");
        example.CreatedBy.Should().Be(AdminId);
        example.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Given_SpamExample_When_RelabellingWithEmptyAdmin_Then_ReturnsCreatedByEmptyAndChangesNothing()
    {
        var listing = BuildListing();
        var example = TrainingExample.ForSpamDecision(listing, true, AdminId, Now).Value;

        var result = example.RelabelSpam(listing, false, Guid.Empty, Now.AddHours(1));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("TrainingExample.CreatedByEmpty");
        result.Error.Message.Should().Be("Created by must not be empty.");
        example.Label.Should().Be("spam");
        example.CreatedBy.Should().Be(AdminId);
        example.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Given_CategoryExample_When_SellerPicksAnotherCategoryAgain_Then_RelabelsWithNewChoice()
    {
        var listing = BuildListing(Category.Textbook);
        var example = TrainingExample.ForCategoryChoice(listing, "Notes", SellerId, Now).Value;
        var originalId = example.Id;
        listing.UpdateDetails("Lab goggles", Category.LabEquipment, Condition.Good, 25m, "Never used");
        var updatedAt = Now.AddHours(2);

        var result = example.RelabelCategory(listing, "Notes", SellerId, updatedAt);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(example);
        example.Id.Should().Be(originalId);
        example.Kind.Should().Be(TrainingExampleKind.Category);
        example.Label.Should().Be("LabEquipment");
        example.Title.Should().Be("Lab goggles");
        example.Description.Should().Be("Never used");
        example.Source.Should().Be(TrainingExampleSource.SellerChoice);
        example.CreatedBy.Should().Be(SellerId);
        example.CreatedAt.Should().Be(Now);
        example.UpdatedAt.Should().Be(updatedAt);
    }

    [Fact]
    public void Given_SpamExample_When_RelabellingCategory_Then_ReturnsKindMismatchAndChangesNothing()
    {
        var listing = BuildListing(Category.Textbook);
        var example = TrainingExample.ForSpamDecision(listing, true, AdminId, Now).Value;

        var result = example.RelabelCategory(listing, "Notes", SellerId, Now.AddHours(1));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("TrainingExample.KindMismatch");
        result.Error.Message.Should().Be("This example is not the category example of this listing.");
        example.Label.Should().Be("spam");
        example.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Given_CategoryExampleOfAnotherListing_When_RelabellingCategory_Then_ReturnsKindMismatchAndChangesNothing()
    {
        var example = TrainingExample.ForCategoryChoice(BuildListing(Category.Textbook), "Notes", SellerId, Now).Value;
        var otherListing = BuildListing(Category.Electronics);

        var result = example.RelabelCategory(otherListing, "Notes", SellerId, Now.AddHours(1));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("TrainingExample.KindMismatch");
        result.Error.Message.Should().Be("This example is not the category example of this listing.");
        example.Label.Should().Be("Textbook");
        example.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Given_CategoryExample_When_RelabellingWithInvalidSuggestion_Then_ReturnsSuggestionInvalidAndChangesNothing()
    {
        var listing = BuildListing(Category.Textbook);
        var example = TrainingExample.ForCategoryChoice(listing, "Notes", SellerId, Now).Value;
        listing.UpdateDetails("Algebra book", Category.Electronics, Condition.Good, 25m);

        var result = example.RelabelCategory(listing, "Books", SellerId, Now.AddHours(1));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("TrainingExample.SuggestionInvalid");
        result.Error.Message.Should().Be("No valid suggested category was sent.");
        example.Label.Should().Be("Textbook");
        example.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Given_CategoryExample_When_RelabellingWithSuggestionSameAsChosen_Then_ReturnsSameAsSuggestedAndChangesNothing()
    {
        var listing = BuildListing(Category.Textbook);
        var example = TrainingExample.ForCategoryChoice(listing, "Notes", SellerId, Now).Value;
        listing.UpdateDetails("Algebra book", Category.Notes, Condition.Good, 25m);

        var result = example.RelabelCategory(listing, "Notes", SellerId, Now.AddHours(1));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("TrainingExample.SameAsSuggested");
        result.Error.Message.Should().Be("The chosen category is the suggested one.");
        example.Label.Should().Be("Textbook");
        example.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Given_CategoryExample_When_RelabellingWithEmptySeller_Then_ReturnsCreatedByEmptyAndChangesNothing()
    {
        var listing = BuildListing(Category.Textbook);
        var example = TrainingExample.ForCategoryChoice(listing, "Notes", SellerId, Now).Value;

        var result = example.RelabelCategory(listing, "Notes", Guid.Empty, Now.AddHours(1));

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("TrainingExample.CreatedByEmpty");
        result.Error.Message.Should().Be("Created by must not be empty.");
        example.CreatedBy.Should().Be(SellerId);
        example.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void Given_CategoryExample_When_RelabellingWithInvalidSuggestionAndEmptySeller_Then_ReturnsSuggestionInvalidFirst()
    {
        var listing = BuildListing(Category.Textbook);
        var example = TrainingExample.ForCategoryChoice(listing, "Notes", SellerId, Now).Value;

        var result = example.RelabelCategory(listing, "Books", Guid.Empty, Now.AddHours(1));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("TrainingExample.SuggestionInvalid");
    }

    [Theory]
    [InlineData("Textbook", Category.Textbook)]
    [InlineData("notes", Category.Notes)]
    [InlineData("  ELECTRONICS ", Category.Electronics)]
    [InlineData("LabEquipment", Category.LabEquipment)]
    [InlineData("other", Category.Other)]
    public void Given_CategoryName_When_ParsingSuggestedCategory_Then_ReturnsCategory(string value, Category expected)
    {
        var result = TrainingExample.ParseSuggestedCategory(value);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Books")]
    [InlineData("0")]
    [InlineData("3")]
    [InlineData("Textbook,Notes")]
    public void Given_NotACategoryName_When_ParsingSuggestedCategory_Then_ReturnsNull(string? value)
    {
        var result = TrainingExample.ParseSuggestedCategory(value);

        result.Should().BeNull();
    }

    [Fact]
    public void Given_TrainingExampleType_When_ListingProperties_Then_HasNoPersonalData()
    {
        var names = typeof(TrainingExample).GetProperties().Select(p => p.Name);

        names.Should().NotContain(PersonalDataProperties);
    }
}

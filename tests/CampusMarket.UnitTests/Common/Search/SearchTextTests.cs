using CampusMarket.Api.Common.Search;

namespace CampusMarket.UnitTests.Common.Search;

public class SearchTextTests
{
    [Theory]
    [InlineData("Căști", "casti")]
    [InlineData("ȘTIINȚIFIC", "stiintific")]
    [InlineData("șț", "st")]
    [InlineData("şţ", "st")]
    [InlineData("ŞŢ", "st")]
    [InlineData("Laptop-15", "laptop-15")]
    public void Given_Text_When_Folding_Then_ReturnsLowerCaseWithoutDiacritics(string text, string expected)
    {
        var folded = SearchText.Fold(text);

        folded.Should().Be(expected);
    }

    [Fact]
    public void Given_EmptyText_When_Folding_Then_ReturnsEmpty()
    {
        var folded = SearchText.Fold("");

        folded.Should().BeEmpty();
    }
}

using CampusMarket.Api.Infrastructure.ML;

namespace CampusMarket.UnitTests.Infrastructure.ML;

public class PriceTextFeaturesTests
{
    [Theory]
    [InlineData("Laptop Dell Latitude", "dell")]
    [InlineData("ThinkPad T480", "lenovo")]
    [InlineData("MacBook Air", "apple")]
    [InlineData("Casti Audio Technica ATH-M50", "audio technica")]
    [InlineData("Hard disk Western Digital 1TB", "western digital")]
    [InlineData("LAPTOP DELL", "dell")]
    [InlineData("laptop DeLl", "dell")]
    public void Given_TitleWithKnownBrand_When_Extracting_Then_ReturnsLowercaseBrand(string title, string expected)
    {
        var values = PriceTextFeatures.Extract(title, null);

        values.Brand.Should().Be(expected);
    }

    [Theory]
    [InlineData("Scaun birou")]
    [InlineData("dellicious")]
    [InlineData("")]
    public void Given_TitleWithoutBrand_When_Extracting_Then_ReturnsNone(string title)
    {
        var values = PriceTextFeatures.Extract(title, string.Empty);

        values.Brand.Should().Be("none");
        PriceTextFeatures.NoBrand.Should().Be("none");
    }

    [Fact]
    public void Given_TwoBrands_When_Extracting_Then_FirstBrandWins()
    {
        var values = PriceTextFeatures.Extract("Dell vs HP", null);

        values.Brand.Should().Be("dell");
    }

    [Fact]
    public void Given_BrandOnlyInDescription_When_Extracting_Then_ReturnsThatBrand()
    {
        var values = PriceTextFeatures.Extract("Laptop de studenti", "Marca Asus, folosit un an");

        values.Brand.Should().Be("asus");
    }

    [Fact]
    public void Given_DifferentBrandsInTitleAndDescription_When_Extracting_Then_TitleBrandWins()
    {
        var values = PriceTextFeatures.Extract("Laptop Lenovo", "mai bun decat un Dell");

        values.Brand.Should().Be("lenovo");
    }

    [Fact]
    public void Given_BrandList_When_Reading_Then_AllLowercaseDistinctAndWithoutNone()
    {
        var brands = PriceTextFeatures.Brands;

        brands.Should().NotBeEmpty();
        brands.Should().OnlyContain(b => b == b.ToLowerInvariant());
        brands.Should().OnlyHaveUniqueItems();
        brands.Should().NotContain("none");
        brands.Should().Contain(["lenovo", "audio technica"]);
    }

    [Theory]
    [InlineData("RAM 16GB", 16f)]
    [InlineData("SSD 512 GB", 512f)]
    [InlineData("HDD 1TB", 1024f)]
    [InlineData("HDD 2 TB", 2048f)]
    [InlineData("HDD 1,5 TB", 1536f)]
    [InlineData("HDD 1.5 TB", 1536f)]
    [InlineData("16GB RAM 512GB SSD", 512f)]
    [InlineData("512GB SSD 16GB RAM", 512f)]
    public void Given_NumberWithUnit_When_Extracting_Then_ReturnsLargestStorageInGb(string title, float expected)
    {
        var values = PriceTextFeatures.Extract(title, null);

        values.StorageGb.Should().Be(expected);
    }

    [Fact]
    public void Given_StorageInTitleAndDescription_When_Extracting_Then_ReturnsTheLargestOfBoth()
    {
        var values = PriceTextFeatures.Extract("Laptop 256GB", "Se poate adauga un disc de 1TB");

        values.StorageGb.Should().Be(1024f);
    }

    [Theory]
    [InlineData("Dell 15 inch 2020")]
    [InlineData("GB")]
    [InlineData("Laptop 512GBs")]
    [InlineData("Laptop x512GB")]
    [InlineData("")]
    public void Given_NoValidStorage_When_Extracting_Then_ReturnsZero(string title)
    {
        var values = PriceTextFeatures.Extract(title, null);

        values.StorageGb.Should().Be(0f);
    }

    [Theory]
    [InlineData("Laptop i3", 3f)]
    [InlineData("Laptop i5", 5f)]
    [InlineData("Laptop i7", 7f)]
    [InlineData("Laptop i9", 9f)]
    [InlineData("Core i7-8550U", 7f)]
    [InlineData("Ryzen 5 3600", 5f)]
    [InlineData("Ryzen7", 7f)]
    [InlineData("Ryzen 9 5900X", 9f)]
    [InlineData("Ryzen 3", 3f)]
    [InlineData("i5 and i7", 7f)]
    [InlineData("i7 and i5", 7f)]
    public void Given_Processor_When_Extracting_Then_ReturnsTier(string title, float expected)
    {
        var values = PriceTextFeatures.Extract(title, null);

        values.CpuTier.Should().Be(expected);
    }

    [Theory]
    [InlineData("Laptop i2")]
    [InlineData("Laptop i11")]
    [InlineData("Router wifi5")]
    [InlineData("Ryzen 55")]
    [InlineData("Scaun birou")]
    public void Given_NoValidProcessor_When_Extracting_Then_ReturnsZero(string title)
    {
        var values = PriceTextFeatures.Extract(title, null);

        values.CpuTier.Should().Be(0f);
    }

    [Theory]
    [InlineData("Laptop sigilat")]
    [InlineData("Laptop nou")]
    [InlineData("Laptop new")]
    [InlineData("Laptop sealed")]
    [InlineData("Laptop NOU")]
    [InlineData("Laptop Sigilat")]
    [InlineData("Laptop ȘIGILAT")]
    [InlineData("Laptop ŞIGILAT")]
    [InlineData("Laptop șigilat")]
    [InlineData("Laptop şigilat")]
    [InlineData("Nou, in cutie")]
    [InlineData("Laptop (sealed)")]
    [InlineData("Laptop nou!")]
    public void Given_NewOrSealedWord_When_Extracting_Then_ReturnsOne(string title)
    {
        var values = PriceTextFeatures.Extract(title, null);

        values.IsNewOrSealed.Should().Be(1f);
    }

    [Theory]
    [InlineData("Laptop noua")]
    [InlineData("Laptop nouă")]
    [InlineData("Laptop renovat")]
    [InlineData("Laptop newton")]
    [InlineData("Laptop unsealed")]
    [InlineData("Laptop anou")]
    [InlineData("Laptop folosit")]
    public void Given_WordThatOnlyContainsKeyword_When_Extracting_Then_ReturnsZero(string title)
    {
        var values = PriceTextFeatures.Extract(title, null);

        values.IsNewOrSealed.Should().Be(0f);
    }

    [Theory]
    [InlineData("Laptop sigilat", "")]
    [InlineData("Laptop", "sigilat")]
    [InlineData("Laptop sigilat", "sigilat")]
    public void Given_NewWordInTitleDescriptionOrBoth_When_Extracting_Then_ReturnsOne(string title, string description)
    {
        var values = PriceTextFeatures.Extract(title, description);

        values.IsNewOrSealed.Should().Be(1f);
    }

    [Theory]
    [InlineData("Laptop i7", "", 7f)]
    [InlineData("Laptop", "procesor i7", 7f)]
    [InlineData("Laptop i5", "se poate face upgrade la i9", 9f)]
    public void Given_ProcessorInTitleDescriptionOrBoth_When_Extracting_Then_ReturnsHighestTier(string title, string description, float expected)
    {
        var values = PriceTextFeatures.Extract(title, description);

        values.CpuTier.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(null, "")]
    [InlineData("", null)]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    public void Given_NullEmptyOrBlankText_When_Extracting_Then_ReturnsDefaults(string? title, string? description)
    {
        var values = PriceTextFeatures.Extract(title, description);

        values.Should().Be(new PriceTextFeatures.Values("none", 0f, 0f, 0f));
    }

    [Fact]
    public void Given_AllFeaturesInOneListing_When_Extracting_Then_FillsAllFour()
    {
        var values = PriceTextFeatures.Extract("Laptop Lenovo ThinkPad i7 512GB sigilat", "Nou, in cutie");

        values.Should().Be(new PriceTextFeatures.Values("lenovo", 512f, 7f, 1f));
    }

    [Fact]
    public void Given_RomanianLettersInBothForms_When_Normalizing_Then_ReturnsLowercasePlainLetters()
    {
        var normalized = PriceTextFeatures.Normalize("ȘȚĂÂÎ şţ");

        normalized.Should().Be("staai st");
    }
}

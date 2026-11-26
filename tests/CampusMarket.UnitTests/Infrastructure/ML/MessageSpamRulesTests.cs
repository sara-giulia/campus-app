using CampusMarket.Api.Infrastructure.ML;

namespace CampusMarket.UnitTests.Infrastructure.ML;

public class MessageSpamRulesTests
{
    [Theory]
    [InlineData("Intra pe http://oferta")]
    [InlineData("Intra pe https://plata-sigura/card")]
    [InlineData("Detalii pe www.oferta")]
    [InlineData("Plateste pe plata-sigura.com")]
    [InlineData("Uite aici bit.ly/x")]
    [InlineData("Vezi oferta.ro")]
    [InlineData("Vezi oferta.net")]
    [InlineData("Vezi oferta.org")]
    [InlineData("Vezi oferta.info")]
    [InlineData("Vezi oferta.biz")]
    [InlineData("Vezi oferta.xyz")]
    [InlineData("Vezi oferta.io")]
    [InlineData("Vezi oferta.me")]
    [InlineData("Vezi oferta.online")]
    [InlineData("Vezi oferta.site")]
    [InlineData("Vezi oferta.link")]
    [InlineData("Vezi oferta.top")]
    public void Given_TextWithLink_When_Matching_Then_ReturnsTrue(string text)
    {
        var matches = MessageSpamRules.Matches(text);

        matches.Should().BeTrue();
    }

    [Theory]
    [InlineData("Intra pe bani-rapizi punct ro")]
    [InlineData("Scrie-mi cvv cardului")]
    [InlineData("Am nevoie de cvc")]
    [InlineData("Spune-mi cele 3 cifre din spate")]
    [InlineData("Trimite codul de pe spate")]
    [InlineData("Trimite-mi codul primit prin SMS")]
    [InlineData("Banca iti trimite un cod, spune-mi-l")]
    [InlineData("Trimite 150 lei avans")]
    [InlineData("Vreau plata in avans")]
    [InlineData("Plata inainte, apoi trimit")]
    [InlineData("Platesti acum si e al tau")]
    public void Given_ScamSign_When_Matching_Then_ReturnsTrue(string text)
    {
        var matches = MessageSpamRules.Matches(text);

        matches.Should().BeTrue();
    }

    [Theory]
    [InlineData("bani rapid")]
    [InlineData("bani usor")]
    [InlineData("profit garantat")]
    [InlineData("castig garantat")]
    [InlineData("castigi zilnic")]
    [InlineData("fara risc")]
    [InlineData("bitcoin")]
    [InlineData("crypto")]
    [InlineData("credit rapid")]
    [InlineData("imprumut rapid")]
    [InlineData("card cadou")]
    [InlineData("carduri cadou")]
    [InlineData("gift card")]
    [InlineData("bonus la prima depunere")]
    [InlineData("datele cardului")]
    [InlineData("numarul cardului")]
    [InlineData("data expirarii")]
    [InlineData("poza cu cardul")]
    [InlineData("enter your card")]
    [InlineData("card details")]
    [InlineData("datele de logare")]
    public void Given_BaitPhrase_When_Matching_Then_ReturnsTrue(string phrase)
    {
        var matches = MessageSpamRules.Matches($"Salut, {phrase} pentru tine");

        matches.Should().BeTrue();
    }

    [Theory]
    [InlineData("Suna-ma la 0745 123 456")]
    [InlineData("Am WhatsApp, scrie-mi acolo")]
    [InlineData("ana.pop@student.uaic.ro")]
    [InlineData("Codul de la interfon e 23")]
    [InlineData("Nu cer avans, platesti cand il vezi")]
    [InlineData("Merge si 60 de lei?")]
    public void Given_NormalChat_When_Matching_Then_ReturnsFalse(string text)
    {
        var matches = MessageSpamRules.Matches(text);

        matches.Should().BeFalse();
    }

    [Fact]
    public void Given_DiacriticsAndCapitals_When_Matching_Then_ReturnsTrue()
    {
        var matches = MessageSpamRules.Matches("Câștig GARANTAT");

        matches.Should().BeTrue();
    }

    [Fact]
    public void Given_EmailNextToLink_When_Matching_Then_ReturnsTrue()
    {
        var matches = MessageSpamRules.Matches("scrie pe ana@x.ro sau intra pe www.oferta.ro");

        matches.Should().BeTrue();
    }

    [Fact]
    public void Given_EmailAlone_When_Matching_Then_ReturnsFalse()
    {
        var matches = MessageSpamRules.Matches("Scrie-mi pe ana@x.ro");

        matches.Should().BeFalse();
    }
}

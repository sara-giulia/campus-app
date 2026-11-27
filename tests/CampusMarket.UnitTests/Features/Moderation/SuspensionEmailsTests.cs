using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Features.Moderation;

namespace CampusMarket.UnitTests.Features.Moderation;

public class SuspensionEmailsTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid AdminId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly string Nl = Environment.NewLine;

    private static Member Suspended(DateTimeOffset? until)
    {
        var member = Member.Create("Ana Pop", "ana@campus.ro", Now).Value;
        member.Suspend(AdminId, "Scam attempts", until, Now);
        return member;
    }

    private static string TemporaryBody(string when) =>
        $"Salut Ana Pop,{Nl}{Nl}" +
        $"Contul tău CampusMarket a fost suspendat până la {when} (ora României).{Nl}" +
        $"Motiv: Scam attempts{Nl}{Nl}" +
        "Până atunci nu te poți autentifica, iar anunțurile tale nu sunt vizibile. " +
        $"Rezervările și ofertele în curs au fost anulate. După această dată contul și anunțurile revin automat.{Nl}" +
        "Dacă crezi că este o greșeală, contactează un administrator CampusMarket." +
        $"{Nl}{Nl}----------{Nl}{Nl}" +
        $"Hello Ana Pop,{Nl}{Nl}" +
        $"Your CampusMarket account was suspended until {when} (Romania time).{Nl}" +
        $"Reason: Scam attempts{Nl}{Nl}" +
        "Until then you cannot sign in and your listings are not visible. " +
        $"Your ongoing reservations and offers were cancelled. After this date your account and your listings come back automatically.{Nl}" +
        $"If you think this is a mistake, contact a CampusMarket administrator.{Nl}";

    [Fact]
    public void Given_WinterEnd_When_BuildingSuspendedBody_Then_ShowsBucharestTimeTwoHoursAhead()
    {
        var member = Suspended(new DateTimeOffset(2026, 1, 15, 8, 30, 0, TimeSpan.Zero));

        var body = SuspensionEmails.SuspendedBody(member);

        body.Should().Be(TemporaryBody("15.01.2026 10:30"));
    }

    [Fact]
    public void Given_SummerEnd_When_BuildingSuspendedBody_Then_ShowsBucharestTimeThreeHoursAhead()
    {
        var member = Suspended(new DateTimeOffset(2026, 7, 15, 21, 30, 0, TimeSpan.Zero));

        var body = SuspensionEmails.SuspendedBody(member);

        body.Should().Be(TemporaryBody("16.07.2026 00:30"));
    }

    [Fact]
    public void Given_PermanentSuspension_When_BuildingSuspendedBody_Then_RomanianFirstThenEnglish()
    {
        var member = Suspended(null);

        var body = SuspensionEmails.SuspendedBody(member);

        body.Should().Be(
            $"Salut Ana Pop,{Nl}{Nl}" +
            $"Contul tău CampusMarket a fost suspendat definitiv.{Nl}" +
            $"Motiv: Scam attempts{Nl}{Nl}" +
            "Nu te mai poți autentifica, iar anunțurile tale nu mai sunt vizibile. " +
            $"Rezervările și ofertele în curs au fost anulate.{Nl}" +
            "Dacă crezi că este o greșeală, contactează un administrator CampusMarket." +
            $"{Nl}{Nl}----------{Nl}{Nl}" +
            $"Hello Ana Pop,{Nl}{Nl}" +
            $"Your CampusMarket account was suspended permanently.{Nl}" +
            $"Reason: Scam attempts{Nl}{Nl}" +
            "You can no longer sign in and your listings are no longer visible. " +
            $"Your ongoing reservations and offers were cancelled.{Nl}" +
            $"If you think this is a mistake, contact a CampusMarket administrator.{Nl}");
    }

    [Fact]
    public void Given_SuspendedBody_When_Splitting_Then_RomanianBeforeTheSeparatorAndEnglishAfter()
    {
        var member = Suspended(null);

        var parts = SuspensionEmails.SuspendedBody(member).Split("----------");

        parts.Should().HaveCount(2);
        parts[0].Should().StartWith("Salut Ana Pop,");
        parts[1].Trim().Should().StartWith("Hello Ana Pop,");
    }

    [Fact]
    public void Given_SuspendedBody_When_Reading_Then_HasNoTokenNoLinkAndNoEmail()
    {
        var member = Suspended(Now.AddDays(3));

        var body = SuspensionEmails.SuspendedBody(member);

        body.Should().NotContain("http");
        body.Should().NotContain("token");
        body.Should().NotContain("ana@campus.ro");
        body.Should().NotContain(AdminId.ToString());
    }

    [Fact]
    public void Given_Subjects_When_Reading_Then_RomanianThenEnglish()
    {
        var suspended = SuspensionEmails.SuspendedSubject;
        var lifted = SuspensionEmails.LiftedSubject;

        suspended.Should().Be("Contul tău CampusMarket a fost suspendat / Your CampusMarket account was suspended");
        lifted.Should().Be("Contul tău CampusMarket a fost reactivat / Your CampusMarket account was reactivated");
    }

    [Fact]
    public void Given_Member_When_BuildingLiftedBody_Then_ContainsTheLinkInBothLanguages()
    {
        var member = Member.Create("Ana Pop", "ana@campus.ro", Now).Value;

        var body = SuspensionEmails.LiftedBody(member, "https://campus.example/my/listings");

        body.Should().Be(
            $"Salut Ana Pop,{Nl}{Nl}" +
            "Suspendarea contului tău CampusMarket a fost ridicată. Te poți autentifica din nou, " +
            $"iar anunțurile tale sunt din nou vizibile:{Nl}https://campus.example/my/listings" +
            $"{Nl}{Nl}----------{Nl}{Nl}" +
            $"Hello Ana Pop,{Nl}{Nl}" +
            "The suspension of your CampusMarket account was lifted. You can sign in again " +
            $"and your listings are visible again:{Nl}https://campus.example/my/listings{Nl}");
    }
}

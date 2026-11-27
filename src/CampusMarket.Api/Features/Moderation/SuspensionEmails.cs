using System.Globalization;
using CampusMarket.Api.Domain.Members;

namespace CampusMarket.Api.Features.Moderation;

// The emails of SuspendMember and LiftSuspension: Romanian first, then English, in the same email.
// Nothing sensitive in them: no token, no link that logs in, no reporter and no report text; only the
// member's name, the end date and the reason the Admin wrote for this member.
internal static class SuspensionEmails
{
    public const string SuspendedSubject =
        "Contul tău CampusMarket a fost suspendat / Your CampusMarket account was suspended";

    public const string LiftedSubject =
        "Contul tău CampusMarket a fost reactivat / Your CampusMarket account was reactivated";

    private const string Separator = "----------";

    // Called right after Member.Suspend, so SuspendedUntil and SuspensionReason are set.
    public static string SuspendedBody(Member member)
    {
        var nl = Environment.NewLine;
        var reason = member.SuspensionReason ?? string.Empty;

        string ro, en;
        if (member.SuspendedUntil is { } until)
        {
            var when = RomaniaTime(until);
            ro =
                $"Salut {member.Name},{nl}{nl}" +
                $"Contul tău CampusMarket a fost suspendat până la {when} (ora României).{nl}" +
                $"Motiv: {reason}{nl}{nl}" +
                $"Până atunci nu te poți autentifica, iar anunțurile tale nu sunt vizibile. " +
                $"Rezervările și ofertele în curs au fost anulate. După această dată contul și anunțurile revin automat.{nl}" +
                "Dacă crezi că este o greșeală, contactează un administrator CampusMarket.";
            en =
                $"Hello {member.Name},{nl}{nl}" +
                $"Your CampusMarket account was suspended until {when} (Romania time).{nl}" +
                $"Reason: {reason}{nl}{nl}" +
                $"Until then you cannot sign in and your listings are not visible. " +
                $"Your ongoing reservations and offers were cancelled. After this date your account and your listings come back automatically.{nl}" +
                "If you think this is a mistake, contact a CampusMarket administrator.";
        }
        else
        {
            ro =
                $"Salut {member.Name},{nl}{nl}" +
                $"Contul tău CampusMarket a fost suspendat definitiv.{nl}" +
                $"Motiv: {reason}{nl}{nl}" +
                $"Nu te mai poți autentifica, iar anunțurile tale nu mai sunt vizibile. " +
                $"Rezervările și ofertele în curs au fost anulate.{nl}" +
                "Dacă crezi că este o greșeală, contactează un administrator CampusMarket.";
            en =
                $"Hello {member.Name},{nl}{nl}" +
                $"Your CampusMarket account was suspended permanently.{nl}" +
                $"Reason: {reason}{nl}{nl}" +
                $"You can no longer sign in and your listings are no longer visible. " +
                $"Your ongoing reservations and offers were cancelled.{nl}" +
                "If you think this is a mistake, contact a CampusMarket administrator.";
        }

        return $"{ro}{nl}{nl}{Separator}{nl}{nl}{en}{nl}";
    }

    // myListingsLink: {Frontend:BaseUrl}/my/listings (a plain page link, it does not log in).
    public static string LiftedBody(Member member, string myListingsLink)
    {
        var nl = Environment.NewLine;
        var ro =
            $"Salut {member.Name},{nl}{nl}" +
            $"Suspendarea contului tău CampusMarket a fost ridicată. Te poți autentifica din nou, " +
            $"iar anunțurile tale sunt din nou vizibile:{nl}{myListingsLink}";
        var en =
            $"Hello {member.Name},{nl}{nl}" +
            $"The suspension of your CampusMarket account was lifted. You can sign in again " +
            $"and your listings are visible again:{nl}{myListingsLink}";

        return $"{ro}{nl}{nl}{Separator}{nl}{nl}{en}{nl}";
    }

    // The members are in Romania. When the server has no time zone data (a minimal container) the
    // UTC time is shown, marked as such, rather than failing the email.
    private static string RomaniaTime(DateTimeOffset moment)
    {
        if (TimeZoneInfo.TryFindSystemTimeZoneById("Europe/Bucharest", out var zone))
            return TimeZoneInfo.ConvertTime(moment, zone).ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

        return moment.UtcDateTime.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture) + " UTC";
    }
}

namespace CampusMarket.ML.DatasetGenerator;

// How a spam pattern prices its listing.
//  - Absurd: the loud prices of the original patterns (1 leu, 5-49 lei or 50000-99999 lei);
//    a disguised row keeps the price of the item it imitates.
//  - TooCheap: "too good to be true", a fraction of what the item is worth.
//  - Plausible: an ordinary price, so the price alone gives nothing away.
public enum SpamPrice
{
    Absurd,
    TooCheap,
    Plausible,
}

// One spam pattern: its own titles (for loud rows) and description texts. Every row of a pattern,
// loud or disguised, gets TemplateId = Id, so the group-aware split keeps the whole pattern (its
// titles and its texts) in one part: a spam text in test is never also in train.
public sealed record SpamPattern(string Id, string Style, string[] Titles, string[] Descriptions, SpamPrice Price);

public static class Spam
{
    // Original 12 loud titles and 12 description bodies (spam-00 ... spam-11). Their lengths are
    // part of the main random stream (see Generator.GenerateSpam), so they must not change.
    public static readonly string[] AllCapsTitles =
    {
        "CASTIGA 500 LEI PE ZI DE ACASA!!!",
        "BANI RAPIZI, SUNA ACUM!!!",
        "OFERTA LIMITATA, CITESTE DESCRIEREA!!!",
        "URGENT!!! PROMOTIE CAMPUS!!!",
        "GRATIS!!! DOAR AZI!!!",
        "CEL MAI BUN PRET!!! NU RATA!!!",
        "GIVEAWAY IPHONE!!! SCRIE PE WHATSAPP!!!",
        "VINZI RAPID? INCERCI METODA MEA!!!",
        "MUNCA ONLINE 300 EURO PE SAPTAMANA!!!",
        "ACCES VIP LA PLATFORMA, DOAR 1 LEU!!!",
        "CURS GRATUIT TRADING CRYPTO!!!",
        "CADOU STUDENTI!!! NU RATA OCAZIA!!!",
    };

    public static readonly string[] NormalLookingTitles =
    {
        "Manual analiza matematica, an I",
        "Notite POO, anul II",
        "Casti bluetooth pentru facultate",
        "Halat laborator medicina",
        "Bicicleta city, 7 viteze",
        "Laptop pentru facultate, 8GB RAM",
        "Casio fx-991EX pentru examen",
        "Breadboard 830 puncte",
        "Lampa birou LED",
        "Scaun birou ergonomic",
    };

    public static readonly string[] DescriptionBodies =
    {
        "Scrie-mi pe whatsapp 07{P1} {P2} {P3} pentru detalii si livrare rapida.",
        "Contact doar pe telegram @student{N}, nu raspund la mesaje aici.",
        "Link in bio pentru oferta completa, intra pe site-ul meu si inregistreaza-te.",
        "Castiga 500 lei pe zi de acasa! Scrie-mi si iti spun metoda, 100% garantat!!!",
        "GIVEAWAY!!! Distribuie postarea si poti castiga un iphone. Scrie pe WhatsApp 07{P1} {P2} {P3}!!!",
        "Metoda sigura de castig online, am castigat deja 10000 lei!!! Suna la 07{P1} {P2} {P3}.",
        "OFERTA LIMITATA, pret 1 leu doar azi, mesaj privat pe telegram @student{N}.",
        "Curs trading crypto GRATUIT, link in bio, incepi sa castigi de maine!!!",
        "VINZI RAPID produsele tale? Inscrie-te pe platforma, 500 lei bonus, link in descriere.",
        "Suna acum la 07{P1} {P2} {P3} pentru bani rapizi!!! Urgent!!!",
        "Pret negociabil, dar doar pe whatsapp, nu raspund aici. 07{P1} {P2} {P3}.",
        "Contacteaza-ma pe telegram @campus{N}, livrare oriunde in tara, plata cash sau transfer.",
    };

    // The loud title of each original pattern (index into AllCapsTitles), so a title belongs to
    // exactly one pattern instead of being shared by all twelve.
    private static readonly int[] LegacyTitleOfBody = { 5, 3, 2, 0, 6, 8, 9, 10, 7, 1, 4, 11 };

    private static readonly string[] LegacyStyles =
    {
        "whatsapp-phone", "telegram-handle", "link-in-bio", "easy-money", "giveaway-whatsapp",
        "easy-money-phone", "limited-offer-telegram", "crypto-course", "platform-signup",
        "call-now", "whatsapp-only", "telegram-delivery",
    };

    public const int LegacyPatternCount = 12;

    // New patterns (spam-12 ... spam-23): other styles than "write me on WhatsApp / Telegram".
    // Placeholders: {N} 3 digits, {D4} 4 digits, {S} a short-link code, {OBF} an obfuscated
    // phone number, {H} a handle, {PRICE} a price in lei. No real people, numbers or live links:
    // phone numbers are spelled or masked and the short links are written with broken dots.
    private static readonly SpamPattern[] NewPatterns =
    {
        new("spam-12", "fake-giveaway-follow",
            new[] { "CONCURS: LAPTOP GRATUIT PENTRU STUDENTI", "Giveaway de inceput de semestru 🎁", "IMPARTIM 10 TABLETE GRATIS" },
            new[]
            {
                "Concurs pentru studenti: dam gratuit 3 laptopuri. Da follow la pagina, like si tag la 3 prieteni, castigatorii se anunta vineri.",
                "Giveaway! Distribuie anuntul in 5 grupuri de facultate si trimite-mi capturile, extragerea e duminica seara.",
                "Ai fost selectat in tragerea la sorti! Intra pe pagina din profil si completeaza formularul cu datele cardului ca sa primesti premiul.",
            },
            SpamPrice.Plausible),
        new("spam-13", "fake-giveaway-fee",
            new[] { "AI CASTIGAT O TABLETA, REVENDICA ACUM", "Premiu pentru tine: casti wireless gratuite", "FELICITARI, ESTI CASTIGATORUL SAPTAMANII" },
            new[]
            {
                "Felicitari, ai castigat o tableta noua! Platesti doar taxa de procesare de 19 lei si ti-o trimitem maine.",
                "Esti unul dintre cei 50 de castigatori. Ca sa iti rezervam premiul achiti 25 lei taxa de ambalare in urmatoarele 2 ore.",
                "Premiul tau te asteapta, mai trebuie doar confirmarea: un transfer simbolic de 9 lei si coletul pleaca azi.",
            },
            SpamPrice.Absurd),
        new("spam-14", "crypto-doubling",
            new[] { "BITCOIN: DEPUI 200, SCOTI 1000", "Investitie crypto sigura pentru studenti", "PROFIT GARANTAT DIN MONEDE DIGITALE" },
            new[]
            {
                "Investitie sigura in Bitcoin: depui 200 lei, dupa o saptamana retragi 1000. Fara risc, am capturi cu retragerile.",
                "Am un bot de trading care face 8% pe zi. Imi trimiti suma, o investesc eu si iti dau profitul in fiecare vineri.",
                "Moneda noua lansata luna asta, cine intra acum isi inmulteste banii de 20 de ori. Locuri limitate pentru studenti.",
            },
            SpamPrice.Plausible),
        new("spam-15", "forex-signals",
            new[] { "SEMNALE FOREX VIP, PROFIT ZILNIC", "Grup privat de trading, primele zile gratis", "INVATA SA FACI 5000 LEI PE LUNA DIN TRADING" },
            new[]
            {
                "Grup VIP de semnale forex, profit zilnic de 15%. Primele 3 zile gratuit, apoi abonament de 49 lei pe luna.",
                "Mentorat de trading pentru studenti: iti arat strategia mea si in prima luna iti recuperezi investitia de 5 ori.",
                "Copiezi tranzactiile mele si castigi fara sa stii nimic despre bursa. Contul minim e de 250 lei.",
            },
            SpamPrice.Absurd),
        new("spam-16", "obfuscated-phone",
            new[] { "Vand ieftin, detalii doar la telefon", "Oferta speciala, nu scriu aici", "Pret bun, sunati-ma direct" },
            new[]
            {
                "Nu folosesc chatul de aici, da-mi mesaj la {OBF} si iti raspund imediat.",
                "Pentru poze si pret final: {OBF} (scrie numarul cu cifre, aplicatia ascunde numerele).",
                "Cel mai repede ma prinzi la {OBF}, seara dupa ora 18. Pe site nu verific mesajele.",
            },
            SpamPrice.Plausible),
        new("spam-17", "other-app-handle",
            new[] { "Detalii pe insta, raspund mai repede acolo", "Vand mai multe lucruri, vezi pagina mea", "Stoc mare, comenzi in privat" },
            new[]
            {
                "Pe insta {H} raspund mai repede, aici intru rar. Am si alte produse in story.",
                "Ma gasesti pe discord ca {H}, acolo trimit pozele si facem plata.",
                "Lasa-mi un mesaj pe snap sau pe signal, cauta {H}. Aici nu primesc notificari.",
            },
            SpamPrice.Plausible),
        new("spam-18", "too-good-price",
            new[] { "IPHONE 15 PRO SIGILAT 400 LEI", "MacBook Air nou la pret de lichidare", "PLAYSTATION 5 LA JUMATATE DE PRET" },
            new[]
            {
                "Nou, sigilat, il dau ieftin pentru ca plec din tara saptamana asta. Primul care scrie il ia, nu tin pentru nimeni.",
                "Pret atat de mic pentru ca e din stocul unui magazin care s-a inchis. Am mai multe bucati, garantie inclusa.",
                "Il dau la {PRICE} lei, pretul real e de 5 ori mai mare. Trimit doar prin curier, nu ne vedem in campus.",
            },
            SpamPrice.TooCheap),
        new("spam-19", "short-link",
            new[] { "Catalog complet cu reduceri, vezi linkul", "Lichidare stoc electronice, link in descriere", "TOATE OFERTELE INTR-UN SINGUR LINK" },
            new[]
            {
                "Toate detaliile si pozele aici: bit(.)ly/{S}",
                "Catalogul complet e pe tinyurl[.]com/{S}, comanda direct de acolo cu reducere 70%.",
                "Intra pe cutt . ly / {S} si foloseste codul STUDENT pentru transport gratuit.",
            },
            SpamPrice.Plausible),
        new("spam-20", "mixed-ro-en-deal",
            new[] { "BEST DEAL ON CAMPUS, LIMITED STOCK", "Brand new, original, super pret", "Hot sale pentru studenti, only today" },
            new[]
            {
                "Hey! Best deal on campus, 100% original, DM me pe insta pentru pret special. Limited stock, nu ratati!",
                "Brand new items, shipping in 24h oriunde in tara. Check my page pentru full catalog, payment upfront only.",
                "Super offer only this week: buy 2 get 1 free, plus cadou surpriza. Send me a message pe WhatsApp, not here.",
            },
            SpamPrice.Absurd),
        new("spam-21", "mixed-ro-en-job",
            new[] { "WORK FROM HOME, 50$ PE ORA", "Job online pentru studenti, no experience", "Easy money: 2 ore pe zi de pe telefon" },
            new[]
            {
                "Work from home si castigi 50$ pe ora. No experience needed, doar un telefon si 2 ore pe zi. Apply now, scrie-mi in privat.",
                "We are hiring studenti pentru like-uri si review-uri platite, 300 lei pe saptamana. Training gratuit, start imediat.",
                "Remote job, plata zilnica in crypto. Fill the form din profilul meu si te adaug in grupul de onboarding.",
            },
            SpamPrice.Plausible),
        new("spam-22", "fake-recruitment",
            new[] { "Angajam studenti, 150 lei pe zi", "Castiga bani din postat anunturi", "Colaborare platita, program flexibil" },
            new[]
            {
                "Cautam studenti care sa posteze anunturi pentru noi, 150 lei pe zi, plata in fiecare seara. Detalii doar in privat.",
                "Colaborare platita: copiezi texte intr-un formular, 2 lei pe text. Inainte platesti 49 lei pentru cont de colaborator.",
                "Program flexibil, lucrezi din camin. Pachetul de start costa 99 lei si il recuperezi din prima zi.",
            },
            SpamPrice.Absurd),
        new("spam-23", "pyramid-team",
            new[] { "INTRA IN ECHIPA MEA SI CASTIGI DIN PRIETENI", "Afacere pentru studenti, venit pasiv", "VENIT PASIV DIN PRIMA LUNA" },
            new[]
            {
                "Intra in echipa mea si castigi din fiecare prieten pe care il aduci. Pachetul de start e 99 lei si il scoti in prima saptamana.",
                "Venit pasiv fara munca: aduci doi oameni, ei aduc alti doi, iar tu primesti din tot ce se vinde sub tine.",
                "Am ajuns la 4000 lei pe luna doar invitand colegi de camin. Iti arat sistemul la o cafea, trebuie doar sa crezi in el.",
            },
            SpamPrice.Plausible),
    };

    public static readonly IReadOnlyList<SpamPattern> Patterns = BuildPatterns();

    public static IReadOnlyList<SpamPattern> New => NewPatterns;

    public static string PickTitle(Random rng)
    {
        return AllCapsTitles[rng.Next(AllCapsTitles.Length)];
    }

    public static string PickNormalLookingTitle(Random rng)
    {
        return NormalLookingTitles[rng.Next(NormalLookingTitles.Length)];
    }

    public static string PickDescription(Random rng)
    {
        return PickDescriptionWithIndex(rng).Description;
    }

    // Picks one of the original 12 bodies and fills its placeholders. The index identifies the
    // original pattern (spam-00 ... spam-11). The number of draws on rng is fixed (five), which
    // keeps the main random stream of the generator aligned.
    public static (string Description, int Index) PickDescriptionWithIndex(Random rng)
    {
        var index = rng.Next(DescriptionBodies.Length);
        var template = DescriptionBodies[index];
        var description = template
            .Replace("{P1}", rng.Next(10, 100).ToString("D2"))
            .Replace("{P2}", rng.Next(100, 1000).ToString("D3"))
            .Replace("{P3}", rng.Next(100, 1000).ToString("D3"))
            .Replace("{N}", rng.Next(100, 1000).ToString());
        return (description, index);
    }

    // Fills the placeholders of a new-pattern text. Every placeholder is always drawn, in the same
    // order, so the number of draws does not depend on the text.
    public static string Fill(string template, Random rng, int price)
    {
        var n = rng.Next(100, 1000).ToString();
        var d4 = rng.Next(1000, 10000).ToString();
        var slug = ShortCode(rng);
        var obfuscated = ObfuscatedPhone(rng);
        var handle = Handle(rng);
        return template
            .Replace("{N}", n)
            .Replace("{D4}", d4)
            .Replace("{S}", slug)
            .Replace("{OBF}", obfuscated)
            .Replace("{H}", handle)
            .Replace("{PRICE}", price.ToString());
    }

    private static IReadOnlyList<SpamPattern> BuildPatterns()
    {
        var all = new List<SpamPattern>(LegacyPatternCount + NewPatterns.Length);
        for (var i = 0; i < LegacyPatternCount; i++)
        {
            all.Add(new SpamPattern(
                $"spam-{i:D2}",
                LegacyStyles[i],
                new[] { AllCapsTitles[LegacyTitleOfBody[i]] },
                new[] { DescriptionBodies[i] },
                SpamPrice.Absurd));
        }
        all.AddRange(NewPatterns);
        return all;
    }

    private static readonly string[] DigitWords =
    {
        "zero", "unu", "doi", "trei", "patru", "cinci", "sase", "sapte", "opt", "noua",
    };

    // Phone numbers written the way sellers hide them from filters. Always the same draws:
    // eight digits and one style.
    private static string ObfuscatedPhone(Random rng)
    {
        var digits = new int[8];
        for (var i = 0; i < digits.Length; i++)
        {
            digits[i] = rng.Next(10);
        }
        var style = rng.Next(4);
        var tail = string.Concat(digits.Select(d => d.ToString()));
        return style switch
        {
            0 => "0 7 " + string.Join(" ", digits.Select(d => d.ToString())),
            1 => "zero sapte " + string.Join(" ", digits.Take(4).Select(d => DigitWords[d])) + " " + tail[4..],
            2 => "07xx " + tail[2..5] + " " + tail[5..],
            _ => "07." + tail[..2] + "." + tail[2..5] + "." + tail[5..],
        };
    }

    private static readonly string[] HandleStems = { "campus.deals", "student.shop", "oferte_camin", "vanzari.rapide", "deals4students" };

    private static string Handle(Random rng)
    {
        var stem = HandleStems[rng.Next(HandleStems.Length)];
        return "@" + stem + "_" + rng.Next(10, 100);
    }

    private const string CodeAlphabet = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private static string ShortCode(Random rng)
    {
        var chars = new char[6];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = CodeAlphabet[rng.Next(CodeAlphabet.Length)];
        }
        return new string(chars);
    }
}

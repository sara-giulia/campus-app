using CampusMarket.Api.Common.Listings;
using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Members;

namespace CampusMarket.Api.Infrastructure.Persistence.Seed;

// Demo data for the Render live demo (seeded once, into an empty database).
// The data is written in Romanian and goes through the domain factories
// (Member.Register / Listing.Create / Listing.Publish / Reserve / ConfirmSale),
// so the same validation that every real request goes through also runs here.
// Nothing here is real: names are generic, emails use @demo.campusmarket.local,
// and no phone numbers, addresses or external links appear in any text.
public static class DemoListings
{
    public const string EmailDomain = "@demo.campusmarket.local";

    // The existing demo member (demo@campusmarket.ro) stays first so an older
    // demo deployment still accepts the same sign-in email. The other members
    // use the dedicated demo domain so they cannot collide with real sign-ups.
    private static readonly DemoMember[] MemberData =
    [
        new("Demo Student", "demo@campusmarket.ro"),
        new("Andrei Popescu", "andrei.popescu" + EmailDomain),
        new("Maria Ionescu", "maria.ionescu" + EmailDomain),
        new("Ioana Dumitrescu", "ioana.dumitrescu" + EmailDomain),
        new("Vlad Stanescu", "vlad.stanescu" + EmailDomain),
    ];

    // Exactly 60 listings, spread across all 5 categories, 5 sellers and the
    // last 30 days. 54 stay Active, 3 are Reserved and 3 are Sold, so the
    // listings endpoint still returns multiple pages of Active items.
    // Fields: Title, Category, Condition, Price (lei), Description,
    //         SellerIndex (0..4), AgeDays (0..29, days before "now"),
    //         FinalStatus (Active / Reserved / Sold).
    private static readonly DemoListingTemplate[] ListingData =
    [
        new("Manual Analiza Matematica anul I", Category.Textbook, Condition.Good, 40m, "Stare buna, cateva notite in creion pe ultimele pagini.", 0, 0, ListingStatus.Active),
        new("Manual Programare C++ facultate", Category.Textbook, Condition.Good, 55m, "Folosit un singur semestru, fara sublinieri.", 1, 1, ListingStatus.Active),
        new("Culegere algebra liniara", Category.Textbook, Condition.Worn, 25m, "Coperta putin uzata, continutul intact.", 2, 2, ListingStatus.Active),
        new("Manual Fizica anul II", Category.Textbook, Condition.Good, 50m, "Editie noua, folosit la cursul de mecanica.", 3, 3, ListingStatus.Active),
        new("Manual Baze de date relationale", Category.Textbook, Condition.New, 80m, "Carte sigilata, luata dubla din greseala.", 4, 4, ListingStatus.Active),
        new("Curs Retele de calculatoare", Category.Textbook, Condition.Good, 45m, "Suportul de curs tiparit, in stare foarte buna.", 0, 5, ListingStatus.Active),
        new("Culegere analiza matematica II", Category.Textbook, Condition.Worn, 20m, "Ideal pentru pregatirea examenului.", 1, 6, ListingStatus.Reserved),
        new("Manual Chimie generala", Category.Textbook, Condition.Good, 35m, "Folosit la laboratorul de chimie anul I.", 2, 7, ListingStatus.Active),
        new("Manual Programare orientata pe obiecte", Category.Textbook, Condition.Good, 60m, "Cuprinde exemple in Java si C#.", 3, 8, ListingStatus.Active),
        new("Manual Sisteme de operare Linux", Category.Textbook, Condition.New, 70m, "Nu a mai fost deschis, luat in plus la inceput de an.", 4, 9, ListingStatus.Active),
        new("Manual Statistica si probabilitati", Category.Textbook, Condition.Good, 45m, "Explicatii clare, multe exercitii rezolvate.", 0, 10, ListingStatus.Sold),
        new("Culegere exercitii analiza numerica", Category.Textbook, Condition.Good, 30m, "Exercitii grupate pe capitole, cu raspunsuri.", 1, 11, ListingStatus.Active),
        new("Manual Electronica digitala", Category.Textbook, Condition.Worn, 25m, "Cateva pagini indoite la colturi.", 2, 12, ListingStatus.Active),
        new("Manual Teoria grafurilor", Category.Textbook, Condition.Good, 55m, "Folosit la cursul optional din anul III.", 3, 13, ListingStatus.Active),
        new("Manual Algoritmi si structuri de date", Category.Textbook, Condition.Good, 65m, "Carte folosita la pregatirea pentru interviuri.", 4, 14, ListingStatus.Active),

        new("Notite curs analiza matematica anul I", Category.Notes, Condition.New, 15m, "Notite scrise de mana, complete pentru semestrul I.", 0, 15, ListingStatus.Active),
        new("Notite seminar POO", Category.Notes, Condition.New, 20m, "Exemple rezolvate pentru fiecare laborator.", 1, 16, ListingStatus.Active),
        new("Notite curs retele de calculatoare", Category.Notes, Condition.Good, 18m, "Scrise clar, cu scheme desenate.", 2, 17, ListingStatus.Sold),
        new("Notite laborator baze de date", Category.Notes, Condition.New, 22m, "Comenzi SQL comentate pentru fiecare laborator.", 3, 18, ListingStatus.Active),
        new("Notite curs sisteme de operare", Category.Notes, Condition.Good, 17m, "Cuprinde partitionare, procese si sincronizare.", 4, 19, ListingStatus.Active),
        new("Notite seminar algoritmi", Category.Notes, Condition.New, 20m, "Teme rezolvate pas cu pas.", 0, 20, ListingStatus.Active),
        new("Notite examen fizica anul I", Category.Notes, Condition.Good, 12m, "Formule si rezolvari pentru subiectele vechi.", 1, 21, ListingStatus.Active),
        new("Notite curs statistica", Category.Notes, Condition.Good, 15m, "Exemple cu seturi de date reale discutate la curs.", 2, 22, ListingStatus.Active),
        new("Notite laborator electronica", Category.Notes, Condition.New, 18m, "Scheme si masuratori pentru toate laboratoarele.", 3, 23, ListingStatus.Active),
        new("Notite seminar algebra", Category.Notes, Condition.Good, 14m, "Exercitii rezolvate complet, utile la examen.", 4, 24, ListingStatus.Active),

        new("Laptop Lenovo ThinkPad T470 i5", Category.Electronics, Condition.Good, 1500m, "Procesor i5, 8GB RAM, SSD 256GB, baterie buna.", 0, 25, ListingStatus.Active),
        new("Casti wireless aproape noi", Category.Electronics, Condition.Good, 180m, "Autonomie buna, cutia originala, folosite 2 luni.", 1, 26, ListingStatus.Active),
        new("Monitor LG 24 inch IPS", Category.Electronics, Condition.Good, 450m, "Rezolutie Full HD, fara pixeli morti.", 2, 27, ListingStatus.Active),
        new("Tastatura mecanica Keychron K2", Category.Electronics, Condition.New, 420m, "Switch-uri brown, aproape neutilizata.", 3, 28, ListingStatus.Active),
        new("Mouse Logitech MX Master 3", Category.Electronics, Condition.Good, 320m, "Bateria tine aproape o luna cu o incarcare.", 4, 29, ListingStatus.Reserved),
        new("Camera web Logitech C920", Category.Electronics, Condition.Good, 220m, "Ideala pentru cursurile online.", 0, 2, ListingStatus.Active),
        new("Boxe Bluetooth Anker SoundCore", Category.Electronics, Condition.Good, 260m, "Sunet curat, autonomie peste 10 ore.", 1, 4, ListingStatus.Active),
        new("SSD Samsung 500GB NVMe", Category.Electronics, Condition.Good, 290m, "Testat, raport SMART atasat la cerere.", 2, 6, ListingStatus.Active),
        new("Memorie RAM DDR4 16GB laptop", Category.Electronics, Condition.Good, 240m, "Doua module de 8GB, viteza 3200 MHz.", 3, 8, ListingStatus.Active),
        new("Laptop Dell Latitude 5490", Category.Electronics, Condition.Good, 1800m, "Nu mai am nevoie de el dupa upgrade, merge perfect.", 4, 10, ListingStatus.Active),
        new("iPad generatia 9 wifi 64GB", Category.Electronics, Condition.Good, 1400m, "Folie protectie montata, husa originala.", 0, 12, ListingStatus.Active),
        new("Suport telefon pentru birou", Category.Electronics, Condition.New, 35m, "Reglabil pe inaltime, nefolosit.", 1, 14, ListingStatus.Active),
        new("Incarcator laptop universal 90W", Category.Electronics, Condition.Good, 90m, "Cu adaptoare pentru majoritatea laptopurilor.", 2, 16, ListingStatus.Active),
        new("Hub USB-C multiport HDMI SD", Category.Electronics, Condition.New, 140m, "Porturi HDMI, USB-A, SD si incarcare.", 3, 18, ListingStatus.Active),
        new("Cablu HDMI 2 metri", Category.Electronics, Condition.New, 25m, "Suporta 4K, nefolosit, in ambalaj.", 4, 20, ListingStatus.Active),

        new("Calculator stiintific Casio FX-991", Category.LabEquipment, Condition.Good, 110m, "Toate functiile avansate merg, folosit la examene.", 0, 22, ListingStatus.Active),
        new("Multimetru digital Uni-T", Category.LabEquipment, Condition.Good, 180m, "Baterii noi, carcasa fara zgarieturi.", 1, 24, ListingStatus.Active),
        new("Osciloscop portabil pentru laborator electronica", Category.LabEquipment, Condition.Good, 650m, "Util la laboratoarele de electronica si semnale.", 2, 26, ListingStatus.Active),
        new("Pipeta gradata 10 ml set lab chimie", Category.LabEquipment, Condition.Good, 45m, "Set de 3 pipete, pastrate in tocul original.", 3, 28, ListingStatus.Active),
        new("Trusa desen tehnic facultate", Category.LabEquipment, Condition.Good, 70m, "Compas, echer, raportor, in trusa originala.", 4, 3, ListingStatus.Active),
        new("Compas si rigla tehnica profesionala", Category.LabEquipment, Condition.New, 55m, "Nefolosite, cumparate pentru desen tehnic.", 0, 7, ListingStatus.Active),
        new("Breadboard plus set componente Arduino", Category.LabEquipment, Condition.Good, 160m, "Ideal pentru laboratorul de electronica digitala.", 1, 11, ListingStatus.Active),
        new("Set rezistente laborator electronica", Category.LabEquipment, Condition.New, 30m, "Rezistente diverse valori, in cutie organizatoare.", 2, 15, ListingStatus.Active),

        new("Bicicleta MTB rosie pentru campus", Category.Other, Condition.Good, 650m, "Roti 26 inch, schimbator Shimano, perfecta pentru oras.", 3, 19, ListingStatus.Active),
        new("Birou pliabil cămin studentesc", Category.Other, Condition.Good, 180m, "Se pliaza usor, ocupa putin loc.", 4, 23, ListingStatus.Active),
        new("Scaun ergonomic pentru invatat", Category.Other, Condition.Good, 320m, "Suport lombar reglabil, folosit un an.", 0, 27, ListingStatus.Reserved),
        new("Lampa de birou cu LED reglabil", Category.Other, Condition.New, 85m, "Temperatura culoare reglabila, aproape noua.", 1, 1, ListingStatus.Active),
        new("Set de oale pentru bucataria caminului", Category.Other, Condition.Good, 120m, "Trei oale si o tigaie, curate si functionale.", 2, 5, ListingStatus.Active),
        new("Geaca iarna marime M", Category.Other, Condition.Good, 150m, "Caldurosa, impermeabila, folosita un sezon.", 3, 9, ListingStatus.Active),
        new("Pantofi sport alergare marime 42", Category.Other, Condition.Good, 180m, "Folositi doar la sala, talpa aproape intacta.", 4, 13, ListingStatus.Sold),
        new("Rucsac laptop 17 inch", Category.Other, Condition.Good, 130m, "Compartiment separat pentru laptop, impermeabil.", 0, 17, ListingStatus.Active),
        new("Umbrela de ploaie neagra", Category.Other, Condition.New, 40m, "Automata, in ambalajul original.", 1, 21, ListingStatus.Active),
        new("Set de perne si plapuma pentru cămin", Category.Other, Condition.Good, 200m, "Spalate, gata de folosit.", 2, 25, ListingStatus.Active),
        new("Saltea gonflabila pentru oaspeti", Category.Other, Condition.Good, 90m, "Cu pompa inclusa, testata fara pierderi de aer.", 3, 29, ListingStatus.Active),
        new("Trotineta electrica pliabila", Category.Other, Condition.Good, 1200m, "Autonomie reala 20 km, pliabila pentru metrou.", 4, 2, ListingStatus.Active),
    ];

    public static int ListingCount => ListingData.Length;

    public static int ActiveListingCount => CountByStatus(ListingStatus.Active);

    public static int MemberCount => MemberData.Length;

    public static List<Member> BuildMembers(IPasswordHasher hasher, string password, DateTimeOffset now)
    {
        var result = new List<Member>(MemberData.Length);
        foreach (var data in MemberData)
        {
            var member = Member.Register(data.Name, data.Email, hasher.Hash(password), now, MemberRole.Student);
            if (member.IsSuccess)
                result.Add(member.Value);
        }
        return result;
    }

    // Builds the 60 demo listings against the given seller ids. The seller list
    // must have at least one member; indices in the demo data wrap around it,
    // so the caller may pass one orphan id (when no demo member was created)
    // or the full list of demo members. "now" comes from TimeProvider so tests
    // can use a fake clock; CreatedAt values are "now - AgeDays".
    public static List<Listing> BuildListings(List<Guid> sellerIds, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(sellerIds);
        if (sellerIds.Count == 0)
            throw new ArgumentException("At least one seller id is required.", nameof(sellerIds));

        var result = new List<Listing>(ListingData.Length);
        foreach (var template in ListingData)
        {
            var sellerId = sellerIds[template.SellerIndex % sellerIds.Count];
            var createdAt = now.AddDays(-template.AgeDays);

            var created = Listing.Create(sellerId, template.Title, template.Category, template.Condition, template.Price, createdAt, template.Description);
            if (created.IsFailure)
                continue;

            var listing = created.Value;
            // Every demo listing stays in the search for a full period from the seed, whatever its age.
            listing.Publish(now.AddDays(ListingOptions.DefaultExpiryDays));

            ApplyFinalStatus(listing, template.FinalStatus, sellerIds, template.SellerIndex, now);

            result.Add(listing);
        }
        return result;
    }

    // Moves a published listing to its final status by replaying real domain
    // transitions: Reserve by another member, and (for Sold) ConfirmSale on
    // that reservation. Any member other than the seller works as a buyer;
    // picking the next one by index keeps the choice deterministic even when
    // only one seller id exists (no transition then, so the status stays Active).
    private static void ApplyFinalStatus(Listing listing, ListingStatus finalStatus, List<Guid> sellerIds, int sellerIndex, DateTimeOffset now)
    {
        if (finalStatus == ListingStatus.Active || sellerIds.Count < 2)
            return;

        var buyerId = sellerIds[(sellerIndex + 1) % sellerIds.Count];
        var reservation = listing.Reserve(buyerId, now);
        if (reservation.IsFailure)
            return;

        if (finalStatus == ListingStatus.Sold)
            listing.ConfirmSale(reservation.Value.Id, now);
    }

    private static int CountByStatus(ListingStatus status)
    {
        var count = 0;
        foreach (var template in ListingData)
        {
            if (template.FinalStatus == status)
                count++;
        }
        return count;
    }

    private sealed record DemoMember(string Name, string Email);

    private sealed record DemoListingTemplate(
        string Title,
        Category Category,
        Condition Condition,
        decimal Price,
        string Description,
        int SellerIndex,
        int AgeDays,
        ListingStatus FinalStatus);
}

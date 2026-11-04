namespace CampusMarket.ML.DatasetGenerator;

public static class Spam
{
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
        var template = DescriptionBodies[rng.Next(DescriptionBodies.Length)];
        return template
            .Replace("{P1}", rng.Next(10, 100).ToString("D2"))
            .Replace("{P2}", rng.Next(100, 1000).ToString("D3"))
            .Replace("{P3}", rng.Next(100, 1000).ToString("D3"))
            .Replace("{N}", rng.Next(100, 1000).ToString());
    }
}

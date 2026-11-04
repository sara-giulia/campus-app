namespace CampusMarket.ML.DatasetGenerator;

public sealed record ItemTemplate(
    string[] Titles,
    string[] DescriptionLines,
    int BasePriceMin,
    int BasePriceMax);

public static class Catalog
{
    public static readonly ItemTemplate[] Textbooks =
    {
        new(
            Titles: new[]
            {
                "Analiză matematică - vol. {VOL}, ed. {ED}",
                "Analiza matematica, culegere probleme, an I",
                "Manual Analiză matematică Rogai, an I semestrul {SEM}",
                "ANALIZA MATEMATICA Fihtenholt vol {VOL}",
                "Analiză Matematică - Siretchi, cu exerciții rezolvate",
            },
            DescriptionLines: new[]
            {
                "Manual folosit un semestru, am scris cu creionul pe margine.",
                "Carte in stare buna, fara pete, cateva sublinieri cu pixul.",
                "Coperta ușor uzată, interiorul e ok.",
                "Toate paginile prezente, nimic lipit sau rupt.",
                "Ideal pentru anul I Mate-Info sau Automatica.",
            },
            BasePriceMin: 40,
            BasePriceMax: 120),

        new(
            Titles: new[]
            {
                "Programare orientată pe obiecte în C++ - Ciocârlan",
                "POO - Manual C++ Herbert Schildt",
                "Introducere in POO (Java) - manual an II",
                "Programare Orientata pe Obiecte, culegere probleme",
                "Manual POO C# an II, editura UB",
            },
            DescriptionLines: new[]
            {
                "Manualul de la cursul de POO, anul II.",
                "Toate exemplele de la laborator sunt in carte.",
                "Carte folosită 2 semestre, câteva pagini îndoite.",
                "Explicatii clare, exemple din C++ si Java.",
                "Vine cu CD-ul original, nedeschis.",
            },
            BasePriceMin: 35,
            BasePriceMax: 90),

        new(
            Titles: new[]
            {
                "Baze de date - Popescu, Editura Infomedia",
                "Baze de date relaționale - manual an II",
                "SQL și proiectarea bazelor de date - manual complet",
                "BAZE DE DATE Connolly editia 4",
                "Manual baze de date, Oracle si SQL Server, an III",
            },
            DescriptionLines: new[]
            {
                "Acopera tot de la relatii la normalizare si tranzactii.",
                "Carte folosită pentru examenul de baze de date, an II.",
                "Interior aproape nou, copertă putin îndoită.",
                "Contine exemple cu SQL Server si Oracle.",
                "Fara sublinieri, pare ca nu a fost citita.",
            },
            BasePriceMin: 45,
            BasePriceMax: 110),

        new(
            Titles: new[]
            {
                "Fizică generală - mecanică și termodinamică, ed. {ED}",
                "Manual Fizică pentru admitere politehnică",
                "Fizica - Culegere probleme rezolvate (Hristev)",
                "FIZICA GENERALA Sears Zemansky",
                "Fizica - electricitate si magnetism, an I ETTI",
            },
            DescriptionLines: new[]
            {
                "Manual de bază pentru fizică la facultate.",
                "Carte cu toate formulele pentru examen.",
                "Stare foarte bună, folosită un semestru.",
                "Culegere cu peste 500 de probleme rezolvate.",
                "Cateva pagini cu sublinieri la formule.",
            },
            BasePriceMin: 40,
            BasePriceMax: 100),

        new(
            Titles: new[]
            {
                "Anatomie - Papilian vol. {VOL}, ed. {ED}",
                "Anatomia omului - Sinelnikov vol. {VOL}",
                "Manual anatomie an I medicina, cu atlas",
                "ANATOMIE descriptiva, splanchnologia",
                "Anatomie, aparat cardiovascular, an I medicină",
            },
            DescriptionLines: new[]
            {
                "Manualul folosit la UMF, an I medicină.",
                "Carte in stare foarte buna, am folosit-o la examen.",
                "Vine cu semne de carte si cateva note pe margine.",
                "Perfect pentru anul I de medicina generala.",
                "Imagini clare, nimic lipit sau rupt.",
            },
            BasePriceMin: 70,
            BasePriceMax: 180),

        new(
            Titles: new[]
            {
                "Algebră liniară și geometrie analitică, an I",
                "Matematici speciale - manual ETTI/AC",
                "Structuri de date și algoritmi - Cormen (CLRS)",
                "Sisteme de operare - Tanenbaum, ed. {ED}",
                "Retele de calculatoare - Andrew Tanenbaum",
                "Economie politică - manual an I ASE",
                "Chimie organică, manual an I farmacie",
                "Introducere in Machine Learning - manual engleza",
            },
            DescriptionLines: new[]
            {
                "Manual de referință pentru cursul respectiv.",
                "Carte aproape nouă, folosită doar pentru examen.",
                "Stare bună, câteva sublinieri în interior.",
                "Fără pete, fără pagini rupte.",
                "Ideal pentru studenții la Informatică / Automatică.",
            },
            BasePriceMin: 45,
            BasePriceMax: 150),
    };

    public static readonly ItemTemplate[] Notes =
    {
        new(
            Titles: new[]
            {
                "Notițe Analiză matematică, an I semestrul {SEM}",
                "Rezumat examen Analiza, an I",
                "Notite curs Analiza matematica, scrise de mana",
                "Probleme rezolvate analiza, examen iunie",
                "NOTITE ANALIZA MATEMATICA anul 1",
            },
            DescriptionLines: new[]
            {
                "Notițe făcute la curs, aproape toată materia acoperită.",
                "Scrise de mana, cu demonstratii si exemple.",
                "60 de pagini, cuprind si exercitii rezolvate.",
                "Perfect pentru pregatirea in sesiune.",
                "Rezumat clar al cursului, cu toate teoremele.",
            },
            BasePriceMin: 15,
            BasePriceMax: 45),

        new(
            Titles: new[]
            {
                "Notițe POO, anul II C++",
                "Rezumat Programare orientata pe obiecte",
                "Notite laborator POO, toate laboratoarele",
                "Probleme rezolvate pentru examen POO",
                "Proiecte si notite POO Java, an II",
            },
            DescriptionLines: new[]
            {
                "Notițe luate la curs si la seminar, complete.",
                "Include exemple de cod si probleme de examen.",
                "Acopera mostenire, polimorfism, template-uri.",
                "Scris citet, pe caiet A4.",
                "Cu scheme si diagrame UML pentru proiectul final.",
            },
            BasePriceMin: 12,
            BasePriceMax: 40),

        new(
            Titles: new[]
            {
                "Notițe Baze de date - an II",
                "Rezumat SQL pentru examen BD",
                "Laboratoare BD rezolvate, an II",
                "NOTITE curs BAZE DE DATE normalizare",
                "Probleme BD cu SQL rezolvate",
            },
            DescriptionLines: new[]
            {
                "Notite de la cursul de BD, 40 de pagini.",
                "Include exemple SQL Server + diagrame ER.",
                "Toate formele normale explicate pe exemple.",
                "Rezumat ideal pentru pregatire rapida.",
                "Scrise lizibil, cu culori pentru elemente diferite.",
            },
            BasePriceMin: 15,
            BasePriceMax: 40),

        new(
            Titles: new[]
            {
                "Notițe Fizică - mecanică, an I",
                "Rezumat termodinamică, examen fizică",
                "Notite seminar fizica electromagnetism",
                "Fizica - formule esentiale pentru examen",
                "Probleme rezolvate fizica, admitere politehnica",
            },
            DescriptionLines: new[]
            {
                "Notite scrise de mana la curs.",
                "Contine toate formulele importante.",
                "Exercitii rezolvate pas cu pas.",
                "Carte A5, format compact pentru examen.",
                "Scrise frumos, fara greseli.",
            },
            BasePriceMin: 12,
            BasePriceMax: 38),

        new(
            Titles: new[]
            {
                "Notițe Anatomie, an I medicină",
                "Rezumat osteologie si artrologie",
                "Fise anatomie, aparat digestiv",
                "Notite UMF, an I, semestrul {SEM}",
                "Rezumat examen anatomie, toracele",
            },
            DescriptionLines: new[]
            {
                "Notite UMF anul I, scrise de mana.",
                "Include scheme desenate cu culori.",
                "Acopera toate lucrarile practice de anatomie.",
                "Rezumat clar pentru examene scrise.",
                "Fise de recapitulare pentru oral.",
            },
            BasePriceMin: 20,
            BasePriceMax: 50),

        new(
            Titles: new[]
            {
                "Notițe Electronică digitală, an II",
                "Rezumat Rețele de calculatoare, pentru examen",
                "Notite Algoritmi si structuri de date, an I",
                "Rezumat Sisteme de operare, an II CTI",
                "Notite Matematici speciale, serii Fourier",
                "Rezumat Statistica si probabilitati",
                "Notite chimie organica, an I farmacie",
            },
            DescriptionLines: new[]
            {
                "Notite complete de la curs si seminar.",
                "Scrise de mana, bine organizate.",
                "Toate temele din programa acoperite.",
                "Cu exemple si probleme rezolvate.",
                "Perfect pentru restante si examene de iarna.",
            },
            BasePriceMin: 15,
            BasePriceMax: 40),
    };

    public static readonly ItemTemplate[] Electronics =
    {
        new(
            Titles: new[]
            {
                "Laptop Lenovo ThinkPad T480, i5 + 16GB RAM",
                "Laptop pentru facultate, 8GB RAM, SSD 256",
                "LAPTOP ASUS VivoBook, i3, 8GB",
                "Ultrabook 14 inch pentru studenti, ssd 512",
                "Laptop gaming second hand, i7 + 16GB + GTX1650",
            },
            DescriptionLines: new[]
            {
                "Laptop folosit la cursuri, bateria tine 4 ore.",
                "Fara zgarieturi majore, functioneaza perfect.",
                "Windows preinstalat, actualizat.",
                "Vine cu incarcator original si husa.",
                "Ideal pentru proiecte de facultate si Visual Studio.",
            },
            BasePriceMin: 1500,
            BasePriceMax: 4500),

        new(
            Titles: new[]
            {
                "Casio fx-991EX ClassWiz, pentru examen",
                "Calculator stiintific Casio fx-991EX",
                "CASIO FX 991EX, ca nou, pentru BAC/admitere",
                "Casio fx-991ES Plus, calculator programabil",
                "Calculator Casio fx-82MS pentru liceu/an I",
            },
            DescriptionLines: new[]
            {
                "Calculator in stare perfecta, baterie plina.",
                "Folosit un an la facultate.",
                "Carcasa fara zgarieturi.",
                "Include capacul original.",
                "Acceptat la examenele de admitere si de licenta.",
            },
            BasePriceMin: 80,
            BasePriceMax: 160),

        new(
            Titles: new[]
            {
                "Casti bluetooth, baterie 20 ore",
                "Casti over-ear pentru facultate, cu microfon",
                "CASTI WIRELESS cu anulare zgomot",
                "Headphones gaming, 7.1 surround",
                "Casti in-ear sport, rezistente la apa",
            },
            DescriptionLines: new[]
            {
                "Sunet foarte bun, folosite pentru cursuri online.",
                "Vin cu cutie si cablu de incarcare.",
                "Pernute foarte comode, se poarta mult timp.",
                "Fara zgarieturi, functioneaza perfect.",
                "Microfonul se aude clar la Zoom/Meet.",
            },
            BasePriceMin: 70,
            BasePriceMax: 500),

        new(
            Titles: new[]
            {
                "Monitor 24 inch Full HD, HDMI + DP",
                "Monitor LG 27'' IPS, pentru programare",
                "MONITOR DELL 24 inch, 75Hz",
                "Monitor curbat 32 inch, gaming 144Hz",
                "Monitor 22'' second hand, pentru studenti",
            },
            DescriptionLines: new[]
            {
                "Monitor folosit la laborator acasa, fara pixeli morti.",
                "Vine cu cablu HDMI si stand original.",
                "Imagine foarte buna, culori calibrate.",
                "Ideal pentru programare si proiecte.",
                "Carcasa putin zgariata in spate, fata e perfecta.",
            },
            BasePriceMin: 350,
            BasePriceMax: 1500),

        new(
            Titles: new[]
            {
                "Tastatura mecanica, switch-uri rosii",
                "TASTATURA RGB gaming, cu numpad",
                "Mouse gaming 16000 DPI, 7 butoane",
                "Mouse wireless pentru facultate",
                "Kit tastatura + mouse wireless",
            },
            DescriptionLines: new[]
            {
                "Tastatura folosita 6 luni, taste perfecte.",
                "RGB-ul functioneaza, se configureaza din software.",
                "Mouse foarte confortabil, pentru sesiuni lungi.",
                "Vin cu cabluri originale si cutie.",
                "Fara defecte, totul merge perfect.",
            },
            BasePriceMin: 60,
            BasePriceMax: 450),

        new(
            Titles: new[]
            {
                "Telefon Samsung Galaxy A52, 128GB",
                "iPhone SE 2020, 64GB, baterie buna",
                "TELEFON Xiaomi Redmi Note 11, dual sim",
                "Telefon Huawei P30 lite, 128GB, in stare buna",
                "Smartphone second hand, Android 12, 6GB RAM",
            },
            DescriptionLines: new[]
            {
                "Telefon folosit cu grija, fara zgarieturi.",
                "Bateria tine o zi intreaga de utilizare normala.",
                "Vine cu incarcator si folie de sticla.",
                "Resetat la setarile din fabrica, gata de folosit.",
                "Camera foarte buna, suficient de rapid pentru facultate.",
            },
            BasePriceMin: 300,
            BasePriceMax: 2200),
    };

    public static readonly ItemTemplate[] LabEquipment =
    {
        new(
            Titles: new[]
            {
                "Halat laborator alb, bumbac, marimea M",
                "Halat laborator medicina, mărimea L",
                "HALAT laborator chimie, lung, nou",
                "Halat laborator, model unisex, mărimea S",
                "Halat alb facultate, cu buzunare, marimea XL",
            },
            DescriptionLines: new[]
            {
                "Halat folosit un semestru la laborator.",
                "Spalat si calcat, fara pete.",
                "Bumbac 100%, confortabil pentru ore lungi.",
                "Vin cu nasturi de rezerva.",
                "Marime standard conforma cu regulamentul facultatii.",
            },
            BasePriceMin: 35,
            BasePriceMax: 90),

        new(
            Titles: new[]
            {
                "Ochelari protectie laborator, policarbonat",
                "OCHELARI protectie, antistropi, lab chimie",
                "Ochelari laborator, cu benzi reglabile",
                "Ochelari protectie fizica/chimie, reglabili",
                "Ochelari protectie si manusi, kit laborator",
            },
            DescriptionLines: new[]
            {
                "Ochelari in stare perfecta, folositi doar la 2 lucrari.",
                "Vin cu husa de protectie.",
                "Lentile transparente, fara zgarieturi.",
                "Benzi reglabile, confortabili pentru ore intregi.",
                "Conformi cu cerintele de la laborator.",
            },
            BasePriceMin: 20,
            BasePriceMax: 70),

        new(
            Titles: new[]
            {
                "Multimetru digital UNI-T UT61E",
                "Multimetru profesional, pentru laborator electronica",
                "MULTIMETRU digital, cu sonde, nou",
                "Multimetru Fluke 101, calibrat",
                "Multimetru pentru lab electronica, masoara V/I/R",
            },
            DescriptionLines: new[]
            {
                "Multimetru folosit la laborator, functioneaza perfect.",
                "Vine cu sonde si manual de utilizare.",
                "Baterie noua, afisaj clar.",
                "Ideal pentru proiecte electronica si arduino.",
                "Masoara tensiune, curent, rezistenta, capacitate.",
            },
            BasePriceMin: 70,
            BasePriceMax: 280),

        new(
            Titles: new[]
            {
                "Breadboard 830 puncte, cu fire jumper",
                "KIT breadboard + componente pentru Arduino",
                "Breadboard mare, cu sine de alimentare",
                "Set breadboard + 65 fire jumper m-m, m-f, f-f",
                "Breadboard 400 puncte, pentru proiecte mici",
            },
            DescriptionLines: new[]
            {
                "Breadboard nou, folosit la un singur proiect.",
                "Vine cu set de fire jumper de diferite lungimi.",
                "Contactele sunt in stare perfecta.",
                "Perfect pentru laborator si proiecte personale.",
                "Include si o punte de alimentare 5V/3V3.",
            },
            BasePriceMin: 15,
            BasePriceMax: 60),

        new(
            Titles: new[]
            {
                "Kit Arduino UNO R3 original, cu componente",
                "KIT ARDUINO complet, cu 200+ componente",
                "Kit Arduino Mega + senzori + motoare",
                "Set invatare Arduino cu manual in romana",
                "Kit robotica Arduino cu sasiu si roti",
            },
            DescriptionLines: new[]
            {
                "Kit folosit pentru un semestru, toate componentele sunt ok.",
                "Vine cu cutie originala si cablu USB.",
                "Perfect pentru laborator de proiect IoT.",
                "Include breadboard, senzori, LED-uri, rezistente.",
                "Arduino UNO R3 verificat ca functioneaza.",
            },
            BasePriceMin: 100,
            BasePriceMax: 350),

        new(
            Titles: new[]
            {
                "Trusa disectie, 12 piese, inox",
                "Kit disectie an I medicina, inox cu etui",
                "TRUSA DISECTIE completa, pentru medicina generala",
                "Trusa disectie pentru stomatologie",
                "Set instrumente disectie, folosit un semestru",
            },
            DescriptionLines: new[]
            {
                "Trusa in stare foarte buna, dezinfectata.",
                "Vine cu etui de piele si toate instrumentele.",
                "Lamele ascutite, bisturie noi de rezerva.",
                "Folosita la UMF un semestru.",
                "Ideal pentru anul I medicina generala.",
            },
            BasePriceMin: 50,
            BasePriceMax: 150),
    };

    public static readonly ItemTemplate[] Others =
    {
        new(
            Titles: new[]
            {
                "Bicicleta city, roti 28, 7 viteze",
                "BICICLETA mountain bike, 26 inch",
                "Bicicleta pentru facultate, usoara, aluminiu",
                "Bicicleta second hand, cadru M, revizuita",
                "Bicicleta pliabila, ideala pentru camin",
            },
            DescriptionLines: new[]
            {
                "Bicicleta folosita un an, perfect functionala.",
                "Revizuita recent, frane noi.",
                "Vine cu antifurt cadou.",
                "Potrivita pentru drumul casa-facultate.",
                "Fara defecte, cadru fara zgarieturi majore.",
            },
            BasePriceMin: 300,
            BasePriceMax: 1500),

        new(
            Titles: new[]
            {
                "Lampă birou LED, cu brat reglabil",
                "LAMPA LED pentru invatat, cu temperatura reglabila",
                "Lampă birou tactila, 3 niveluri de intensitate",
                "Veioza LED USB, ideala pentru camin",
                "Lampa de birou cu incarcare wireless integrata",
            },
            DescriptionLines: new[]
            {
                "Lampa folosita cateva luni, functioneaza perfect.",
                "Vine cu cablu USB-C si manual.",
                "Lumina placuta, nu oboseste ochii.",
                "Ideala pentru sesiunile de studiu.",
                "Fara defecte, carcasa ca noua.",
            },
            BasePriceMin: 35,
            BasePriceMax: 180),

        new(
            Titles: new[]
            {
                "Scaun birou ergonomic, cu suport lombar",
                "SCAUN birou cu rotile, inaltime reglabila",
                "Scaun gaming second hand, cu cotiere",
                "Scaun ergonomic IKEA Markus (replica)",
                "Scaun birou tapitat cu plasa, foarte aerisit",
            },
            DescriptionLines: new[]
            {
                "Scaun folosit 1 an, in stare foarte buna.",
                "Toate functiile merg, nu scartaie.",
                "Perfect pentru ore lungi la calculator.",
                "Tapiterie fara pete sau zgarieturi.",
                "Suporta pana la 110 kg, verificat.",
            },
            BasePriceMin: 100,
            BasePriceMax: 500),

        new(
            Titles: new[]
            {
                "Set caserole camin, 10 bucati, etanse",
                "FRIGIDER MIC pentru camin, 50L",
                "Fierbator electric 1.7L, ideal pentru camin",
                "Multicooker pentru camin, 1.5L, 7 programe",
                "Aspirator portabil pentru camera de camin",
            },
            DescriptionLines: new[]
            {
                "Folosit in camin un an, in stare foarte buna.",
                "Curatat si dezinfectat inainte de vanzare.",
                "Vine cu accesoriile originale.",
                "Perfect pentru viata la camin.",
                "Fara defecte functionale, aspect ingrijit.",
            },
            BasePriceMin: 25,
            BasePriceMax: 300),

        new(
            Titles: new[]
            {
                "Set 3 romane Mircea Eliade, editie cartonata",
                "Harry Potter set complet, 7 volume",
                "Dictionar roman-englez, Teora, editia 2018",
                "Carte Sapiens - Yuval Noah Harari",
                "Lotul de lectura obligatorie BAC, 5 volume",
            },
            DescriptionLines: new[]
            {
                "Carti in stare foarte buna, citite o data.",
                "Fara sublinieri sau pagini rupte.",
                "Vin din casa de nefumatori.",
                "Ideale pentru biblioteca personala.",
                "Coperte protejate cu folie transparenta.",
            },
            BasePriceMin: 20,
            BasePriceMax: 100),
    };

    public static readonly (Category Category, string Title, string? Description, int BasePriceMin, int BasePriceMax)[] HardExamples =
    {
        (Category.Notes, "Notițe Electronică digitală, an II", "Notite de la curs si seminar, cu scheme logice si tabele de adevar.", 15, 40),
        (Category.Notes, "Rezumat curs Sisteme de calcul, pentru examen", "Notite scrise de mana, acopera toata materia.", 15, 40),
        (Category.Notes, "Fise arhitectura calculatoarelor, an II", "Fise de recapitulare, 25 de pagini.", 12, 35),
        (Category.Notes, "Notițe Rețele de calculatoare, Cisco CCNA", "Notite complete pentru pregatirea CCNA.", 20, 50),
        (Category.Notes, "Notite laborator robotica, cu diagrame", "Include laboratoarele de robotica cu Arduino.", 18, 45),
        (Category.Textbook, "Manual Arhitectura calculatoarelor - Patterson Hennessy", "Manual clasic de arhitectura calculatoarelor, ed. 5.", 90, 220),
        (Category.Textbook, "Manual Sisteme de operare - Tanenbaum", "Manualul standard pentru cursul de SO.", 80, 180),
        (Category.Textbook, "Introducere in Machine Learning - manual engleza", "Manual englez pentru cursul optional de ML.", 70, 160),
        (Category.LabEquipment, "Kit Arduino UNO pentru laborator electronică", "Kitul oficial folosit la lab de electronica digitala.", 120, 300),
        (Category.LabEquipment, "Breadboard mare + componente, pentru laboratorul de electronica", "Breadboard 830 puncte cu set complet de componente.", 40, 110),
        (Category.LabEquipment, "Multimetru + sonde, kit laborator electronica", "Multimetru Fluke cu set de sonde noi.", 100, 250),
        (Category.Electronics, "Carte digitala + ebook reader Kindle", "Kindle Paperwhite cu peste 100 de carti incarcate.", 300, 700),
        (Category.Other, "Suport laptop ergonomic din aluminiu", "Suport pentru laptop, ridica ecranul la nivelul ochilor.", 40, 150),
    };
}

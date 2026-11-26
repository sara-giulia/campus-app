namespace CampusMarket.ML.DatasetGenerator;

// One kind of chat message: its TemplateId (the split keeps every row of a template in one part, so test
// has kinds of messages the model never saw), whether it is spam, and a few phrasings with {slots}.
public sealed record MessageTemplate(string Id, bool IsSpam, string[] Phrasings);

// The messages a buyer and a seller write to each other about a listing, and the spam and scams that
// show up in the chat of a marketplace. Normal messages also give a phone number, a WhatsApp or an
// e-mail address and talk about money: between two students who meet on campus that is normal, so the
// model must not learn that a number or "whatsapp" alone is spam. Spam is what asks for money or card
// data in advance, sends a link to "receive the money", or promotes easy money, crypto, loans or channels.
public static class MessageTemplates
{
    public static readonly MessageTemplate[] Normal =
    [
        new("msg-availability", false, [
            "Bună, mai este disponibil {item}?",
            "Salut! Mai ai {item}?",
            "Bună ziua, anunțul cu {item} mai e valabil?",
            "Hei, {item} mai e de vânzare?",
            "Mai e disponibil? Aș fi interesat.",
        ]),
        new("msg-still-available-answer", false, [
            "Da, încă e disponibil.",
            "Da, îl mai am, dacă vrei poți să vii să îl vezi.",
            "Încă e la mine, nu l-a luat nimeni.",
            "Da, e disponibil până se găsește cineva.",
        ]),
        new("msg-final-price", false, [
            "Cât e prețul final?",
            "Care e ultimul preț?",
            "Se poate negocia prețul?",
            "Prețul e negociabil?",
            "Cât mai lași din preț?",
        ]),
        new("msg-offer", false, [
            "Îți dau {price} lei pe {item}, e ok?",
            "Merge și {price} de lei?",
            "Ofer {price} lei, dacă vrei.",
            "Ai accepta {price} lei?",
            "Pot să îți dau {price} lei, mai mult nu am momentan.",
        ]),
        new("msg-counter-offer", false, [
            "La {price} lei ți-l las, mai jos nu pot.",
            "Hai {price} și batem palma.",
            "Pot să las la {price} lei dacă îl iei azi.",
            "{price} e prețul minim, l-am luat cu mult mai mult.",
        ]),
        new("msg-accept-price", false, [
            "Bine, e ok {price} lei.",
            "Ok, accept, ne vedem și îți dau banii.",
            "S-a făcut, {price} lei.",
            "Perfect, rămâne {price}.",
        ]),
        new("msg-decline-price", false, [
            "Nu, scuze, e prea puțin.",
            "Mersi, dar nu pot să las atât.",
            "Nu cobor sub {price}, sorry.",
            "Prea puțin, mai am pe cineva interesat.",
        ]),
        new("msg-meeting-place", false, [
            "Ne putem vedea {place}?",
            "Te aștept {place} {time}.",
            "Putem să ne întâlnim {place}, {day} {time}?",
            "Eu sunt {place} {day}, dacă îți convine.",
            "Vin {place} {time}, e bine?",
        ]),
        new("msg-meeting-time", false, [
            "Când ai putea să te vezi cu mine?",
            "{day} {time} îți convine?",
            "Pot doar {day}, după {time}.",
            "Azi nu pot, merge {day}?",
            "Sunt liber {day} {time}.",
        ]),
        new("msg-running-late", false, [
            "Scuze, întârzii vreo 10 minute.",
            "Am ajuns, sunt {place}.",
            "Vin, mai am 5 minute.",
            "Am pierdut tramvaiul, ajung în 15 minute.",
        ]),
        new("msg-dorm-room", false, [
            "Sunt în căminul {dorm}, camera {room}, poți să treci.",
            "Stau în {dorm}, la camera {room}.",
            "Treci pe la camera {room}, căminul {dorm}, după {time}.",
        ]),
        new("msg-condition", false, [
            "În ce stare e {item}?",
            "Are zgârieturi?",
            "Funcționează tot?",
            "Are vreun defect?",
            "Cât timp l-ai folosit?",
        ]),
        new("msg-condition-answer", false, [
            "E aproape nou, l-am folosit un semestru.",
            "Are o zgârietură mică pe margine, în rest e perfect.",
            "Merge tot, l-am testat ieri.",
            "E folosit, dar e în stare bună.",
            "Nu are niciun defect, e păstrat cu grijă.",
        ]),
        new("msg-more-photos", false, [
            "Poți să îmi trimiți mai multe poze?",
            "Mai ai o poză din spate?",
            "Poți pune și o poză cu {item} pornit?",
            "Am pus încă două poze în anunț.",
        ]),
        new("msg-details-book", false, [
            "Ce ediție e manualul?",
            "Are notițe scrise în el?",
            "E pentru cursul de anul {year}?",
            "Sunt toate capitolele în notițe?",
            "E ediția din {yearFull}, cea care se cere la curs.",
        ]),
        new("msg-details-electronics", false, [
            "Câți GB de RAM are?",
            "Bateria cât ține?",
            "Vine cu încărcător?",
            "Ai și cutia originală?",
            "Bateria ține cam {hours} ore.",
        ]),
        new("msg-reserve", false, [
            "Poți să mi-l păstrezi până {day}?",
            "Te rog nu îl da altcuiva, vin {day}.",
            "L-am rezervat din aplicație, vin după el {day}.",
            "Îl rezerv eu, ok?",
        ]),
        new("msg-reserve-answer", false, [
            "Ok, ți-l păstrez până {day}.",
            "Bine, nu îl mai dau altcuiva.",
            "Ți-l țin până {day}, dacă nu vii îl dau mai departe.",
        ]),
        new("msg-pay-at-meeting", false, [
            "Plătesc cash când ne vedem.",
            "Îți dau banii pe loc, după ce îl văd.",
            "Pot să îți dau pe Revolut când ne întâlnim?",
            "Îți fac transfer pe loc, când îl primesc.",
            "Accepți plata cu cardul pe Revolut la predare?",
        ]),
        new("msg-share-phone", false, [
            "Dacă vrei, sună-mă la {phone}.",
            "Numărul meu e {phone}, dă-mi un mesaj când ajungi.",
            "Scrie-mi la {phone} dacă nu răspund aici.",
            "Uite numărul meu: {phone}.",
        ]),
        new("msg-share-whatsapp", false, [
            "Am WhatsApp pe {phone}, dacă e mai simplu.",
            "Răspund mai repede pe WhatsApp, {phone}.",
            "Îți scriu pe WhatsApp când plec spre {place}.",
            "Dacă vrei, vorbim pe WhatsApp.",
        ]),
        new("msg-share-email", false, [
            "Îți trimit pe mail lista cu cărțile: {email}.",
            "Poți să îmi scrii și pe mail, {email}.",
            "Mail-ul meu de facultate e {email}.",
        ]),
        new("msg-thanks", false, [
            "Mersi mult!",
            "Mulțumesc, a fost o plăcere.",
            "Mersi, totul e perfect.",
            "Mulțumesc, spor la sesiune!",
            "Mersi frumos, o zi bună!",
        ]),
        new("msg-after-sale", false, [
            "Am ajuns acasă, merge perfect, mersi!",
            "L-am verificat, totul e în regulă.",
            "Mersi încă o dată, îți las un review.",
            "Ți-am lăsat o recenzie, mersi!",
        ]),
        new("msg-sold", false, [
            "Scuze, l-am vândut deja.",
            "Din păcate a plecat azi dimineață.",
            "S-a vândut, îmi pare rău.",
            "Am vândut, dar mai am {item} dacă te interesează.",
        ]),
        new("msg-cancel", false, [
            "Scuze, nu mai am nevoie de el.",
            "M-am răzgândit, mersi oricum.",
            "Nu mai pot ajunge, rămâne pe altă dată.",
            "Anulez rezervarea, am găsit altul.",
        ]),
        new("msg-other-items", false, [
            "Mai ai și alte cărți de anul {year}?",
            "Vinzi și {item}?",
            "Dacă iau două, îmi faci reducere?",
            "Am mai pus câteva anunțuri, uită-te pe profilul meu.",
        ]),
        new("msg-course", false, [
            "E bun pentru examenul de {subject}?",
            "L-am folosit la {subject}, te ajută mult.",
            "Profesorul de {subject} cere exact cartea asta.",
            "Notițele acoperă tot ce se dă la {subject}.",
        ]),
        new("msg-delivery-campus", false, [
            "Poți să mi-l aduci {place}?",
            "Pot să ți-l las la portarul căminului {dorm}.",
            "Îl las la o colegă din camera {room}, dacă nu sunt eu.",
            "Trimit un prieten să îl ia, e ok?",
        ]),
        new("msg-shipping-legit", false, [
            "Nu sunt în oraș, poți să mi-l trimiți prin curier? Plătesc la primire.",
            "Îl trimit cu ramburs, plătești când îl primești.",
            "Pot să îl trimit prin poștă, costul transportului e la tine.",
            "Merge cu plata la livrare, prin curier?",
        ]),
        new("msg-question-generic", false, [
            "De ce îl vinzi?",
            "Ți-a fost util?",
            "E original?",
            "Cât l-ai luat nou?",
            "Unde l-ai cumpărat?",
        ]),
        new("msg-small-talk", false, [
            "Și eu sunt la {faculty}, în ce an ești?",
            "Ești și tu la {faculty}?",
            "Succes la examene!",
            "Spor la învățat!",
        ]),
        new("msg-link-specs", false, [
            "Uite specificațiile oficiale, sunt pe site-ul producătorului.",
            "Modelul exact e în poza a doua, îl poți căuta pe net.",
            "Am găsit recenzii bune pe YouTube pentru modelul ăsta.",
        ]),
        new("msg-price-reference", false, [
            "Nou costă vreo {priceHigh} lei, eu îl dau la {price}.",
            "La magazin e {priceHigh} lei, al meu e ca nou.",
            "Am văzut că nou e {priceHigh}, e un preț bun.",
        ]),
        new("msg-ok-short", false, [
            "Ok.",
            "Bine.",
            "Da.",
            "Super!",
            "Perfect.",
            "Ok, mersi.",
        ]),
        new("msg-wait-answer", false, [
            "Revin cu un răspuns diseară.",
            "Mă gândesc și îți zic.",
            "Te anunț până {day}.",
            "Vorbesc cu colegul de cameră și îți spun.",
        ]),
        new("msg-urgent-legit", false, [
            "Am nevoie de el urgent pentru laboratorul de {day}.",
            "Îmi trebuie până la examen, {day}.",
            "Poți azi? Am colocviu {day}.",
        ]),
        new("msg-money-split", false, [
            "Îl luăm împreună cu un coleg, plătim jumătate fiecare.",
            "Am doar {price} lei cash acum, restul ți-l dau pe Revolut.",
            "Îți dau {price} acum și restul când primesc bursa.",
        ]),
        new("msg-photo-problem", false, [
            "Nu se văd pozele la mine, poți să le pui iar?",
            "Poza e cam neclară, mai pune una te rog.",
        ]),
        new("msg-gift-legit", false, [
            "Ți-l dau gratis dacă vii azi după el, mă mut din cămin.",
            "Îl las gratuit, vreau doar să scap de el până vineri.",
        ]),
        // Normal messages with the words scams use too (card, cod, link, avans, curier, transfer), so the
        // model learns the difference from what is asked, not from one word.
        new("msg-card-at-meeting", false, [
            "Pot să plătesc cu cardul când ne vedem? Am Revolut.",
            "N-am cash, merge cu transfer pe loc de pe card?",
            "Accept și card, îți dau IBAN-ul când ești lângă mine și verifici produsul.",
        ]),
        new("msg-door-code", false, [
            "Codul de la interfon e {room}, urci la etajul 2.",
            "Sună la interfon, codul e {room}.",
            "Când ajungi, dă-mi un mesaj, nu merge codul de la ușă.",
        ]),
        new("msg-listing-link", false, [
            "Ți-am trimis linkul la celălalt anunț al meu din aplicație.",
            "E în anunțul meu de aici din aplicație, uită-te la poze.",
            "Am pus link-ul cu specificațiile în descrierea anunțului.",
        ]),
        new("msg-no-advance", false, [
            "Nu cer avans, plătești când îl vezi.",
            "Nu trebuie să plătești nimic înainte, vii și îl încerci.",
            "Banii îi dai doar după ce te uiți la el.",
        ]),
        new("msg-courier-tracking", false, [
            "L-am trimis azi prin Fan Courier, ajunge mâine, plătești la curier.",
            "Coletul a plecat, ai primit SMS de la curier cu ora de livrare?",
            "Curierul te sună înainte să vină, plata e ramburs.",
        ]),
        new("msg-paid-after", false, [
            "Ți-am trimis banii pe Revolut, verifică te rog.",
            "Am făcut transferul de {price} lei, mersi încă o dată!",
            "Banii ar trebui să-ți intre până diseară, am plătit din aplicația băncii.",
        ]),
        new("msg-discount-legit", false, [
            "Dacă iei și cartea, îți fac reducere {percent}% la amândouă.",
            "Îți las mai ieftin dacă vii azi.",
            "Pentru studenții din anul 1 las la {price} lei.",
        ]),
    ];

    public static readonly MessageTemplate[] Spam =
    [
        new("spam-pay-shipping-advance", true, [
            "Sunt în alt oraș, trimite-mi {amount} lei avans și îți trimit coletul azi.",
            "Plata în avans prin transfer, după ce primesc banii trimit produsul.",
            "Ca să ți-l rezerv trebuie să plătești transportul în avans, {amount} lei.",
            "Trimite avansul de {amount} lei pe IBAN-ul meu și pleacă azi cu curierul.",
        ]),
        new("spam-courier-link", true, [
            "Am plătit deja prin serviciul de livrare, intră pe {link} ca să primești banii.",
            "Curierul a confirmat comanda, confirmă și tu aici: {link}",
            "Am făcut plata, banii te așteaptă la {link}, completează datele.",
            "Livrare sigură prin {link}, completează adresa și cardul pentru încasare.",
        ]),
        new("spam-card-data", true, [
            "Ca să îți trimit banii am nevoie de numărul cardului, data expirării și CVV.",
            "Trimite-mi poza cu cardul, față și verso, ca să fac plata.",
            "Dă-mi codul CVV de pe spatele cardului, altfel nu merge transferul.",
            "Completează datele cardului tău ca să primești {amount} lei.",
        ]),
        new("spam-sms-code", true, [
            "Ți-a venit un cod prin SMS, trimite-mi-l să confirm plata.",
            "Banca îți trimite acum un cod, spune-mi-l ca să finalizăm.",
            "Am nevoie de codul de 6 cifre primit pe telefon, e pentru livrare.",
        ]),
        new("spam-overpayment", true, [
            "Ți-am trimis din greșeală {amount} lei în plus, returnează diferența urgent.",
            "Am plătit mai mult, trimite-mi înapoi restul de {amount} lei pe alt cont.",
            "Transferul a intrat dublu, te rog dă-mi înapoi {amount} lei azi.",
        ]),
        new("spam-relative-courier", true, [
            "Sunt plecat în străinătate, verișorul meu vine cu curierul, plătește-l în avans.",
            "Lucrez pe vapor, nu pot să vin, trimite banii la fratele meu și îți trimite produsul.",
            "Sunt la studii afară, plătești curierul meu și ți-l aduce acasă.",
        ]),
        new("spam-easy-money", true, [
            "Câștigă {amount} lei pe zi de acasă, scrie-mi și îți explic metoda!!!",
            "Vrei bani ușor? Am o metodă sigură, 100% garantat.",
            "Fac {amount} lei pe săptămână din telefon, te învăț și pe tine.",
            "BANI RAPIZI fără muncă, întreabă-mă cum!!!",
        ]),
        new("spam-work-from-home", true, [
            "Angajăm studenți, lucru de acasă, {amount} lei pe săptămână, fără experiență!",
            "Job part-time online, 2 ore pe zi, plata zilnic, scrie-mi.",
            "Căutăm colaboratori pentru postat anunțuri, {amount} lei garantat.",
            "Work from home, {amount} euro pe lună, doar un telefon îți trebuie.",
        ]),
        new("spam-crypto", true, [
            "Investește {amount} lei în bitcoin și îi dublezi în 24 de ore.",
            "Am un bot de crypto care face profit garantat, intră în grup.",
            "Platformă nouă de crypto, bonus {percent}% la prima depunere.",
            "Dublăm orice sumă în crypto, profit sigur, scrie-mi.",
        ]),
        new("spam-trading-signals", true, [
            "Semnale de trading gratuite, {percent}% profit pe lună, intră pe canalul meu.",
            "Curs de trading forex, înveți să faci {amount} lei pe zi.",
            "Grupul meu de investiții are profit garantat, locuri limitate!",
        ]),
        new("spam-giveaway", true, [
            "Felicitări, ai câștigat un iPhone! Revendică premiul aici: {link}",
            "Ai fost ales câștigătorul concursului CampusMarket, intră pe {link}",
            "GIVEAWAY!!! Primești o căști gratis dacă distribui pe 10 grupuri.",
            "Ai câștigat un voucher de {amount} lei, plătești doar taxa de livrare.",
        ]),
        new("spam-loan", true, [
            "Credit rapid fără acte și fără verificări, până la {amount} lei.",
            "Împrumut rapid pentru studenți, banii în cont în 10 minute.",
            "Ai nevoie de bani? Îți dau împrumut fără dobândă, scrie-mi.",
        ]),
        new("spam-buy-accounts", true, [
            "Cumpăr conturi de Instagram și TikTok, plătesc bine.",
            "Vând urmăritori și like-uri, 1000 la doar {amount} lei.",
            "Cumpăr conturi de bancă pentru transferuri, {amount} lei pe cont.",
        ]),
        new("spam-channel-promo", true, [
            "Intră pe canalul meu de Telegram {handle}, oferte zilnice!",
            "Dă follow la {handle} pe Instagram pentru reduceri de {percent}%.",
            "Mai multe oferte pe Telegram, caută {handle}.",
            "Abonează-te la {handle}, postez zilnic produse gratis.",
        ]),
        new("spam-betting", true, [
            "Bonus de {amount} lei la pariuri fără depunere, cod promo CAMPUS.",
            "Cazino online cu {percent}% bonus la prima depunere, intră acum.",
            "Pont sigur la pariuri, cotă mare, plătești doar {amount} lei.",
        ]),
        new("spam-pyramid", true, [
            "Intră în echipa mea, aduci 3 prieteni și câștigi {amount} lei.",
            "Afacere de familie, investești {amount} și primești de 5 ori mai mult.",
            "Sistem de recomandări, fiecare om adus îți aduce {amount} lei.",
        ]),
        new("spam-fake-support", true, [
            "Echipa CampusMarket: contul tău va fi suspendat, confirmă datele aici {link}",
            "Suport CampusMarket: verifică-ți contul în 24 de ore la {link}",
            "Anunțul tău a fost blocat, plătește taxa de deblocare la {link}",
        ]),
        new("spam-exam-answers", true, [
            "Vând răspunsurile la examenul de {subject}, {amount} lei, garantat.",
            "Fac diplome și adeverințe, orice facultate, discret.",
            "Am subiectele de la sesiune, plătești în avans {amount} lei.",
        ]),
        new("spam-survey", true, [
            "Completezi un sondaj de 5 minute și primești {amount} lei pe card.",
            "Câștigi bani răspunzând la chestionare, intră pe {link}",
        ]),
        new("spam-english-job", true, [
            "Hello, easy online job, earn {amount}$ per day, contact me on WhatsApp.",
            "Dear friend, I have a business offer for you, guaranteed profit.",
            "Earn money fast from home, no experience, message me now!!!",
        ]),
        new("spam-gift-card", true, [
            "Plătesc doar cu carduri cadou Google Play, îmi trimiți codurile.",
            "Cumpără un card cadou de {amount} lei și trimite-mi codul ca avans.",
        ]),
        new("spam-obfuscated-link", true, [
            "Intră pe bani-rapizi punct ro și te înscrii.",
            "Caută pe net oferte-campus punct com, acolo e mai ieftin.",
            "Scrie în browser castig zilnic punct net, e gratis.",
        ]),
        new("spam-short-link", true, [
            "Oferta completă aici: bit.ly/{code}",
            "Mai multe produse la jumătate de preț: tinyurl.com/{code}",
            "Vezi aici, reducere {percent}%: cutt.ly/{code}",
        ]),
        new("spam-referral-app", true, [
            "Descarcă aplicația cu codul meu și primești {amount} lei bonus.",
            "Înscrie-te cu link-ul meu și câștigăm amândoi {amount} lei.",
            "Folosește codul REF{code} și primești bani pe loc.",
        ]),
        new("spam-fake-escrow", true, [
            "Hai să facem prin serviciul de plată sigură, îți trimit link-ul, completezi cardul.",
            "Folosesc doar plata protejată de pe {link}, acolo îți intră banii.",
            "Prin aplicație nu merge, folosim {link} pentru garanția plății.",
        ]),
        new("spam-too-good", true, [
            "Am {amount} de laptopuri noi la doar 200 lei bucata, plata înainte.",
            "iPhone nou sigilat la 300 lei, doar cu plata în avans.",
            "Vând lot de telefoane la preț de nimic, plătești acum și le primești mâine.",
        ]),
        new("spam-fake-bank-call", true, [
            "Te sună cineva de la bancă să confirme plata, spune-i codul primit.",
            "Banca are nevoie de datele tale de logare ca să deblocheze transferul.",
            "Ca să primești banii, intră în aplicația băncii și aprobă cererea de {amount} lei.",
        ]),
        new("spam-parcel-insurance", true, [
            "Pentru livrare trebuie să plătești asigurarea coletului, {amount} lei, înainte.",
            "Curierul cere o taxă de vamă de {amount} lei, plătește-o pe link: {link}",
            "Coletul e blocat, plătește taxa de eliberare de {amount} lei azi.",
        ]),
        new("spam-buy-everything", true, [
            "Cumpăr orice ai de vânzare, plătesc dublu, dar doar prin link-ul meu.",
            "Îți cumpăr tot lotul, trimit eu curierul și plătesc prin {link}",
            "Iau produsul fără să-l văd, îți trimit acum banii, dă-mi datele cardului.",
        ]),
    ];

    public static readonly string[] Items =
    [
        "manualul de analiză", "calculatorul Casio", "laptopul", "notițele de POO", "halatul de laborator",
        "bicicleta", "căștile", "lampa de birou", "scaunul", "breadboard-ul", "atlasul de anatomie",
        "tableta", "monitorul", "imprimanta", "fierbătorul", "cartea de algebră", "mouse-ul", "tastatura",
        "rucsacul", "ghiozdanul", "trusa de disecție", "stetoscopul", "Arduino-ul", "culegerea de probleme",
        "dicționarul", "cursurile de anul trecut", "microscopul", "rigla de calcul", "boxa", "frigiderul mic",
    ];

    public static readonly string[] Places =
    [
        "în fața la Corp A", "la intrarea în facultate", "la biblioteca centrală", "la cantină",
        "la stația de tramvai", "în Piața Unirii", "în fața căminului", "la Copou", "la Mall",
        "lângă Rectorat", "la Corp C", "în parcul de lângă cămin", "la intrarea în Tudor",
    ];

    public static readonly string[] Times =
    [
        "la 5", "la 14:30", "după curs", "seara pe la 8", "dimineață", "la prânz", "pe la 6", "după 4",
    ];

    public static readonly string[] Days =
    [
        "luni", "marți", "miercuri", "joi", "vineri", "mâine", "poimâine", "în weekend", "sâmbătă",
    ];

    public static readonly string[] Subjects =
    [
        "analiză", "algebră", "POO", "anatomie", "fizică", "chimie", "statistică", "baze de date", "economie",
    ];

    public static readonly string[] Faculties =
    [
        "Informatică", "Medicină", "Drept", "FEAA", "Litere", "Chimie", "Fizică", "Politehnică",
    ];

    public static readonly string[] Names =
    [
        "ana", "andrei", "maria", "vlad", "ioana", "radu", "elena", "mihai", "cristina", "alex",
    ];

    public static readonly string[] LinkWords =
    [
        "plata", "livrare", "curier", "bonus", "premiu", "castig", "oferta", "confirmare", "suport", "cont",
    ];

    public static readonly string[] LinkTlds = ["com", "ro", "net", "xyz", "info", "online", "site"];

    public static readonly string[] HandleWords =
    [
        "oferte", "reduceri", "campus", "deals", "student", "promo", "bani", "gratis",
    ];
}

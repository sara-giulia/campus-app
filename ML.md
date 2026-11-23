# ML în CampusMarket — index

Pagina adună într-un singur loc modelele ML din proiect, cifrele lor și limitele lor.
Detaliile (alegerea modelului, analiza erorilor, cuvintele importante) sunt în
fișele fiecărui model din `docs/ml/`. Toate cifrele de mai jos sunt copiate din
`ml/models/*.json` (metricile pe test, scrise de programul de antrenare) și din
`docs/ml/*.md` / `README.md` (baseline-uri și setul extern). Unde o valoare nu
există în repo, scrie „lipsește”.

## Modelele

Toate patru sunt antrenate cu ML.NET pe `ml/data/listings.csv` (date sintetice,
generate, dataset v2: 5000 de rânduri, 10% spam, seed 2026) și sunt salvate ca
`.zip` în `ml/models/`, lângă un fișier `.json` cu metadatele și metricile pe test.

- **Preț** (`price-regression.zip`) — sugerează un preț în lei din titlu,
  descriere, categorie și stare. FastTree pe `log1p(Price)`, ales din șase
  candidați după MAE pe validare. Doar pe anunțuri care nu sunt spam. Este doar o
  sugestie, vânzătorul alege prețul. Fișa: [docs/ml/price.md](docs/ml/price.md).
- **Categorie** (`category-classification.zip`) — sugerează una din cele cinci
  categorii (`Electronics`, `LabEquipment`, `Notes`, `Other`, `Textbook`) din
  titlu și descriere, cu o încredere. SDCA maximum entropy. Sub încrederea minimă
  0.60 (aleasă pe validare) API-ul nu dă nicio sugestie. Fișa:
  [docs/ml/category.md](docs/ml/category.md).
- **Spam** (`spam-detection.zip`) — dă probabilitatea ca un anunț să fie spam din
  titlu, descriere și preț. SDCA logistic regression, prag 0.40 (ales pe
  validare). Un anunț peste prag rămâne publicat, dar primește un raport automat
  pentru Admin. În API, peste model mai sunt și reguli fixe (`SpamRules.cs`).
  Fișa: [docs/ml/spam.md](docs/ml/spam.md).
- **Anunțuri similare** (`similar-listings.zip`) — transformă titlul și
  descrierea în vectori de text și întoarce cele 5 anunțuri active cele mai
  apropiate (similaritate cosinus). Nu prezice o etichetă; varianta „titlu și
  descriere” a fost aleasă pe validare. Fișa:
  [docs/ml/similar.md](docs/ml/similar.md).

Alte modele în repo: nu există; `ml/models/` conține doar cele patru `.zip`.

## Metrici

Test = partea de test (15%, grupată după `TemplateId`, 711 rânduri; preț,
categorie și similare folosesc doar cele 693 de rânduri non-spam). Extern =
`ml/data/external.csv` (122 de rânduri, 102 non-spam, 20 spam), nefolosit
niciodată la antrenare sau la alegerea pragurilor.

| Model | Metrică | Test (model) | Test (baseline) | Extern (model) | Extern (baseline) | Baseline-ul de bătut |
|---|---|---|---|---|---|---|
| Preț | R² | 0.451 | 0.274 | 0.239 | 0.232 | media prețului pe `Category × Condition` din train |
| Preț | MAE (lei) | 268.53 | 345.44 | 174.27 | 200.77 | idem |
| Preț | RMSE (lei) | 606.21 | 697.05 | lipsește | lipsește | idem |
| Preț | MAPE | 75.3% | 176.5% | 79.3% | 132.7% | idem |
| Categorie | Micro accuracy | 0.853 | 0.281 / 0.519 | 0.618 | 0.196 / lipsește | cea mai frecventă clasă / regulă cu cuvinte cheie |
| Categorie | Macro accuracy | 0.856 | 0.200 / 0.484 | 0.616 | 0.200 / lipsește | idem |
| Categorie | Macro-F1 | 0.864 | lipsește | lipsește | lipsește | — |
| Spam (prag 0.40) | Precision | 1.000 | 0.667 | 0.909 | 0.765 | regulă: telefon, link, whatsapp/telegram, `!!!`, preț ≤ 5 lei |
| Spam (prag 0.40) | Recall | 1.000 | 1.000 | 0.500 | 0.650 | idem |
| Spam (prag 0.40) | F1 | 1.000 | 0.800 | 0.645 | 0.703 | idem |
| Spam | PR-AUC | 1.000 | — | 0.852 | — | — |
| Similare | Precision@5 | 0.668 | 0.202 | lipsește | lipsește | 5 anunțuri alese la întâmplare |

Note:

- La pragul 0.50, spam pe extern are precision 1.000, recall 0.500, F1 0.667
  (`docs/ml/spam.md`); tabelul extern din `README.md` folosește aceste valori.
- Baseline-ul de preț pe test apare cu valori puțin diferite în documente:
  `README.md` și tabelul extern din `docs/ml/price.md` dau MAE 345.44 / MAPE
  176.5%, iar tabelul „Final metrics” din `docs/ml/price.md` dau MAE 346.41 /
  RMSE 695.09 / MAPE 179.6%. Mai sus sunt valorile din `README.md`.
- Macro accuracy la categorie: `category-classification.json` dă 0.856, tabelul
  „Baselines” din `README.md` dă 0.858. Mai sus este valoarea din `.json`.
- Modelul de anunțuri similare nu este evaluat pe setul extern
  (`ExternalEvaluation.cs` încarcă doar modelele de preț, categorie și spam).

## Împărțirea datelor

- **70 / 15 / 15** train / validare / test, în `DatasetSplit.cs`, comun pentru
  antrenare și pentru testele de calitate: o primă tăietură scoate 30%, a doua
  împarte acel 30% în două. Seed 2026 (a doua tăietură folosește 2027).
- **Grupat după `TemplateId`** (`samplingKeyColumnName`): toate rândurile unui tip
  de produs ajung într-o singură parte, deci testul conține doar tipuri de produs
  pe care modelul nu le-a văzut.
- **Spam grupat după tipar:** spamul „zgomotos” are `TemplateId = spam-00` …
  `spam-11`, câte unul pentru fiecare din cele 12 texte de descriere din
  `Spam.cs`, deci un tipar întreg ajunge într-o singură parte. Spamul deghizat
  (o treime) primește `TemplateId`-ul produsului imitat, nu al tiparului.
- Validarea servește doar la alegere (modelul de preț, pragul de spam, încrederea
  minimă la categorie, varianta la similare); testul se măsoară o singură dată.
  Pentru preț, câștigătorul este reantrenat pe train + validare și mai are și o
  validare încrucișată grupată în 5 fold-uri.

## Antrenare și evaluare

Din rădăcina repo-ului:

```
dotnet run --project ml/CampusMarket.ML.DatasetGenerator
dotnet run --project ml/CampusMarket.ML.Training
dotnet test tests/CampusMarket.ML.Tests
```

1. Generatorul rescrie `ml/data/listings.csv` (aceiași octeți la fiecare rulare).
2. Antrenarea scrie cele patru `.zip` și `.json` în `ml/models/` și afișează
   metricile, baseline-urile, tabelele de praguri, evaluarea pe `external.csv` și
   analiza erorilor.
3. Testele de calitate pică dacă modelele salvate scad sub pragurile din fișe.

În CI (`.github/workflows/ml.yml`) se rulează și `python3 ml/scripts/check_metrics.py`,
care compară `ml/models/*.json` noi cu cele de referință și pică dacă R², accuracy
sau F1 scad cu mai mult de 0.02 sau dacă MAE crește cu mai mult de 5%.

## Limite

- **Toate:** sunt antrenate pe date generate dintr-un catalog scris de mână
  (~300 de tipuri de produs), doar în română. Pe setul extern, categoria și spamul scad
  mult față de test, iar la preț R² scade (MAE e mai mic, MAPE puțin mai mare). Sunt
  sugestii sau semnale pentru un om, nu decizii automate.
- **Preț:** pe extern R² (0.239) e aproape de baseline (0.232); subestimează
  produsele scumpe din coada Electronics (MAE pe Electronics pe test: 718.14 lei).
  Un produs departe de catalog poate primi un preț foarte greșit.
- **Categorie:** citește aproape numai titlul. Pe extern greșește la materiale de
  pregătire pentru examene („grile”, „materie”, „sinteze”, cuvinte care nu apar în
  date) și trimite spre `Textbook` orice titlu cu cratimă („X - Y”). Chiar peste
  0.60, cam 1 sugestie din 10 e greșită pe test și 1 din 5 pe extern.
- **Spam:** prinde spamul care mută discuția pe Telegram/WhatsApp sau dă un link;
  ratează frauda prin plată (avans, PayPal Friends & Family, crypto). Pragul 0.40
  a fost ales pe o validare saturată și pe extern semnalează un anunț bun
  (bocanci, probabilitate 0.42). Cuvinte foarte comune („pe”, „cu”) au greutăți
  mari.
- **Anunțuri similare:** compară cuvinte, nu sens; ignoră prețul și starea; cam o
  sugestie din trei e din altă categorie; calculează vectorii pentru toate anunțurile
  active la fiecare cerere. Nu are evaluare pe setul extern.

## De ce spam are 1.0 pe test, dar recall 0.50 pe extern

- **Spamul generat are puține tipare.** `Spam.cs` are 12 titluri ALL CAPS, 10
  titluri normale și doar **12 texte de descriere**, din care se aleg toate
  rândurile de spam (și cele zgomotoase, și cele deghizate). Fiecare text conține
  un semnal de contact: WhatsApp, Telegram, „link in bio”, un număr de telefon sau
  `!!!`. Modelul învață exact aceste semnale (cele mai puternice cuvinte: „pe”,
  „telegram”, „link”, „suna”, „pe telegram”).
- **Testul nu are un tipar cu adevărat nou.** Gruparea după `spam-00` … `spam-11`
  ține spamul zgomotos al unui tipar într-o singură parte, dar spamul deghizat
  folosește aceleași 12 descrieri și e grupat după produs, nu după tipar, așa că
  textele pot apărea și în train. Pe test sunt 18 rânduri spam; toate sunt prinse,
  fără niciun anunț bun semnalat (TP 18, FP 0, FN 0). Validarea e la fel de
  saturată: orice prag între 0.15 și 0.65 dă F1 1.000.
- **Setul extern e scris de echipă după anunțuri reale.** Are 20 de rânduri spam,
  dintre care 10 subtile: titlu și preț normale, fără cuvinte de înșelătorie.
  Modelul le prinde pe cele 10 zgomotoase și le ratează pe cele 10 subtile
  (TP 10, FN 10 → recall 0.500). Cele ratate sunt fraude prin plată (avans prin
  transfer, PayPal Friends & Family, crypto, conturi Netflix), un preț absurd fără
  text, un telefon mascat („07xx xxx xxx”) sau „!!!” lipit de un cuvânt rar —
  tipuri de text pe care generatorul nu le-a produs niciodată.
- **Concluzie:** 1.0 pe test arată doar că modelul recunoaște tiparele
  generatorului; 0.50 pe extern e cifra cinstită. Pentru a o crește trebuie
  adăugat spam de tip fraudă prin plată în generator (sau anunțuri reale
  raportate) și ales din nou pragul pe date care nu sunt saturate. Până atunci,
  regulile din `SpamRules.cs` rămân lângă model în API.

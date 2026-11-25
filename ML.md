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
  descriere, categorie și stare. LightGbm pe `log1p(Price)` cu trăsăturile Text (marcă, stocare, procesor, nou/sigilat, din `PriceTextFeatures`), ales din 12
  candidați (2 seturi de trăsături × 3 algoritmi × 2 ținte) după MAE pe validare. Câștigul față de setul Current este mic pe test. Doar pe anunțuri care nu sunt spam. Este doar o
  sugestie, vânzătorul alege prețul. Fișa: [docs/ml/price.md](docs/ml/price.md).
- **Categorie** (`category-classification.zip`) — sugerează una din cele cinci
  categorii (`Electronics`, `LabEquipment`, `Notes`, `Other`, `Textbook`) din
  titlu și descriere, cu o încredere. SDCA maximum entropy. Sub încrederea minimă
  0.60 (aleasă pe validare) API-ul nu dă nicio sugestie. Fișa:
  [docs/ml/category.md](docs/ml/category.md).
- **Spam** (`spam-detection.zip`) — dă probabilitatea ca un anunț să fie spam din
  titlu, descriere și preț. SDCA logistic regression, prag 0.35 (ales pe
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

Test = partea de test (15%, grupată după `TemplateId`). Numărul de rânduri
diferă pe model, după `dataset.rowCountTest` din `ml/models/*.json`: preț,
categorie și similare au fost antrenate pe setul dinainte de cele 12 tipare noi
de spam (711 rânduri de test, din care folosesc doar cele 693 non-spam); spam a
fost reantrenat pe setul nou (780 de rânduri de test, 87 spam). Extern =
`ml/data/external.csv` (122 de rânduri, 102 non-spam, 20 spam), nefolosit
niciodată la antrenare sau la alegerea pragurilor.

| Model | Metrică | Test (model) | Test (baseline) | Extern (model) | Extern (baseline) | Baseline-ul de bătut |
|---|---|---|---|---|---|---|
| Preț | R² | 0.480 | 0.274 | 0.312 | 0.232 | media prețului pe `Category × Condition` din train |
| Preț | MAE (lei) | 267.44 | 345.44 | 159.74 | 200.77 | idem |
| Preț | RMSE (lei) | 589.85 | 697.05 | lipsește | lipsește | idem |
| Preț | MAPE | 76.4% | 176.5% | 68.7% | 132.7% | idem |
| Categorie | Micro accuracy | 0.853 | 0.281 / 0.519 | 0.618 | 0.196 / lipsește | cea mai frecventă clasă / regulă cu cuvinte cheie |
| Categorie | Macro accuracy | 0.856 | 0.200 / 0.484 | 0.616 | 0.200 / lipsește | idem |
| Categorie | Macro-F1 | 0.864 | lipsește | lipsește | lipsește | — |
| Spam (prag 0.35) | Precision | 1.000 | 0.890 | 0.680 (la 0.50) | 0.765 | regulă: telefon, link, whatsapp/telegram, `!!!`, preț ≤ 5 lei |
| Spam (prag 0.35) | Recall | 0.747 | 0.839 | 0.850 (la 0.50) | 0.650 | idem |
| Spam (prag 0.35) | F1 | 0.855 | 0.864 | 0.756 (la 0.50) | 0.703 | idem |
| Spam | PR-AUC | 0.975 | — | lipsește | — | — |
| Similare | Precision@5 | 0.668 | 0.202 | lipsește | lipsește | 5 anunțuri alese la întâmplare |

Note:

- Spam pe extern: programul de antrenare afișează precision, recall și F1 doar la
  pragul 0.50 (coloana „Extern (model)”). La pragul API-ului, 0.35, pe extern sunt
  **10 anunțuri bune semnalate** și 3 spam ratate (`docs/ml/spam.md`). Pe test, la
  0.50: precision 1.000, recall 0.701, F1 0.824.
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
- **Spam:** pe extern, la pragul 0.35, semnalează **10 din cele 102 anunțuri bune**
  (bocanci și un stetoscop la 0.91) și ratează 3 spam. Cele mai puternice 10 cuvinte
  spre spam sunt generice („pe”, „in”, „e”, „pret”, „lei”, „raspund”, „doar”), deci
  modelul învață și stilul datelor generate, nu doar semnalele de contact.
- **Anunțuri similare:** compară cuvinte, nu sens; ignoră prețul și starea; cam o
  sugestie din trei e din altă categorie; calculează vectorii pentru toate anunțurile
  active la fiecare cerere. Nu are evaluare pe setul extern.

## De ce spam avea 1.0 pe test și ce s-a schimbat

- **Înainte, testul nu avea un tipar cu adevărat nou.** Tot spamul era ales din doar
  12 texte de descriere (`spam-00` … `spam-11`). Spamul zgomotos era grupat după
  tipar, dar spamul deghizat era grupat după produsul imitat, așa că aceleași texte
  ajungeau și în train, și în test. Pe test erau 18 rânduri spam, toate prinse
  (TP 18, FP 0, FN 0), iar validarea era saturată (orice prag între 0.15 și 0.65 dădea
  F1 1.000). 1.0 arăta doar că modelul recunoaște textele pe care le văzuse deja.
- **Acum:** `Spam.cs` are **12 tipare noi** (`spam-12` … `spam-23`: giveaway fals,
  giveaway cu taxă, dublare crypto, semnale forex, telefon ascuns, cont pe altă
  aplicație, preț prea bun, link scurt, ofertă și job în română amestecată cu engleză,
  recrutare falsă, schemă piramidală), folosite de cam jumătate din rândurile de spam.
  Fiecare rând de spam, zgomotos sau deghizat, are tiparul ca `TemplateId`, iar
  `DatasetSplit` amestecă cele 24 de tipare și le taie 70 / 15 / 15 după număr, deci
  validarea și testul au doar tipare întregi pe care modelul nu le-a văzut. Rândurile
  non-spam au rămas identice.
- **Cifrele cinstite:** pe test (780 de rânduri, 87 spam), la pragul 0.35: precision
  1.000, recall 0.747, F1 0.855 (FP 0, FN 22); AUC 0.996. Pragul 0.35 e mijlocul
  pragurilor 0.25 … 0.50, care au toate cel mai bun F1 pe validare (0.959). Pe extern
  recall-ul a urcat de la 0.500 la 0.850 (la 0.50), dar anunțurile bune semnalate au
  crescut de la 1 la 10 (la pragul API-ului).
- **Concluzie:** spamul nou e prins mai bine, dar modelul semnalează prea multe
  anunțuri bune pe text real și se bazează pe cuvinte generice. Pentru a-l folosi pe
  trafic real trebuie adăugate anunțuri reale (bune și raportate) și ales din nou
  pragul pe ele. Până atunci, regulile din `SpamRules.cs` rămân lângă model în API.

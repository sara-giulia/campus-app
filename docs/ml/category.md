# Category suggestion model — model card

The category model suggests one of the five CampusMarket categories for a listing
from its title and description, with a confidence. It is only a suggestion: the
seller still picks the category. The API exposes it as `POST
/api/v1/listings/category-suggestion` (`{ category, confidence }`); the saved
model lives at `ml/models/category-classification.zip`.

## What the model does

- **Task:** multiclass classification into `Electronics`, `LabEquipment`, `Notes`,
  `Other` and `Textbook`, on non-spam rows only (spam has a category, but its text
  has nothing to do with it).
- **Features:** `Title` and `Description`, each through ML.NET's `FeaturizeText`
  (lower case, diacritics removed, words and pairs of words with their punctuation,
  groups of three letters, normalised), concatenated.
- **Trainer:** SDCA maximum entropy (`SdcaMaximumEntropy`), seed 2026,
  `NumberOfThreads = 1` so two runs give the same model.
- **Output:** the predicted category and one probability per category. The API
  returns the highest probability, rounded to two decimals, as the confidence, and
  returns `category: null` when it is below `Ml:Category:MinConfidence` (0.60).

## Data and split

- `ml/data/listings.csv`, dataset v2: 5000 generated student listings (see
  `ml/data/README.md`), 10% spam. Titles and descriptions imitate a real
  marketplace: stripped diacritics, typos, random capitals, emojis, empty or very
  long descriptions, and **~3% of the non-spam rows carry the wrong category on
  purpose** (the seller picked the wrong dropdown).
- 70 / 15 / 15 split **grouped by `TemplateId`**: every item type is in exactly one
  of train, validation and test, so the test part only has item types the model
  has never seen. Validation is used only to choose the minimum confidence; test is
  measured once.
- `ml/data/external.csv`: 122 listings written after real listings (102 non-spam),
  never used for training or for choosing anything.

## Metrics

Test part (693 non-spam rows) and external set (102 non-spam rows):

| Metric | Test (model) | Test (most frequent class) | Test (keyword rule) | External (model) | External (most frequent class) |
|---|---|---|---|---|---|
| Micro accuracy | **0.853** | 0.281 | 0.519 | **0.618** | 0.196 |
| Macro accuracy | **0.856** | 0.200 | 0.484 | **0.616** | 0.200 |
| Macro-F1 | **0.864** | | | | |
| Log loss | 0.473 | | | | |
| Calibration error (ECE) | 0.036 | | | | |

Per class on the test part:

| Class | Support | Precision | Recall | F1 |
|---|---|---|---|---|
| Electronics | 155 | 0.841 | 0.852 | 0.846 |
| LabEquipment | 131 | 0.899 | 0.748 | 0.817 |
| Notes | 64 | 0.935 | 0.906 | 0.921 |
| Other | 195 | 0.756 | 0.856 | 0.803 |
| Textbook | 148 | 0.944 | 0.919 | 0.932 |

Most confusions go to and from `Other` (27 LabEquipment and 20 Electronics rows end
up there). The confusion matrix, the calibration table and how they are computed
are in the README ("Classification report").

## Minimum confidence

Chosen on validation by `Evaluation/ThresholdSelection.cs`: the smallest threshold
(0.30 to 0.95, step 0.05) at which the suggestions still shown are right at least
90% of the time. Result: **0.60**, configured as `Ml:Category:MinConfidence`.

| Where | Listings that get a suggestion | Suggestions that are right |
|---|---|---|
| Validation | 85.1% | 90.3% |
| Test | 85.6% | 91.1% |
| External | 64.7% | 80.3% |

On the external set the model is less sure (fewer suggestions) and wrong more often
when it is sure: real listings are written differently from the generated ones
(see the error analysis).

## Which words matter

The words with the biggest weight per category (`Evaluation/FeatureWeights.cs`,
printed by the training program; T = title, D = description):

| Category | Strongest words |
|---|---|
| Electronics | canon, sony, 1tb, mouse, asus, laser, wacom, casio, bac, watch (T) |
| LabEquipment | breadboard, buc, medicina, kit, letcon, sursa, stetoscop, sonda, balanta, pipeta (T) |
| Notes | fise, notite, rezumat, fsie, fsie securitate, scrise, si, mana, bd, scrise de (T) |
| Other | ski, haltere, abonament, philips, microunde, sah, raft, copii (T), toate (D), set (T) |
| Textbook | - (T), carte, editia, manual (T), contine (D), vol., culegere (T), cu rezumat, ✨, explicatii (D) |

The model decides almost only from the **title**. The strongest Textbook "word" is
the dash: 32% of the generated textbook titles are "Title - Author" against under 1%
in the other categories.

## Error analysis

`Evaluation/ErrorAnalysis.cs` lists the wrong predictions made with the **highest
confidence**: those are the ones the API really shows. The training program prints
them for the test part and the external set after every retrain.

### Test part: mostly wrong labels, not wrong predictions

| Title | Label | Predicted | Confidence | What happened |
|---|---|---|---|---|
| fIZICA ELeCTrOmAGNETISM, MaNuaL cOmplEt | Electronics | Textbook | 1.00 | wrong label: a physics textbook ("manual") |
| KIT ARDUINO MEGA, pentru robotica | Textbook | LabEquipment | 1.00 | wrong label: the same kit is `LabEquipment` 11 times out of 12 in the data |
| Notite curs Testare software, UB | Other | Notes | 0.98 | wrong label: course notes |
| NOTITE DREPT PENAL, partea generala | LabEquipment | Notes | 0.95 | wrong label |
| Coarda sarit Everalst 1910 Classic | LabEquipment | Other | 0.94 | wrong label: a skipping rope is `Other` 5 times out of 6 |
| Muzicuta Hohner Special 20, C major | LabEquipment | Other | 0.94 | wrong label: a harmonica is `Other` 4 times out of 5 |
| Notite curs Machine Learning, UB | LabEquipment | Notes | 0.92 | wrong label |
| Acer Aspire 5, i5 gen 11, 8GB | Other | Electronics | 0.91 | wrong label: a laptop |
| Manual baze de date, Oracle si SQL Server | Notes | Textbook | 0.99 | "manual" is a strong Textbook word; a person would also read it as a textbook |
| Carte VHDL si FPGA, Pedroni | Notes | Textbook | 0.99 | same with "carte" (book) and an author name |
| BAZE DE DATE RELATIONALE, manual an II | Notes | Textbook | 0.96 | same with "manual" |
| Set LED 5mm, verde albastru rosu alb | LabEquipment | Other | 0.98 | **real mistake**: no lab word, and "set" pushes towards `Other` |
| Manusi fara pudra, nitril, marimea M | LabEquipment | Other | 0.95 | **real mistake**: a lab consumable described like clothing |

Eight of the thirteen are the deliberate wrong labels of dataset v2: the model is
right and the test metric counts it as wrong, so the real accuracy on the test part
is somewhat higher than 0.853. The three "manual / carte" rows labelled `Notes` sit
on the border between two categories. The real mistakes are small lab consumables
(LEDs, gloves) whose titles have none of the lab words the model learned.

### External set: real listings are written differently

| Title | Label | Predicted | Confidence | What happened |
|---|---|---|---|---|
| Set grile rezolvate admitere medicina CHIMIE | Notes | Textbook | 0.97 | "grile" does not occur in the training data |
| Carte sinteze istorie bac | Notes | Textbook | 0.91 | "carte" says Textbook; "sinteze" never seen |
| carte grile admitere medicina Brasov | Notes | Textbook | 0.90 | same |
| Materie BAC istorie-esentialul | Notes | Electronics | 0.75 | "materie" never seen; "bac" is an Electronics word in the data |
| materie bacalaureat geografie | Notes | Other | 0.72 | no known word at all |
| Laptop Lenovo Ideapad 320 | Electronics | Other | 0.96 | "laptop" is also in 27 `Other` accessory titles (stands, bags), and the description has only generic words ("e ca nou, il vand deoarece nu il folosesc") |
| Calculator Texas Instruments TI-84 Plus | LabEquipment | Electronics | 0.95 | arguable label: calculators (Casio) are `Electronics` in the data |
| Casti SONY WF-1000XM6 - sigilate | Electronics | Textbook | 0.65 | the dash in the title, the Textbook artefact |
| Bilet concert Subcarpati - Iasi | Other | Textbook | 0.60 | the dash again |
| Router Wireless Gigabit TP-Link Archer C3200 | Electronics | Other | 0.63 | only 4 routers in the training data |
| eBook Reader Amazon Kindle 6.8 inch | Electronics | Other | 0.60 | the description lists accessories (husă, geantă, pop socket) |
| Gramatica limbii germane | Textbook | Electronics | 0.69 | no textbook word ("manual", "carte") in the title |

The biggest gap is **Notes**: in the generated data notes are "fise / notite /
rezumat", while real students sell "grile", "materie", "sinteze" for the bac or the
admission exam, words that occur zero times in `listings.csv`. The second is the
dash artefact, which sends any "X - Y" title towards Textbook.

## Limits — when not to trust the suggestion

- Only the five categories exist; anything else (clothes, tickets, furniture) is
  pushed into the closest one, usually `Other`.
- The model was trained on generated listings. It reads titles, not meaning:
  exam-preparation materials, brands or products it has not seen, and titles with a
  dash are where it is wrong while sounding sure.
- A confidence below 0.60 is hidden on purpose; even above it, about 1 suggestion in
  10 is wrong on test and 1 in 5 on real-looking listings.
- Do not use it to move or reject listings automatically: it is a pre-filled field
  the seller confirms.
- Before using it on real traffic: retrain with real listings, add exam-preparation
  vocabulary to Notes, and remove the "Title - Author" pattern from the generator.

## Quality gate

`tests/CampusMarket.ML.Tests` fails the build when, on the test part, the micro
accuracy drops below 0.8, the macro-F1 below 0.8 or the ECE goes above 0.1, when
three listings written by hand get the wrong category, when the committed model
does not have ten positive words for each category, or when `appsettings.json` and
the model's `.json` disagree on the minimum confidence.

## Reproducing the metrics

```
dotnet run --project ml/CampusMarket.ML.Training
dotnet test tests/CampusMarket.ML.Tests
```

The training program prints, for this model: the metrics, the classification
report, the minimum-confidence table, the strongest words and, at the end, the
error analysis on the test part and the external set.

# Spam detection model — model card

The spam model gives the probability that a listing is spam from its title,
description and price. When a seller publishes a listing, `PublishListing` asks
the model; a listing at or above `Ml:Spam:Threshold` (0.35) is **still published**
but gets an automatic report that an Admin reviews (see Moderation in the README).
The saved model lives at `ml/models/spam-detection.zip`.

## What the model does

- **Task:** binary classification (`IsSpam`) on all rows.
- **Features:** `Title` and `Description` through `FeaturizeText`, and `Price` cut
  into 20 bins (spam often has absurd prices), concatenated.
- **Trainer:** SDCA logistic regression with Platt calibration, seed 2026,
  `NumberOfThreads = 1`.
- **Output:** a probability between 0 and 1. The API decides with the configured
  threshold, not with the model's built-in 0.5.

## Data and split

- `ml/data/listings.csv`, dataset v2: 5000 rows, **10% spam** (500 rows). Two thirds
  of the spam is loud (ALL CAPS titles, often absurd prices); one
  third is disguised as a normal listing in a real category. A few normal listings
  carry a single spam-like word.
- **24 spam patterns.** The 12 original ones (`spam-00` … `spam-11`: WhatsApp /
  Telegram / link in bio, "CASTIGA 500 LEI PE ZI!!!", a phone number) and **12 new
  ones** (`spam-12` … `spam-23` in `Spam.cs`): fake giveaway (follow / share), fake
  giveaway with a fee, crypto doubling, forex signals, an obfuscated phone number, a
  handle on another app, a too-good price, a short link, mixed Romanian / English deal,
  mixed Romanian / English job, fake recruitment and a pyramid "team". About half of the
  spam rows use a new pattern. The new patterns are drawn from a separate `Random`
  (seed 2027), so every non-spam row is byte for byte the same as before.
- **Split fix.** 70 / 15 / 15 grouped by `TemplateId`. Every spam row, loud **and
  disguised**, now has its pattern as `TemplateId`, and `DatasetSplit` shuffles the 24
  patterns with `Random(2026)` and cuts them 70 / 15 / 15 by count, so validation and
  test only hold whole patterns the model never saw in train. Before, disguised spam
  was grouped by the product it imitated, so the same 12 spam texts were in train and
  in test.
- The test part has 780 rows, 87 of them spam. Validation is used only for the
  threshold.
- `ml/data/external.csv`: 122 listings, 20 of them spam written by hand, ten of
  which are subtle (normal title and price, no scam keyword).

## Why the old 1.0 was inflated

With 12 spam texts that every spam row was drawn from, and disguised spam split by
product instead of by pattern, the test part only had spam texts the model had already
seen in train. It scored 1.000 on everything (TP 18, FP 0, FN 0) and validation was
saturated too (every threshold from 0.15 to 0.65 gave F1 1.000), so neither could say
how the model behaves on spam it has not seen. With the split by pattern, the test
part has whole patterns that are not in train, and the numbers drop to what the model
really does on new spam.

## Metrics

Before = the committed model before the 12 new patterns and the split fix (threshold
0.40). After = the current model (threshold 0.35).

| Metric | Test, before (0.40) | Test, after (0.35) | Test, after (0.50) | Test, rule baseline (after) |
|---|---|---|---|---|
| Spam rows on test | 18 of 711 | 87 of 780 | 87 of 780 | 87 of 780 |
| Precision | 1.000 | **1.000** | 1.000 | 0.890 |
| Recall | 1.000 | **0.747** | 0.701 | 0.839 |
| F1 | 1.000 | **0.855** | 0.824 | 0.864 |
| AUC | 1.000 | 0.996 | 0.996 | |
| PR-AUC | 1.000 | 0.975 | 0.975 | |
| Calibration error (ECE) | 0.007 | 0.025 | 0.025 | |
| Counts | TP 18, FP 0, FN 0 | FP 0, FN 22 | TP 61, FP 0, TN 693, FN 26 | |

| External set | Before (0.40) | Before (0.50) | After (0.35) | After (0.50) | Rule baseline |
|---|---|---|---|---|---|
| Precision | 0.909 | 1.000 | | 0.680 | 0.765 |
| Recall | 0.500 | 0.500 | | 0.850 | 0.650 |
| F1 | 0.645 | 0.667 | | 0.756 | 0.703 |
| Good listings flagged (FP) | 1 | 0 | **10** | 8 | |
| Spam let through (FN) | 10 | 10 | **3** | 3 | |

The training program prints the external precision, recall and F1 only at 0.50, and
the error lists (FP and FN) at the API threshold 0.35. The rule baseline
(`Evaluation/Baselines.cs`) flags phone numbers, links, "whatsapp / telegram", "!!!"
and prices up to 5 lei.

The new model catches much more of the external spam (recall 0.500 → 0.850), but it
flags many more good listings there (1 → 10 at the API threshold). On test the rule
baseline has a higher F1 than the model (0.864 vs 0.855 at 0.35).

## Threshold

Chosen on validation by `Evaluation/ThresholdSelection.cs`: the threshold with the
best F1, the balance between spam let through and good listings flagged. Every
threshold from 0.25 to 0.50 gives the best F1 on validation, **0.959** (precision
1.000, recall 0.921, 0 good listings flagged, 6 spam missed); below 0.25 good listings
start to be flagged, above 0.50 more spam is missed. The middle one, **0.35**, is kept
and configured as `Ml:Spam:Threshold`. On test at 0.35: precision 1.000, recall
0.747, F1 0.855 (FP 0, FN 22).

## Which words matter

| Towards spam | Weight | Towards a normal listing | Weight |
|---|---|---|---|
| pe (D) | 8.11 | fara (D) | -3.20 |
| in (D) | 2.92 | perfect (D) | -2.76 |
| intra (D) | 2.66 | include (D) | -2.41 |
| e (D) | 2.66 | cu (D) | -2.17 |
| pe (T) | 2.62 | vine (D) | -2.14 |
| pret (T) | 2.42 | perfect pentru (D) | -1.98 |
| pret (D) | 2.36 | vand (D) | -1.92 |
| lei (D) | 2.24 | vine cu (D) | -1.63 |
| raspund (D) | 1.82 | set (T) | -1.54 |
| doar (D) | 1.80 | zgarieturi (D) | -1.49 |

The price bins have a weight of -0.54. The top 10 words towards spam are now
**generic words** of the generated spam texts, not contact signals. The contact words
still push towards spam, but lower down: "mesaj" is 23rd, "link" (T) 29th,
"whatsapp," 38th and "telegram" 44th. `FeaturizeText` keeps the punctuation glued to
the word, so "whatsapp," and "whatsapp" are different words, and "!!!" never becomes a
word of its own; only the groups of three letters see it. The rule baseline catches it
directly.

## Error analysis

`Evaluation/ErrorAnalysis.cs` lists, at the chosen threshold, the normal listings
flagged as spam (highest probability first) and the spam let through (lowest
probability first).

**Test part, at 0.35:** no good listing is flagged; 22 spam rows are let through. The
ten with the lowest probability (0.03 to 0.12) all have the description "Suna acum
la 07.. pentru bani rapizi!!! Urgent!!!", a phone-number pattern that is not in train.

### External set: ten good listings flagged

| Title | Description (start) | Price | Probability |
|---|---|---|---|
| Bocanci drumetie Salomon marimea 42 | purtati pe 3 trasee, talpa in stare buna | 280 | 0.91 |
| Littmann Cardiology IV stetoscop | Stetoscop Littmann Cardiology IV, l-am folosit de cateva ori in facul… | 500 | 0.91 |
| Curs Facultate SEAA Finanțe Bănci - Operațiunile instituțiilor de cred | Alte cursuri xerox pe care le am la vânzare pentru suma modica de 5 l… | 5 | 0.80 |
| Repere Cronologice Istorie pentru Academia de Politie | Repere Cronologice Istorie pentru Academia de Politie. Materialele su… | 60 | 0.78 |
| Introducere în contabilitate concepte și aplicatii | Introducere în contabilitate concepte și aplicatii, Emil Horomnea. Ca… | 60 | 0.73 |
| TOTUL DESPRE C SI C++ Manual fundamental de programare in c si c++ | TOTUL DESPRE C SI C++ (MANUALUL FUNDAMENTAL DE PROGRAMARE IN C SI C++… | 80 | 0.53 |
| Admitere UMF Cluj | Salut! Eu am sustinut admiterea de anul acesta in cadrul facultatii d… | 22 | 0.53 |
| Kit sutura chirurgicala pentru studenti | Kit noduri si suturi chirurgicale pentru studenti. Silicon care imita… | 150 | 0.52 |
| Sticlarie pentru laborator | Set de sticlarie de laborator folosita, constand in cilindri gradati … | 200 | 0.43 |
| materie bacalaureat geografie | Vând materie pentru bacalaureat Geografie, organizat pe folii transpa… | 50 | 0.39 |

Most of them are ordinary sentences full of "pe", "in" and a price in lei, the
generic words the model now weighs towards spam.

### External set: three spam let through

| Title | Description (start) | Price | Probability |
|---|---|---|---|
| Scaun birou | (empty) | 1 | 0.01 |
| Grile rezolvate licenta | 100% promovare garantata!!! Avem si raspunsurile de la examen inainte… | 99 | 0.07 |
| Kit Arduino complet | Suna-ma la 07xx xxx xxx, am si alte produse. Livrare doar cu plata in… | 60 | 0.12 |

An absurd price with no text, an impossible promise with "!!!" glued to a rare word,
and a masked phone number. The payment-fraud texts that were missed before
(advance transfer, PayPal Friends & Family, crypto) are now caught.

## Rules on top of the model (in the API)

The model is trained on generated listings and misses short, real-world spam ("Spam, click aici www…", "telegram @…", "plata in avans"). `SpamDetector` therefore also flags a listing when a clear rule matches (`Infrastructure/ML/SpamRules.cs`): a link, a Romanian phone number, an `@handle`, WhatsApp / Telegram / "scrie-mi pe", money bait or pay-in-advance phrases, or `!!!`; diacritics are removed and email addresses ignored before comparing. A listing caught only by a rule is reported with probability 0.9. The metrics above are the model's alone; the rules are checked by the integration tests in `Moderation/SpamRulesTests.cs`.

## Limits — when not to trust the probability

- **It flags good listings on real text.** On the external set, at 0.35, it flags
  **10 of the 102 good listings** as spam (hiking boots and a stethoscope at 0.91).
  Validation and test cannot show this: both have 0 good listings flagged.
- **It learns the style of the generated data, not only contact signals.** The top 10
  words towards spam are generic (e, pe, pret, lei, raspund, doar); the contact words
  (mesaj, link, whatsapp, telegram) come only from 23rd place down. An ordinary
  sentence with "pe" and a price in lei can be pushed over the threshold.
- On test it misses about a quarter of the spam from patterns it never saw (recall
  0.747 at 0.35), and the rule baseline has a slightly higher F1 (0.864).
- It reads words as they are written: punctuation glued to a word ("garantata!!!"),
  masked phone numbers ("07xx xxx xxx") and word variants ("suna-ma") slip through.
- It is a signal for a human, not a decision: a flagged listing stays published and
  goes to the Admin's moderation list. Do not use it to hide or delete listings
  automatically.
- Romanian text only, with a little mixed Romanian / English in two spam patterns.
- Before using it on real traffic: add real reported listings and real good listings
  to the data, keep the rule baseline next to the model, and choose the threshold
  again on real data.

## Quality gate

`tests/CampusMarket.ML.Tests` fails the build when, on the test part, the AUC drops
below 0.95, the F1 at the API threshold below 0.84 (it is 0.855) or the PR-AUC below
0.95; when a good test listing is flagged or more than 25 spam rows are let through
at 0.35 (22 are); when four listings written by hand (two spam, two normal) are
classified wrongly; when the committed model does not have ten words on each side,
or "whatsapp,", "telegram", "link" and "mesaj" are not among its 50 words towards spam;
or when `appsettings.json` and the model's `.json` disagree on the threshold.

## Reproducing the metrics

```
dotnet run --project ml/CampusMarket.ML.Training
dotnet test tests/CampusMarket.ML.Tests
```

The training program prints, for this model: the metrics, the classification
report, the threshold table, the strongest words and, at the end, the error analysis
on the test part and the external set. A model retrained on another machine has the
same test metrics, but its probabilities on the external rows can differ by a few
hundredths, so the lists keep the same rows while the last digits move.

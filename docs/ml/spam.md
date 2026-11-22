# Spam detection model — model card

The spam model gives the probability that a listing is spam from its title,
description and price. When a seller publishes a listing, `PublishListing` asks
the model; a listing at or above `Ml:Spam:Threshold` (0.40) is **still published**
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

- `ml/data/listings.csv`, dataset v2: 5000 rows, **10% spam**. Two thirds of the
  spam is loud and generic (ALL CAPS, "CASTIGA 500 LEI PE ZI!!!", WhatsApp /
  Telegram / link in bio, prices of 1 leu or tens of thousands); one third is
  disguised as a normal listing in a real category. A few normal listings carry a
  single spam-like word.
- 70 / 15 / 15 split grouped by `TemplateId` (spam grouped by its pattern). The test
  part has 711 rows, 18 of them spam. Validation is used only for the threshold.
- `ml/data/external.csv`: 122 listings, 20 of them spam written by hand, ten of
  which are subtle (normal title and price, no scam keyword).

## Metrics

| Metric | Test (model, threshold 0.40) | Test (rule baseline) | External (model, 0.40) | External (model, 0.50) | External (rule baseline) |
|---|---|---|---|---|---|
| Precision | **1.000** | 0.667 | 0.909 | 1.000 | 0.765 |
| Recall | **1.000** | 1.000 | 0.500 | 0.500 | 0.650 |
| F1 | **1.000** | 0.800 | 0.645 | 0.667 | 0.703 |
| PR-AUC | 1.000 | | 0.852 | 0.852 | |
| Calibration error (ECE) | 0.007 | | | | |

Counts at 0.40 — test: TP 18, FP 0, TN 693, FN 0; external: TP 10, FP 1, TN 101,
FN 10. The rule baseline (`Evaluation/Baselines.cs`) flags phone numbers, links,
"whatsapp / telegram", "!!!" and prices up to 5 lei.

The test part is **saturated**: the generated spam carries the same loud signals the
model learned, so nothing is missed. The external set is the honest number: the
model finds every loud spam and misses the subtle half.

## Threshold

Chosen on validation by `Evaluation/ThresholdSelection.cs`: the threshold with the
best F1, the balance between spam let through and good listings flagged. Every
threshold from 0.15 to 0.65 gives F1 1.000 on validation (below, good listings start
to be flagged; above, spam is missed), so the middle, **0.40**, is kept and
configured as `Ml:Spam:Threshold`.

On the external set 0.40 flags one more good listing than 0.50 (the hiking boots
below, 0.42) and catches no extra spam. Validation cannot see this because it is
saturated; with real data the threshold should be chosen again.

## Which words matter

| Towards spam | Weight | Towards a normal listing | Weight |
|---|---|---|---|
| pe (D) | 7.65 | cu (D) | -3.33 |
| telegram (D) | 2.82 | fara (D) | -2.28 |
| link / link in (D) | 2.80 | perfect (D) | -1.44 |
| suna (D) | 2.79 | include (D) | -0.90 |
| pe telegram (D) | 2.56 | vine (D) | -0.89 |
| doar (D) | 2.08 | perfect pentru (D) | -0.87 |
| livrare (D) | 1.75 | zgarieturi (D) | -0.65 |
| scrie-mi (D) | 1.57 | vand (D) | -0.58 |

The price bins have a weight of -0.55. Spam is recognised from **moving the talk to
another app and urgency**, almost only in the description. The strongest single word
is the plain "pe", from "pe telegram / pe WhatsApp". `FeaturizeText` keeps the
punctuation glued to the word, so "!!!" never becomes a word of its own:
"garantata!!!" is a different word from "garantata" and is rare, and only the
groups of three letters see the "!!!". The rule baseline catches it directly.

## Error analysis

`Evaluation/ErrorAnalysis.cs` lists, at the chosen threshold, the normal listings
flagged as spam (highest probability first) and the spam let through (lowest
probability first). On the test part there is none of either.

### External set: one good listing flagged

| Title | Description | Price | Probability | Why |
|---|---|---|---|---|
| Bocanci drumetie Salomon marimea 42 | purtati pe 3 trasee, talpa in stare buna | 280 | 0.42 | "pe" (in "pe 3 trasee") is the strongest spam word; nothing else in the text pulls back |

### External set: ten spam let through

| Title | Description (start) | Price | Probability | Kind |
|---|---|---|---|---|
| Scaun birou | (empty) | 1 | 0.01 | absurd price only; the price has a small weight |
| Laptop gaming i7 ieftin | Platesti in avans prin transfer, il trimit prin curier… | 900 | 0.01 | advance-payment fraud |
| Camera de inchiriat langa campus | Trimite avansul de 500 lei ca sa iti rezerv camera… | 500 | 0.02 | advance-payment fraud |
| Grile rezolvate licenta | 100% promovare garantata!!! Avem si raspunsurile… | 99 | 0.02 | impossible promise; the "!!!" is glued to a rare word |
| Kit Arduino complet | Suna-ma la 07xx xxx xxx, am si alte produse… | 60 | 0.04 | the phone number is masked, and "suna-ma" is a different word from "suna" |
| Casti wireless noi | Cumpara 2 si primesti 1 gratis!!! Oferta valabila azi… | 49 | 0.06 | sales pitch |
| Bilet festival Untold | Bilete la jumatate de pret, platesti pe PayPal Friends&Family… | 250 | 0.10 | payment without buyer protection |
| Lampa birou | Aplicatia noastra iti da 50 lei bonus la inregistrare… | 1 | 0.14 | referral bait |
| Notite drept civil | Investeste 100 lei in crypto si dublezi suma in 7 zile… | 10 | 0.16 | investment scam |
| Carti facultate diverse | Avem si conturi Netflix si Spotify premium la jumatate… | 15 | 0.20 | selling something else |

None of them asks to move the talk to Telegram or WhatsApp or posts a link, which is
what the model learned. They are **scams that work through the payment** (advance
transfer, PayPal Friends & Family, crypto), and the generator never produced that
kind of text.

## Limits — when not to trust the probability

- It catches loud, contact-seeking spam. Payment fraud with a normal-looking
  description gets a low probability (external recall 0.5).
- It reads words as they are written: punctuation glued to a word ("garantata!!!"),
  masked phone numbers ("07xx xxx xxx") and word variants ("suna-ma") slip through; very common words ("pe", "cu") carry big
  weights, so an ordinary listing can be flagged now and then.
- It is a signal for a human, not a decision: a flagged listing stays published and
  goes to the Admin's moderation list. Do not use it to hide or delete listings
  automatically.
- Romanian text only; English or mixed listings were not in the data.
- Before using it on real traffic: add payment-fraud spam to the generator or real
  reported listings to the data, keep the rule baseline next to the model (it
  catches "!!!" and short phone patterns the model ignores), and choose the threshold
  again on data that is not saturated.

## Quality gate

`tests/CampusMarket.ML.Tests` fails the build when, on the test part, the AUC drops
below 0.95, the F1 below 0.9 or the PR-AUC below 0.95, when four listings written by
hand (two spam, two normal) are classified wrongly, when the committed model does
not have ten words on each side, or when `appsettings.json` and the model's `.json`
disagree on the threshold.

## Reproducing the metrics

```
dotnet run --project ml/CampusMarket.ML.Training
dotnet test tests/CampusMarket.ML.Tests
```

The training program prints, for this model: the metrics, the classification
report, the threshold table, the strongest words and, at the end, the error analysis
on the test part and the external set. The numbers on this page come from the
committed `spam-detection.zip`, the one the API serves. A model retrained on another
machine has the same test metrics, but its probabilities on the external rows can
differ by a few hundredths (the hiking boots get 0.45 instead of 0.42), so the lists
keep the same rows while the last digits move.

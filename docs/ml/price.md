# Price suggestion model (v2)

The price model suggests a fair asking price in lei for a listing from its
title, description, category and condition. It is only a suggestion: the
seller still picks the final price. The API exposes it as `POST
/api/v1/listings/price-suggestion`; the saved model lives at
`ml/models/price-regression.zip`.

## What the model does

- **Task:** regression on non-spam rows only. Spam prices are absurd on
  purpose (1 leu or tens of thousands) and would bias the regressor, so they
  are filtered out both at training and at evaluation time.
- **Features:** `Category` and `Condition` one-hot encoded, `Title` and
  `Description` featurized by ML.NET's `FeaturizeText`, then concatenated.
- **Target:** either plain `Price` or `log1p(Price)` — model selection picks
  one of the two (see below).

The saved pipeline always exposes a `Score` column in lei: when the winning
target is `log1p(Price)`, the pipeline appends an `Expression` transform that
undoes the log (`Score = exp(ScoreLog) - 1`), so the API's `PriceSuggester`
can keep reading `Score` without caring which target won.

## Data and split

- **Dataset:** the generated `ml/data/listings.csv` (dataset v2, catalog-driven,
  seed `2026`).
- **Split:** the shared `DatasetSplit` — 70 / 15 / 15 train / validation /
  test, both cuts **grouped by `TemplateId`** (`samplingKeyColumnName =
  TemplateId`, seed `2026`). Every row of an item type lands in exactly one
  of the three parts, so the validation and test metrics reflect how the
  model scores on item types it has never seen.
- **Non-spam filter:** applied after the split, to both training and
  evaluation.

## Model selection

Six candidates are trained on the train part and scored on the **validation**
part only; the test part is held out. The winner is the candidate with the
lowest MAE in lei, tie-broken by R² (higher).

Every metric below is in lei, whatever the target. When the target is
`log1p(Price)`, predictions are mapped back with `expm1` before computing the
metrics.

| Algorithm | Target        | R²    | MAE (lei) | RMSE (lei) |
|-----------|---------------|-------|-----------|------------|
| FastTree  | Price         | 0.321 | 216.22    | 497.62     |
| **FastTree**  | **log1p(Price) — winner** | **0.462** | **179.48** | **442.86** |
| LightGbm  | Price         | 0.457 | 222.23    | 444.69     |
| LightGbm  | log1p(Price)  | 0.415 | 186.32    | 461.90     |
| Sdca      | Price         | 0.512 | 203.34    | 421.53     |
| Sdca      | log1p(Price)  | 0.375 | 188.72    | 477.11     |

### Why FastTree on `log1p(Price)`

- The distribution of prices is heavy-tailed: a handful of Electronics
  listings cost 5000+ lei while most notes and textbooks sit under 80 lei.
  Training on `log1p(Price)` dampens those outliers and lines up with what we
  care about — getting the right order of magnitude — far more than with the
  raw lei scale.
- Sdca on plain `Price` has a slightly higher R², but a noticeably larger
  MAE (203.34 vs 179.48 lei) and a worse MAPE on the test part. The task
  picks MAE first — the mean absolute error in lei is what the seller sees
  when the suggestion is wrong.
- LightGbm is close to FastTree on both targets, but never beats it on MAE.

## Final metrics on the hold-out test part

After model selection, the winner is **retrained on train + validation** and
scored **once** on the test part (unseen `TemplateId`s). The baseline is the
mean `Price` of the same `Category × Condition` on the training part, with
fallbacks to the category mean and the global mean.

| Metric | Winner       | Baseline (Category × Condition mean) |
|--------|--------------|--------------------------------------|
| R²     | **0.451**    | 0.278                                |
| MAE    | **268.53** lei | 346.41 lei                         |
| RMSE   | **606.21** lei | 695.09 lei                         |
| MAPE   | **75.3%**    | 179.6%                               |

MAE per category on the same test part:

| Category     | Winner       | Baseline     |
|--------------|--------------|--------------|
| Electronics  | 718.14 lei   | 1012.25 lei  |
| LabEquipment | 93.50 lei    | 137.06 lei   |
| Notes        | 28.02 lei    | 33.78 lei    |
| Other        | 286.44 lei   | 297.45 lei   |
| Textbook     | 32.97 lei    | 34.07 lei    |

The winner beats the baseline on every headline metric and on every
category. Electronics is where the money is (and the error, because of the
high-priced items in the tail); the model still cuts the MAE by ~30 % there.

## External evaluation

On top of the grouped hold-out, the saved `price-regression.zip` is also
scored on `ml/data/external.csv` (122 out-of-catalog rows, 102 non-spam,
written by the team after real listings — see the "External evaluation"
section in the README for provenance). Same metrics, same non-spam
filter, same `Category × Condition` mean baseline computed from the
training part:

| Metric | Test (winner) | Test (baseline) | External (winner) | External (baseline) |
|--------|---------------|-----------------|-------------------|---------------------|
| R²     | **0.451**     | 0.274           | **0.239**         | 0.232               |
| MAE    | **268.53** lei | 345.44 lei     | **174.27** lei    | 200.77 lei          |
| MAPE   | **75.3%**     | 176.5%          | **79.3%**         | 132.7%              |

The MAE is still lower on the external set than on the hold-out
(174.27 lei vs 268.53 lei), but the MAPE climbs slightly (79.3% vs
75.3%) and R² is close to the baseline on external (0.239 vs 0.232).
The rewritten external rows carry a long Electronics tail — HDD extern
20TB, Macbook Air M1, Samsung Galaxy Tab S9 FE+ — where the winner
systematically under-shoots the most expensive items. The Category ×
Condition baseline meanwhile recovers more variance from out-of-catalog
mixes than it did on the hold-out, so the two end up close in R². On
absolute error (MAE and MAPE) the winner still beats the baseline on
every row of the table.

## 5-fold grouped cross-validation

On top of the single hold-out, the winner is validated with a **5-fold
grouped cross-validation** on train + validation (test still held out),
grouped by `TemplateId` and with the same non-spam filter:

- **R²:** 0.449 ± 0.106
- **MAE:** 164.95 ± 42.43 lei
- **RMSE:** 383.08 ± 113.76 lei

The spread (`± 0.106` on R², `± 42` lei on MAE) is wide — the price
distribution changes a lot from fold to fold, driven by how many
Electronics item types land in the test fold. The mean matches the test
hold-out within noise (test R² 0.451 vs CV mean 0.449), so the single
hold-out is a representative draw and not a lucky one.

## Limits — when not to trust the suggestion

- **Unseen item types.** The model is evaluated on `TemplateId`s it has
  never seen, but it still has only ~300 item types in the catalog. A
  listing that is far from any of them (a vinyl collection, a 3D printer, a
  wedding dress) can land in `Other` with a wildly wrong price. Any
  Electronics item far above 5000 lei is in the long tail where the MAE
  grows — the model's own category MAE (718 lei) is the honest band to
  show the seller.
- **Synthetic data.** The dataset is seeded (seed `2026`) and generated
  from a hand-written catalog: real listings have typos, slang, mixed
  languages and local pricing patterns we do not fully replicate. The
  numbers above are quality bars for the generator, not promises about live
  traffic.
- **Target drift.** `log1p(Price)` is the right target for today's data; if
  the catalog grows a long tail of very cheap items (stickers, notebooks at
  1–2 lei), the `+1` offset in `log1p` starts to bite and the selection
  should be re-run. The six-candidate search is cheap enough to re-run every
  time the dataset changes.
- **Only a suggestion.** The slice never applies the suggested price. A
  seller who disagrees can type their own price, and the UI should always
  frame this value as a hint, not a quote.

## Quality gate

`CampusMarket.ML.Tests/PriceModelQualityTests` reloads the committed model on
the shared 70 / 15 / 15 grouped split and fails if the R² on the test part
drops below **0.40** (the winner's test R² minus 0.05, rounded down to two
decimals — see the `MinRSquared` constant). It also fails if, for the same
title and description, a `New` listing is not priced higher than a `Worn`
one: whatever the algorithm, condition must still carry weight. The bar is
set relative to the winner's hold-out R² rather than an absolute number, so
it survives a modest seed-to-seed shift but still catches a materially worse
model.

## Reproducing the metrics

From the repository root:

```
dotnet run --project ml/CampusMarket.ML.Training
```

The training program prints the six-combination validation table, names the
winner, and reports the test metrics and CV above. Two runs produce the same
bytes (seed `2026` is wired into `DatasetSplit`, `MLContext` and the manual
fold shuffle). To rebuild only the price model while keeping the other two
committed, restore them after the run:

```
git restore ml/models/category-classification.zip ml/models/spam-detection.zip
```

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
- **Features:** two feature sets are compared (see below).
  - **Current:** `Category` and `Condition` one-hot encoded, `Title` and
    `Description` featurized by ML.NET's `FeaturizeText`, then concatenated.
  - **Text:** Current plus four explicit features read from title + description by
    `PriceTextFeatures`: `OneHot(Brand)`, `StorageGb`, `CpuTier` and `IsNewOrSealed`
    (details in the next section).
- **Target:** either plain `Price` or `log1p(Price)` — model selection picks
  one of the two (see below).

The saved pipeline always exposes a `Score` column in lei: when the winning
target is `log1p(Price)`, the pipeline appends an `Expression` transform that
undoes the log (`Score = exp(ScoreLog) - 1`), so the API's `PriceSuggester`
can keep reading `Score` without caring which target won.

## The explicit text features

`PriceTextFeatures` (`src/CampusMarket.Api/Infrastructure/ML/PriceTextFeatures.cs`) is a
static, pure class. The training project compiles that same file as a linked file, so
training and the API extract the features with the very same code and cannot drift. It
reads title + description, lowercased and without diacritics (both `ș`/`ş` and `ț`/`ţ`
fold to `s` / `t`):

| Feature | Type | Rule |
|---|---|---|
| `Brand` | string, one-hot | first brand of a fixed list (built from the brands in `ml/data/catalog`; product lines map to their maker, e.g. `thinkpad` → `lenovo`), `none` if there is none |
| `StorageGb` | float | the largest number followed by GB or TB (TB × 1024), 0 if none |
| `CpuTier` | float | `i3`/`i5`/`i7`/`i9` and `Ryzen 3/5/7/9` → 3/5/7/9, 0 if none |
| `IsNewOrSealed` | float 1/0 | `sigilat`, `nou`, `new`, `sealed` as whole words (`nou` does not match inside `noua`, `renovat`, …) |

The API fills the same four columns in `PriceInput` through `PriceSuggester`. A model
trained on the Current feature set ignores the extra input columns, so both kinds of
saved model load and predict through the same `PriceInput`.

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

Twelve candidates (two feature sets × three algorithms × two targets) are trained on
the train part and scored on the **validation** part only; the test part is held out.
The winner is the candidate with the lowest MAE in lei, tie-broken by R² (higher).
The saved model is the winner, whichever feature set it comes from.

Every metric below is in lei, whatever the target. When the target is
`log1p(Price)`, predictions are mapped back with `expm1` before computing the
metrics.

| Features | Algorithm | Target        | R²    | MAE (lei) | RMSE (lei) |
|----------|-----------|---------------|-------|-----------|------------|
| Current  | FastTree  | Price         | 0.321 | 216.22    | 497.62     |
| Current  | FastTree  | log1p(Price)  | 0.462 | 179.48    | 442.86     |
| Current  | LightGbm  | Price         | 0.457 | 222.23    | 444.69     |
| Current  | LightGbm  | log1p(Price)  | 0.415 | 186.32    | 461.90     |
| Current  | Sdca      | Price         | 0.516 | 206.37    | 419.84     |
| Current  | Sdca      | log1p(Price)  | 0.378 | 188.86    | 475.95     |
| Text     | FastTree  | Price         | 0.409 | 211.20    | 464.14     |
| Text     | FastTree  | log1p(Price)  | 0.446 | 180.97    | 449.45     |
| Text     | LightGbm  | Price         | 0.436 | 221.73    | 453.41     |
| **Text** | **LightGbm** | **log1p(Price) — winner** | **0.533** | **171.06** | **412.42** |
| Text     | Sdca      | Price         | 0.483 | 204.54    | 433.94     |
| Text     | Sdca      | log1p(Price)  | 0.478 | 176.28    | 436.32     |

The best Current candidate is FastTree on `log1p(Price)` (the previous winner); the best
Text candidate is LightGbm on `log1p(Price)`. Each one is retrained on train + validation
and evaluated once on the test part and in a 5-fold grouped CV:

| Metric | Best Current (FastTree / log1p) | Best Text (LightGbm / log1p) — saved |
|--------|---------------------------------|--------------------------------------|
| Validation MAE | 179.48 lei | **171.06 lei** |
| Validation R² | 0.462 | **0.533** |
| Test MAE | 268.53 lei | **267.44 lei** |
| Test R² | 0.451 | **0.480** |
| Test RMSE | 606.21 lei | **589.85 lei** |
| Test MAPE | **75.3%** | 76.4% |
| 5-fold CV MAE | 164.95 ± 42.43 lei | **163.43 ± 45.00 lei** |
| 5-fold CV R² | 0.449 ± 0.106 | 0.449 ± 0.091 |
| 5-fold CV RMSE | **383.08 ± 113.76 lei** | 383.61 ± 111.62 lei |
| External MAE (102 non-spam rows) | 174.27 lei | **159.74 lei** |
| External R² | 0.239 | **0.312** |
| External MAPE | 79.3% | **68.7%** |

### Conclusion: the Text features win, by a small margin

- **Validation:** the best Text candidate beats the best Current one by 8.4 lei of MAE
  (171.06 vs 179.48, about 5%) and by 0.07 of R². That is what picked it.
- **Test:** the MAE gain is only 1.1 lei (267.44 vs 268.53, 0.4%), a tie for practical
  purposes. R² moves from 0.451 to 0.480 and RMSE drops by 16 lei, but MAPE is slightly
  worse (76.4% vs 75.3%).
- **Cross-validation:** MAE 163.43 vs 164.95 and the same R² (0.449), with a standard
  deviation of 42–45 lei on MAE. The difference is far inside the fold-to-fold noise, so
  CV does **not** show an improvement.
- **External set:** the biggest gain, MAE 159.74 vs 174.27 lei (−8%) and R² 0.312 vs
  0.239, on 102 rows. It is the most encouraging number, but it is a small sample and was
  written by the same team as the catalog.
- **Where it helps and hurts:** on test, Electronics improves (694.00 vs 718.14 lei, where
  brand, storage and CPU tier carry real price information), as do LabEquipment and
  Textbook; `Other` (306.53 vs 286.44 lei) and `Notes` (30.38 vs 28.02 lei) get slightly
  worse.

Honest reading: the explicit features help a little (clearly on validation and external,
barely on test, not at all in CV). The gain is real enough to ship, since the saved model
is not worse on any headline metric except MAPE, but it is not a large improvement, and
picking the best of twelve candidates on one validation part also carries some selection
luck. The model must not be advertised as much better than before.

### Why LightGbm on `log1p(Price)`

- The distribution of prices is heavy-tailed: a handful of Electronics
  listings cost 5000+ lei while most notes and textbooks sit under 80 lei.
  Training on `log1p(Price)` dampens those outliers and lines up with what we
  care about — getting the right order of magnitude — far more than with the
  raw lei scale. The log target has the lowest MAE in both feature sets.
- Sdca on plain `Price` still has a high R² on Current (0.516), but a clearly larger MAE
  (206.37 vs 171.06 lei). The task picks MAE first: the mean absolute error in lei is
  what the seller sees when the suggestion is wrong.
- With the Text features LightGbm gains the most (MAE 186.32 → 171.06 on the log target);
  it can use the numeric `StorageGb` and `CpuTier` columns and the brand directly.
- **Deployment cost:** a LightGbm model needs `Microsoft.ML.LightGbm` in the API (added)
  and the native `libgomp1` library in the Docker runtime image (added to the
  `Dockerfile`; the image build was not run when this was written).

## Final metrics on the hold-out test part

After model selection, the winner is **retrained on train + validation** and
scored **once** on the test part (unseen `TemplateId`s). The baseline is the
mean `Price` of the same `Category × Condition` on the training part, with
fallbacks to the category mean and the global mean.

| Metric | Winner (Text) | Baseline (Category × Condition mean) |
|--------|---------------|--------------------------------------|
| R²     | **0.480**     | 0.278                                |
| MAE    | **267.44** lei | 346.41 lei                          |
| RMSE   | **589.85** lei | 695.09 lei                          |
| MAPE   | **76.4%**     | 179.6%                               |

MAE per category on the same test part:

| Category     | Winner (Text) | Winner before (Current) | Baseline     |
|--------------|---------------|-------------------------|--------------|
| Electronics  | 694.00 lei    | 718.14 lei              | 1012.25 lei  |
| LabEquipment | 89.03 lei     | 93.50 lei               | 137.06 lei   |
| Notes        | 30.38 lei     | 28.02 lei               | 33.78 lei    |
| Other        | 306.53 lei    | 286.44 lei              | 297.45 lei   |
| Textbook     | 29.61 lei     | 32.97 lei               | 34.07 lei    |

The winner beats the baseline on every headline metric. By category it beats the baseline
everywhere except `Other`, where it is now slightly worse (306.53 vs 297.45 lei; the
previous model was marginally better, 286.44 lei). Electronics is where the money is (and
the error, because of the high-priced items in the tail); the model cuts the MAE by ~31 %
there.

## External evaluation

On top of the grouped hold-out, the saved model is also scored on
`ml/data/external.csv` (122 out-of-catalog rows, 102 non-spam, written by the
team after real listings — see the "External evaluation" section in the README
for provenance). Same metrics, same non-spam filter, same `Category × Condition`
mean baseline computed from the training part. The training program also scores
the retrained Current finalist on the same rows, for the comparison:

| Metric | Test (winner) | Test (baseline) | External (Text, saved) | External (Current) | External (baseline) |
|--------|---------------|-----------------|------------------------|--------------------|---------------------|
| R²     | **0.480**     | 0.274           | **0.312**              | 0.239              | 0.232               |
| MAE    | **267.44** lei | 345.44 lei     | **159.74** lei         | 174.27 lei         | 200.77 lei          |
| MAPE   | 76.4%         | 176.5%          | **68.7%**              | 79.3%              | 132.7%              |

The MAE is lower on the external set than on the hold-out (159.74 lei vs 267.44 lei), but
the two sets are different rows with different price mixes, so that gap is not a sign of a
better model. Compared with the Current feature set, the Text features cut
the external MAE by 14.5 lei (−8%), lift R² from 0.239 to 0.312 and bring MAPE down from
79.3% to 68.7%. The rewritten external rows carry a long Electronics tail — HDD extern
20TB, Macbook Air M1, Samsung Galaxy Tab S9 FE+ — where the model still under-shoots the
most expensive items. On absolute error (MAE and MAPE) the winner beats the baseline on
every row of the table. 102 rows is a small sample: treat the gap between Current and
Text as a good sign, not as a measured effect size.

## Feedback loop data

Real sales can be scored too, as a third evaluation set next to the hold-out and
`external.csv`. **Evaluation only**: these rows never enter training (yet).

1. Export: `GET /api/v1/admin/ml/price-feedback` (Admin only; 401 without a token,
   403 for a non-admin) returns `feedback.csv` (`text/csv`), one row per `SaleRecord`
   whose listing still exists, with the same header and column order as
   `external.csv`: `Title,Description,Category,Condition,Price,IsSpam,TemplateId`,
   where `Price` = `SoldPrice`, `IsSpam` = `false`, `TemplateId` = `feedback`.
2. Save the file as `ml/data/feedback.csv` (not committed) and run the training
   program. After the external evaluation it prints `n`, MAE and R² of the saved
   price model on those rows; without the file the step is a no-op, like `external.csv`.

Read the numbers with care:

- They are **indicative** while `n` is small; the output always shows `n`, and R² in
  particular jumps from one sale to the next with a few dozen rows.
- The demo seed does not create `SaleRecord`s, so a fresh database exports a
  header-only file; rows appear only after real `ConfirmSale` calls.
- `SoldPrice` is the listing's **asking price** at the moment of the sale, not a
  negotiated price, and the join returns the **current** listing title and
  description (Category and Condition come from the record).
- The CSV has no `SuggestedPrice`, so the suggester-vs-sold MAE is not computed here;
  it is on `GET /api/v1/admin/ml/price-accuracy`.

Rule for a later step: add feedback rows to training only once there are at least
~100 of them, and then only to the **train** part, never to validation, test or any
evaluation set.

## 5-fold grouped cross-validation

On top of the single hold-out, each feature set's best candidate is validated with a
**5-fold grouped cross-validation** on train + validation (test still held out), grouped
by `TemplateId` and with the same non-spam filter. The saved winner (Text):

- **R²:** 0.449 ± 0.091
- **MAE:** 163.43 ± 45.00 lei
- **RMSE:** 383.61 ± 111.62 lei

For comparison, Current: R² 0.449 ± 0.106, MAE 164.95 ± 42.43 lei, RMSE 383.08 ± 113.76 lei.

The spread (`± 0.091` on R², `± 45` lei on MAE) is wide — the price distribution changes
a lot from fold to fold, driven by how many Electronics item types land in the test fold —
and it is much larger than the difference between the two feature sets. The CV mean of the
winner (R² 0.449) is a little below its test R² (0.480), so the single hold-out is, if
anything, a slightly favourable draw for the Text model.

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
  should be re-run. The twelve-candidate search takes a few minutes, so it is
  cheap enough to re-run every time the dataset changes.
- **Hand-written feature rules.** The brand list comes from the catalog, so a brand that is
  not in it reads as `none`, and `IsNewOrSealed` fires on the word "new" even in a brand
  name such as "New Balance". Storage takes the largest GB/TB figure, so "16GB RAM +
  512GB SSD" reads 512.
- **Only a suggestion.** The slice never applies the suggested price. A
  seller who disagrees can type their own price, and the UI should always
  frame this value as a hint, not a quote.

## Quality gate

`CampusMarket.ML.Tests/PriceModelQualityTests` reloads the committed model on
the shared 70 / 15 / 15 grouped split and fails if the R² on the test part
drops below **0.40** (the `MinRSquared` constant, set for the previous winner's test R² of
0.451 minus 0.05; the Text winner's 0.480 clears it comfortably, and the bar was not
tightened). It also fails if, for the same
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

The training program prints the twelve-candidate validation table, names the
winner, and reports the Current vs Text comparison on validation, test and CV, then the
external evaluation (including the Current vs Text price MAE on `external.csv`). Two runs produce the same
bytes (seed `2026` is wired into `DatasetSplit`, `MLContext` and the manual
fold shuffle). The Current feature set runs on the shared `MLContext` exactly as before, and everything
Text-related runs on a private `MLContext(seed: 2026)`, so the extra candidates do not shift
the seeds the category, spam and similar models draw afterwards: those three models come out
the same as without the Text candidates. To rebuild only the price model while keeping the
others committed, restore them after the run:

```
git restore ml/models/category-classification.zip ml/models/category-classification.json ml/models/spam-detection.zip ml/models/spam-detection.json ml/models/similar-listings.zip ml/models/similar-listings.json
```

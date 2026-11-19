# CampusMarket ML data

**Synthetic data, generated.** `listings.csv` is produced by
`ml/CampusMarket.ML.DatasetGenerator`. It contains no real users, no real
listings and no personal data: every title, description, phone number and
contact is fabricated. The dataset exists so the AI module can be trained and
evaluated reproducibly without touching real accounts.

## Files

- `catalog/*.json` — the source of truth for dataset v2: one file per category
  (`textbook.json`, `notes.json`, `electronics.json`, `labequipment.json`,
  `other.json`), with 300+ item types in total. Each item has:
  - `id` — a unique kebab-case slug the generator writes to the CSV as
    `TemplateId` (e.g. `tb-analiza-mate-y1`, `elec-laptop-thinkpad`).
  - `titles` — 8+ natural Romanian titles.
  - `descriptions` — 8+ Romanian descriptions, short and long, formal and
    casual student style.
  - `priceMin` / `priceMax` — realistic price range in lei.
- `listings.csv` — UTF-8 (no BOM), LF line endings, standard CSV quoting:
  text fields are wrapped in `"…"` and an embedded `"` is escaped as `""`.

## CSV columns

| # | Column | Type | Notes |
|---|---|---|---|
| 0 | `Title` | string | 3–100 chars (`Listing.MaxTitleLength`) |
| 1 | `Description` | string | empty in ~12% of rows, otherwise ≤ 1000 chars |
| 2 | `Category` | enum | `Textbook`, `Notes`, `Electronics`, `LabEquipment`, `Other` |
| 3 | `Condition` | enum | `New`, `Good`, `Worn` |
| 4 | `Price` | int | whole lei, always `> 0` |
| 5 | `IsSpam` | bool | `true` / `false` |
| 6 | `TemplateId` | string | item-type id from the catalog, or `spam-generic` for loud spam |

`TemplateId` is the **last** column so column indexes 0–5 (the ones consumed
by `ListingRow` in the trainer and by the API models) do not shift.

## How dataset v2 is generated

- About **5000 rows**, seed `new Random(2026)` (`// NOSONAR`: fixed on
  purpose, reproducible dataset, not used for security), so every run
  produces the same bytes.
- Rows are drawn from the catalog uniformly across all 300+ item types.
  Price = `priceMin..priceMax` × condition multiplier (`New` 1.0, `Good` 0.7,
  `Worn` 0.45) × ±20% noise.
- **Variation layer** (applied to titles and descriptions):
  - stripped diacritics in ~55% of rows,
  - swap-letter typos (~18%),
  - student abbreviations (~35%): `vand`, `neg.`, `pt`, `stare f buna`,
    `sem`, `ed.`, `vol.`,
  - randomized capitalization or ALL CAPS,
  - emojis occasionally pasted in the title or description (~12%),
  - empty description (~12%) or stretched to a long paragraph (~8%).
- **Label noise:** ~3% of non-spam rows carry the **wrong category** (seller
  picked the wrong dropdown).
- **Spam** (10%, `IsSpam = true`):
  - two thirds are loud and generic: ALL-CAPS titles ("CASTIGA 500 LEI PE
    ZI!!!"), WhatsApp / Telegram / link-in-bio hooks, absurd prices (1 leu
    or 50000–100000 lei); `Category = Other`, `TemplateId = spam-generic`.
  - one third are **disguised**: a normal-looking title in a real category,
    realistic price, the scam hint only in the description.
- About **6% of normal listings** carry a single spam-adjacent word
  (`urgent`, `promotie`, `rapid`, …) without being spam, so the classifier
  has to look at the whole context, not a single keyword.

All phone numbers are obviously fake and no real names, links or brands of
real people appear.

## Regenerate

From the repository root:

```
dotnet run --project ml/CampusMarket.ML.DatasetGenerator
```

The command rewrites `ml/data/listings.csv` and prints the row count, the
number of item types per `Category`, the rows per `Category` and `Condition`,
the spam percentage, the min / avg / max price per `Category`, the distinct
`TemplateId` count, and 10 sample rows. Two runs produce identical bytes;
compare the printed SHA256 hashes to check.

# CampusMarket ML data

**Synthetic data, generated.** `listings.csv` is produced by
`ml/CampusMarket.ML.DatasetGenerator`. It contains no real users, no real
listings and no personal data: every title, description, phone number and
contact is fabricated. The dataset exists so the AI module can be trained and
evaluated reproducibly without touching real accounts.

## File

- `listings.csv` — UTF-8 (no BOM), LF line endings, standard CSV quoting:
  text fields are wrapped in `"…"` and an embedded `"` is escaped as `""`.

## Columns

| Column | Type | Notes |
|---|---|---|
| `Title` | string | 3–100 chars (`Listing.MaxTitleLength`) |
| `Description` | string | empty in ~15% of rows, otherwise ≤ 1000 chars |
| `Category` | enum | `Textbook`, `Notes`, `Electronics`, `LabEquipment`, `Other` (names from `Domain/Listings/Category.cs`) |
| `Condition` | enum | `New`, `Good`, `Worn` (names from `Domain/Listings/Condition.cs`) |
| `Price` | int | whole lei, always `> 0` |
| `IsSpam` | bool | `true` / `false` |

## How it is generated

- 3000 rows, seed `new Random(42)`, so every run produces the same bytes.
- About **10% spam**; the rest is split across the five categories.
- **Titles** are realistic Romanian student listings, with and without
  diacritics, some typos and some ALL CAPS:
  - `Textbook`: manuals for real subjects and years (Analiză matematică, POO,
    Baze de date, Fizică, Anatomie…), editions, authors.
  - `Notes`: course notes, exam summaries, solved problems, per subject and
    year.
  - `Electronics`: laptops, calculators (Casio fx-991EX…), headphones,
    monitors, keyboards, phones.
  - `LabEquipment`: lab coats, goggles, multimeters, breadboards, Arduino
    kits, dissection kits.
  - `Other`: bikes, desk lamps, chairs, dorm items, books that are not
    textbooks.
- **Price** has a base range per category and item type (a laptop costs much
  more than a mouse), multiplied by condition (`New` 1.0, `Good` 0.7, `Worn`
  0.45), plus ±15% random noise. The price is learnable from category,
  condition and the title words.
- **Hard examples** (~10% of non-spam rows): the category cannot be guessed
  from a single word — e.g. "Notițe Electronică digitală" is `Notes`, a
  textbook about computers is `Textbook`, an Arduino kit sold for a course is
  `LabEquipment`.
- **Spam** (`IsSpam = true`): off-platform contact (WhatsApp 07xx xxx xxx,
  Telegram `@…`, "link in bio"), easy money ("câștigă 500 lei pe zi"), fake
  giveaways, ALL CAPS, repeated `!!!`, prices of `1` leu or absurdly high
  (50000–100000 lei). About one third of the spam is **disguised** in a
  normal category with a normal-looking title, and the spam is only in the
  description. All phone numbers are obviously fake and no real names, links
  or brands of real people appear.

## Regenerate

From the repository root:

```
dotnet run --project ml/CampusMarket.ML.DatasetGenerator
```

The command rewrites `ml/data/listings.csv` and prints the row count, the
rows per `Category` and `Condition`, the spam percentage, the min / avg / max
price per `Category`, and 10 sample rows. Two runs produce identical bytes;
compare the printed SHA256 hashes to check.

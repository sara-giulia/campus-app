# Similar listings model — model card

The similar-listings model finds, for a listing, the five Active listings that look
most like it, so the listing page can show "similar listings". The API exposes it as
`GET /api/v1/listings/{id}/similar` (public); the saved model lives at
`ml/models/similar-listings.zip`.

## What the model does

- **Idea:** every listing becomes a text vector built from its title and description,
  and two listings are similar when their vectors point the same way (cosine
  similarity). There is no label to predict: the model is the text-to-vector step.
- **Pipeline:** `FeaturizeText` on `Title` and on `Description` (lower case,
  diacritics removed, words, pairs of words and groups of three letters, each vector
  normalised), concatenated, then `NormalizeLpNorm` so every vector has length 1 and
  the dot product of two vectors is their cosine similarity. The vocabulary and the
  weights are learnt on the train part.
- **In the API:** `SimilarListingsFinder` turns the listing and every other Active
  listing into vectors and returns their similarities (0 to 1);
  `GetSimilarListingsHandler` keeps the five highest (equal similarities: the newest
  first). The target can have any status, like `GET /listings/{id}`; only Active
  listings are suggested, never the listing itself.

## Data and split

- `ml/data/listings.csv`, dataset v2, non-spam rows only (spam is not something to
  recommend). Same 70 / 15 / 15 split grouped by `TemplateId` as the other models.
- Validation chooses the variant; test is measured once.

## How it is evaluated

There is no "right answer" for similarity, so the plan's measure is used: for every
listing, **how many of its 5 closest listings have the same category** (precision@5),
averaged over all listings of the part.

Listings with the same `TemplateId` are variations of the same item (the same book,
the same laptop); finding those is trivial, so they are **left out of each other's
candidates**. The score therefore measures whether the model finds *different* items
of the same kind. The reference is what 5 listings picked at random among the same
candidates would score.

## Model selection and metrics

| Variant | Precision@5 on validation |
|---|---|
| Title only | 0.597 |
| **Title and description** (chosen) | **0.672** |

| Metric (test part, 693 listings) | Model | Random pick |
|---|---|---|
| Precision@5 | **0.668** | 0.202 |

Two thirds of the suggested listings are of the same category, against one in five
by chance. The description helps here, unlike for the category model: two
listings of the same kind share words in their descriptions ("stare buna",
"exercitii rezolvate", "8GB RAM") even when their titles differ.

## Limits — when not to trust the suggestions

- It compares **words**, not meaning: "Analiza matematica" and "Calcul diferential"
  are far apart if they share no word, and two very different items with the same
  generic description ("stare foarte buna, folosit un semestru") come out close.
- The category is not used directly: in about one suggestion in three the category
  differs, usually for items described with words of another category (the same
  border cases as in `docs/ml/category.md`: lab consumables and `Other`, textbooks
  and notes).
- Price and condition are ignored: a 40 lei textbook and a 400 lei one are equally
  similar if their texts are.
- Every request turns all Active listings into vectors, which is fine for a campus
  marketplace (hundreds of listings) but would need precomputed vectors for tens of
  thousands.
- A listing with no known word (empty description and a title of unknown words) has
  an empty vector and a similarity of 0 to everything; it still gets 5 suggestions,
  the newest Active listings.

## Quality gate

`tests/CampusMarket.ML.Tests/SimilarListingsTests.cs` fails the build when, on the
test part, precision@5 drops below 0.6 or is less than 0.3 above the random pick, or
when three pairs written by hand are in the wrong order (a math textbook must be
closer to a math exercise book than to a laptop, a laptop closer to another laptop
than to course notes, notes closer to other notes than to a bicycle).

## Reproducing the metrics

```
dotnet run --project ml/CampusMarket.ML.Training
dotnet test tests/CampusMarket.ML.Tests
```

The training program prints the validation table, the test precision@5 and the random
pick for this model, and writes `ml/models/similar-listings.json` next to the model.

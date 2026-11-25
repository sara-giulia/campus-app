# Search by meaning — model card

`GET /api/v1/listings?search=...` now finds listings by meaning too, not only by the words of the
title: "calculator portabil" finds a laptop only if the words match, but "scooter electric" finds
"Trotineta electrica pliabila", "tabletă apple" finds the iPad and "aparat de măsurat tensiunea" finds
the multimeter. The listings found by words still come first, in the same order as before; the ones
found only by meaning come after them and are marked (`"match": "Meaning"`, "Găsit după sens" in the
frontend).

## What runs

- **Model:** [EmbeddingGemma-300m](https://huggingface.co/google/embeddinggemma-300m) (Google, 2025),
  a pretrained multilingual embedding model (100+ languages, Romanian included), in the ONNX export of
  [onnx-community/embeddinggemma-300m-ONNX](https://huggingface.co/onnx-community/embeddinggemma-300m-ONNX)
  at revision `5090578`, the 4-bit variant `model_q4` (~200 MB). It is used as it is: no training on our data.
- **Locally, in the API:** ONNX Runtime (`Microsoft.ML.OnnxRuntime`) runs the model, and the model's own
  SentencePiece file is read with `Microsoft.ML.Tokenizers` (the ML.NET tokenizers). No external API,
  nothing leaves the server. `GemmaTextEmbedder` turns a text into 768 numbers of length 1.
- **The texts the model sees** (the prompts it was trained with), in lower case:
  - a listing: `title: none | text: {title}. {description}` (cut at 256 tokens);
  - a search: `task: search result | query: {search}`.
- **Storage:** PostgreSQL with [pgvector](https://github.com/pgvector/pgvector): table `ListingEmbeddings`
  (`vector(768)`, one row per listing, with the model name and a SHA-256 of the text it was made from),
  HNSW index for the cosine distance. The search is one SQL query (`ORDER BY embedding <=> query LIMIT 10`).
- **Indexing:** `ListingIndexService` (background) gives a vector to every `Active` listing that has none
  or whose text or model changed: at start (seed, old listings, a new model) and one second after a save
  that touched a listing (publish, edit). No timer, so Neon can sleep when nobody changes listings.
  It takes ~50–60 ms per listing on one core of a laptop (the demo seed: ~3 s).
- **Search:** the vector of the search (cached in memory, ~30 ms to make on one core) against the stored
  vectors of the `Active` listings. A listing counts as found by meaning when (`SemanticSelectionRules`):
  similarity ≥ **0.36**, at most **0.15** under the best match of that search, at most **10** of them.
  The category, condition and price filters apply to them too. If the model is missing or fails, the search
  answers with the words only (`NoSemanticSearch`, logged).

## How the model was chosen

Five pretrained multilingual models, all run the same way (ONNX, CPU, one thread), on the demo seed (54
Active listings) with 31 labelled searches (`ml/data/search/demo-queries.json`). Here only the order by
similarity is measured, without thresholds: is a right listing in the first three?

| Model | Size on disk | Memory (RSS) | Hit@3 | MRR@10 |
|---|---:|---:|---:|---:|
| multilingual-e5-small (int8) | 118 MB | 381 MB | 0.67 | 0.67 |
| paraphrase-multilingual-MiniLM-L12-v2 (int8) | 118 MB | not measured | 0.67 | 0.58 |
| paraphrase-multilingual-mpnet-base-v2 (int8) | 279 MB | not measured | 0.67 | 0.69 |
| multilingual-e5-base (int8) | 279 MB | 536 MB | 0.85 | 0.74 |
| **EmbeddingGemma-300m (q4)** | **197 MB** | **186 MB** | **0.85** | **0.74** |

EmbeddingGemma is as good as the biggest one and uses the least memory, which decides it: Render's free
instance has 512 MB and a tenth of a CPU. The 8-bit variant of EmbeddingGemma (309 MB) was not better
than the 4-bit one on either catalog. A second "reranker" model that reads the search and every listing
together would be more precise, but a 300 MB+ cross-encoder run 10–60 times per search does not fit in
that memory or that CPU (seconds per search), so it was left out.

Smaller choices, all measured on both catalogs: the title and the description together are better than the
title alone or the best of the two; lower case helps a little (titles with random capitals like
"CASTI wireless" read as other words); removing the diacritics as well made it worse.

## Evaluation

`tests/CampusMarket.ML.Tests/SemanticSearchEvaluationTests.cs` runs the real model with the same code as
the API (`ListingSearch.Rank` and `SemanticMatchSelection`) on two catalogs and compares three searches:
the one before this change (the title contains the text), the order by meaning only, and the search now.
The searches are written the way a student asks, mostly not with the words of the titles, plus a few for
things nobody sells (they should find nothing).

- **Demo seed:** 54 listings, 31 searches labelled by hand (`ml/data/search/demo-queries.json`).
- **Dataset:** `ml/data/listings.csv`, non-spam, the first 3 rows of each of the 322 item types (966
  listings, with the typos and random capitals of the generated data), 50 searches whose answers are given
  as item types (`TemplateId`), so the labels do not depend on how the titles are written
  (`ml/data/search/dataset-queries.json`). The thresholds were chosen on these two sets; there is no third
  set, so the numbers are a little optimistic.

| Catalog | Search | Hit@3 | MRR@10 | P@10 | R@10 | results when nothing matches |
|---|---|---:|---:|---:|---:|---:|
| demo | words only (before) | 0.04 | 0.04 | 0.04 | 0.02 | 0.0 |
| demo | meaning, no threshold | 0.81 | 0.74 | 0.11 | 0.98 | 54 |
| demo | **words + meaning (now)** | **0.70** | **0.67** | **0.43** | **0.72** | **2.0** |
| dataset | words only (before) | 0.04 | 0.04 | 0.04 | 0.04 | 0.0 |
| dataset | meaning, no threshold | 0.73 | 0.68 | 0.32 | 0.64 | 966 |
| dataset | **words + meaning (now)** | **0.73** | **0.70** | **0.47** | **0.58** | **4.2** |

Hit@3: a right listing among the first three. MRR@10: 1 / the position of the first right listing. P@10 /
R@10: right listings among the first ten / how many of the right ones are there. The last column is the
average number of results for the searches with no right listing (4 on the demo, 5 on the dataset).

The thresholds cost a little Hit@3 on the demo (0.81 → 0.70) and in exchange a search for something nobody
sells shows 2 listings instead of all 54. They were chosen from a grid (minimum 0.26–0.44, distance from the
best 0.05–1.0, at most 10 or 20): below 0.36 the absent searches fill up, above it the right listings start to
disappear.

The test fails if the search gets worse than: demo Hit@3 0.62 and MRR 0.58, dataset Hit@3 0.68, MRR 0.65 and
P@10 0.42, at most 3 / 6 results for the absent searches, and it checks that the tokens are exactly the ones
of the reference tokenizer.

## What it gets wrong

- **"calculator portabil"** does not find the laptops: for the model "calculator" is first the pocket
  calculator (in Romanian it means both), so it finds `Calculator stiintific Casio`. "computer portabil"
  finds both laptops (by meaning) and "laptop pentru facultate" too; "notebook" finds nothing (under the
  threshold).
- **"căști fără fir"** on the demo catalog finds a mattress (`Saltea gonflabila`) before `Casti wireless`,
  which is under the threshold; "casti" finds it by words.
- Household words with few examples (`veioză`, `vase de gătit`, `lenjerie de pat`, `uscător de păr`) are
  under the threshold: nothing is found rather than something wrong.
- "mașină de spălat" and "mobilă de grădină" (not sold) still show a vacuum cleaner, a hair dryer and some
  furniture: close, but not what was asked. They are marked as found by meaning.
- The order inside the matches by words is still "newest first": the model does not reorder them.

## Running it

- The model is not in git (`.gitignore`): `ml/scripts/download-search-model.sh` downloads it into
  `ml/models/semantic-search/` and checks the SHA-256 of every file (Git Bash on Windows works too). The
  Dockerfile and the CI run the same script. Without the files the API starts, logs
  `Search by meaning is off (...)` and searches by words only.
- Settings, section `SemanticSearch`: `Enabled` (true), `ModelPath` (`ml-models/semantic-search`),
  `Threads` (1), `MinSimilarity` (0.36), `MaxDistanceFromBest` (0.15), `MaxResults` (10).
- PostgreSQL needs the `vector` extension: the migration `AddListingEmbeddings` creates it (Neon and
  Supabase have it; locally `docker-compose.yml` uses `pgvector/pgvector:pg17`).
- Licence: EmbeddingGemma is under the [Gemma Terms of Use](https://ai.google.dev/gemma/terms) (use and
  redistribution allowed, with Google's prohibited-use policy passed on); the ONNX export, ONNX Runtime
  (MIT), Microsoft.ML.Tokenizers (MIT) and pgvector (PostgreSQL licence) are open source.

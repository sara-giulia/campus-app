# Load tests (k6, 20 users, 30 s, API run locally)

The API was run locally with `dotnet run -c Release --project src/CampusMarket.Api` (Development environment, SQLite), not in Docker; Sara agreed. k6 v2.4.0, scripts in `load-tests/`. The token for the price suggestion comes from `POST /api/v1/auth/login` for a test account and is passed with `-e TOKEN=...`.

| Scenario | Requests/s | Avg time | p95 | Errors |
|---|---|---|---|---|
| Search (`GET /api/v1/listings?page=1&pageSize=20`) | 5204 | 3.76 ms | 9.80 ms | 0.00% |
| Price suggestion (`POST /api/v1/listings/price-suggestion`) | 33391 | 0.53 ms | 1.16 ms | 0.00% |

Conclusions: the price suggestion is faster than search. `SuggestPriceHandler` uses only `IPriceSuggester` and never touches the database, and the model is kept in a `PredictionEnginePool` registered once (`AddCampusMarketMl`), so a prediction takes under a millisecond; every search runs an EF Core query on SQLite (and, in Development, logs it to the console). The highest price suggestion time (790 ms) is most likely the first prediction, before the pool was warm. The local database had few published listings, so search responses were small; with more data, search would be slower, while the price suggestion would stay the same.

How to run: start the API, then `k6 run -e BASE_URL=http://localhost:5112 load-tests/search.js` and `k6 run -e BASE_URL=http://localhost:5112 -e TOKEN=<token> load-tests/price-suggestion.js` (the port is the one in "Now listening on").

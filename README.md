# City Report Aggregator

[![CI](https://github.com/mariannapap/Aggregator/actions/workflows/ci.yml/badge.svg)](https://github.com/mariannapap/Aggregator/actions/workflows/ci.yml)

An ASP.NET Core (.NET 10) API that implements the **Gateway Aggregation** pattern over four keyless [Open-Meteo](https://open-meteo.com/) APIs. One request resolves a city to coordinates, then fans out **in parallel to the weather, air-quality and marine APIs** for every matching place, and returns one composite response.

```
GET /city-report/{country}/{city}
          │
          ▼
   Geocoding API ──► places (settlements + airports in that country)
          │
          ├──► Weather API      ┐
          ├──► Air Quality API  ├ per place, all in parallel
          └──► Marine API       ┘
          │
          ▼
   one response: filters + places[] with per-source status
```

## Run it

Requirements: Docker.

```bash
docker compose up --build
```

The API listens on `http://localhost:8080`. The interactive API reference (Scalar) is at `http://localhost:8080/scalar` and the OpenAPI document at `/openapi/v1.json`.

### Get a token

Every endpoint except `/health` and `/mock-login` requires a JWT bearer token. For the demo, a **mock issuer** is enabled in `docker-compose.yml`:

```bash
curl -X POST http://localhost:8080/mock-login -H "Content-Type: application/json" -d '{"user":"valid"}'
```

| `user` | Token | Result on protected endpoints |
|---|---|---|
| `valid` | signed with the trusted key, has the `reader` role | `200` |
| `invalid` | signed with an untrusted key | `401` with `WWW-Authenticate: Bearer error="invalid_token"` |
| `forbidden` | valid, but without the `reader` role | `403` |

Tokens live **2 minutes** (clock skew 5 s), so expiry can be shown live.

```bash
TOKEN=$(curl -s -X POST http://localhost:8080/mock-login -H "Content-Type: application/json" -d '{"user":"valid"}' | sed -E 's/.*"access_token":"([^"]+)".*/\1/')
curl -H "Authorization: Bearer $TOKEN" "http://localhost:8080/city-report/GR/Athens"
```

[src/Aggregator.Api/Aggregator.Api.http](src/Aggregator.Api/Aggregator.Api.http) logs in and calls every endpoint from Visual Studio, Rider or VS Code (REST Client), including a degraded response (an inland city, where the marine API has no data).

### Run the tests

```bash
docker build --target test --progress=plain .
```

Runs all unit and integration tests; the build fails if any test fails. No network access is needed: external APIs are replaced by fake HTTP handlers and recorded responses. With the .NET 10 SDK installed, `dotnet test` runs the same tests (`global.json` pins the SDK and selects Microsoft.Testing.Platform). CI runs both suites below on every push.

### End-to-end tests

A separate suite runs **the shipped Docker image** against [WireMock](https://wiremock.org/) standing in for the four Open-Meteo APIs. The test runner is a third container that calls the API over the Docker network and switches WireMock's behaviour mid-scenario (failures, delays).

```powershell
./e2e/run.ps1                          # quick scenarios (~30 s)
./e2e/run.ps1 -Scope all               # + long scenarios (~3.5 min)
```
```bash
./e2e/run.sh            # quick
./e2e/run.sh all 60     # all, with a 60 s load phase
```

| Scenario | What it proves | Scope |
|---|---|---|
| Smoke | health, 401 without token, token → 200 with all four sources `ok`, 404, statistics | quick |
| Fallback lifecycle | weather goes down → `stale` with the last values → recovers → `ok` | quick |
| Circuit-breaker lifecycle | repeated marine failures open the circuit (calls stop reaching the API) → after the break a probe succeeds → closed | quick |
| Token expiry | a token works until its lifetime ends, then 401 "expired" | quick |
| Anomaly detection | latency jumps from ~1 ms to 700 ms → the background service logs the anomaly | long |
| Sustained load | 20 clients for 60 s over 300 cities: all 200, p95 < 2 s, and `/stats` matches the number of calls WireMock received | long |

The e2e environment (`docker-compose.e2e.yml`) shortens timings so scenarios take seconds: 2 s cache, 5 s circuit break, 12 s tokens, 10 s anomaly checks over a 1-minute window.

### Test report

The latest results are in [docs/test-report.md](docs/test-report.md): every test grouped by the requirement it proves (R1–R11, O1–O2), marked as a success or failure scenario, plus line and branch coverage. To regenerate it:

```bash
docker build --target test-report-export --output type=local,dest=docs .
```

Tests are tagged with `[Requirement("R6")]` and `[FailureScenario]`; [tools/TestReport.cs](tools/TestReport.cs) (a .NET 10 single-file app) turns the xUnit results into the report, and `dotnet-coverage` with ReportGenerator adds the coverage summary.

## Endpoints

### `GET /city-report/{country}/{city}`

| Parameter | Where | Values | Default |
|---|---|---|---|
| `country` | path | ISO 3166-1 alpha-2 (e.g. `GR`) | required |
| `city` | path | city or airport name, 1–100 characters | required |
| `include` | query | comma-separated: `weather`, `airQuality`, `marine`, `hourly`. Only the APIs for the requested sections are called; hourly rows come from the included sections (all three with `include=hourly` on its own) | `weather,airQuality,marine` |
| `region` | query | region name, fuzzy-matched (`krete` → Crete, `attiki` → Attica, `macedonia` → both Macedonias) | none |
| `sort` | query | `relevance`, `name`, `temperature`; prefix `-` for descending | `relevance` |
| `hourly.category` | query | `weather`, `airQuality` or `marine`: narrows hourly rows to one section, which must also be in `include` (or `include=hourly` alone) | all included |
| `hourly.from`, `hourly.to` | query | ISO 8601 date-times inside the 24 h window | whole window |
| `hourly.sort` | query | `time` or `-time` | `time` |

Example: `GET /city-report/GR/Chania?region=crete&sort=-temperature&include=weather,hourly&hourly.category=weather`

```json
{
  "filters": {
    "region": { "requested": "crete", "matched": ["Crete"], "applied": true },
    "hourly": { "appliedFrom": "2026-11-15T16:00:00+00:00", "appliedTo": "2026-11-16T15:00:00+00:00", "clamped": false }
  },
  "places": [
    {
      "city": "Chania",
      "country": "GR",
      "region": "Crete",
      "coordinates": { "latitude": 35.51124, "longitude": 24.02921 },
      "distanceKm": 274.9,
      "weather": { "temperature": 19.4, "windSpeed": 2.5 },
      "hourly": [
        { "time": "2026-11-15T16:00:00+00:00", "category": "weather", "metric": "temperature", "value": 19.4, "unit": "°C" }
      ],
      "sources": { "geocoding": "ok", "weather": "ok" }
    },
    { "city": "Chania International Airport", "region": "Crete", "distanceKm": 273.9, "…": "…" }
  ]
}
```

| Field | Meaning |
|---|---|
| `distanceKm` | distance from the Acropolis, Athens: the **relevance** value; nearest first by default |
| `weather` | current temperature (°C) and wind speed (km/h) |
| `airQuality.usAqi` | current US AQI (0–500 scale) |
| `marine` | current wave height (m) and sea surface temperature (°C); `sources.marine` is `no_data` for places away from the sea |
| `hourly` | one row per metric per hour, for a fixed window of **24 hours starting at the current hour (UTC)** |
| `sources` | status of each external API used for this place (see below) |
| `filters` | how the region and hourly filters were applied; a region that matches nothing is **ignored and reported** (`applied: false`) |

**Per-source status:** `ok` · `partial` (some values or hours missing) · `no_data` (valid answer, nothing usable) · `stale` (fresh call failed, served from the last good cached value) · `unavailable` (failed, nothing cached).

| Status code | When |
|---|---|
| `200` | Coordinates were resolved. Weather, air quality or marine may still be `no_data`/`stale`/`unavailable`, as shown in `sources`. |
| `400` | Invalid input (validation problem with the failing fields), e.g. `sort=temperature` without `include=weather`. |
| `401` / `403` | Missing or invalid token / token without the required role. |
| `404` | No city or airport with that name in that country. |
| `503` | Geocoding failed and no cached coordinates exist: nothing else can run without them. |

All errors are RFC 9457 Problem Details.

### `GET /stats`

Total requests and average response time **per external API**, grouped in performance buckets (thresholds configurable; defaults fast < 100 ms, average 100–200 ms, slow > 200 ms). Every real HTTP attempt is counted, retries included; cache hits are not, because they never reach the API. `failedRequests` counts attempts that ended in a timeout, a network error, a 5xx or a 429; Open-Meteo's 400 ("no data for this request") is an answer, not a failure. Every API is listed from startup, and an average is `null` until there is something to average.

```json
{
  "thresholds": { "fastBelowMs": 100, "slowAboveMs": 200 },
  "apis": {
    "weather": {
      "totalRequests": 15, "failedRequests": 1, "averageMs": 140.22,
      "buckets": { "fast": { "count": 6, "averageMs": 63.97 }, "average": { "count": 5, "averageMs": 176.86 }, "slow": { "count": 4, "averageMs": 208.8 } }
    }
  }
}
```

### `GET /health`

Anonymous liveness check.

### Performance anomaly logging

A background service checks the statistics every minute and logs a warning when an API's average over the **last 5 minutes** is more than **50% above** its average over all earlier requests **and at least 50 ms slower** (at least 5 requests in each). The absolute floor is our interpretation of the brief's "e.g. >50% bigger": a purely relative rule flags normal noise at low latencies (an end-to-end run logged 1.46 ms vs 0.56 ms as an anomaly). `Anomaly:MinimumIncreaseMs` = 0 restores the purely relative rule. All values are configurable.

## Architecture

| Project | Responsibility |
|---|---|
| `Aggregator.Core` | Model, the source contract, orchestration (`CityReportService`), query logic (fuzzy region matching, relevance, sorting, hourly window), statistics and anomaly detection, and its own registration (`AddCore`). **Depends only on** the `Microsoft.Extensions` options and DI abstractions. |
| `Aggregator.Infrastructure` | Open-Meteo adapters, HTTP clients and resilience, caching, the statistics handler. |
| `Aggregator.Api` | Composition root: endpoints, contracts and mapping, validation, authentication, error handling, the background service. |

### One generic contract for every external API

```csharp
public interface IExternalSource<in TQuery, TResult>
    where TQuery : ISourceQuery
    where TResult : class
{
    string Name { get; }
    Task<SourceResult<TResult>> FetchAsync(TQuery query, CancellationToken cancellationToken);
}
```

Geocoding, weather, air quality and marine each implement it with their own typed model. Cross-cutting concerns attach to the contract, not to each source:

- **Caching:** one open-generic decorator for all sources (`services.Decorate(typeof(IExternalSource<,>), typeof(CachingSource<,>))`).
- **Resilience:** one standard resilience handler per typed `HttpClient`, configured per API from `appsettings.json`.
- **Statistics:** a `DelegatingHandler` inside the resilience pipeline measures every attempt.

Adding an API means writing an adapter and a model; caching, resilience, statistics and statuses come with the contract. The orchestrator calls the sources explicitly, so it reads like the dependency graph (geocoding first, then weather, air quality and marine in parallel). Marine was added this way: an endpoint definition on the shared Open-Meteo client, a model, one response property and one orchestrator line. Adding a source also adds one property to the response; that trade-off is deliberate, in exchange for precise typed models and OpenAPI schema.

### Resilience and fallback

| Layer | Mechanism |
|---|---|
| Transient failures | `Microsoft.Extensions.Http.Resilience` standard handler per API: 3 retries (exponential + jitter, honours `Retry-After`), 2 s per attempt, 8 s total, circuit breaker. |
| Concurrency | At most **10 concurrent calls per API across all requests** (the handler's concurrency limiter, queue 200). Open-Meteo's air-quality API returns 429 above ~30 concurrent calls. |
| Fallback | If the fresh call fails, the last good value (kept 24 h) is served as `stale`. |
| Partial results | One failing source never fails the request; its status says what happened. Only a failure of the geocoding layer (the dependency of everything else) returns `503`. |

### Caching

`HybridCache` (in-memory, stampede-protected), **per external API and per location**, never per aggregated response (an aggregate would freeze one source's failure for everyone). Coordinates are rounded to 2 decimals (~1 km, smaller than Open-Meteo's grid) for both the cache key and the request.

| API | Fresh for | Why |
|---|---|---|
| Geocoding | 24 h | Coordinates don't change; Open-Meteo sends `max-age=86400`. |
| Current weather | 15 min | The API's current value advances every 15 min. |
| Current marine | 15 min | Like weather, the current value advances every 15 min. |
| Current air quality / hourly data | 1 h | Hourly values; forecasts change only with new model runs (6–24 h). |

### Security

The API only **validates** tokens: signature, issuer, audience, lifetime, and a `reader` role policy on `/city-report` and `/stats`; anything else requires an authenticated user by default. The mock issuer exists only for the demo: it is off unless `MockAuth:Enabled` is `true`, and signs with an RSA key generated in memory at startup, so there are no secrets to manage. In production the mock is disabled and `Auth:Authority` points to a real identity provider (e.g. Microsoft Entra ID); no code changes.

## Configuration

Everything is in `src/Aggregator.Api/appsettings.json` and can be overridden with environment variables (`Section__Key`), as `docker-compose.yml` does. Options are validated at startup.

Each HTTP client has its own full `Resilience` block, even where the values are currently the same. This is deliberate: every external API can be tuned independently without code changes (Open-Meteo's air-quality API, for example, rejects more than ~30 concurrent calls, while the forecast API accepts more).

| Section | Settings |
|---|---|
| `HttpClients:{Geocoding,Weather,AirQuality,Marine}` | `BaseUrl`, `CacheTtl`, `HourlyCacheTtl`, `LastGoodRetention`, `Resilience` (retry, timeouts, circuit breaker, concurrency limiter) |
| `Geocoding` | `Count` (100), `Language`, `AllowedFeatureCodes` (settlements `PPL`, `PPLA`–`PPLA4`, `PPLC` and airports `AIRP`) |
| `CityReport` | `CoordinateDecimals`, `HourlyWindowHours` (24), `MaxCityLength`, `MaxRegionLength` |
| `Relevance` | reference point (the Acropolis) |
| `RegionMatching` | `MaxEditDistanceRatio` (0.3) |
| `Statistics` | bucket thresholds |
| `Anomaly` | `Interval`, `WindowMinutes`, `Ratio`, `MinSamples`, `MinimumIncreaseMs` |
| `Auth`, `MockAuth` | issuer, audience, required role, clock skew, authority; mock on/off and token lifetime |
| `OpenApi` | `EnableUi` |

## Assumptions and decisions

- **"Aggregated data"** is a composite object: each place combines geocoding, weather, air quality and marine data, and each nested list has its own filters and sorting.
- **Filtering and sorting (R5):** date (`hourly.from`/`to`, `hourly.sort`), relevance (distance from the Acropolis), category (`hourly.category`), plus region and temperature.
- **Ambiguous names:** all matches in the country are returned (settlements and airports only), nearest first. `country` is mandatory.
- **Hourly data** covers a fixed 24 h window from the current hour (UTC), so responses stay bounded and every API always covers it.
- **Provider limits never become our API contract:** an adapter that receives less than requested reports `partial` or `no_data` instead.
- All four APIs come from one provider (Open-Meteo); they are separate APIs on separate hosts with separate rate limits.

## Possible extensions

- A distributed L2 cache (Redis) behind `HybridCache`, for several API instances.
- Batching cache misses into Open-Meteo's multi-location requests for very large result sets.
- OpenTelemetry export of the existing traces and `aggregator.external_api.duration` metric.
- A real identity provider instead of the mock issuer.

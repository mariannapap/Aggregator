# City Report Aggregator

[![CI](https://github.com/mariannapap/Aggregator/actions/workflows/ci.yml/badge.svg)](https://github.com/mariannapap/Aggregator/actions/workflows/ci.yml)

An ASP.NET Core (.NET 10) API that implements the **Gateway Aggregation** pattern over five keyless APIs from two vendors: four from [Open-Meteo](https://open-meteo.com/) and NASA's [EONET](https://eonet.gsfc.nasa.gov/) natural-events API. One request resolves a city to coordinates, then fans out **in parallel to the weather, air-quality and marine APIs** (and, on request, natural hazards) for every matching place, and returns one composite response.

```
GET /city-report/{country}/{city}
          │
          ▼
   Geocoding API ──► places (settlements + airports in that country)
          │
          ├──► Weather API      ┐
          ├──► Air Quality API  │ per place, all in parallel
          ├──► Marine API       │
          └──► NASA EONET       ┘ (include=hazards)
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

A separate suite runs **the shipped Docker image** against [WireMock](https://wiremock.org/) standing in for all five external APIs. The test runner is a third container that calls the API over the Docker network and switches WireMock's behaviour mid-scenario (failures, delays).

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
| `include` | query | comma-separated: `weather`, `airQuality`, `marine`, `hazards`, `hourly`. `hazards` is opt-in (one more call per place). Only the APIs for the requested sections are called; hourly rows come from the included sections (all three with `include=hourly` on its own) | `weather,airQuality,marine` |
| `region` | query | region name, fuzzy-matched (`krete` → Crete, `attiki` → Attica, `macedonia` → both Macedonias) | none |
| `sort` | query | `relevance` (best name match first: exact, then prefix, then contains, ignoring case and accents; then capital, administrative seat, town, airport; then the geocoder's order), `distance` (from the Acropolis, nearest first), `name`, `temperature`; prefix `-` for descending | `relevance` |
| `limit` | query | how many places to return, 1–100; applied after the region filter and the sort, **before** any weather, air-quality or marine call (with `sort=temperature`, weather is fetched for every place to rank them, the other APIs only for the page) | `20` |
| `offset` | query | places to skip, for paging; past the end returns an empty list | `0` |
| `hourly.category` | query | `weather`, `airQuality` or `marine`: narrows hourly rows to one section, which must also be in `include` (or `include=hourly` alone) | all included |
| `hourly.from`, `hourly.to` | query | ISO 8601 date-times; bounds outside the 24 h window are clamped to it, and a range that does not overlap it at all returns `400` | whole window |
| `hourly.sort` | query | `time` or `-time` | `time` |

Example: `GET /city-report/GR/Chania?region=crete&sort=-temperature&include=weather,hourly&hourly.category=weather`

```json
{
  "filters": {
    "region": { "requested": "crete", "matched": ["Crete"], "applied": true },
    "hourly": { "appliedFrom": "2026-11-15T16:00:00+00:00", "appliedTo": "2026-11-16T15:00:00+00:00", "clamped": false },
    "totalPlaces": 2, "truncated": false
  },
  "degraded": false,
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
      "sources": {
        "geocoding": { "status": "ok", "fetchedAt": "2026-11-15T09:12:40+00:00" },
        "weather": { "status": "ok", "fetchedAt": "2026-11-15T16:05:02+00:00" }
      }
    },
    { "city": "Chania International Airport", "region": "Crete", "distanceKm": 273.9, "…": "…" }
  ]
}
```

| Field | Meaning |
|---|---|
| `distanceKm` | distance from the Acropolis, Athens, in km; `sort=distance` orders by it |
| `weather` | current temperature (°C) and wind speed (km/h) |
| `airQuality.usAqi` | current US AQI (0–500 scale) |
| `marine` | current wave height (m) and sea surface temperature (°C); `sources.marine` is `no_data` for places away from the sea |
| `hazards` | open natural events (wildfires, storms, floods, volcanoes…) seen in the last 30 days within 150 km, nearest first: `title`, `category`, `date`, `distanceKm`. From NASA EONET, with `include=hazards`; `sources.hazards` is `no_data` when nothing is near |
| `hourly` | one row per metric per hour, for a fixed window of **24 hours starting at the current hour (UTC)** |
| `sources` | for each external API used for this place: `status` (see below) and `fetchedAt`, when its data was fetched from the API. A `stale` value keeps its original `fetchedAt`, so its age is visible; `unavailable` has none |
| `degraded` | `true` when any source of any place is `partial`, `stale` or `unavailable`; `no_data` is a valid answer, not a degradation |
| `filters` | how the region and hourly filters were applied (a region that matches nothing is **ignored and reported**, `applied: false`), plus `totalPlaces` (places after the region filter) and `truncated` (more places exist than this page returns) |

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

Adding an API means writing an adapter and a model; caching, resilience, statistics and statuses come with the contract. The orchestrator calls the sources explicitly, so it reads like the dependency graph (geocoding first, then weather, air quality and marine in parallel). Marine was added this way: an endpoint definition on the shared Open-Meteo client, a model, one response property and one orchestrator line. Hazards proves the same contract with a second vendor: its own adapter, query type (`AreaQuery`), response shape and timeouts (EONET answers in 1–9 s, so it gets 10 s per attempt instead of 2 s), with caching, resilience, statistics, statuses and the cooldown coming from the shared registration unchanged. Adding a source also adds one property to the response; that trade-off is deliberate, in exchange for precise typed models and OpenAPI schema.

### Resilience and fallback

| Layer | Mechanism |
|---|---|
| Transient failures | `Microsoft.Extensions.Http.Resilience` standard handler per API: 3 retries (exponential + jitter, honours `Retry-After`), 2 s per attempt, 8 s total, circuit breaker. |
| Concurrency | At most **10 concurrent calls per API across all requests** (the handler's concurrency limiter, queue 200). Open-Meteo's air-quality API returns 429 above ~30 concurrent calls. |
| Fallback | If the fresh call fails, the last good value (kept 24 h) is served as `stale`, with its original `fetchedAt`. |
| Cooldown | After a failure, the same data is not requested again for 30 s; the last good value (or `unavailable`) is served meanwhile. This also covers broken payloads, which the circuit breaker never sees because the HTTP call itself succeeded. |
| Partial results | One failing source never fails the request; its status says what happened. Only a failure of the geocoding layer (the dependency of everything else) returns `503`. |

### Caching

`HybridCache` (in-memory, stampede-protected, limited to 100 MB by default, `Cache:SizeLimitMegabytes`), **per external API and per location**, never per aggregated response (an aggregate would freeze one source's failure for everyone). Coordinates are rounded to 2 decimals (~1 km, smaller than Open-Meteo's grid) for both the cache key and the request.

| API | Fresh for | Why |
|---|---|---|
| Geocoding | 24 h (a city that is not found: 10 min) | Coordinates don't change; Open-Meteo sends `max-age=86400`. A not-found result is kept briefly so a typo is cheap but a newly listed place appears soon. |
| Current weather | 15 min | The API's current value advances every 15 min. |
| Current marine | 15 min | Like weather, the current value advances every 15 min. |
| Hazards | 45 min | Events change slowly; EONET is slow (1–9 s), so cache hits matter. |
| Current air quality / hourly data | 1 h | Hourly values; forecasts change only with new model runs (6–24 h). |

### Security

The API only **validates** tokens: signature, issuer, audience, lifetime, and a `reader` role policy on `/city-report` and `/stats` (the role is read from the `roles` claim, the array Microsoft Entra ID issues, and the mock issues the same shape); anything else requires an authenticated user by default. The mock issuer exists only for the demo: it is off unless `MockAuth:Enabled` is `true`, and signs with an RSA key generated in memory at startup, so there are no secrets to manage. In production the mock is disabled and `Auth:Authority` points to a real identity provider (e.g. Microsoft Entra ID); no code changes.

### Telemetry

Off unless `Telemetry:OtlpEndpoint` is set (for example `Telemetry__OtlpEndpoint=http://collector:4317`); then OpenTelemetry exports ASP.NET Core and `HttpClient` traces and metrics, plus the `aggregator.external_api.duration` histogram, over OTLP. HybridCache runs a shared fetch without the caller's context, so each cache fetch starts a `fetch {source}` span parented to the request: one trace shows the request, every fetch and every HTTP call to the external APIs. When callers share a fetch, it appears in the trace of the request that started it.

## Configuration

Everything is in `src/Aggregator.Api/appsettings.json` and can be overridden with environment variables (`Section__Key`), as `docker-compose.yml` does. Options are validated at startup.

Each HTTP client has its own full `Resilience` block, even where the values are currently the same. This is deliberate: every external API can be tuned independently without code changes (Open-Meteo's air-quality API, for example, rejects more than ~30 concurrent calls, while the forecast API accepts more).

| Section | Settings |
|---|---|
| `HttpClients:{Geocoding,Weather,AirQuality,Marine,Hazards}` | `BaseUrl`, `CacheTtl`, `HourlyCacheTtl`, `NoDataCacheTtl`, `LastGoodRetention`, `FailureCooldown`, `Resilience` (retry, timeouts, circuit breaker, concurrency limiter) |
| `Cache` | `SizeLimitMegabytes` (100): the in-memory cache's size limit; HybridCache sizes each entry by its serialized length |
| `Geocoding` | `Count` (100), `Language`, `AllowedFeatureCodes` (settlements `PPL`, `PPLA`–`PPLA4`, `PPLC` and airports `AIRP`) |
| `Hazards` | `RadiusKm` (150), `Days` (30: EONET leaves some events "open" for years), `Limit` (50) |
| `CityReport` | `CoordinateDecimals`, `HourlyWindowHours` (24), `MaxCityLength`, `MaxRegionLength` |
| `Distance` | reference point for `distanceKm` (the Acropolis) |
| `RegionMatching` | `MaxEditDistanceRatio` (0.3) |
| `Statistics` | bucket thresholds |
| `Anomaly` | `Interval`, `WindowMinutes`, `Ratio`, `MinSamples`, `MinimumIncreaseMs` |
| `Auth`, `MockAuth` | issuer, audience, required role, clock skew, authority; mock on/off and token lifetime |
| `OpenApi` | `EnableUi` |
| `Telemetry` | `OtlpEndpoint` (none): when set, traces and metrics are exported over OTLP (gRPC) |

## Assumptions and decisions

- **"Aggregated data"** is a composite object: each place combines geocoding, weather, air quality and marine data, and each nested list has its own filters and sorting.
- **Filtering and sorting (R5):** date (`hourly.from`/`to`, `hourly.sort`), relevance (how well each place matches the searched name, then its type), distance from the Acropolis, category (`hourly.category`), plus region and temperature.
- **Ambiguous names:** all matches in the country are returned (settlements and airports only), best match first (see `relevance`). `country` is mandatory.
- **Hourly data** covers a fixed 24 h window from the current hour (UTC), so responses stay bounded and every API always covers it.
- **Provider limits never become our API contract:** an adapter that receives less than requested reports `partial` or `no_data` instead.
- Four APIs come from Open-Meteo (separate APIs on separate hosts with separate rate limits); the fifth, NASA EONET, is a different vendor, to prove the extension point beyond one provider's client and response shape.
- **One cache entry per request shape.** Current-only, hourly-only and current + hourly requests for a place are cached separately, each with its natural lifetime (current 15 min, hourly 1 h). One combined entry for every shape would force the shorter 15-minute lifetime on hourly data and refetch it four times as often.
- **No inbound rate limiting.** There is no measured baseline of real traffic to size a limit from, and a guessed limit would either block legitimate users or protect nothing. Instead, each request's upstream cost is bounded (`limit`, the per-API concurrency limiter, the cache). ASP.NET Core's built-in rate limiter is the next step once real usage is known.

## Possible extensions

- A distributed L2 cache (Redis) behind `HybridCache`, for several API instances.
- Batching cache misses into Open-Meteo's multi-location requests for very large result sets.
- A real identity provider instead of the mock issuer.

# City Report Aggregator

[![CI](https://github.com/mariannapap/Aggregator/actions/workflows/ci.yml/badge.svg)](https://github.com/mariannapap/Aggregator/actions/workflows/ci.yml)

An ASP.NET Core (.NET 10) API that implements the **Gateway Aggregation** pattern over five keyless APIs from two vendors: four from [Open-Meteo](https://open-meteo.com/) and NASA's [EONET](https://eonet.gsfc.nasa.gov/) natural-events API. One request resolves a city to the matching places in a country, then calls the **weather, air-quality and marine APIs** (and, on request, natural hazards) for every place in parallel, and returns one composite response.

```
GET /city-report/{country}/{city}
          │
          ▼
   Geocoding API ──► places (settlements + airports in that country)
          │
          ├──► Weather API      ┐
          ├──► Air Quality API  │ per place, in parallel
          ├──► Marine API       │ (up to 5 places at a time per request)
          └──► NASA EONET       ┘ (include=hazards)
          │
          ▼
   one response: filters + places[] with a status per source
```

## Contents

- [Run it](#run-it) · [Reviewer walkthrough](#reviewer-walkthrough) · [Tests](#tests)
- [Endpoints](#endpoints) · [Architecture](#architecture) · [Configuration](#configuration)
- [Assumptions and decisions](#assumptions-and-decisions) · [Known limitations](#known-limitations)

## Run it

Requirements: Docker.

```bash
docker compose up --build
```

The API listens on `http://localhost:8080`. The interactive API reference (Scalar) is at `http://localhost:8080/scalar` and the OpenAPI document at `/openapi/v1.json`; `docker-compose.yml` turns both on with `OpenApi__EnableUi`. Every query parameter and every field of the API's own request and response types is described there, with units.

### Get a token

`/health` is anonymous. So are `/mock-login` when the mock issuer is enabled, and `/scalar` and `/openapi/v1.json` when the API reference is enabled. Everything else needs a JWT bearer token; an anonymous call to any other path, mapped or not, gets `401`. For the demo, `docker-compose.yml` enables a **mock issuer**:

```bash
curl -X POST http://localhost:8080/mock-login -H "Content-Type: application/json" -d '{"user":"valid"}'
```

| `user` | Token | Result on `/city-report` and `/stats` |
|---|---|---|
| `valid` | signed with the trusted key, has the `reader` role | `200` |
| `invalid` | signed with an untrusted key | `401` with `WWW-Authenticate: Bearer error="invalid_token"` |
| `forbidden` | valid, but without the `reader` role | `403` |

Tokens live **2 minutes** (clock skew 5 s), so expiry can be shown live.

```bash
TOKEN=$(curl -s -X POST http://localhost:8080/mock-login -H "Content-Type: application/json" -d '{"user":"valid"}' | sed -E 's/.*"access_token":"([^"]+)".*/\1/')
curl -H "Authorization: Bearer $TOKEN" "http://localhost:8080/city-report/GR/Athens"
```

[src/Aggregator.Api/Aggregator.Api.http](src/Aggregator.Api/Aggregator.Api.http) logs in and calls every endpoint from Visual Studio, Rider or VS Code (REST Client). It includes an inland city, where the marine API answers `no_data`.

## Reviewer walkthrough

About ten minutes, in this order:

1. `docker compose up --build`, open `http://localhost:8080/scalar`, call `POST /mock-login` with `{"user":"valid"}` and paste the token into Scalar's authentication.
2. `GET /city-report/GR/Athens`: every place called Athens in Greece, best name match first. Each place has `sources` (status and `fetchedAt` per API) and the response has `degraded`.
3. `GET /city-report/GR/Chania?region=crete&sort=-temperature&include=weather,hourly&hourly.category=weather`: the brief's filters and sorting (see [how they map](#filtering-and-sorting)).
4. `GET /stats`: calls per external API, performance buckets and each API's anomaly state.
5. `./e2e/run.sh` (or `./e2e/run.ps1`): the shipped image against WireMock, including an API going down and coming back (`stale`, then `ok`) and a circuit breaker opening and closing.
6. The code, in this order:
   - [CityReportService](src/Aggregator.Core/CityReports/CityReportService.cs) (orchestration);
   - [IExternalSource](src/Aggregator.Core/Sources/IExternalSource.cs) (the contract every API implements);
   - [CachingSource](src/Aggregator.Infrastructure/Caching/CachingSource.cs) and [StaleAwareCache](src/Aggregator.Infrastructure/Caching/StaleAwareCache.cs) (caching, fallback, cooldown, call budget);
   - `AddSource` in [InfrastructureServiceCollectionExtensions](src/Aggregator.Infrastructure/InfrastructureServiceCollectionExtensions.cs) (what every API gets by registration);
   - [StatisticsStore](src/Aggregator.Core/Statistics/StatisticsStore.cs) (lock-free statistics).
7. [docs/test-report.md](docs/test-report.md): every test grouped by the requirement it proves.

## Tests

```bash
docker build --target test --progress=plain .
```

Runs all unit and integration tests; the build fails if any test fails. No network access is needed: the external APIs are replaced by fake HTTP handlers and recorded responses. With the .NET 10 SDK installed, `dotnet test` runs the same tests (`global.json` pins the SDK and selects Microsoft.Testing.Platform). It also lists the live smoke tests, which skip unless they are switched on (see below). CI ([.github/workflows/ci.yml](.github/workflows/ci.yml)) runs these tests and the quick end-to-end scenarios on every push to `main` and every pull request.

### End-to-end tests

A separate suite runs **the shipped Docker image** against [WireMock](https://wiremock.org/) standing in for all five external APIs. The test runner is a third container that calls the API over the Docker network and changes WireMock's behaviour mid-scenario (failures, delays).

```powershell
./e2e/run.ps1                          # quick scenarios (~30 s)
./e2e/run.ps1 -Scope all               # + long scenarios (~5 min)
```
```bash
./e2e/run.sh            # quick
./e2e/run.sh all 60     # all, with a 60 s load phase
```

| Scenario | What it proves | Scope |
|---|---|---|
| Smoke | health; 401 without a token; with a token, 200 with geocoding, weather, air quality and marine `ok`; hazards `no_data`; 404; statistics for all five APIs | quick |
| Fallback lifecycle | weather goes down → `stale` with the last values → recovers → `ok` | quick |
| Circuit-breaker lifecycle | repeated marine failures open the circuit (calls stop reaching the API) → after the break a probe succeeds → closed | quick |
| Token expiry | a token works until its lifetime ends, then 401 "expired" | quick |
| Anomaly detection | latency jumps from ~1 ms to 700 ms → `/stats` shows weather `anomalous`, and the warning is logged exactly once | long |
| Sustained load | 20 clients for 60 s over 300 cities: all 200, p95 < 2 s, and `/stats` matches the number of calls WireMock received | long |

The e2e environment (`docker-compose.e2e.yml`) shortens timings so scenarios take seconds: 2 s cache for weather, air quality and marine, 2 s weather cooldown, 5 s marine circuit break, 12 s tokens, anomaly checks every 10 s over a 1-minute window.

### Live smoke tests

[tests/Aggregator.LiveTests](tests/Aggregator.LiveTests) runs the app against the **real** Open-Meteo and EONET APIs, to catch upstream changes that stubs cannot (a renamed field, a new date format). The tests check shapes and plausible ranges, never exact values. They skip unless `AGGREGATOR_LIVE_TESTS=1`. The Docker test stages and CI don't run them; [.github/workflows/live-smoke.yml](.github/workflows/live-smoke.yml) runs them on demand (`workflow_dispatch`):

```bash
docker build --target build -t aggregator-build .
docker run --rm -e AGGREGATOR_LIVE_TESTS=1 aggregator-build dotnet tests/Aggregator.LiveTests/bin/Release/net10.0/Aggregator.LiveTests.dll
```

### Test report

[docs/test-report.md](docs/test-report.md) lists every unit and integration test grouped by the requirement it proves (R1–R11, O1–O2), marked as a success or failure scenario, plus line and branch coverage. Code generated at build time (source-generated logging, the OpenAPI XML-comment code) is excluded from coverage. To regenerate it:

```bash
docker build --target test-report-export --output type=local,dest=docs .
```

Tests are tagged with `[Requirement("R6")]` and `[FailureScenario]`. [tools/TestReport.cs](tools/TestReport.cs) (a .NET 10 single-file app) turns the xUnit results into the report, and `dotnet-coverage` with ReportGenerator adds the coverage summary.

## Endpoints

### `GET /city-report/{country}/{city}`

| Parameter | Where | Values | Default |
|---|---|---|---|
| `country` | path | ISO 3166-1 alpha-2 (e.g. `GR`) | required |
| `city` | path | city or airport name, 1–100 characters | required |
| `include` | query | comma-separated: `weather`, `airQuality`, `marine`, `hazards`, `hourly`. `hazards` is opt-in (one more call per place). Only the APIs for the requested sections are called. Hourly rows come from the included sections (all three with `include=hourly` on its own) | `weather,airQuality,marine` |
| `region` | query | region name, fuzzy-matched (`krete` → Crete, `attiki` → Attica, `macedonia` → both Macedonias) | none |
| `sort` | query | `relevance`: best name match first (exact, then prefix, then contains, ignoring case and accents), then capital, administrative seat, town, airport, then the geocoder's order. Also `distance` (from the Acropolis, nearest first), `name`, `temperature`. Prefix `-` for descending | `relevance` |
| `limit` | query | how many places to return, 1–100. Applied after the region filter and the sort, **before** any weather, air-quality, marine or hazards call. With `sort=temperature`, weather is fetched for every place to rank them, and the other APIs only for the page | `20` |
| `offset` | query | places to skip, for paging; past the end returns an empty list | `0` |
| `hourly.category` | query | `weather`, `airQuality` or `marine`: narrows hourly rows to one section, which must also be in `include` (or `include=hourly` alone) | all included |
| `hourly.from`, `hourly.to` | query | ISO 8601 date-times. Bounds outside the 24 h window are clamped to it; a range that does not overlap it at all returns `400` | whole window |
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

(Shortened: `hourly` has one row per metric per hour, 48 rows here.)

A **degraded** response (`GET /city-report/GR/Piraeus?limit=1`): the weather API is down but its last good value is served, and the marine API is down with nothing cached. The request still succeeds:

```json
{
  "filters": { "totalPlaces": 1, "truncated": false },
  "degraded": true,
  "places": [
    {
      "city": "Piraeus",
      "country": "GR",
      "region": "Attica",
      "coordinates": { "latitude": 37.94745, "longitude": 23.63708 },
      "distanceKm": 8.2,
      "weather": { "temperature": 17.1, "windSpeed": 11.2 },
      "airQuality": { "usAqi": 38 },
      "sources": {
        "geocoding": { "status": "ok", "fetchedAt": "2026-11-15T09:12:40+00:00" },
        "weather": { "status": "stale", "fetchedAt": "2026-11-15T14:02:10+00:00" },
        "airQuality": { "status": "ok", "fetchedAt": "2026-11-15T16:01:55+00:00" },
        "marine": { "status": "unavailable" }
      }
    }
  ]
}
```

| Field | Meaning |
|---|---|
| `distanceKm` | distance from the Acropolis, Athens, in km; `sort=distance` orders by it |
| `weather` | current temperature (°C) and wind speed (km/h) |
| `airQuality.usAqi` | current US AQI (0–500 scale) |
| `marine` | current wave height (m) and sea surface temperature (°C); `sources.marine` is `no_data` for places away from the sea |
| `hazards` | open natural events (wildfires, storms, floods, volcanoes…) from the last 30 days within 150 km, nearest first: `title`, `category`, `date`, `distanceKm`. From NASA EONET, with `include=hazards`; `sources.hazards` is `no_data` when nothing is near |
| `hourly` | one row per metric per hour, for a fixed window of **24 hours starting at the current hour (UTC)** |
| `sources` | for each external API called for this place: `status` (see below) and `fetchedAt`, when its data was fetched from the API. A `stale` value keeps its original `fetchedAt`, so its age is visible; `unavailable` has none |
| `degraded` | `true` when any source of any returned place is `partial`, `stale` or `unavailable`; `no_data` is a valid answer, not a degradation |
| `filters` | how the region and hourly filters were applied (a region that matches nothing is **ignored and reported**, `applied: false`), plus `totalPlaces` (places after the region filter, before paging) and `truncated` (`limit` or `offset` left some of them out) |

**Per-source status:**
- `ok`
- `partial`: some values or hours are missing, e.g. an hourly row whose time could not be read
- `no_data`: a valid answer with nothing usable
- `stale`: the fresh call failed or ran out of time, so the last good value is served
- `unavailable`: it failed and there is no earlier value

| Status code | When |
|---|---|
| `200` | The places were found. Weather, air quality, marine or hazards may still be `no_data`, `stale` or `unavailable`, as shown in `sources`. |
| `400` | Invalid input (a validation problem naming the failing fields), e.g. `sort=temperature` without `include=weather`. |
| `401` / `403` | Missing or invalid token / token without the required role. |
| `404` | No city or airport with that name in that country. |
| `503` | Geocoding failed and no earlier answer for that city is cached: nothing else can run without the coordinates. |

All errors are RFC 9457 Problem Details.

#### Filtering and sorting

How the brief's examples (date, relevance, category) map to the parameters:

| Brief | Filter | Sort |
|---|---|---|
| date | `hourly.from`, `hourly.to` | `hourly.sort=time` / `-time` |
| relevance | — | `sort=relevance` (the default) |
| category | `hourly.category`; `include` chooses the sections | — |
| beyond the brief | `region`; `limit` and `offset` for paging | `sort=distance`, `name`, `temperature` |

### `GET /stats`

Total requests and average response time **per external API**, grouped in performance buckets (configurable; defaults fast < 100 ms, average 100–200 ms, slow > 200 ms). What is counted:
- Every HTTP attempt, retries included. Cache hits never reach the API, so they are not counted.
- A duration is measured until the response headers arrive.
- `failedRequests` counts attempts that threw (network error, attempt timeout) or got a 5xx, 408 or 429. Open-Meteo's 400 ("no data for this request") is an answer, not a failure.
- Every API is listed from startup. An average is `null` until there is something to average.

Each API also has its **anomaly state** (see below), computed when `/stats` is called by the same detector the background service uses.

```json
{
  "thresholds": { "fastBelowMs": 100, "slowAboveMs": 200 },
  "anomalyRules": { "windowMinutes": 5, "ratio": 1.5, "minimumIncreaseMs": 50, "minSamples": 5 },
  "apis": {
    "weather": {
      "totalRequests": 15, "failedRequests": 1, "averageMs": 140.22,
      "buckets": { "fast": { "count": 6, "averageMs": 63.97 }, "average": { "count": 5, "averageMs": 176.86 }, "slow": { "count": 4, "averageMs": 208.8 } },
      "anomaly": { "state": "normal", "recentRequests": 6, "recentAverageMs": 151.31, "baselineRequests": 9, "baselineAverageMs": 132.83 }
    }
  }
}
```

### `GET /health`

Anonymous liveness check.

### Performance anomaly logging

A background service runs every minute and, for each API, compares two averages:
- **recent:** the last **5 complete minutes** (the minute in progress is left out, because it is only partly over);
- **baseline:** every earlier attempt since the service started.

An API is **anomalous** when all of these hold:
- the recent average is more than **50 % above** the baseline average;
- it is also at least **50 ms** above it;
- both sides have at least **5 attempts**. With fewer, the state is `insufficient_data`.

Logging happens on changes only:
- a Warning (event 3001) once, when an API becomes anomalous;
- an Information line (event 3003) once, when it stops being anomalous; the line says whether it is back to `normal` or just has too little traffic to judge.

The 50 ms floor is our interpretation of the brief's "e.g. >50% bigger". A purely relative rule flags normal noise at low latencies: an end-to-end run logged 1.46 ms against 0.56 ms as an anomaly. `Anomaly:MinimumIncreaseMs` = 0 restores the purely relative rule. All values are configurable.

## Architecture

| Project | Responsibility |
|---|---|
| `Aggregator.Core` | Model, the source contract, orchestration (`CityReportService`), query logic (fuzzy region matching, relevance, sorting, hourly window), statistics and anomaly detection, and its own registration (`AddCore`). **Depends only on** the `Microsoft.Extensions` options and DI abstractions. |
| `Aggregator.Infrastructure` | Open-Meteo and NASA EONET adapters, HTTP clients and resilience, caching (fallback, cooldown, call budget), the statistics handler. |
| `Aggregator.Api` | Composition root: endpoints, contracts and mapping, validation, authentication, error handling, the background service, telemetry. |

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

Geocoding, weather, air quality, marine and hazards each implement it with their own typed model. Cross-cutting concerns attach to the contract, not to each source:

- **Caching:** one open-generic decorator for all sources (`services.Decorate(typeof(IExternalSource<,>), typeof(CachingSource<,>))`).
- **Resilience:** one standard resilience handler per typed `HttpClient`, configured per API from `appsettings.json`.
- **Statistics:** a `DelegatingHandler` inside the resilience pipeline measures every attempt.

Adding an API means writing an adapter and a model; caching, resilience, statistics and statuses come with the contract. The orchestrator calls the sources explicitly, so it reads like the dependency graph (geocoding first, then the per-place APIs in parallel).
- **Marine** was added this way: an endpoint definition on the shared Open-Meteo client, a model, one response property and one orchestrator line.
- **Hazards** proves the same contract with a second vendor. It has its own adapter, query type (`AreaQuery`), response shape and timeouts: EONET answers in 1–9 s, so it gets 10 s per attempt instead of 2 s. Caching, resilience, statistics, statuses and the cooldown come from the shared registration unchanged.

Adding a source also adds one property to the response. That trade-off is deliberate, in exchange for precise typed models and OpenAPI schema.

### Resilience and fallback

| Layer | Mechanism |
|---|---|
| Transient failures | `Microsoft.Extensions.Http.Resilience` standard handler per API: retries (exponential + jitter, honours `Retry-After`), attempt and total timeouts, circuit breaker. Open-Meteo: 3 retries, 2 s per attempt, 8 s total. EONET: 1 retry, 10 s per attempt, 20 s total. |
| Concurrency | At most **10 concurrent calls per API across all requests** (the handler's concurrency limiter, queue 200). Open-Meteo's air-quality API returns 429 above ~30 concurrent calls. **Within one request**, at most 5 places are processed at a time (`CityReport:MaxConcurrentPlaces`), so one request makes at most 5 concurrent calls per API. |
| Call budget | One source call, as the caller sees it, is limited to that API's total timeout (8 s / 20 s), **including** the wait for a limiter permit and the response body. See [Timeouts](#timeouts). |
| Fallback | If the fresh call fails or runs out of its budget, the last good value is served as `stale`, with its original `fetchedAt`. It is kept for 24 h after it stops being fresh. For hourly data, a value fetched for an earlier window can stand in, filtered to the requested hours. |
| Cooldown | After a failure, the same data (same API, place and request shape) is not requested again for 30 s; the last good value (or `unavailable`) is served meanwhile. This also covers broken payloads, which the circuit breaker never sees because the HTTP call itself succeeded. Running out of the call budget does **not** start a cooldown: the time may have gone on queueing behind our own traffic. |
| Partial results | One failing source never fails the request; its status says what happened. Only a geocoding failure (the dependency of everything else) with nothing cached returns `503`. |

```
a source call for (API, place, request shape)
  │
  ├─ fresh in the cache? ─────────── yes ──► the cached answer (ok / partial / no_data)
  │
  ├─ cooling down after a failure? ── yes ─────────────────────┐
  │                                                            │
  ├─ call the API (retries, timeouts, breaker; within the call budget)
  │     ├─ answer ──► cache it as fresh; if it has data, also keep it as the last good value
  │     │             ──► ok / partial / no_data
  │     └─ failure, or the budget runs out ────────────────────┤
  │                                                            ▼
  │                         a last good value (≤ 24 h after it stopped being fresh)?
  │                              ├─ yes ──► stale, with its original fetchedAt
  │                              └─ no  ──► unavailable
```

A `partial` answer never replaces a complete last good value of the same query.

#### Timeouts

| Timeout | Default (Open-Meteo / EONET) | Covers | When it runs out |
|---|---|---|---|
| Attempt (`AttemptTimeout`) | 2 s / 10 s | one HTTP attempt, until the response headers | the attempt fails and is retried |
| Total (`TotalRequestTimeout`) | 8 s / 20 s | all attempts and the delays between them, until the last attempt's headers. **Not** the wait for a limiter permit: the limiter runs before it | the call fails: cooldown, last good value |
| Call budget (the same value as Total) | 8 s / 20 s | one source call: the wait for a permit, every attempt, and the response body | the caller gets the last good value or `unavailable`, with no cooldown. The budget is also set as `HttpClient.Timeout`, which ends the HTTP call itself (a failure, so cooldown) even when no caller is waiting any more |

There is no deadline for a whole request; its duration is bounded through the call budgets. The worst case with the defaults, when every call uses up its budget:
- geocoding takes up to 8 s;
- the page is processed 5 places at a time, each round up to 8 s, or 20 s with `include=hazards`. The default page of 20 places is 4 rounds, so up to 32 s (80 s with hazards);
- with `sort=temperature`, the weather of every matching place (up to 100) comes first, 5 at a time, up to 8 s per round.

Cached answers take none of this time.

### Caching

`HybridCache` caches **per external API and per location**, never per aggregated response: an aggregate would freeze one source's failure for everyone. It is in memory, stampede-protected (concurrent requests for the same data share one call), and limited to 100 MB by default (`Cache:SizeLimitMegabytes`). Coordinates are rounded to 2 decimals (~1 km, smaller than Open-Meteo's grid) for both the cache key and the request.

| API | Fresh for | Why |
|---|---|---|
| Geocoding | 24 h (a city that is not found: 10 min) | Coordinates don't change; Open-Meteo sends `max-age=86400`. A not-found result is kept briefly so a typo is cheap but a newly listed place appears soon. |
| Current weather | 15 min | The API's current value advances every 15 min. |
| Current marine | 15 min | Like weather, the current value advances every 15 min. |
| Hazards | 45 min | Events change slowly; EONET is slow (1–9 s), so cache hits matter. |
| Current air quality / hourly data | 1 h | Hourly values; forecasts change only with new model runs (6–24 h). |

A request for current and hourly data together is cached for the shorter of the two (15 min for weather and marine). `no_data` answers are cached like any other answer, but only answers with data become the last good value.

### Security

The API only **validates** tokens:
- RS256 signature, issuer, audience and lifetime;
- a `reader` role policy on `/city-report` and `/stats`. The role is read from the `roles` claim, the array Microsoft Entra ID issues; the mock issues the same shape;
- anything else requires an authenticated user by default (the fallback policy), apart from the anonymous routes listed under [Get a token](#get-a-token).

The mock issuer exists only for the demo. It is off unless `MockAuth:Enabled` is `true`, and it signs with an RSA key generated in memory at startup, so there are no secrets to manage (and its tokens stop working after a restart).

**With a real identity provider** (e.g. Microsoft Entra ID) no code changes; set:

| Setting | Value |
|---|---|
| `MockAuth:Enabled` | `false` (the default) |
| `Auth:Authority` | the provider's https URL, e.g. `https://login.microsoftonline.com/{tenant-id}/v2.0`. The signing keys are discovered from its OpenID Connect metadata; startup fails without it when the mock is off |
| `Auth:Issuer` | the tokens' `iss`. The issuer from the provider's metadata is accepted as well |
| `Auth:Audience` | the API's application ID URI or client ID, as in the tokens' `aud` |
| `Auth:RequiredRole` | the app role that grants access, e.g. `Reports.Read` |
| `Auth:ClockSkew` | tolerance for token lifetimes (default 5 s) |

An integration test runs this path against a fake provider that serves OpenID Connect discovery and signing keys. An Entra-shaped token is accepted; a wrong audience, a token signed with the mock key, or an RS512 token gets `401`.

### Telemetry

Off unless `Telemetry:OtlpEndpoint` is set (for example `Telemetry__OtlpEndpoint=http://collector:4317`). Then OpenTelemetry exports, over OTLP:
- ASP.NET Core and `HttpClient` traces and metrics;
- the `aggregator.external_api.duration` histogram.

HybridCache runs a shared fetch without the caller's context, so each cache fetch starts a `fetch {source}` span parented to the request. One trace therefore shows the request, every fetch and every HTTP call to the external APIs. When callers share a fetch, it appears in the trace of the request that started it.

## Configuration

Everything is in `src/Aggregator.Api/appsettings.json` and can be overridden with environment variables (`Section__Key`), as `docker-compose.yml` does. Options are validated at startup; an invalid value stops the app.

Each HTTP client has its own full `Resilience` block, even where the values are currently the same. This is deliberate: every external API can be tuned independently without code changes. Open-Meteo's air-quality API, for example, rejects more than ~30 concurrent calls, while the forecast API accepts more.

| Section | Settings |
|---|---|
| `HttpClients:{Geocoding,Weather,AirQuality,Marine,Hazards}` | `BaseUrl`, `CacheTtl`, `HourlyCacheTtl`, `NoDataCacheTtl`, `LastGoodRetention`, `FailureCooldown`, `Resilience` (retry, timeouts, circuit breaker, concurrency limiter). `Resilience:TotalRequestTimeout` also sets that API's call budget |
| `Cache` | `SizeLimitMegabytes` (100): the in-memory cache's size limit; HybridCache sizes each entry by its serialized length |
| `Geocoding` | `Count` (100), `Language`, `AllowedFeatureCodes` (settlements `PPL`, `PPLA`–`PPLA4`, `PPLC` and airports `AIRP`) |
| `Hazards` | `RadiusKm` (150), `Days` (30: EONET leaves some events "open" for years), `Limit` (50 events per EONET call) |
| `CityReport` | `CoordinateDecimals` (2, at most 4), `HourlyWindowHours` (24), `MaxCityLength`, `MaxRegionLength`, `DefaultPlaceLimit` (20), `MaxPlaceLimit` (100), `MaxConcurrentPlaces` (5) |
| `Distance` | reference point for `distanceKm` (the Acropolis) |
| `RegionMatching` | `MaxEditDistanceRatio` (0.3) |
| `Statistics` | `FastBelowMs` (100), `SlowAboveMs` (200), `RecentMinutesKept` (10; at least `Anomaly:WindowMinutes` + 1) |
| `Anomaly` | `Interval`, `WindowMinutes`, `Ratio`, `MinSamples`, `MinimumIncreaseMs` |
| `Auth`, `MockAuth` | issuer, audience, required role, clock skew, authority; mock on/off and token lifetime |
| `OpenApi` | `EnableUi` |
| `Telemetry` | `OtlpEndpoint` (none): when set, traces and metrics are exported over OTLP (gRPC) |

## Assumptions and decisions

- **"Aggregated data"** is a composite object: each place combines geocoding, weather, air quality, marine and (on request) hazards data, and each nested list has its own filters and sorting.
- **Ambiguous names:** all matches in the country are returned (settlements and airports only), best match first (see `relevance`). `country` is mandatory.
- **Hourly data** covers a fixed 24 h window from the current hour (UTC), so responses stay bounded and every API always covers it.
- **Provider limits never become our API contract:** an adapter that receives less than requested reports `partial` or `no_data` instead.
- **Bad upstream data:** a hourly row whose time can't be read is skipped (`partial`). If no time can be read at all, the response is treated as a failure, so the last good value is served instead of an empty answer.
- **Two vendors.** Four APIs come from Open-Meteo (separate APIs on separate hosts with separate rate limits). The fifth, NASA EONET, is a different vendor, to prove the extension point beyond one provider's client and response shape.
- **One cache entry per request shape.** Current-only, hourly-only and current + hourly requests for a place are cached separately, each with its natural lifetime. One combined entry for every shape would force the shorter 15-minute lifetime on hourly data and refetch it four times as often.

### Decisions not taken

- **Caching whole responses.** One source's failure would be frozen into the aggregate for every caller until it expired. Caching each source separately lets a recovered API show up on the next request.
- **One flat list of items across all APIs.** A unified feed would need a lowest-common-denominator item shape. Grouping by place keeps typed models, units and a status per source.
- **Turning sources on and off with feature flags.** Not needed for this scope: callers choose sections with `include`, and only the APIs those sections need are called.
- **Per-API bucket thresholds.** One set of thresholds for all APIs (the brief's 100 / 200 ms, configurable). EONET, which answers in seconds, therefore lands almost entirely in `slow`; the anomaly detector compares each API with its own history, so it is not affected.
- **Inbound rate limiting.** There is no measured baseline of real traffic to size a limit from, and a guessed limit would either block legitimate users or protect nothing. Instead, each request's upstream cost is bounded (`limit`, `MaxConcurrentPlaces`, the per-API concurrency limiter, the cache). ASP.NET Core's built-in rate limiter is the next step once real usage is known.
- **A deadline for the whole request.** It would cap the total response time, but cut off later places mid-flight. Per-call budgets keep each source's answer independent; the cost is a looser bound on the total (see [Timeouts](#timeouts)).

## Known limitations

- **One instance.** The cache, the statistics, the anomaly state and the cooldowns live in the process's memory. Several instances would each keep their own; nothing is shared and everything resets on restart.
- **The anomaly baseline grows from startup.** Shortly after a start it rests on few requests; after a long run, a slow drift barely moves it.
- **A stale hourly value can have fewer rows.** One fetched for an earlier window is filtered to the requested hours, so the hours after its window are missing. Its status is `stale`, and `fetchedAt` shows its age.
- **Cooldown can outlast a recovery.** For up to 30 s after a failure, that data is served from the last good value (or `unavailable`) even if the API has recovered meanwhile.
- **Hazards are approximate.** EONET returns at most 50 events per call for a box around the place, before the 150 km radius filter, so a busy area could be cut short. Each event is placed at its latest reported position, and an area at the average of its outline.
- **Geocoding returns at most 100 results** (Open-Meteo's maximum), filtered to settlements and airports in the country.
- **Distances are measured from the Acropolis** (configurable), a choice aimed at Greece.
- **Units are Open-Meteo's defaults** (°C, km/h, m); there is no unit parameter.
- **Unknown paths answer `401`, not `404`, to anonymous callers**, because the fallback authorization policy runs first.
- **A shared fetch belongs to one trace.** When concurrent requests share a cache fetch, its spans appear only in the trace of the request that started it.

## Possible extensions

- A distributed L2 cache (Redis) behind `HybridCache`, so several instances share the cache.
- Batching cache misses into Open-Meteo's multi-location requests for very large result sets.
- Inbound rate limiting, once real traffic shows what to limit.

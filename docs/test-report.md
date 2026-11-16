# Test report

Generated 2026-11-16 21:01 UTC from the xUnit results of the Docker `test-report` stage.

**370 tests · 370 passed · 0 failed · 0 skipped · 9.3 s** ✅

| Test project | Tests | Passed | Failed | Time |
|---|---|---|---|---|
| Aggregator.UnitTests | 266 | 266 | 0 | 1.3 s |
| Aggregator.IntegrationTests | 104 | 104 | 0 | 8.1 s |

Each test is tagged with the requirement(s) it proves and whether it emulates a success or a failure scenario. A test with several data rows (a theory) counts as one scenario.

The end-to-end suite (`tests/Aggregator.EndToEndTests`) runs the Docker image against WireMock and is reported separately by `e2e/run.ps1` and `e2e/run.sh`; it is not part of these numbers.

## Coverage by requirement

| Requirement | | Success scenarios | Failure scenarios | Result |
|---|---|---|---|---|
| **R1** | .NET service using ASP.NET Core | 0 | 0 | every integration test hosts the ASP.NET Core app (104 tests) |
| **R2** | Architecture that makes integrating new APIs easy | 18 | 20 | ✅ |
| **R3** | Data from at least three external APIs, simultaneously | 2 | 0 | ✅ |
| **R4** | Endpoint returning the aggregated data | 18 | 4 | ✅ |
| **R5** | Filtering and sorting (date, relevance, category) | 60 | 23 | ✅ |
| **R6** | Error handling, transient failures, fallback | 4 | 93 | ✅ |
| **R7** | Unit tests | 0 | 0 | this report |
| **R8** | Documentation | 8 | 0 | ✅ |
| **R9** | Caching | 30 | 29 | ✅ |
| **R10** | Parallelism | 3 | 1 | ✅ |
| **R11** | Request statistics with performance buckets | 17 | 5 | ✅ |
| **O1** | JWT bearer authentication | 12 | 10 | ✅ |
| **O2** | Performance anomaly logging (background service) | 10 | 3 | ✅ |

## R2: Architecture that makes integrating new APIs easy

| Scenario | Type | Result | Test |
|---|---|---|---|
| By default → hazards are not called | success | ✅ | `HazardsApiTests` |
| Hazards calls → are counted in the statistics | success | ✅ | `HazardsApiTests` |
| Include hazards → returns nearby events from the second vendor → nearest first | success | ✅ | `HazardsApiTests` |
| Every external source → is resolved as the caching decorator around its adapter | success | ✅ | `StartupTests` |
| Area query → key follows the rounded centre | success | ✅ | `HazardsSourceTests` |
| Build uri → asks for recent open events → in a box west north east south | success | ✅ | `HazardsSourceTests` |
| Fetch → keeps events within the radius → nearest first → at their latest position | success | ✅ | `HazardsSourceTests` |
| Fetch → polygon → is placed at the average of its outer ring | success | ✅ | `HazardsSourceTests` |
| Fetch → keeps settlements and airports only | success | ✅ | `GeocodingSourceTests` |
| Fetch → sends name country count and language | success | ✅ | `GeocodingSourceTests` |
| Air quality → current and hourly → parses us aqi | success | ✅ | `LocationSourceTests` |
| Marine → coast → parses waves and sea temperature | success | ✅ | `LocationSourceTests` |
| Weather → current and hourly → parses everything | success | ✅ | `LocationSourceTests` |
| Weather → current only → does not request hourly | success | ✅ | `LocationSourceTests` |
| Weather → sends rounded coordinates and absolute hours | success | ✅ | `LocationSourceTests` |
| Build uri → requests only what the query needs (3 cases) | success | ✅ | `OpenMeteoLocationClientTests` |
| Fetch → complete current and hourly → returns ok | success | ✅ | `OpenMeteoLocationClientTests` |
| Fetch → hourly units from response → override defaults | success | ✅ | `OpenMeteoLocationClientTests` |
| Hazards down → only hazards are unavailable | failure | ✅ | `HazardsApiTests` |
| Fetch → malformed events → are skipped → not fatal | failure | ✅ | `HazardsSourceTests` |
| Fetch → no events nearby → is no data | failure | ✅ | `HazardsSourceTests` |
| Fetch → server error or broken payload → is unavailable (3 cases) | failure | ✅ | `HazardsSourceTests` |
| Fetch → network failure → returns unavailable | failure | ✅ | `GeocodingSourceTests` |
| Fetch → no results key → returns no data | failure | ✅ | `GeocodingSourceTests` |
| Fetch → only results without coordinates → returns no data | failure | ✅ | `GeocodingSourceTests` |
| Fetch → results without coordinates → are skipped and logged | failure | ✅ | `GeocodingSourceTests` |
| Fetch → server error → returns unavailable | failure | ✅ | `GeocodingSourceTests` |
| Air quality → no usable values → returns no data | failure | ✅ | `LocationSourceTests` |
| Marine → inland → returns no data | failure | ✅ | `LocationSourceTests` |
| Weather → bad request → returns no data | failure | ✅ | `LocationSourceTests` |
| Weather → missing hourly rows → returns partial | failure | ✅ | `LocationSourceTests` |
| Weather → server error → returns unavailable | failure | ✅ | `LocationSourceTests` |
| Fetch → bad request → returns no data | failure | ✅ | `OpenMeteoLocationClientTests` |
| Fetch → malformed json → returns unavailable | failure | ✅ | `OpenMeteoLocationClientTests` |
| Fetch → missing current field → with hourly → returns partial | failure | ✅ | `OpenMeteoLocationClientTests` |
| Fetch → missing hourly rows → returns partial | failure | ✅ | `OpenMeteoLocationClientTests` |
| Fetch → nothing usable → returns no data | failure | ✅ | `OpenMeteoLocationClientTests` |
| Fetch → server error → returns unavailable | failure | ✅ | `OpenMeteoLocationClientTests` |

## R3: Data from at least three external APIs, simultaneously

| Scenario | Type | Result | Test |
|---|---|---|---|
| Get → starts every location call before any completes | success | ✅ | `CityReportServiceConcurrencyTests` |
| Get → starts no location call before geocoding completes | success | ✅ | `CityReportServiceConcurrencyTests` |

## R4: Endpoint returning the aggregated data

| Scenario | Type | Result | Test |
|---|---|---|---|
| City report → default include → returns weather air quality and marine but no hourly | success | ✅ | `CityReportApiTests` |
| City report → lower case country → is accepted | success | ✅ | `CityReportApiTests` |
| City report → valid request → aggregates all sources per place | success | ✅ | `CityReportApiTests` |
| City report → with marine → returns marine values and only marine hourly rows | success | ✅ | `CityReportApiTests` |
| By default → hazards are not called | success | ✅ | `HazardsApiTests` |
| Hazards calls → are counted in the statistics | success | ✅ | `HazardsApiTests` |
| Include hazards → returns nearby events from the second vendor → nearest first | success | ✅ | `HazardsApiTests` |
| By default → returns the configured limit → and calls the location apis only for those | success | ✅ | `PagingApiTests` |
| Limit and offset → return the matching slice of the full order | success | ✅ | `PagingApiTests` |
| Offset past the end → returns 200 with no places | success | ✅ | `PagingApiTests` |
| Sort by temperature with a limit → ranks every place → but calls the other apis only for the page | success | ✅ | `PagingApiTests` |
| Parse → limit and offset → are applied | success | ✅ | `CityReportQueryParserTests` |
| Parse → no paging → uses the configured default limit from the start | success | ✅ | `CityReportQueryParserTests` |
| Get → default request → calls every location source per place without hourly | success | ✅ | `CityReportServiceTests` |
| Get → marine → maps current values per place | success | ✅ | `CityReportServiceTests` |
| Get → offset past the end → returns no places but reports the total | success | ✅ | `CityReportServiceTests` |
| Get → page covering every place → is not truncated | success | ✅ | `CityReportServiceTests` |
| Get → page → fetches location data only for the places returned | success | ✅ | `CityReportServiceTests` |
| City report → geocoding down without cache → returns 503 problem details | failure | ✅ | `CityReportApiTests` |
| City report → unknown city → returns 404 problem details | failure | ✅ | `CityReportApiTests` |
| Hazards down → only hazards are unavailable | failure | ✅ | `HazardsApiTests` |
| Invalid paging → returns 400 (3 cases) | failure | ✅ | `PagingApiTests` |

## R5: Filtering and sorting (date, relevance, category)

| Scenario | Type | Result | Test |
|---|---|---|---|
| City report → hourly follows include → only included sections produce rows | success | ✅ | `CityReportApiTests` |
| City report → region sort and hourly category → are applied end to end | success | ✅ | `CityReportApiTests` |
| Sort by temperature with a limit → ranks every place → but calls the other apis only for the page | success | ✅ | `PagingApiTests` |
| Default sort → puts the capital named paris first | success | ✅ | `RelevanceApiTests` |
| Distance sort → keeps the old nearest to the acropolis order | success | ✅ | `RelevanceApiTests` |
| Hourly bounds → are clamped and reported in filters | success | ✅ | `SourceStatusApiTests` |
| Parse → defaults → include every data section sorted by relevance | success | ✅ | `CityReportQueryParserTests` |
| Parse → empty hourly parameters → do not count as present | success | ✅ | `CityReportQueryParserTests` |
| Parse → full query → maps every option | success | ✅ | `CityReportQueryParserTests` |
| Parse → hourly category in include or hourly on its own → is accepted (3 cases) | success | ✅ | `CityReportQueryParserTests` |
| Parse → iso timestamp → is accepted as utc (7 cases) | success | ✅ | `CityReportQueryParserTests` |
| Parse → limit and offset → are applied | success | ✅ | `CityReportQueryParserTests` |
| Parse → no paging → uses the configured default limit from the start | success | ✅ | `CityReportQueryParserTests` |
| Parse → sort with optional dash → sets the direction (2 cases) | success | ✅ | `CityReportQueryParserTests` |
| Get → default sort → is exact name first then prefix then the rest | success | ✅ | `CityReportServiceTests` |
| Get → default sort → puts the best name match first → not the nearest place | success | ✅ | `CityReportServiceTests` |
| Get → hourly category → restricts hourly rows to that section | success | ✅ | `CityReportServiceTests` |
| Get → hourly marine only → calls only marine for hourly rows | success | ✅ | `CityReportServiceTests` |
| Get → hourly only → fetches hourly from every section | success | ✅ | `CityReportServiceTests` |
| Get → hourly with sections → fetches hourly only for included sections | success | ✅ | `CityReportServiceTests` |
| Get → hourly → merges filters and sorts rows | success | ✅ | `CityReportServiceTests` |
| Get → region matches → filters places before calling sources | success | ✅ | `CityReportServiceTests` |
| Get → sort by temperature descending → puts missing temperatures last | success | ✅ | `CityReportServiceTests` |
| Get → sort by temperature with a limit → ranks every place but fetches the rest only for the page | success | ✅ | `CityReportServiceTests` |
| Get → weather not included → does not call weather | success | ✅ | `CityReportServiceTests` |
| Apply → descending → is latest first | success | ✅ | `HourlyFilterTests` |
| Apply → keeps rows within inclusive bounds | success | ✅ | `HourlyFilterTests` |
| Apply → no rows → returns empty | success | ✅ | `HourlyFilterTests` |
| Apply → same hour → is ordered by category then metric | success | ✅ | `HourlyFilterTests` |
| Overlaps → no bounds → is true | success | ✅ | `HourlyFilterTests` |
| Overlaps → range touching or inside the window → is true (4 cases) | success | ✅ | `HourlyFilterTests` |
| Resolve range → bounds inside window → are kept | success | ✅ | `HourlyFilterTests` |
| Resolve range → no bounds → covers the whole window | success | ✅ | `HourlyFilterTests` |
| Resolve range → offset bounds → are normalized to utc | success | ✅ | `HourlyFilterTests` |
| Sort → distance descending → is farthest first | success | ✅ | `PlaceSorterTests` |
| Sort → distance → is nearest first | success | ✅ | `PlaceSorterTests` |
| Sort → empty → returns empty | success | ✅ | `PlaceSorterTests` |
| Sort → name descending → is reverse alphabetical | success | ✅ | `PlaceSorterTests` |
| Sort → name ties → are ordered by country then region | success | ✅ | `PlaceSorterTests` |
| Sort → name → does not depend on the current culture | success | ✅ | `PlaceSorterTests` |
| Sort → name → is alphabetical ignoring case | success | ✅ | `PlaceSorterTests` |
| Sort → relevance → is best match rank first → and ties keep the incoming order | success | ✅ | `PlaceSorterTests` |
| Sort → temperature ascending → puts missing temperatures last | success | ✅ | `PlaceSorterTests` |
| Sort → temperature descending → puts missing temperatures last | success | ✅ | `PlaceSorterTests` |
| Sort → temperature ties → are ordered by distance | success | ✅ | `PlaceSorterTests` |
| Includes hourly for → follows include or everything when hourly is alone (4 cases) | success | ✅ | `ReportSectionsExtensionsTests` |
| To section → maps every category (3 cases) | success | ✅ | `ReportSectionsExtensionsTests` |
| Contains → includes start and excludes end | success | ✅ | `HourlyWindowTests` |
| Starting at current hour → truncates to the hour in utc | success | ✅ | `HourlyWindowTests` |
| Distance km → closer place → is smaller | success | ✅ | `DistanceCalculatorTests` |
| Distance km → reference point → is zero | success | ✅ | `DistanceCalculatorTests` |
| Distance km → thessaloniki → is about three hundred kilometres | success | ✅ | `DistanceCalculatorTests` |
| Rank → exact name beats prefix beats contains beats no match → whatever the type | success | ✅ | `PlaceRankerTests` |
| Rank → name match → ignores case accents and surrounding spaces (3 cases) | success | ✅ | `PlaceRankerTests` |
| Rank → same name match → orders capital then seats by level then town then airport | success | ✅ | `PlaceRankerTests` |
| Levenshtein distance → returns edit count (4 cases) | success | ✅ | `RegionMatcherTests` |
| Match → exact match → wins over contains | success | ✅ | `RegionMatcherTests` |
| Match → partial name → returns every containing region | success | ✅ | `RegionMatcherTests` |
| Match → single region → returns it (5 cases) | success | ✅ | `RegionMatcherTests` |
| Normalize → strips diacritics and case | success | ✅ | `RegionMatcherTests` |
| City report → hourly category not included → returns 400 | failure | ✅ | `CityReportApiTests` |
| City report → invalid query → returns 400 validation problem | failure | ✅ | `CityReportApiTests` |
| Hourly range outside the window → returns 400 naming the window → without calling any api (2 cases) | failure | ✅ | `SourceStatusApiTests` |
| Parse → city longer than configured limit → is rejected | failure | ✅ | `CityReportQueryParserTests` |
| Parse → hourly category not in include → is rejected (2 cases) | failure | ✅ | `CityReportQueryParserTests` |
| Parse → hourly parameters without hourly → are rejected | failure | ✅ | `CityReportQueryParserTests` |
| Parse → include without sections → is rejected (2 cases) | failure | ✅ | `CityReportQueryParserTests` |
| Parse → invalid hourly options → reports the field (5 cases) | failure | ✅ | `CityReportQueryParserTests` |
| Parse → invalid include → reports only include not the checks that depend on it | failure | ✅ | `CityReportQueryParserTests` |
| Parse → invalid include → still validates independent values | failure | ✅ | `CityReportQueryParserTests` |
| Parse → invalid input → reports the field (7 cases) | failure | ✅ | `CityReportQueryParserTests` |
| Parse → invalid paging → reports the field (7 cases) | failure | ✅ | `CityReportQueryParserTests` |
| Parse → non iso timestamp → is rejected (4 cases) | failure | ✅ | `CityReportQueryParserTests` |
| Parse → region longer than configured limit → is rejected | failure | ✅ | `CityReportQueryParserTests` |
| Parse → several problems → reports all of them | failure | ✅ | `CityReportQueryParserTests` |
| Parse → sort with more than one leading dash → is rejected (3 cases) | failure | ✅ | `CityReportQueryParserTests` |
| Get → hourly range outside the window → is rejected before any source is called | failure | ✅ | `CityReportServiceTests` |
| Get → hourly range outside window → is clamped and reported | failure | ✅ | `CityReportServiceTests` |
| Get → region does not match → ignores filter and reports it | failure | ✅ | `CityReportServiceTests` |
| Overlaps → one bound only and outside → is false | failure | ✅ | `HourlyFilterTests` |
| Overlaps → range entirely outside the window → is false (4 cases) | failure | ✅ | `HourlyFilterTests` |
| Resolve range → bounds outside window → are clamped and reported | failure | ✅ | `HourlyFilterTests` |
| Match → nothing close → is not applied | failure | ✅ | `RegionMatcherTests` |

## R6: Error handling, transient failures, fallback

| Scenario | Type | Result | Test |
|---|---|---|---|
| Fresh report → has a fetch time per source → and is not degraded | success | ✅ | `DataAgeApiTests` |
| No data is an answer → not a degradation | success | ✅ | `DataAgeApiTests` |
| Concurrency limit → never exceeds the permits per api | success | ✅ | `ResilienceTests` |
| Hourly bounds → are clamped and reported in filters | success | ✅ | `SourceStatusApiTests` |
| One source fails → the others keep serving their own cached entries | failure | ✅ | `CacheBehaviourTests` |
| Failing api → is not called again during the cooldown | failure | ✅ | `CacheLimitApiTests` |
| Simultaneous identical requests while an api is down → do not flood it | failure | ✅ | `CacheStampedeTests` |
| City report → geocoding down without cache → returns 503 problem details | failure | ✅ | `CityReportApiTests` |
| City report → hourly category not included → returns 400 | failure | ✅ | `CityReportApiTests` |
| City report → invalid query → returns 400 validation problem | failure | ✅ | `CityReportApiTests` |
| City report → one source down → returns 200 with its status and others intact (2 cases) | failure | ✅ | `CityReportApiTests` |
| City report → unknown city → returns 404 problem details | failure | ✅ | `CityReportApiTests` |
| Stale source → shows when its data was fetched → and the report is degraded | failure | ✅ | `DataAgeApiTests` |
| Unavailable source → has no fetch time → and the report is degraded | failure | ✅ | `DataAgeApiTests` |
| Malformed request body → returns 400 problem details | failure | ✅ | `ErrorHandlingTests` |
| Malformed request body → when binding throws → is still 400 not a 500 | failure | ✅ | `ErrorHandlingTests` |
| Unexpected exception → returns 500 problem details without internals | failure | ✅ | `ErrorHandlingTests` |
| Hazards down → only hazards are unavailable | failure | ✅ | `HazardsApiTests` |
| Invalid paging → returns 400 (3 cases) | failure | ✅ | `PagingApiTests` |
| Concurrency queue full → rejected places are unavailable but the request succeeds | failure | ✅ | `ResilienceTests` |
| Geocoding down with cached coordinates → returns 200 with geocoding stale | failure | ✅ | `ResilienceTests` |
| Repeated failures → open the circuit → calls for another place never reach the api | failure | ✅ | `ResilienceTests` |
| Response slower than attempt timeout → is retried then unavailable | failure | ✅ | `ResilienceTests` |
| Source down after expiry → serves last good value as stale | failure | ✅ | `ResilienceTests` |
| Too many requests with retry after → waits the requested time then recovers | failure | ✅ | `ResilienceTests` |
| Transient failure then success → is retried and reported ok | failure | ✅ | `ResilienceTests` |
| Answer without usable values → is no data → and the other sources are unaffected | failure | ✅ | `SourceStatusApiTests` |
| Hourly range outside the window → returns 400 naming the window → without calling any api (2 cases) | failure | ✅ | `SourceStatusApiTests` |
| Incomplete current values → are partial → and the hourly rows are kept | failure | ✅ | `SourceStatusApiTests` |
| Malformed value after expiry → serves the last good value as stale | failure | ✅ | `SourceStatusApiTests` |
| Malformed value from one source → degrades only that source | failure | ✅ | `SourceStatusApiTests` |
| Upstream bad request → is called once without retry → and reported as no data | failure | ✅ | `SourceStatusApiTests` |
| Invalid configuration → stops the app at startup (7 cases) | failure | ✅ | `StartupTests` |
| Server errors → count as failed → but an upstream 400 does not | failure | ✅ | `StatisticsApiTests` |
| Parse → city longer than configured limit → is rejected | failure | ✅ | `CityReportQueryParserTests` |
| Parse → hourly category not in include → is rejected (2 cases) | failure | ✅ | `CityReportQueryParserTests` |
| Parse → hourly parameters without hourly → are rejected | failure | ✅ | `CityReportQueryParserTests` |
| Parse → include without sections → is rejected (2 cases) | failure | ✅ | `CityReportQueryParserTests` |
| Parse → invalid hourly options → reports the field (5 cases) | failure | ✅ | `CityReportQueryParserTests` |
| Parse → invalid include → reports only include not the checks that depend on it | failure | ✅ | `CityReportQueryParserTests` |
| Parse → invalid include → still validates independent values | failure | ✅ | `CityReportQueryParserTests` |
| Parse → invalid input → reports the field (7 cases) | failure | ✅ | `CityReportQueryParserTests` |
| Parse → invalid paging → reports the field (7 cases) | failure | ✅ | `CityReportQueryParserTests` |
| Parse → non iso timestamp → is rejected (4 cases) | failure | ✅ | `CityReportQueryParserTests` |
| Parse → region longer than configured limit → is rejected | failure | ✅ | `CityReportQueryParserTests` |
| Parse → several problems → reports all of them | failure | ✅ | `CityReportQueryParserTests` |
| Parse → sort with more than one leading dash → is rejected (3 cases) | failure | ✅ | `CityReportQueryParserTests` |
| After a failure → the source is not called during the cooldown → and the last good value is served | failure | ✅ | `CacheLimitTests` |
| Cooldown → is not extended by the requests it turns away | failure | ✅ | `CacheLimitTests` |
| Unexpected exception → also starts a cooldown | failure | ✅ | `CacheLimitTests` |
| Without a cooldown → every request calls the source | failure | ✅ | `CacheLimitTests` |
| Full weather graph → survives the last good path through the shared store | failure | ✅ | `CacheRoundTripTests` |
| Serving stale → does not extend the last good retention | failure | ✅ | `CacheRoundTripTests` |
| Caller cancels during the fetch → cancellation propagates and nothing is cached | failure | ✅ | `StaleAwareCacheFailureTests` |
| Caller cancels while the source throws → cancellation still propagates | failure | ✅ | `StaleAwareCacheFailureTests` |
| Expected unavailability → is not logged as an error | failure | ✅ | `StaleAwareCacheFailureTests` |
| Last good past its retention → is not served | failure | ✅ | `StaleAwareCacheFailureTests` |
| Partial result → is kept as last good and served stale with its data | failure | ✅ | `StaleAwareCacheFailureTests` |
| Source recovers → fresh value replaces stale and becomes the new last good | failure | ✅ | `StaleAwareCacheFailureTests` |
| Stampede → one waiting caller cancels → the others still get the value | failure | ✅ | `StaleAwareCacheFailureTests` |
| Stampede → while the source fails with history → one upstream call and every caller gets stale | failure | ✅ | `StaleAwareCacheFailureTests` |
| Stampede → while the source fails → one upstream call and every caller gets unavailable | failure | ✅ | `StaleAwareCacheFailureTests` |
| Unexpected exception in the source → serves the last good value and logs an error | failure | ✅ | `StaleAwareCacheFailureTests` |
| Unexpected exception without history → is unavailable and not cached | failure | ✅ | `StaleAwareCacheFailureTests` |
| While serving stale → every request still tries the source | failure | ✅ | `StaleAwareCacheFailureTests` |
| Get → failure after expired no data → returns unavailable not stale | failure | ✅ | `StaleAwareCacheTests` |
| Get → failure after expiry → serves last good as stale | failure | ✅ | `StaleAwareCacheTests` |
| Get → stale value → keeps the time it was originally fetched | failure | ✅ | `StaleAwareCacheTests` |
| Get → unavailable without history → is not cached and returns unavailable | failure | ✅ | `StaleAwareCacheTests` |
| Get → unavailable → has no fetch time | failure | ✅ | `StaleAwareCacheTests` |
| Get → geocoding unavailable → returns geocoding unavailable | failure | ✅ | `CityReportServiceTests` |
| Get → marine no data inland → keeps other sections and reports no data | failure | ✅ | `CityReportServiceTests` |
| Get → no places → returns city not found | failure | ✅ | `CityReportServiceTests` |
| Get → stale geocoding → still builds report and reports status | failure | ✅ | `CityReportServiceTests` |
| Fetch → malformed events → are skipped → not fatal | failure | ✅ | `HazardsSourceTests` |
| Fetch → no events nearby → is no data | failure | ✅ | `HazardsSourceTests` |
| Fetch → server error or broken payload → is unavailable (3 cases) | failure | ✅ | `HazardsSourceTests` |
| Network failure → is recorded as failed and rethrown | failure | ✅ | `StatisticsHandlerTests` |
| Server error timeout or throttling → is recorded as failed (4 cases) | failure | ✅ | `StatisticsHandlerTests` |
| Fetch → network failure → returns unavailable | failure | ✅ | `GeocodingSourceTests` |
| Fetch → no results key → returns no data | failure | ✅ | `GeocodingSourceTests` |
| Fetch → only results without coordinates → returns no data | failure | ✅ | `GeocodingSourceTests` |
| Fetch → results without coordinates → are skipped and logged | failure | ✅ | `GeocodingSourceTests` |
| Fetch → server error → returns unavailable | failure | ✅ | `GeocodingSourceTests` |
| Air quality → no usable values → returns no data | failure | ✅ | `LocationSourceTests` |
| Marine → inland → returns no data | failure | ✅ | `LocationSourceTests` |
| Weather → bad request → returns no data | failure | ✅ | `LocationSourceTests` |
| Weather → missing hourly rows → returns partial | failure | ✅ | `LocationSourceTests` |
| Weather → server error → returns unavailable | failure | ✅ | `LocationSourceTests` |
| Caller cancels → cancellation propagates instead of a failed outcome | failure | ✅ | `OpenMeteoHttpTests` |
| Timeout not caused by the caller → is a failed outcome | failure | ✅ | `OpenMeteoHttpTests` |
| Fetch → bad request → returns no data | failure | ✅ | `OpenMeteoLocationClientTests` |
| Fetch → malformed json → returns unavailable | failure | ✅ | `OpenMeteoLocationClientTests` |
| Fetch → missing current field → with hourly → returns partial | failure | ✅ | `OpenMeteoLocationClientTests` |
| Fetch → missing hourly rows → returns partial | failure | ✅ | `OpenMeteoLocationClientTests` |
| Fetch → nothing usable → returns no data | failure | ✅ | `OpenMeteoLocationClientTests` |
| Fetch → server error → returns unavailable | failure | ✅ | `OpenMeteoLocationClientTests` |

## R8: Documentation

| Scenario | Type | Result | Test |
|---|---|---|---|
| Bearer scheme → with the mock disabled → does not mention mock login | success | ✅ | `OpenApiDocumentTests` |
| Bearer scheme → with the mock enabled → points to mock login | success | ✅ | `OpenApiDocumentTests` |
| City report parameters → have descriptions (5 cases) | success | ✅ | `OpenApiDocumentTests` |
| City report summary → names every location api | success | ✅ | `OpenApiDocumentTests` |
| Document → declares the bearer scheme without a global requirement | success | ✅ | `OpenApiDocumentTests` |
| Mock login → is anonymous in the document | success | ✅ | `OpenApiDocumentTests` |
| Protected operations → require bearer and document 401 and 403 (2 cases) | success | ✅ | `OpenApiDocumentTests` |
| Source status → is documented as an enum with every value | success | ✅ | `OpenApiDocumentTests` |

## R9: Caching

| Scenario | Type | Result | Test |
|---|---|---|---|
| Different filters and sorts → reuse the source entries → but each gets its own response | success | ✅ | `CacheBehaviourTests` |
| Different request shapes → do not share an entry | success | ✅ | `CacheBehaviourTests` |
| Ttls apply per source → only the expired source is refetched | success | ✅ | `CacheBehaviourTests` |
| In memory cache size → is limited from configuration (2 cases) | success | ✅ | `CacheLimitApiTests` |
| Unknown city → is checked again after the not found lifetime | success | ✅ | `CacheLimitApiTests` |
| Simultaneous identical requests → call each api once | success | ✅ | `CacheStampedeTests` |
| City report → second request → is served from cache | success | ✅ | `CityReportApiTests` |
| Geocoding query → another city or country → has another key (3 cases) | success | ✅ | `CacheKeyTests` |
| Location query → requests that differ → have different keys → and equal keys mean equal requests | success | ✅ | `CacheKeyTests` |
| No data → is kept only for its shorter lifetime | success | ✅ | `CacheLimitTests` |
| Normal lifetime → applies to answers with data → or without a shorter no data lifetime (2 cases) | success | ✅ | `CacheLimitTests` |
| Geocoding → uses the standard ttl | success | ✅ | `CachePolicyTests` |
| Location → combined query → uses hourly ttl when it is the shorter one | success | ✅ | `CachePolicyTests` |
| Location → uses the ttl of what is requested → and the shorter one when combined (3 cases) | success | ✅ | `CachePolicyTests` |
| Cached partial → stays partial on a cache hit | success | ✅ | `CacheRoundTripTests` |
| Full weather graph → survives serialization through the shared store | success | ✅ | `CacheRoundTripTests` |
| Place list → with missing region and extreme coordinates → survives the shared store | success | ✅ | `CacheRoundTripTests` |
| Fetch → asks the policy for the ttl of the query | success | ✅ | `CachingSourceTests` |
| Fetch → concurrent identical queries → call the source once | success | ✅ | `CachingSourceTests` |
| Fetch → different queries → are cached separately | success | ✅ | `CachingSourceTests` |
| Fetch → same query in another case → calls the source once | success | ✅ | `CachingSourceTests` |
| Fetch → same query through different sources → is cached separately | success | ✅ | `CachingSourceTests` |
| Name → is the inner source name | success | ✅ | `CachingSourceTests` |
| Get → cache hits → share one immutable instance | success | ✅ | `StaleAwareCacheTests` |
| Get → fresh value → is fetched once | success | ✅ | `StaleAwareCacheTests` |
| Get → no data → is cached like any answer | success | ✅ | `StaleAwareCacheTests` |
| Get → stamps the fetch time → and a cache hit keeps it | success | ✅ | `StaleAwareCacheTests` |
| Hybrid cache read only lookup → on a miss → stores nothing | success | ✅ | `StaleAwareCacheTests` |
| Geocoding query → key ignores the case of the city | success | ✅ | `HourlyWindowTests` |
| Location query → rounds coordinates once for key and request | success | ✅ | `HourlyWindowTests` |
| One source fails → the others keep serving their own cached entries | failure | ✅ | `CacheBehaviourTests` |
| Failing api → is not called again during the cooldown | failure | ✅ | `CacheLimitApiTests` |
| Simultaneous identical requests while an api is down → do not flood it | failure | ✅ | `CacheStampedeTests` |
| Geocoding down with cached coordinates → returns 200 with geocoding stale | failure | ✅ | `ResilienceTests` |
| Source down after expiry → serves last good value as stale | failure | ✅ | `ResilienceTests` |
| Malformed value after expiry → serves the last good value as stale | failure | ✅ | `SourceStatusApiTests` |
| After a failure → the source is not called during the cooldown → and the last good value is served | failure | ✅ | `CacheLimitTests` |
| Cooldown → is not extended by the requests it turns away | failure | ✅ | `CacheLimitTests` |
| Unexpected exception → also starts a cooldown | failure | ✅ | `CacheLimitTests` |
| Without a cooldown → every request calls the source | failure | ✅ | `CacheLimitTests` |
| Full weather graph → survives the last good path through the shared store | failure | ✅ | `CacheRoundTripTests` |
| Serving stale → does not extend the last good retention | failure | ✅ | `CacheRoundTripTests` |
| Caller cancels during the fetch → cancellation propagates and nothing is cached | failure | ✅ | `StaleAwareCacheFailureTests` |
| Caller cancels while the source throws → cancellation still propagates | failure | ✅ | `StaleAwareCacheFailureTests` |
| Expected unavailability → is not logged as an error | failure | ✅ | `StaleAwareCacheFailureTests` |
| Last good past its retention → is not served | failure | ✅ | `StaleAwareCacheFailureTests` |
| Partial result → is kept as last good and served stale with its data | failure | ✅ | `StaleAwareCacheFailureTests` |
| Source recovers → fresh value replaces stale and becomes the new last good | failure | ✅ | `StaleAwareCacheFailureTests` |
| Stampede → one waiting caller cancels → the others still get the value | failure | ✅ | `StaleAwareCacheFailureTests` |
| Stampede → while the source fails with history → one upstream call and every caller gets stale | failure | ✅ | `StaleAwareCacheFailureTests` |
| Stampede → while the source fails → one upstream call and every caller gets unavailable | failure | ✅ | `StaleAwareCacheFailureTests` |
| Unexpected exception in the source → serves the last good value and logs an error | failure | ✅ | `StaleAwareCacheFailureTests` |
| Unexpected exception without history → is unavailable and not cached | failure | ✅ | `StaleAwareCacheFailureTests` |
| While serving stale → every request still tries the source | failure | ✅ | `StaleAwareCacheFailureTests` |
| Get → failure after expired no data → returns unavailable not stale | failure | ✅ | `StaleAwareCacheTests` |
| Get → failure after expiry → serves last good as stale | failure | ✅ | `StaleAwareCacheTests` |
| Get → stale value → keeps the time it was originally fetched | failure | ✅ | `StaleAwareCacheTests` |
| Get → unavailable without history → is not cached and returns unavailable | failure | ✅ | `StaleAwareCacheTests` |
| Get → unavailable → has no fetch time | failure | ✅ | `StaleAwareCacheTests` |

## R10: Parallelism

| Scenario | Type | Result | Test |
|---|---|---|---|
| Concurrency limit → never exceeds the permits per api | success | ✅ | `ResilienceTests` |
| Get → starts every location call before any completes | success | ✅ | `CityReportServiceConcurrencyTests` |
| Get → starts no location call before geocoding completes | success | ✅ | `CityReportServiceConcurrencyTests` |
| Concurrency queue full → rejected places are unavailable but the request succeeds | failure | ✅ | `ResilienceTests` |

## R11: Request statistics with performance buckets

| Scenario | Type | Result | Test |
|---|---|---|---|
| Statistics → after calls → reports every api | success | ✅ | `CityReportApiTests` |
| Hazards calls → are counted in the statistics | success | ✅ | `HazardsApiTests` |
| Before any call → every api is listed with null averages | success | ✅ | `StatisticsApiTests` |
| Cache hits → are not counted | success | ✅ | `StatisticsApiTests` |
| Retries → are counted as separate attempts | success | ✅ | `StatisticsApiTests` |
| Upstream fetches → stay in the callers trace → despite the shared cache fetch | success | ✅ | `TelemetryTests` |
| With an endpoint → requests are traced → and metrics are collected | success | ✅ | `TelemetryTests` |
| Without an endpoint → nothing is registered | success | ✅ | `TelemetryTests` |
| Response → is recorded as succeeded (2 cases) | success | ✅ | `StatisticsHandlerTests` |
| Statistics → fast threshold not above slow → is valid (2 cases) | success | ✅ | `StatisticsOptionsValidatorTests` |
| Monitored apis → are listed before their first call → without averages | success | ✅ | `StatisticsStoreTests` |
| Recent → only counts the requested minutes | success | ✅ | `StatisticsStoreTests` |
| Recent → unknown api → is empty | success | ✅ | `StatisticsStoreTests` |
| Record → groups durations into buckets | success | ✅ | `StatisticsStoreTests` |
| Record → parallel writers → loses no updates | success | ✅ | `StatisticsStoreTests` |
| Snapshot → keeps apis separate and ordered | success | ✅ | `StatisticsStoreTests` |
| Snapshot → while writers run → is always internally consistent | success | ✅ | `StatisticsStoreTests` |
| Server errors → count as failed → but an upstream 400 does not | failure | ✅ | `StatisticsApiTests` |
| Network failure → is recorded as failed and rethrown | failure | ✅ | `StatisticsHandlerTests` |
| Server error timeout or throttling → is recorded as failed (4 cases) | failure | ✅ | `StatisticsHandlerTests` |
| Statistics → fast threshold above slow → fails | failure | ✅ | `StatisticsOptionsValidatorTests` |
| Record → failed attempts → are counted and still timed | failure | ✅ | `StatisticsStoreTests` |

## O1: JWT bearer authentication

| Scenario | Type | Result | Test |
|---|---|---|---|
| Entra shaped token → with a roles array → returns 200 | success | ✅ | `AuthTests` |
| Mock token → carries the role in a roles array → like entra | success | ✅ | `AuthTests` |
| Valid hand made token → returns 200 | success | ✅ | `AuthTests` |
| Health → is anonymous | success | ✅ | `CityReportApiTests` |
| Optional endpoints → when disabled → are not mapped | success | ✅ | `CityReportApiTests` |
| Bearer scheme → with the mock disabled → does not mention mock login | success | ✅ | `OpenApiDocumentTests` |
| Bearer scheme → with the mock enabled → points to mock login | success | ✅ | `OpenApiDocumentTests` |
| Document → declares the bearer scheme without a global requirement | success | ✅ | `OpenApiDocumentTests` |
| Mock login → is anonymous in the document | success | ✅ | `OpenApiDocumentTests` |
| Protected operations → require bearer and document 401 and 403 (2 cases) | success | ✅ | `OpenApiDocumentTests` |
| Validate → mock disabled → absolute https authority → succeeds | success | ✅ | `AuthOptionsValidatorTests` |
| Validate → mock enabled → does not need authority | success | ✅ | `AuthOptionsValidatorTests` |
| Expired token → returns 401 with expiry reason | failure | ✅ | `AuthTests` |
| Malformed token or wrong scheme → returns 401 (3 cases) | failure | ✅ | `AuthTests` |
| Token for another issuer or audience → returns 401 (2 cases) | failure | ✅ | `AuthTests` |
| Token with the singular role claim → returns 403 | failure | ✅ | `AuthTests` |
| City report → token signed with untrusted key → returns 401 | failure | ✅ | `CityReportApiTests` |
| City report → token without role → returns 403 | failure | ✅ | `CityReportApiTests` |
| City report → without token → returns 401 | failure | ✅ | `CityReportApiTests` |
| Mock login → unknown user → returns 400 | failure | ✅ | `CityReportApiTests` |
| Validate → mock disabled without authority → fails (2 cases) | failure | ✅ | `AuthOptionsValidatorTests` |
| Validate → mock disabled → non https or relative authority → fails (2 cases) | failure | ✅ | `AuthOptionsValidatorTests` |

## O2: Performance anomaly logging (background service)

| Scenario | Type | Result | Test |
|---|---|---|---|
| Tick → no anomalies → runs the detector and logs nothing | success | ✅ | `AnomalyBackgroundServiceTests` |
| Tick → with anomaly → logs warning | success | ✅ | `AnomalyBackgroundServiceTests` |
| Detect → increase at least the minimum → is reported (2 cases) | success | ✅ | `AnomalyDetectorTests` |
| Detect → no baseline yet → reports nothing | success | ✅ | `AnomalyDetectorTests` |
| Detect → recent average above threshold → reports anomaly | success | ✅ | `AnomalyDetectorTests` |
| Detect → recent average within threshold → reports nothing | success | ✅ | `AnomalyDetectorTests` |
| Detect → relatively slower but only by milliseconds → reports nothing | success | ✅ | `AnomalyDetectorTests` |
| Detect → too few recent samples → reports nothing | success | ✅ | `AnomalyDetectorTests` |
| Detect → without minimum increase → uses the ratio alone | success | ✅ | `AnomalyDetectorTests` |
| Anomaly → defaults → are valid | success | ✅ | `StatisticsOptionsValidatorTests` |
| Tick → detector throws → logs error and keeps running | failure | ✅ | `AnomalyBackgroundServiceTests` |
| Anomaly → history shorter than window plus current minute → fails (2 cases) | failure | ✅ | `StatisticsOptionsValidatorTests` |
| Anomaly → interval longer than window → fails | failure | ✅ | `StatisticsOptionsValidatorTests` |

## Code coverage

### Summary
<details open><summary>Summary</summary>

|||
|:---|:---|
| Generated on: | 11/16/2026 - 21:01:41 |
| Parser: | MultiReport (2x Cobertura) |
| Assemblies: | 3 |
| Classes: | 147 |
| Files: | 124 |
| **Line coverage:** | 92.2% (2015 of 2185) |
| Covered lines: | 2015 |
| Uncovered lines: | 170 |
| Coverable lines: | 2185 |
| Total lines: | 3574 |
| **Branch coverage:** | 80.5% (979 of 1215) |
| Covered branches: | 979 |
| Total branches: | 1215 |

</details>

#### Coverage
<details><summary>Aggregator.Api - 82.5%</summary>

|**Name**|**Line**|**Branch**|
|:---|---:|---:|
|**Aggregator.Api**|**82.5%**|**67.1%**|
|Aggregator.Api.Auth.AuthOptions|100%||
|Aggregator.Api.Auth.AuthOptionsValidator|100%|100%|
|Aggregator.Api.Auth.AuthRegistration|100%|100%|
|Aggregator.Api.Auth.IssuedToken|100%||
|Aggregator.Api.Auth.MockAuthOptions|100%||
|Aggregator.Api.Auth.MockSigningKeys|100%||
|Aggregator.Api.Auth.MockTokenIssuer|100%|100%|
|Aggregator.Api.CityReports.CityReportQuery|100%||
|Aggregator.Api.CityReports.CityReportQueryParser|100%|98.8%|
|Aggregator.Api.CityReports.ParseResult|100%||
|Aggregator.Api.Composition.BearerRequirementTransformer|100%|100%|
|Aggregator.Api.Composition.BearerSecurityTransformer|100%|83.3%|
|Aggregator.Api.Composition.TelemetryRegistration|100%|100%|
|Aggregator.Api.Contracts.AirQualityResponse|100%||
|Aggregator.Api.Contracts.ApiStatisticsResponse|100%||
|Aggregator.Api.Contracts.BucketResponse|100%||
|Aggregator.Api.Contracts.BucketThresholdsResponse|100%||
|Aggregator.Api.Contracts.CityReportMapper|98.1%|98.3%|
|Aggregator.Api.Contracts.CityReportResponse|100%||
|Aggregator.Api.Contracts.CoordinatesResponse|100%||
|Aggregator.Api.Contracts.FiltersResponse|100%||
|Aggregator.Api.Contracts.HazardResponse|100%||
|Aggregator.Api.Contracts.HourlyRangeResponse|100%||
|Aggregator.Api.Contracts.HourlyReadingResponse|100%||
|Aggregator.Api.Contracts.MarineResponse|100%||
|Aggregator.Api.Contracts.MockLoginRequest|100%||
|Aggregator.Api.Contracts.PerformanceBucketsResponse|100%||
|Aggregator.Api.Contracts.PlaceResponse|100%||
|Aggregator.Api.Contracts.RegionFilterResponse|100%||
|Aggregator.Api.Contracts.SourceResponse|100%||
|Aggregator.Api.Contracts.StatisticsMapper|100%|100%|
|Aggregator.Api.Contracts.StatisticsResponse|100%||
|Aggregator.Api.Contracts.TokenResponse|100%||
|Aggregator.Api.Contracts.WeatherResponse|100%||
|Aggregator.Api.Endpoints.CityReportEndpoints|100%|100%|
|Aggregator.Api.Endpoints.MockLoginEndpoints|100%|100%|
|Aggregator.Api.Endpoints.StatisticsEndpoints|100%||
|Aggregator.Api.ErrorHandling.BadRequestExceptionHandler|100%|100%|
|Aggregator.Api.Hosting.AnomalyBackgroundService|97.2%|80%|
|Microsoft.AspNetCore.OpenApi.Generated|44.9%|32%|
|Program|100%|100%|
|System.Runtime.CompilerServices|0%||
|System.Text.RegularExpressions.Generated|78.9%|73.6%|
|System.Text.RegularExpressions.Generated.RunnerFactory|||
|System.Text.RegularExpressions.Generated.RunnerFactory.Runner|||

</details>
<details><summary>Aggregator.Core - 99.6%</summary>

|**Name**|**Line**|**Branch**|
|:---|---:|---:|
|**Aggregator.Core**|**99.6%**|**94.2%**|
|Aggregator.Core.CityReports.CityReport|100%||
|Aggregator.Core.CityReports.CityReportOptions|100%||
|Aggregator.Core.CityReports.CityReportOptionsValidator|100%|100%|
|Aggregator.Core.CityReports.CityReportOutcome|100%||
|Aggregator.Core.CityReports.CityReportOutcome.Found|100%||
|Aggregator.Core.CityReports.CityReportOutcome.HourlyRangeOutsideWindow|100%||
|Aggregator.Core.CityReports.CityReportRequest|100%||
|Aggregator.Core.CityReports.CityReportService|100%|98.3%|
|Aggregator.Core.CityReports.CityReportService<TResult>|100%|98.3%|
|Aggregator.Core.CityReports.HourlyFilter|100%|100%|
|Aggregator.Core.CityReports.HourlyOptions|100%||
|Aggregator.Core.CityReports.HourlyRange|100%|100%|
|Aggregator.Core.CityReports.PlacePage|100%||
|Aggregator.Core.CityReports.PlaceReport|100%||
|Aggregator.Core.CityReports.PlaceSort|100%||
|Aggregator.Core.CityReports.PlaceSorter|100%|91.6%|
|Aggregator.Core.CityReports.ReportFilters|100%||
|Aggregator.Core.CityReports.ReportSectionsExtensions|87.5%|83.3%|
|Aggregator.Core.CoreServiceCollectionExtensions|100%|100%|
|Aggregator.Core.Model.AirQuality|100%||
|Aggregator.Core.Model.Coordinates|100%||
|Aggregator.Core.Model.CurrentAirQuality|100%||
|Aggregator.Core.Model.CurrentMarine|100%||
|Aggregator.Core.Model.CurrentWeather|100%||
|Aggregator.Core.Model.Hazard|100%||
|Aggregator.Core.Model.Hazards|100%||
|Aggregator.Core.Model.HourlyReading|100%||
|Aggregator.Core.Model.HourlyWindow|100%|50%|
|Aggregator.Core.Model.Marine|100%||
|Aggregator.Core.Model.Place|100%||
|Aggregator.Core.Model.SourceResult|100%||
|Aggregator.Core.Model.SourceResult<T>|100%||
|Aggregator.Core.Model.SourceState|100%||
|Aggregator.Core.Model.Weather|100%||
|Aggregator.Core.Query.DistanceCalculator|100%||
|Aggregator.Core.Query.PlaceRanker|94.4%|69.4%|
|Aggregator.Core.Query.RegionMatch|100%||
|Aggregator.Core.Query.RegionMatcher|100%|100%|
|Aggregator.Core.Query.RegionMatchingOptions|100%||
|Aggregator.Core.Sources.AreaQuery|100%||
|Aggregator.Core.Sources.GeocodingQuery|100%||
|Aggregator.Core.Sources.LocationQuery|100%|100%|
|Aggregator.Core.Statistics.AnomalyDetector|100%|100%|
|Aggregator.Core.Statistics.AnomalyOptions|100%||
|Aggregator.Core.Statistics.AnomalyOptionsValidator|100%|100%|
|Aggregator.Core.Statistics.ApiStatisticsSnapshot|100%||
|Aggregator.Core.Statistics.BucketSnapshot|100%||
|Aggregator.Core.Statistics.MonitoredApi|100%||
|Aggregator.Core.Statistics.PerformanceAnomaly|100%||
|Aggregator.Core.Statistics.StatisticsOptions|100%||
|Aggregator.Core.Statistics.StatisticsOptionsValidator|100%|100%|
|Aggregator.Core.Statistics.StatisticsStore|100%|97.3%|
|Aggregator.Core.Statistics.StatisticsStore.ApiStatistics|100%|100%|
|Aggregator.Core.Statistics.StatisticsStore.MinuteSlot|100%||
|Aggregator.Core.Statistics.StatisticsStore.Totals|100%|100%|
|Aggregator.Core.Statistics.WindowSample|100%|50%|

</details>
<details><summary>Aggregator.Infrastructure - 99.4%</summary>

|**Name**|**Line**|**Branch**|
|:---|---:|---:|
|**Aggregator.Infrastructure**|**99.4%**|**87.5%**|
|Aggregator.Infrastructure.Caching.AreaCachePolicy|100%||
|Aggregator.Infrastructure.Caching.CacheTelemetry|100%||
|Aggregator.Infrastructure.Caching.CacheTimes|100%||
|Aggregator.Infrastructure.Caching.CachingLog|100%|75%|
|Aggregator.Infrastructure.Caching.CachingSource<TQuery, TResult>|100%|100%|
|Aggregator.Infrastructure.Caching.CachingSource<TQuery, TResult>|100%|50%|
|Aggregator.Infrastructure.Caching.GeocodingCachePolicy|100%||
|Aggregator.Infrastructure.Caching.LocationCachePolicy|100%|87.5%|
|Aggregator.Infrastructure.Caching.SourceUnavailableException|100%||
|Aggregator.Infrastructure.Caching.StaleAwareCache|97.9%|100%|
|Aggregator.Infrastructure.Caching.StaleAwareCache<T>|97.9%|100%|
|Aggregator.Infrastructure.Eonet.HazardsLog|100%|66.6%|
|Aggregator.Infrastructure.Eonet.HazardsSource|98.3%|87%|
|Aggregator.Infrastructure.Eonet.HazardsSource.CategoryItem|100%||
|Aggregator.Infrastructure.Eonet.HazardsSource.EventItem|100%||
|Aggregator.Infrastructure.Eonet.HazardsSource.EventsResponse|100%||
|Aggregator.Infrastructure.Eonet.HazardsSource.GeometryItem|100%||
|Aggregator.Infrastructure.Eonet.HazardsSource.Sighting|100%||
|Aggregator.Infrastructure.Http.ExternalApiMetrics|100%||
|Aggregator.Infrastructure.Http.StatisticsHandler|100%|100%|
|Aggregator.Infrastructure.InfrastructureServiceCollectionExtensions|100%|100%|
|Aggregator.Infrastructure.InfrastructureServiceCollectionExtensions<TQuery,<br/> TResult, TSource>|100%|100%|
|Aggregator.Infrastructure.OpenMeteo.AirQualitySource|100%|100%|
|Aggregator.Infrastructure.OpenMeteo.GeocodingSource|100%|90%|
|Aggregator.Infrastructure.OpenMeteo.GeocodingSource.GeocodingResponse|100%||
|Aggregator.Infrastructure.OpenMeteo.GeocodingSource.GeocodingResult|100%||
|Aggregator.Infrastructure.OpenMeteo.HourlySeries|93.7%|83.3%|
|Aggregator.Infrastructure.OpenMeteo.MarineSource|100%|100%|
|Aggregator.Infrastructure.OpenMeteo.MetricSeries|100%||
|Aggregator.Infrastructure.OpenMeteo.OpenMeteoEndpoint|100%|100%|
|Aggregator.Infrastructure.OpenMeteo.OpenMeteoError|100%||
|Aggregator.Infrastructure.OpenMeteo.OpenMeteoHttp|100%|66.6%|
|Aggregator.Infrastructure.OpenMeteo.OpenMeteoHttp<T>|100%|66.6%|
|Aggregator.Infrastructure.OpenMeteo.OpenMeteoLocationClient|100%|85.4%|
|Aggregator.Infrastructure.OpenMeteo.OpenMeteoLocationClient.LocationRespons<br/>e|100%||
|Aggregator.Infrastructure.OpenMeteo.OpenMeteoLocationClient<TCurrent, TResu<br/>lt>|100%|85.4%|
|Aggregator.Infrastructure.OpenMeteo.OpenMeteoLog|100%|75%|
|Aggregator.Infrastructure.OpenMeteo.OpenMeteoResponse<T>|100%||
|Aggregator.Infrastructure.OpenMeteo.OpenMeteoSeries|100%||
|Aggregator.Infrastructure.OpenMeteo.PivotResult|100%||
|Aggregator.Infrastructure.OpenMeteo.WeatherSource|100%|100%|
|Aggregator.Infrastructure.Options.CacheOptions|100%||
|Aggregator.Infrastructure.Options.GeocodingOptions|100%||
|Aggregator.Infrastructure.Options.HazardsOptions|100%||
|Aggregator.Infrastructure.Options.SourceClientOptions|100%||
|Aggregator.Infrastructure.Options.SourceClientOptionsValidator|100%|100%|

</details>

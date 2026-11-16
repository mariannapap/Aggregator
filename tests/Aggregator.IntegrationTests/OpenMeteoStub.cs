using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Globalization;
using System.Net;
using System.Text;
using System.Web;

namespace Aggregator.IntegrationTests;

public sealed class OpenMeteoStub : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<Func<HttpResponseMessage>>> scripts = new();
    private readonly ConcurrentDictionary<string, TimeSpan> delays = new();
    private readonly ConcurrentDictionary<string, Task> holds = new();
    private readonly ConcurrentDictionary<string, bool> down = new();
    private readonly ConcurrentDictionary<string, bool> stalledBodies = new();
    private readonly ConcurrentDictionary<string, string> omittedCurrentFields = new();
    private readonly ConcurrentDictionary<string, (int Index, string Json)> replacedHourlyTimes = new();
    private readonly ConcurrentDictionary<string, int> callsByHost = new();
    private readonly ConcurrentDictionary<string, int> inFlight = new();
    private readonly ConcurrentDictionary<string, int> maxInFlight = new();
    private int calls;

    public bool GeocodingDown { get => IsDown(OpenMeteoHosts.Geocoding); set => SetDown(OpenMeteoHosts.Geocoding, value); }

    public bool AirQualityDown { get => IsDown(OpenMeteoHosts.AirQuality); set => SetDown(OpenMeteoHosts.AirQuality, value); }

    public bool MarineDown { get => IsDown(OpenMeteoHosts.Marine); set => SetDown(OpenMeteoHosts.Marine, value); }

    public int Calls => Volatile.Read(ref calls);

    public int? GeneratedPlaces { get; set; }

    public void SetDown(string host, bool isDown) => down[host] = isDown;

    public void SetDelay(string host, TimeSpan delay) => delays[host] = delay;

    public void StallBody(string host) => stalledBodies[host] = true;

    public void Hold(string host, Task release) => holds[host] = release;

    public void OmitCurrentField(string host, string field) => omittedCurrentFields[host] = field;

    public void ReplaceHourlyTime(string host, int index, string json) => replacedHourlyTimes[host] = (index, json);

    public void Script(string host, params HttpStatusCode[] statuses)
    {
        var queue = scripts.GetOrAdd(host, _ => new ConcurrentQueue<Func<HttpResponseMessage>>());
        foreach (var status in statuses)
            queue.Enqueue(() => Status(status));
    }

    public void ScriptJson(string host, string json) =>
        scripts.GetOrAdd(host, _ => new ConcurrentQueue<Func<HttpResponseMessage>>()).Enqueue(() => Json(json));

    public void ScriptTooManyRequests(string host, TimeSpan retryAfter) =>
        scripts.GetOrAdd(host, _ => new ConcurrentQueue<Func<HttpResponseMessage>>()).Enqueue(() =>
        {
            var response = Status(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(retryAfter);
            return response;
        });

    public int CallsTo(string host) => callsByHost.GetValueOrDefault(host);

    public int MaxInFlight(string host) => maxInFlight.GetValueOrDefault(host);

    // Generated places sit at latitude 36 + 0.05 * index, so their temperatures are 18.5, 19.0, 19.5, …
    public static double TemperatureAt(double latitude) => Math.Round(18.5 + (latitude - 36) * 10, 1);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref calls);
        var uri = request.RequestUri!;
        var host = uri.Host;
        callsByHost.AddOrUpdate(host, 1, (_, count) => count + 1);
        var current = inFlight.AddOrUpdate(host, 1, (_, count) => count + 1);
        maxInFlight.AddOrUpdate(host, current, (_, max) => Math.Max(max, current));
        try
        {
            if (holds.TryGetValue(host, out var release))
                await release.WaitAsync(cancellationToken);

            if (delays.TryGetValue(host, out var delay) && delay > TimeSpan.Zero)
                await Task.Delay(delay, cancellationToken);

            if (stalledBodies.GetValueOrDefault(host))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new StalledStream()) };

            if (scripts.TryGetValue(host, out var queue) && queue.TryDequeue(out var scripted))
                return scripted();

            return IsDown(host) ? Status(HttpStatusCode.InternalServerError) : Respond(host, HttpUtility.ParseQueryString(uri.Query));
        }
        finally
        {
            inFlight.AddOrUpdate(host, 0, (_, count) => count - 1);
        }
    }

    private HttpResponseMessage Respond(string host, NameValueCollection query) => host switch
    {
        OpenMeteoHosts.Geocoding when GeneratedPlaces is { } count => Json(GeocodingResults(count)),
        OpenMeteoHosts.Geocoding when query["name"] == "Athens" => Fixture("geocoding-athens-gr.json"),
        OpenMeteoHosts.Geocoding => Fixture("geocoding-not-found.json"),
        OpenMeteoHosts.Weather => Json(Series(host, query, ("temperature_2m", TemperatureAt(Latitude(query))), ("wind_speed_10m", 7.2))),
        OpenMeteoHosts.AirQuality => Json(Series(host, query, ("us_aqi", 54))),
        OpenMeteoHosts.Marine => Json(Series(host, query, ("wave_height", 0.1), ("sea_surface_temperature", 20.5))),
        OpenMeteoHosts.Hazards => Json(HazardEvents(query)),
        _ => Status(HttpStatusCode.NotFound)
    };

    // One wildfire at the centre of the requested box and one storm far outside any radius (which the adapter must drop).
    private static string HazardEvents(NameValueCollection query)
    {
        var box = query["bbox"]!.Split(',').Select(value => double.Parse(value, CultureInfo.InvariantCulture)).ToArray();
        var (longitude, latitude) = ((box[0] + box[2]) / 2, (box[1] + box[3]) / 2);
        return string.Create(
            CultureInfo.InvariantCulture,
            $$"""
            {"events":[
              {"title":"Wildfire near the place","categories":[{"id":"wildfires","title":"Wildfires"}],
               "geometry":[{"date":"2026-11-15T10:00:00Z","type":"Point","coordinates":[{{longitude}},{{latitude}}]}]},
              {"title":"Storm far away","categories":[{"id":"severeStorms","title":"Severe Storms"}],
               "geometry":[{"date":"2026-11-15T10:00:00Z","type":"Point","coordinates":[{{longitude + 20}},{{latitude}}]}]}
            ]}
            """);
    }

    private bool IsDown(string host) => down.GetValueOrDefault(host);

    private static double Latitude(NameValueCollection query) => double.Parse(query["latitude"]!, CultureInfo.InvariantCulture);

    private static string GeocodingResults(int count) =>
        "{\"results\":[" + string.Join(',', Enumerable.Range(0, count).Select(index => string.Create(
            CultureInfo.InvariantCulture,
            $$"""{"name":"Place {{index}}","latitude":{{36 + index * 0.05}},"longitude":{{24 + index * 0.05}},"feature_code":"PPL","country_code":"GR","admin1":"{{(index % 2 == 0 ? "Attica" : "Crete")}}"}"""))) + "]}";

    private string Series(string host, NameValueCollection query, params (string Field, double Value)[] fields)
    {
        var parts = new List<string>();
        if (query["current"] is not null)
        {
            var omitted = omittedCurrentFields.GetValueOrDefault(host);
            parts.Add("\"current\":{" + string.Join(',', fields.Where(field => field.Field != omitted).Select(field => Number(field.Field, field.Value))) + "}");
        }

        if (query["start_hour"] is { } startHour)
        {
            var start = DateTimeOffset.ParseExact(startHour, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
            var replaced = replacedHourlyTimes.GetValueOrDefault(host, (Index: -1, Json: ""));
            var times = string.Join(',', Enumerable.Range(0, 24).Select(hour => hour == replaced.Index ? replaced.Json : $"\"{start.AddHours(hour).ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture)}\""));
            var series = fields.Select(field => $"\"{field.Field}\":[{string.Join(',', Enumerable.Repeat(field.Value.ToString(CultureInfo.InvariantCulture), 24))}]");
            parts.Add("\"hourly\":{\"time\":[" + times + "]," + string.Join(',', series) + "}");
        }

        return "{" + string.Join(',', parts) + "}";
    }

    private static string Number(string field, double value) => $"\"{field}\":{value.ToString(CultureInfo.InvariantCulture)}";

    private static HttpResponseMessage Fixture(string name) =>
        Json(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", name)));

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Status(HttpStatusCode status) => new(status) { Content = new StringContent("{}") };
}

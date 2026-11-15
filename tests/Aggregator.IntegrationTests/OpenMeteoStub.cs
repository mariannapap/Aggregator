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
    private readonly ConcurrentDictionary<string, bool> down = new();
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

    public void Script(string host, params HttpStatusCode[] statuses)
    {
        var queue = scripts.GetOrAdd(host, _ => new ConcurrentQueue<Func<HttpResponseMessage>>());
        foreach (var status in statuses)
            queue.Enqueue(() => Status(status));
    }

    public void ScriptTooManyRequests(string host, TimeSpan retryAfter) =>
        scripts.GetOrAdd(host, _ => new ConcurrentQueue<Func<HttpResponseMessage>>()).Enqueue(() =>
        {
            var response = Status(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(retryAfter);
            return response;
        });

    public int CallsTo(string host) => callsByHost.GetValueOrDefault(host);

    public int MaxInFlight(string host) => maxInFlight.GetValueOrDefault(host);

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
            if (delays.TryGetValue(host, out var delay) && delay > TimeSpan.Zero)
                await Task.Delay(delay, cancellationToken);

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
        OpenMeteoHosts.Weather => Json(Series(query, ("temperature_2m", 18.5), ("wind_speed_10m", 7.2))),
        OpenMeteoHosts.AirQuality => Json(Series(query, ("us_aqi", 54))),
        OpenMeteoHosts.Marine => Json(Series(query, ("wave_height", 0.1), ("sea_surface_temperature", 20.5))),
        _ => Status(HttpStatusCode.NotFound)
    };

    private bool IsDown(string host) => down.GetValueOrDefault(host);

    private static string GeocodingResults(int count) =>
        "{\"results\":[" + string.Join(',', Enumerable.Range(0, count).Select(index => string.Create(
            CultureInfo.InvariantCulture,
            $$"""{"name":"Place {{index}}","latitude":{{36 + index * 0.05}},"longitude":{{24 + index * 0.05}},"feature_code":"PPL","country_code":"GR","admin1":"{{(index % 2 == 0 ? "Attica" : "Crete")}}"}"""))) + "]}";

    private static string Series(NameValueCollection query, params (string Field, double Value)[] fields)
    {
        var parts = new List<string>();
        if (query["current"] is not null)
            parts.Add("\"current\":{" + string.Join(',', fields.Select(field => Number(field.Field, field.Value))) + "}");

        if (query["start_hour"] is { } startHour)
        {
            var start = DateTimeOffset.ParseExact(startHour, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
            var times = string.Join(',', Enumerable.Range(0, 24).Select(hour => $"\"{start.AddHours(hour).ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture)}\""));
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

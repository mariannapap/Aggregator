using System.Collections.Specialized;
using System.Globalization;
using System.Net;
using System.Text;
using System.Web;

namespace Aggregator.IntegrationTests;

public sealed class OpenMeteoStub : HttpMessageHandler
{
    private int calls;

    public bool GeocodingDown { get; set; }

    public bool AirQualityDown { get; set; }

    public bool MarineDown { get; set; }

    public int Calls => Volatile.Read(ref calls);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref calls);
        var uri = request.RequestUri!;
        var query = HttpUtility.ParseQueryString(uri.Query);
        return Task.FromResult(uri.Host switch
        {
            "geocoding-api.open-meteo.com" when GeocodingDown => Status(HttpStatusCode.ServiceUnavailable),
            "geocoding-api.open-meteo.com" when query["name"] == "Athens" => Fixture("geocoding-athens-gr.json"),
            "geocoding-api.open-meteo.com" => Fixture("geocoding-not-found.json"),
            "api.open-meteo.com" => Json(Series(query, ("temperature_2m", 18.5), ("wind_speed_10m", 7.2))),
            "air-quality-api.open-meteo.com" when AirQualityDown => Status(HttpStatusCode.InternalServerError),
            "air-quality-api.open-meteo.com" => Json(Series(query, ("us_aqi", 54))),
            "marine-api.open-meteo.com" when MarineDown => Status(HttpStatusCode.InternalServerError),
            "marine-api.open-meteo.com" => Json(Series(query, ("wave_height", 0.1), ("sea_surface_temperature", 20.5))),
            _ => Status(HttpStatusCode.NotFound)
        });
    }

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

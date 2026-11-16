using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aggregator.Infrastructure.Http;
using Microsoft.Extensions.Logging;

namespace Aggregator.Infrastructure.OpenMeteo;

internal static class OpenMeteoHttp
{
    private const string TimeFormat = "yyyy-MM-dd'T'HH:mm";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static async Task<OpenMeteoResponse<T>> GetAsync<T>(
        HttpClient http,
        string relativeUri,
        string source,
        ILogger logger,
        CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using var response = await http.GetAsync(new Uri(relativeUri, UriKind.Relative), cancellationToken);
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var error = await response.Content.ReadFromJsonAsync<OpenMeteoError>(SerializerOptions, cancellationToken);
                logger.RequestRejected(source, error?.Reason);
                return new OpenMeteoResponse<T>(OpenMeteoOutcome.Rejected, null);
            }

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<T>(SerializerOptions, cancellationToken);
            return new OpenMeteoResponse<T>(body is null ? OpenMeteoOutcome.Failed : OpenMeteoOutcome.Success, body);
        }
        // Network, HTTP, timeout, open-circuit, limiter and JSON errors make the call Failed (unavailable); only the caller's cancellation propagates.
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            if (ExpectedFailure.Describe(exception) is { } reason)
                logger.CallFailed(source, reason);
            else
                logger.CallFailedUnexpectedly(source, exception);

            return new OpenMeteoResponse<T>(OpenMeteoOutcome.Failed, null);
        }
    }

    public static string FormatHour(DateTimeOffset time) =>
        time.ToUniversalTime().ToString(TimeFormat, CultureInfo.InvariantCulture);

    public static DateTimeOffset? TryParseTime(JsonElement value) =>
        value.ValueKind == JsonValueKind.String
        && DateTimeOffset.TryParseExact(value.GetString(), TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var time)
            ? time
            : null;

    public static string FormatCoordinate(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}

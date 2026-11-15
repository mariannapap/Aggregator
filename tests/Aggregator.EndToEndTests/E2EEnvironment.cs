using System.Globalization;

namespace Aggregator.EndToEndTests;

public static class E2EEnvironment
{
    public static Uri ApiUrl { get; } = new(Read("E2E_API_URL", "http://localhost:8080"));

    public static Uri WireMockUrl { get; } = new(Read("E2E_WIREMOCK_URL", "http://localhost:8099"));

    public static string ApiLogPath { get; } = Read("E2E_API_LOG", "/logs/api.log");

    public static TimeSpan LoadDuration { get; } = TimeSpan.FromSeconds(int.Parse(Read("E2E_LOAD_SECONDS", "60"), CultureInfo.InvariantCulture));

    public static string UniqueCity(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    private static string Read(string name, string fallback) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : fallback;
}

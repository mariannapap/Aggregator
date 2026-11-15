namespace Aggregator.UnitTests.Support;

internal static class Fixtures
{
    public static readonly DateTimeOffset RecordedAt = new(2026, 11, 15, 15, 20, 0, TimeSpan.Zero);

    public static string Read(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", name));
}

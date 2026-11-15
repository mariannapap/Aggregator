using System.ComponentModel.DataAnnotations;

namespace Aggregator.Api.Auth;

public sealed class MockAuthOptions
{
    public const string SectionName = "MockAuth";

    public bool Enabled { get; set; }

    [Range(typeof(TimeSpan), "00:00:10", "01:00:00")]
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromMinutes(2);
}

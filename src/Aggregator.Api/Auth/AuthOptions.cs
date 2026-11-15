using System.ComponentModel.DataAnnotations;

namespace Aggregator.Api.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";
    public const string ReaderPolicy = "Reader";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Required]
    public string RequiredRole { get; set; } = string.Empty;

    [Range(typeof(TimeSpan), "00:00:00", "00:10:00")]
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(5);

    public string? Authority { get; set; }
}

using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;

namespace Aggregator.Core.Query;

public sealed class RegionMatcher(IOptions<RegionMatchingOptions> options)
{
    public RegionMatch Match(string requested, IEnumerable<string?> candidates)
    {
        var target = Normalize(requested);
        var regions = candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Select(candidate => candidate!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(candidate => (Original: candidate, Normalized: Normalize(candidate)))
            .ToList();

        var exact = regions.Where(region => region.Normalized == target).ToList();
        if (exact.Count > 0)
            return Result(requested, exact);

        var containing = regions.Where(region => region.Normalized.Contains(target, StringComparison.Ordinal)).ToList();
        if (containing.Count > 0)
            return Result(requested, containing);

        var maxDistance = (int)Math.Ceiling(target.Length * options.Value.MaxEditDistanceRatio);
        var scored = regions
            .Select(region => (Region: region, Distance: LevenshteinDistance(target, region.Normalized)))
            .Where(scoredRegion => scoredRegion.Distance <= maxDistance)
            .ToList();
        if (scored.Count == 0)
            return new RegionMatch(requested, []);

        var best = scored.Min(scoredRegion => scoredRegion.Distance);
        return Result(requested, scored.Where(scoredRegion => scoredRegion.Distance == best).Select(scoredRegion => scoredRegion.Region).ToList());
    }

    internal static string Normalize(string value)
    {
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    internal static int LevenshteinDistance(string source, string target)
    {
        var previous = new int[target.Length + 1];
        var current = new int[target.Length + 1];
        for (var j = 0; j <= target.Length; j++)
            previous[j] = j;

        for (var i = 1; i <= source.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= target.Length; j++)
            {
                var substitution = source[i - 1] == target[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + substitution);
            }

            (previous, current) = (current, previous);
        }

        return previous[target.Length];
    }

    private static RegionMatch Result(string requested, IEnumerable<(string Original, string Normalized)> matches) =>
        new(requested, matches.Select(match => match.Original).ToList());
}

using System.Diagnostics.CodeAnalysis;
using Aggregator.Core.CityReports;

namespace Aggregator.Api.CityReports;

internal sealed record ParseResult(CityReportRequest? Request, IReadOnlyDictionary<string, string[]> Errors)
{
    [MemberNotNullWhen(true, nameof(Request))]
    public bool IsValid => Request is not null;
}

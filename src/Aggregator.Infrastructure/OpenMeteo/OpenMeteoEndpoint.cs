using Aggregator.Core.Model;

namespace Aggregator.Infrastructure.OpenMeteo;

internal sealed record OpenMeteoEndpoint(string Source, string Path, HourlyCategory Category, IReadOnlyList<OpenMeteoSeries> Series)
{
    public string Fields => string.Join(',', Series.Select(series => series.Field));
}

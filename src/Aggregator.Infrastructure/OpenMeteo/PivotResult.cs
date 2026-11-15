using Aggregator.Core.Model;

namespace Aggregator.Infrastructure.OpenMeteo;

internal sealed record PivotResult(IReadOnlyList<HourlyReading> Readings, bool Complete);

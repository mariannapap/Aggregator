namespace Aggregator.Infrastructure.OpenMeteo;

internal sealed record OpenMeteoResponse<T>(OpenMeteoOutcome Outcome, T? Body)
    where T : class;

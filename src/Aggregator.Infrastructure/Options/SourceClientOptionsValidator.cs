using Microsoft.Extensions.Options;

namespace Aggregator.Infrastructure.Options;

internal sealed class SourceClientOptionsValidator : IValidateOptions<SourceClientOptions>
{
    public ValidateOptionsResult Validate(string? name, SourceClientOptions options) =>
        options.BaseUrl is null || (options.BaseUrl.IsAbsoluteUri && options.BaseUrl.AbsolutePath.EndsWith('/'))
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"{SourceClientOptions.SectionPrefix}:{name}:BaseUrl ({options.BaseUrl}) must be an absolute URL that ends with '/', " +
                "otherwise relative request paths replace its last segment.");
}

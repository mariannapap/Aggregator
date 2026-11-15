using System.ComponentModel;

namespace Aggregator.Api.Contracts;

public sealed record MockLoginRequest(
    [property: Description("valid (200 on protected endpoints), invalid (signed with an untrusted key: 401) or forbidden (missing the required role: 403).")] string User);

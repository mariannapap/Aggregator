using System.Text.Json.Serialization;
using Aggregator.Api.Auth;
using Aggregator.Api.CityReports;
using Aggregator.Api.Composition;
using Aggregator.Api.Endpoints;
using Aggregator.Api.ErrorHandling;
using Aggregator.Api.Hosting;
using Aggregator.Core;
using Aggregator.Infrastructure;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddCore();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddTelemetry(builder.Configuration);
builder.Services.AddApiAuthentication();
builder.Services.AddSingleton<CityReportQueryParser>();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<BadRequestExceptionHandler>();
builder.Services.AddHealthChecks();
builder.Services.AddOptions<OpenApiUiOptions>().BindConfiguration(OpenApiUiOptions.SectionName);
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecurityTransformer>();
    options.AddOperationTransformer<BearerRequirementTransformer>();
});
builder.Services.AddHostedService<AnomalyBackgroundService>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapCityReports();
app.MapStatistics();

if (app.Services.GetRequiredService<IOptions<MockAuthOptions>>().Value.Enabled)
    app.MapMockLogin();

if (app.Services.GetRequiredService<IOptions<OpenApiUiOptions>>().Value.EnableUi)
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.Run();

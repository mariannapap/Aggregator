# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0.404 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props Aggregator.slnx ./
COPY src/Aggregator.Api/Aggregator.Api.csproj src/Aggregator.Api/
COPY src/Aggregator.Core/Aggregator.Core.csproj src/Aggregator.Core/
COPY src/Aggregator.Infrastructure/Aggregator.Infrastructure.csproj src/Aggregator.Infrastructure/
COPY tests/Aggregator.UnitTests/Aggregator.UnitTests.csproj tests/Aggregator.UnitTests/
COPY tests/Aggregator.IntegrationTests/Aggregator.IntegrationTests.csproj tests/Aggregator.IntegrationTests/
COPY tests/Aggregator.EndToEndTests/Aggregator.EndToEndTests.csproj tests/Aggregator.EndToEndTests/
RUN dotnet restore Aggregator.slnx
COPY . .
RUN dotnet build Aggregator.slnx -c Release --no-restore

FROM build AS test
RUN dotnet tests/Aggregator.UnitTests/bin/Release/net10.0/Aggregator.UnitTests.dll \
 && dotnet tests/Aggregator.IntegrationTests/bin/Release/net10.0/Aggregator.IntegrationTests.dll

FROM build AS test-report
RUN dotnet tool install --tool-path /tools dotnet-coverage --version 18.12.0 \
 && dotnet tool install --tool-path /tools dotnet-reportgenerator-globaltool --version 5.6.0
RUN mkdir -p /report \
 && /tools/dotnet-coverage collect --output /report/unit.cobertura.xml --output-format cobertura \
      "dotnet tests/Aggregator.UnitTests/bin/Release/net10.0/Aggregator.UnitTests.dll -result-xml /report/unit.xml" \
 && /tools/dotnet-coverage collect --output /report/integration.cobertura.xml --output-format cobertura \
      "dotnet tests/Aggregator.IntegrationTests/bin/Release/net10.0/Aggregator.IntegrationTests.dll -result-xml /report/integration.xml" \
 && /tools/reportgenerator "-reports:/report/*.cobertura.xml" -targetdir:/report/coverage -reporttypes:MarkdownSummaryGithub \
      "-assemblyfilters:+Aggregator.Api;+Aggregator.Core;+Aggregator.Infrastructure" \
 && (dotnet run tools/TestReport.cs -- /report/test-report.md /report/coverage/SummaryGithub.md /report/unit.xml /report/integration.xml || true)

FROM scratch AS test-report-export
COPY --from=test-report /report/test-report.md /

FROM build AS e2e
ENTRYPOINT ["sh", "-c", "if [ \"$E2E_SCOPE\" = all ]; then exec dotnet tests/Aggregator.EndToEndTests/bin/Release/net10.0/Aggregator.EndToEndTests.dll; else exec dotnet tests/Aggregator.EndToEndTests/bin/Release/net10.0/Aggregator.EndToEndTests.dll -trait- Duration=Long; fi"]

FROM build AS publish
RUN dotnet publish src/Aggregator.Api/Aggregator.Api.csproj -c Release --no-build -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0.15 AS final
WORKDIR /app
COPY --from=publish /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Aggregator.Api.dll"]

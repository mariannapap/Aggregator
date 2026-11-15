# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props Aggregator.slnx ./
COPY src/Aggregator.Api/Aggregator.Api.csproj src/Aggregator.Api/
COPY src/Aggregator.Core/Aggregator.Core.csproj src/Aggregator.Core/
COPY src/Aggregator.Infrastructure/Aggregator.Infrastructure.csproj src/Aggregator.Infrastructure/
COPY tests/Aggregator.UnitTests/Aggregator.UnitTests.csproj tests/Aggregator.UnitTests/
COPY tests/Aggregator.IntegrationTests/Aggregator.IntegrationTests.csproj tests/Aggregator.IntegrationTests/
RUN dotnet restore Aggregator.slnx
COPY . .
RUN dotnet build Aggregator.slnx -c Release --no-restore

FROM build AS test
RUN dotnet tests/Aggregator.UnitTests/bin/Release/net10.0/Aggregator.UnitTests.dll \
 && dotnet tests/Aggregator.IntegrationTests/bin/Release/net10.0/Aggregator.IntegrationTests.dll

FROM build AS publish
RUN dotnet publish src/Aggregator.Api/Aggregator.Api.csproj -c Release --no-build -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Aggregator.Api.dll"]

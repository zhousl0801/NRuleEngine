FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/NRuleEngine.Domain.Shared/NRuleEngine.Domain.Shared.csproj src/NRuleEngine.Domain.Shared/
COPY src/NRuleEngine.Domain/NRuleEngine.Domain.csproj src/NRuleEngine.Domain/
COPY src/NRuleEngine.Application.Contracts/NRuleEngine.Application.Contracts.csproj src/NRuleEngine.Application.Contracts/
COPY src/NRuleEngine.Application/NRuleEngine.Application.csproj src/NRuleEngine.Application/
COPY src/NRuleEngine.EntityFrameworkCore/NRuleEngine.EntityFrameworkCore.csproj src/NRuleEngine.EntityFrameworkCore/
COPY src/NRuleEngine.HttpApi/NRuleEngine.HttpApi.csproj src/NRuleEngine.HttpApi/
COPY src/NRuleEngine.Host/NRuleEngine.Host.csproj src/NRuleEngine.Host/
RUN dotnet restore src/NRuleEngine.Host/NRuleEngine.Host.csproj

COPY src/ src/
RUN dotnet publish src/NRuleEngine.Host/NRuleEngine.Host.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish ./
RUN mkdir -p /app/data && chown app:app /app/data
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "NRuleEngine.Host.dll"]

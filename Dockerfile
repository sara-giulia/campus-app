FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/CampusMarket.Api/CampusMarket.Api.csproj -c Release -o /app
# Fail the build if the ML model was not copied
RUN test -f /app/ml-models/price-regression.zip

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# Development: uses the dev JWT key and the seeded admin (demo container, not production)
ENV ASPNETCORE_ENVIRONMENT=Development
EXPOSE 8080
ENTRYPOINT ["dotnet", "CampusMarket.Api.dll"]

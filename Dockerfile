FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/CampusMarket.Api/CampusMarket.Api.csproj -c Release -o /app
# Fail the build if one of the trained ML models was not copied
RUN test -f /app/ml-models/price-regression.zip \
    && test -f /app/ml-models/category-classification.zip \
    && test -f /app/ml-models/spam-detection.zip \
    && test -f /app/ml-models/similar-listings.zip

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# curl is used only by the health check below
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
# The SQLite file lives in a folder the non-root user can write to
RUN mkdir /app/data && chown $APP_UID /app/data
ENV ConnectionStrings__CampusMarket="Data Source=/app/data/campusmarket.db"
# Run as the non-root user that the .NET images provide
USER $APP_UID
EXPOSE 8080
# Docker marks the container unhealthy when /health does not answer 200 (API down or database unreachable)
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -fs http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "CampusMarket.Api.dll"]

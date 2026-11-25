# Build the React frontend in its own stage. The dist/ output is copied into
# the API's wwwroot below, so the API serves the SPA from the same origin and
# the browser calls /api/v1/... on that same host (no CORS, no absolute URL).
FROM node:20-alpine AS frontend-build
WORKDIR /frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci --ignore-scripts
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/CampusMarket.Api/CampusMarket.Api.csproj -c Release -o /app
# Fail the build if one of the trained ML models was not copied
RUN test -f /app/ml-models/price-regression.zip \
    && test -f /app/ml-models/category-classification.zip \
    && test -f /app/ml-models/spam-detection.zip \
    && test -f /app/ml-models/similar-listings.zip

FROM mcr.microsoft.com/dotnet/aspnet:11.0
# curl is used only by the health check below; libgomp1 is needed by the native LightGBM library of the price model
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl libgomp1 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
# Static SPA files served by UseStaticFiles; deep links fall back to index.html
COPY --from=frontend-build /frontend/dist ./wwwroot
# The SQLite file lives in a folder the non-root user can write to
RUN mkdir /app/data && chown $APP_UID /app/data
ENV ConnectionStrings__CampusMarket="Data Source=/app/data/campusmarket.db"
# The photo files next to the database, in the same volume
ENV Storage__PhotosPath=/app/data/photos
# Run as the non-root user that the .NET images provide
USER $APP_UID
EXPOSE 8080
# Docker marks the container unhealthy when /health does not answer 200 (API down or database unreachable)
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -fs http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "CampusMarket.Api.dll"]

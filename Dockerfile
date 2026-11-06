FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/CampusMarket.Api/CampusMarket.Api.csproj -c Release -o /app
# Fail the build if the ML model was not copied
RUN test -f /app/ml-models/price-regression.zip

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# The SQLite file lives in a folder the non-root user can write to
RUN mkdir /app/data && chown $APP_UID /app/data
ENV ConnectionStrings__CampusMarket="Data Source=/app/data/campusmarket.db"
# Run as the non-root user that the .NET images provide
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "CampusMarket.Api.dll"]


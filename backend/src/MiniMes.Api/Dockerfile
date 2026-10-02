# Build context is backend/ (see docker-compose.yml), so global.json and Directory.Build.props apply.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props ./
COPY src/MiniMes.Api/MiniMes.Api.csproj src/MiniMes.Api/
RUN dotnet restore src/MiniMes.Api/MiniMes.Api.csproj

COPY src/MiniMes.Api/ src/MiniMes.Api/
RUN dotnet publish src/MiniMes.Api/MiniMes.Api.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# PlantCalendar resolves "Asia/Jakarta" through the system tz database.
RUN apt-get update \
    && apt-get install -y --no-install-recommends tzdata \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "MiniMes.Api.dll"]

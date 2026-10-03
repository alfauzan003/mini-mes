# Build context is backend/ (see docker-compose.yml), so global.json and Directory.Build.props apply.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props ./
COPY src/MiniMes.Simulator/MiniMes.Simulator.csproj src/MiniMes.Simulator/
RUN dotnet restore src/MiniMes.Simulator/MiniMes.Simulator.csproj

COPY src/MiniMes.Simulator/ src/MiniMes.Simulator/
RUN dotnet publish src/MiniMes.Simulator/MiniMes.Simulator.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "MiniMes.Simulator.dll"]

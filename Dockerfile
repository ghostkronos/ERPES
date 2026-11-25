# ==========================================
# Build Stage (.NET 10 SDK)
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["ERPES.ControlTower.csproj", "./"]
RUN dotnet restore "ERPES.ControlTower.csproj"

COPY . .
RUN dotnet publish "ERPES.ControlTower.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ==========================================
# Runtime Stage (ASP.NET Core 10)
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Bind dynamically to $PORT for Render, Railway, and Fly.io compatibility
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet ERPES.ControlTower.dll"]

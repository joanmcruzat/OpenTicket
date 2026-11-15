# syntax=docker/dockerfile:1
# One image serves the API and the Angular app (docs/architecture.md §1.1).

# ---- 1. Angular build ----
FROM node:26-alpine AS web
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY web/ ./
RUN npx ng build --configuration production

# ---- 2. .NET publish ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/OpenTicket.Domain/OpenTicket.Domain.csproj src/OpenTicket.Domain/packages.lock.json src/OpenTicket.Domain/
COPY src/OpenTicket.Application/OpenTicket.Application.csproj src/OpenTicket.Application/packages.lock.json src/OpenTicket.Application/
COPY src/OpenTicket.Infrastructure/OpenTicket.Infrastructure.csproj src/OpenTicket.Infrastructure/packages.lock.json src/OpenTicket.Infrastructure/
COPY src/OpenTicket.API/OpenTicket.API.csproj src/OpenTicket.API/packages.lock.json src/OpenTicket.API/
RUN dotnet restore src/OpenTicket.API/OpenTicket.API.csproj --locked-mode
COPY src/ src/
RUN dotnet publish src/OpenTicket.API/OpenTicket.API.csproj -c Release -o /app --no-restore /p:UseAppHost=false
COPY --from=web /web/dist/openticket-web/browser /app/wwwroot

# ---- 3. Runtime: chiseled (no shell, no package manager), runs as non-root ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    ForwardedHeaders__Enabled=true
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "OpenTicket.API.dll"]

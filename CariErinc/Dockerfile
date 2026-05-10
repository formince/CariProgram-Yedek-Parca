# Railway: Build "Dockerfile" ile bu klasörü kök (Root Directory = CariErinc) seçin.
#
# Zorunlu env (PostgreSQL — Railway eklentisinden veya manuel):
#   ConnectionStrings__DefaultConnection  → ana uygulama DB (tek kiracı veya geliştirme)
#   ConnectionStrings__AdminConnection    → TenantKayit tablosu (admin registry)
#
# Multi-tenant (isteğe bağlı):
#   MultiTenant__Enabled    = true|false
#   MultiTenant__BaseDomain = senindomain.com  (wildcard DNS + SSL ayrıca)
#
# Railway PORT otomatik verilir; aşağıdaki CMD buna uyum sağlar.
#
# Öneri: ASPNETCORE_ENVIRONMENT=Production (Railway Variables)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["CariErinc.csproj", "./"]
RUN dotnet restore "CariErinc.csproj"

COPY . .
RUN dotnet publish "CariErinc.csproj" -c Release -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends libkrb5-3 \
    && rm -rf /var/lib/apt/lists/*
USER app
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

EXPOSE 8080

# shell: Railway'in verdiği PORT
ENTRYPOINT ["sh", "-c", "exec dotnet CariErinc.dll --urls \"http://0.0.0.0:${PORT:-8080}\""]

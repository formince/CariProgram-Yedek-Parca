# Repo kökünden build: Railway Root Directory boş / repo root.
# Alternatif: Railway Root Directory = CariErinc → o klasördeki Dockerfile kullanın.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["CariErinc/CariErinc.csproj", "CariErinc/"]
RUN dotnet restore "CariErinc/CariErinc.csproj"

COPY CariErinc/ CariErinc/
WORKDIR /src/CariErinc
RUN dotnet publish "CariErinc.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080
ENTRYPOINT ["sh", "-c", "exec dotnet CariErinc.dll --urls \"http://0.0.0.0:${PORT:-8080}\""]

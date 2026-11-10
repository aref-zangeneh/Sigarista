FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .

# The restore source is overridable so the image can still be built on networks
# where api.nuget.org (Akamai) is unreachable from the Docker VM:
#   docker compose build --build-arg NUGET_SOURCE=https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json api
ARG NUGET_SOURCE=https://api.nuget.org/v3/index.json
RUN dotnet restore "src/Sugarista.Api/Sugarista.Api.csproj" --source "$NUGET_SOURCE"
RUN dotnet publish "src/Sugarista.Api/Sugarista.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl fontconfig \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Sugarista.Api.dll"]

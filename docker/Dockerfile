# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props ./
COPY src/SalesApi.Domain/SalesApi.Domain.csproj src/SalesApi.Domain/
COPY src/SalesApi.Application/SalesApi.Application.csproj src/SalesApi.Application/
COPY src/SalesApi.Infrastructure/SalesApi.Infrastructure.csproj src/SalesApi.Infrastructure/
COPY src/SalesApi.Api/SalesApi.Api.csproj src/SalesApi.Api/
RUN dotnet restore src/SalesApi.Api/SalesApi.Api.csproj

COPY src/ src/
RUN dotnet publish src/SalesApi.Api/SalesApi.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd --system salesapi \
    && useradd --system --gid salesapi --no-create-home salesapi

COPY --from=build /app/publish .
RUN chown -R salesapi:salesapi /app

USER salesapi

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SalesApi.Api.dll"]

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

# Stage do migrator: gera um EF Core migration bundle self-contained (executável único, sem
# dependência do SDK nem do dotnet-ef na imagem final), usado pelo serviço `migrator` do
# docker-compose para aplicar migrations fora do processo de runtime da Api.
#
# TARGETARCH é preenchido automaticamente pelo BuildKit com a arquitetura de build (amd64 em
# runners de CI comuns, arm64 em Apple Silicon) — o bundle self-contained precisa ser gerado para
# a MESMA arquitetura da imagem final (runtime-deps:8.0 abaixo), senão falha ao rodar (o runtime
# .NET não faz cross-arch automaticamente como um binário framework-dependent faria).
FROM build AS bundle
ARG TARGETARCH
RUN dotnet tool install --global dotnet-ef --version 8.0.11
ENV PATH="$PATH:/root/.dotnet/tools"
RUN RID=$(if [ "$TARGETARCH" = "arm64" ]; then echo linux-arm64; else echo linux-x64; fi) && \
    dotnet ef migrations bundle \
    --project src/SalesApi.Infrastructure/SalesApi.Infrastructure.csproj \
    --startup-project src/SalesApi.Api/SalesApi.Api.csproj \
    --configuration Release \
    --self-contained \
    --target-runtime "$RID" \
    --output /app/efbundle/migrate

FROM mcr.microsoft.com/dotnet/runtime-deps:8.0 AS migrator
WORKDIR /app
COPY --from=bundle /app/efbundle/migrate ./migrate
ENTRYPOINT ["./migrate"]

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

FROM mcr.microsoft.com/dotnet/sdk:10.0.400-noble@sha256:e1fc6e423f543119c406d24e2e687d67c569f18f04a37a8b0005d80ad0dcee80 AS build
WORKDIR /src
COPY portal/ReneB.Portal.csproj portal/
RUN dotnet restore portal/ReneB.Portal.csproj
COPY portal/ portal/
RUN dotnet publish portal/ReneB.Portal.csproj --configuration Release --no-restore --output /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.11-noble@sha256:207cc51496778557731c81ff670333d8ade4a4fec22768fd1be8e78474a84ecf
USER root
RUN apt-get update \
    && apt-get install --yes --no-install-recommends ca-certificates curl qpdf \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd --gid 10001 portal \
    && useradd --uid 10001 --gid portal --no-create-home --shell /usr/sbin/nologin portal

WORKDIR /app
RUN mkdir --parents /app/data /app/backups \
    && chown --recursive 10001:10001 /app
COPY --from=build --chown=10001:10001 /out/ /app/

ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
USER 10001:10001
EXPOSE 8080
ENTRYPOINT ["dotnet", "ReneB.Portal.dll"]

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD curl --fail --silent --show-error http://127.0.0.1:8080/healthz >/dev/null || exit 1

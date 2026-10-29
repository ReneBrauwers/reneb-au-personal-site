FROM mcr.microsoft.com/dotnet/sdk:10.0.403-noble@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b AS build
WORKDIR /src
COPY portal/ReneB.Portal.csproj portal/
RUN dotnet restore portal/ReneB.Portal.csproj
COPY portal/ portal/
RUN dotnet publish portal/ReneB.Portal.csproj --configuration Release --no-restore --output /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14-noble@sha256:de0ee358cdd672f7017a003bed21e9ac9ed48a27c922a59b18175b6e82fa7fee
USER root
RUN apt-get update \
    && apt-get install --yes --no-install-recommends ca-certificates curl poppler-utils qpdf \
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

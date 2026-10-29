# syntax=docker/dockerfile:1
FROM debian:bookworm-slim AS base
RUN --mount=type=secret,id=system_ca,mode=0444 \
    set -eu; \
    if [ -f /run/secrets/system_ca ]; then \
      sed -i 's|http://deb.debian.org|https://deb.debian.org|g' /etc/apt/sources.list.d/debian.sources; \
      export APT_CONFIG=/tmp/campus-apt.conf; \
      printf 'Acquire::https::CaInfo "/run/secrets/system_ca";\n' > "$APT_CONFIG"; fi; \
    apt-get update && apt-get install -y --no-install-recommends \
      php-cli php-sqlite3 nodejs ca-certificates curl libicu72 libssl3 libgssapi-krb5-2 zlib1g \
    && sed -i 's|http://deb.debian.org|https://deb.debian.org|g' /etc/apt/sources.list.d/debian.sources \
    && rm -rf /var/lib/apt/lists/* /tmp/campus-apt.conf
ENV DOTNET_ROOT=/opt/dotnet PATH=/opt/dotnet:$PATH

FROM base AS build
ARG TARGETARCH
ARG DOTNET_SDK_VERSION=8.0.427
RUN --mount=type=secret,id=system_ca,mode=0444 \
    set -eu; \
    if [ -f /run/secrets/system_ca ]; then export CURL_CA_BUNDLE=/run/secrets/system_ca; fi; \
    case "$TARGETARCH" in amd64) arch=x64;; arm64) arch=arm64;; *) exit 1;; esac; \
    url="https://builds.dotnet.microsoft.com/dotnet/Sdk/$DOTNET_SDK_VERSION/dotnet-sdk-$DOTNET_SDK_VERSION-linux-$arch.tar.gz"; \
    curl -fsSL "$url" -o /tmp/sdk.tar.gz; \
    curl -fsSL "$url.sha512" -o /tmp/sdk.sha512; \
    printf '%s  /tmp/sdk.tar.gz\n' "$(awk '{print $1}' /tmp/sdk.sha512)" | sha512sum -c -; \
    mkdir -p /opt/dotnet; tar -xzf /tmp/sdk.tar.gz -C /opt/dotnet; \
    rm /tmp/sdk.tar.gz /tmp/sdk.sha512
WORKDIR /src
COPY global.json ./
COPY queue/SmartQueueSystemGUI.csproj queue/
RUN --mount=type=secret,id=system_ca,mode=0444 \
    if [ -f /run/secrets/system_ca ]; then export SSL_CERT_FILE=/run/secrets/system_ca; fi; \
    dotnet restore queue/SmartQueueSystemGUI.csproj
COPY queue/ queue/
RUN dotnet publish queue/SmartQueueSystemGUI.csproj -c Release --no-restore -o /published

FROM base AS final
COPY --from=build /opt/dotnet/dotnet /opt/dotnet/dotnet
COPY --from=build /opt/dotnet/LICENSE.txt /opt/dotnet/ThirdPartyNotices.txt /opt/dotnet/
COPY --from=build /opt/dotnet/host/ /opt/dotnet/host/
COPY --from=build /opt/dotnet/shared/ /opt/dotnet/shared/
WORKDIR /app
COPY --from=build /published ./queue/
COPY queue/Data/ ./queue/Data/
COPY sims/ ./sims/
COPY scripts/ ./scripts/
RUN useradd --create-home --uid 10001 campus \
    && mkdir -p /app/.runtime/sessions /app/data \
    && chown -R campus:campus /app
ENV HOST=0.0.0.0 PORT=8080 ASPNETCORE_ENVIRONMENT=Production \
    CAMPUS_QUEUE_DLL=/app/queue/SmartQueueSystemGUI.dll CAMPUS_DATA_DIR=/app/data
USER campus
EXPOSE 8080
CMD ["node", "scripts/start.mjs"]

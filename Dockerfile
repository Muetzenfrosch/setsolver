# Set-Finder als Docker-Image: ein schlanker Nginx, der die fertige App ausliefert, sonst nichts.
#
#   docker build --pull -t set-finder .
#   docker run -d -p 8080:80 --restart unless-stopped --name set-finder set-finder
#
# Mit --pull holt Docker bei jedem Bau die aktuellen Basis-Images: die neueste stabile Nginx-Version
# (Tag stable-alpine-slim) und das neueste .NET-10-SDK. Neu bauen genügt also für Updates.

# 1. App bauen. Das SDK wird nur hier gebraucht und landet nicht im fertigen Image.
#    Die App ist WebAssembly und damit plattformunabhängig, deshalb läuft diese Stufe immer nativ.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props ./
COPY src/ src/
# Nginx liefert keine Brotli-Dateien (.br) aus, und .htaccess gilt nur für Apache: beides weglassen.
RUN dotnet publish src/SetGameSolver.Web --configuration Release --output /publish \
    && find /publish/wwwroot -name '*.br' -delete \
    && rm /publish/wwwroot/.htaccess

# 2. Webserver mit der App.
FROM nginx:stable-alpine-slim
LABEL org.opencontainers.image.title="Set-Finder" \
      org.opencontainers.image.description="Finds sets in a photo of SET cards, entirely in the browser" \
      org.opencontainers.image.source="https://github.com/Muetzenfrosch/setsolver" \
      org.opencontainers.image.licenses="MIT"
COPY deploy/docker/default.conf /etc/nginx/conf.d/default.conf
COPY --from=build /publish/wwwroot /usr/share/nginx/html

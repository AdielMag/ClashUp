# Multistage build for ClashUp.Gateway (version-aware reverse proxy + process supervisor).
# Build from the repo root:
#   docker build -f ops/docker/gateway.Dockerfile -t clashup-gateway .
#
# At runtime the gateway talks to the host Docker daemon to spawn per-version
# backend containers, so the host's Docker socket must be mounted:
#   docker run --network host -v /var/run/docker.sock:/var/run/docker.sock clashup-gateway

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props AetherNet.refs.props ./
COPY ClashUp.sln ./

COPY libs/ libs/
COPY src/Shared/ClashUp.Shared/*.csproj src/Shared/ClashUp.Shared/
COPY src/Server/ClashUp.Server.Common/*.csproj src/Server/ClashUp.Server.Common/
COPY src/Server/ClashUp.Gateway/*.csproj src/Server/ClashUp.Gateway/
RUN dotnet restore src/Server/ClashUp.Gateway/ClashUp.Gateway.csproj

COPY src/Shared/ClashUp.Shared/ src/Shared/ClashUp.Shared/
COPY src/Server/ClashUp.Server.Common/ src/Server/ClashUp.Server.Common/
COPY src/Server/ClashUp.Gateway/ src/Server/ClashUp.Gateway/

ARG VERSION=0.0.1
RUN dotnet publish src/Server/ClashUp.Gateway/ClashUp.Gateway.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    -p:Version=$VERSION \
    -p:InformationalVersion=$VERSION

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
# Gateway listens on the gRPC port (5101 default) and an admin/health port (9101).
EXPOSE 5101
EXPOSE 9101

# Draining the gateway should let in-flight matches finish on their backends.
STOPSIGNAL SIGTERM

ENTRYPOINT ["dotnet", "ClashUp.Gateway.dll"]

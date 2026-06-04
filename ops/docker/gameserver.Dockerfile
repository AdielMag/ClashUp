# Multistage build for ClashUp.GameServer.
# Build from the repo root:
#   docker build -f ops/docker/gameserver.Dockerfile -t clashup-gameserver .

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props AetherNet.refs.props ./
COPY ClashUp.sln ./

COPY src/Shared/ClashUp.Shared/*.csproj src/Shared/ClashUp.Shared/
COPY src/Server/ClashUp.Server.Common/*.csproj src/Server/ClashUp.Server.Common/
COPY src/Server/ClashUp.GameServer/*.csproj src/Server/ClashUp.GameServer/
RUN dotnet restore src/Server/ClashUp.GameServer/ClashUp.GameServer.csproj

COPY src/Shared/ClashUp.Shared/ src/Shared/ClashUp.Shared/
COPY src/Server/ClashUp.Server.Common/ src/Server/ClashUp.Server.Common/
COPY src/Server/ClashUp.GameServer/ src/Server/ClashUp.GameServer/

RUN dotnet publish src/Server/ClashUp.GameServer/ClashUp.GameServer.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://0.0.0.0:5101
EXPOSE 5101

# IHostApplicationLifetime hooks pick up SIGTERM and run GracefulDrainService.
STOPSIGNAL SIGTERM

ENTRYPOINT ["dotnet", "ClashUp.GameServer.dll"]

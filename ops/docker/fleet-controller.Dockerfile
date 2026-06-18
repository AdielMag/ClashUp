# Multistage build for ClashUp.FleetController (idle-fleet sleep/wake controller).
# Deployed to Cloud Run; invoked by Cloud Scheduler (/tick) and the dashboard (/wake).
# Build from the repo root:
#   docker build -f ops/docker/fleet-controller.Dockerfile -t clashup-fleet-controller .

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Tools/ClashUp.FleetController/*.csproj src/Tools/ClashUp.FleetController/
RUN dotnet restore src/Tools/ClashUp.FleetController/ClashUp.FleetController.csproj

COPY src/Tools/ClashUp.FleetController/ src/Tools/ClashUp.FleetController/

ARG VERSION=0.0.1
RUN dotnet publish src/Tools/ClashUp.FleetController/ClashUp.FleetController.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    -p:Version=$VERSION \
    -p:InformationalVersion=$VERSION

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "ClashUp.FleetController.dll"]

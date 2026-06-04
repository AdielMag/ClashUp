# Multistage build for ClashUp.Services.
# Build from the repo root:
#   docker build -f ops/docker/services.Dockerfile -t clashup-services .

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the centralized build config first so each subsequent COPY can
# benefit from layer caching.
COPY global.json Directory.Build.props Directory.Packages.props AetherNet.refs.props ./
COPY ClashUp.sln ./

COPY src/Shared/ClashUp.Shared/*.csproj src/Shared/ClashUp.Shared/
COPY src/Server/ClashUp.Server.Common/*.csproj src/Server/ClashUp.Server.Common/
COPY src/Server/ClashUp.Services/*.csproj src/Server/ClashUp.Services/
RUN dotnet restore src/Server/ClashUp.Services/ClashUp.Services.csproj

COPY src/Shared/ClashUp.Shared/ src/Shared/ClashUp.Shared/
COPY src/Server/ClashUp.Server.Common/ src/Server/ClashUp.Server.Common/
COPY src/Server/ClashUp.Services/ src/Server/ClashUp.Services/

RUN dotnet publish src/Server/ClashUp.Services/ClashUp.Services.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://0.0.0.0:5001
EXPOSE 5001

ENTRYPOINT ["dotnet", "ClashUp.Services.dll"]

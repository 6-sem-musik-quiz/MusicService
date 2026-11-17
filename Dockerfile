FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so this layer is cached until a csproj changes
COPY global.json Directory.Build.props ./
COPY src/MusicService.Api/MusicService.Api.csproj src/MusicService.Api/
COPY src/MusicService.Service/MusicService.Service.csproj src/MusicService.Service/
COPY src/MusicService.Repository/MusicService.Repository.csproj src/MusicService.Repository/
COPY src/MusicService.Domain/MusicService.Domain.csproj src/MusicService.Domain/
RUN dotnet restore src/MusicService.Api/MusicService.Api.csproj

COPY src/ src/
RUN dotnet publish src/MusicService.Api/MusicService.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
# The non-root "app" user (uid 1654) that the .NET images ship with.
USER $APP_UID
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MusicService.Api.dll"]

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/WalletsHub.Api/WalletsHub.Api.csproj src/WalletsHub.Api/
RUN dotnet restore src/WalletsHub.Api/WalletsHub.Api.csproj
COPY src/WalletsHub.Api/ src/WalletsHub.Api/
RUN dotnet publish src/WalletsHub.Api/WalletsHub.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app
COPY --from=build /app .
USER app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "WalletsHub.Api.dll"]

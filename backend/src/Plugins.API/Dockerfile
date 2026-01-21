FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Plugins.API/Plugins.API.csproj", "src/Plugins.API/"]
COPY ["BuildingBlocks/Mozaiks.Auth/Mozaiks.Auth.csproj", "BuildingBlocks/Mozaiks.Auth/"]
COPY ["BuildingBlocks/Mozaiks.Auditing/Mozaiks.Auditing.csproj", "BuildingBlocks/Mozaiks.Auditing/"]
COPY ["AspireAdmin/blocunited-microservice.ServiceDefaults/blocunited-microservice.ServiceDefaults.csproj", "AspireAdmin/blocunited-microservice.ServiceDefaults/"]
RUN dotnet restore "src/Plugins.API/Plugins.API.csproj"
COPY . .
WORKDIR "/src/src/Plugins.API"
RUN dotnet build "Plugins.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Plugins.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Plugins.API.dll"]

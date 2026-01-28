# Mozaiks Plugins Service Dockerfile

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["services/Mozaiks.Plugins/Mozaiks.Plugins.csproj", "services/Mozaiks.Plugins/"]
COPY ["aspire/Mozaiks.ServiceDefaults/Mozaiks.ServiceDefaults.csproj", "aspire/Mozaiks.ServiceDefaults/"]
COPY ["building-blocks/Mozaiks.Auditing/Mozaiks.Auditing.csproj", "building-blocks/Mozaiks.Auditing/"]
COPY ["building-blocks/Mozaiks.Auth/Mozaiks.Auth.csproj", "building-blocks/Mozaiks.Auth/"]
RUN dotnet restore "services/Mozaiks.Plugins/Mozaiks.Plugins.csproj"
COPY . .
WORKDIR "/src/services/Mozaiks.Plugins"
RUN dotnet build "Mozaiks.Plugins.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Mozaiks.Plugins.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Mozaiks.Plugins.dll"]

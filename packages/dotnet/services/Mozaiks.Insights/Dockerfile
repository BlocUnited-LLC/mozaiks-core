# Mozaiks Insights Service Dockerfile

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["services/Mozaiks.Insights/Mozaiks.Insights.csproj", "services/Mozaiks.Insights/"]
COPY ["aspire/Mozaiks.ServiceDefaults/Mozaiks.ServiceDefaults.csproj", "aspire/Mozaiks.ServiceDefaults/"]
COPY ["building-blocks/Mozaiks.ApiKeys/Mozaiks.ApiKeys.csproj", "building-blocks/Mozaiks.ApiKeys/"]
COPY ["building-blocks/Mozaiks.Auditing/Mozaiks.Auditing.csproj", "building-blocks/Mozaiks.Auditing/"]
COPY ["building-blocks/Mozaiks.Auth/Mozaiks.Auth.csproj", "building-blocks/Mozaiks.Auth/"]
RUN dotnet restore "services/Mozaiks.Insights/Mozaiks.Insights.csproj"
COPY . .
WORKDIR "/src/services/Mozaiks.Insights"
RUN dotnet build "Mozaiks.Insights.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Mozaiks.Insights.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Mozaiks.Insights.dll"]

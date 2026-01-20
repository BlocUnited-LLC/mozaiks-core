FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Services/Insights/Insights.API/Insights.API.csproj", "Services/Insights/Insights.API/"]
COPY ["AspireAdmin/blocunited-microservice.ServiceDefaults/blocunited-microservice.ServiceDefaults.csproj", "AspireAdmin/blocunited-microservice.ServiceDefaults/"]
RUN dotnet restore "Services/Insights/Insights.API/Insights.API.csproj"
COPY . .
WORKDIR "/src/Services/Insights/Insights.API"
RUN dotnet build "Insights.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Insights.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Insights.API.dll"]

# Mozaiks Billing Service Dockerfile

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["services/Mozaiks.Billing/Mozaiks.Billing.csproj", "services/Mozaiks.Billing/"]
COPY ["aspire/Mozaiks.ServiceDefaults/Mozaiks.ServiceDefaults.csproj", "aspire/Mozaiks.ServiceDefaults/"]
COPY ["building-blocks/Mozaiks.EventBus/Mozaiks.EventBus.csproj", "building-blocks/Mozaiks.EventBus/"]
COPY ["building-blocks/Mozaiks.Auditing/Mozaiks.Auditing.csproj", "building-blocks/Mozaiks.Auditing/"]
COPY ["building-blocks/Mozaiks.Auth/Mozaiks.Auth.csproj", "building-blocks/Mozaiks.Auth/"]
RUN dotnet restore "services/Mozaiks.Billing/Mozaiks.Billing.csproj"
COPY . .
WORKDIR "/src/services/Mozaiks.Billing"
RUN dotnet build "Mozaiks.Billing.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Mozaiks.Billing.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Mozaiks.Billing.dll"]

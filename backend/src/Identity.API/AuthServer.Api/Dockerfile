#See https://aka.ms/customizecontainer to learn how to customize your debug container

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Identity.API/AuthServer.Api/AuthServer.Api.csproj", "src/Identity.API/AuthServer.Api/"]
COPY ["AspireAdmin/blocunited-microservice.ServiceDefaults/blocunited-microservice.ServiceDefaults.csproj", "AspireAdmin/blocunited-microservice.ServiceDefaults/"]
RUN dotnet restore "src/Identity.API/AuthServer.Api/AuthServer.Api.csproj"
COPY . .
WORKDIR "/src/src/Identity.API/AuthServer.Api"
RUN dotnet build "AuthServer.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AuthServer.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AuthServer.Api.dll"]

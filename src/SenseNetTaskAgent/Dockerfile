FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
WORKDIR "/src/SenseNetTaskAgent"
RUN dotnet restore "SenseNetTaskAgent.csproj" 
RUN dotnet build "SenseNetTaskAgent.csproj" -c Release -o /app/build

FROM build AS publish
WORKDIR "/src/SenseNetTaskAgent/"
RUN dotnet publish "SenseNetTaskAgent.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SenseNetTaskAgent.dll"]
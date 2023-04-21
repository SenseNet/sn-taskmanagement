FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "TaskManagement.sln" 
WORKDIR "/src/SenseNetTaskAgent"
RUN dotnet build "SenseNetTaskAgent.csproj" -c Release -o /app/build

FROM build AS publish
WORKDIR "/src/SenseNetTaskAgent/"
RUN dotnet publish "SenseNetTaskAgent.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
RUN apt-get update \
    && apt-get install -y libc6-dev \
    && apt-get install -y libgdiplus \
    && apt-get install -y libx11-dev \    
    && apt-get install -y libfontconfig1
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SenseNetTaskAgent.dll"]
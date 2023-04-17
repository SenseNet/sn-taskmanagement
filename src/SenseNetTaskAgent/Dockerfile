#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "SenseNetTaskAgent/SenseNetTaskAgent.csproj"
WORKDIR "/src/SenseNetTaskAgent"
RUN dotnet build "SenseNetTaskAgent.csproj" -c Release -o /app/build
WORKDIR "/src/TestExecutor/"
RUN dotnet build "TestExecutor.csproj" -c Release -o /app/testexecutorbuild

FROM build AS publish
RUN dotnet publish "SenseNetTaskAgent.csproj" -c Release -o /app/publish /p:UseAppHost=false
WORKDIR "/src/TestExecutor/"
RUN dotnet publish "TestExecutor.csproj" -c Release -o /app/testexecutorpublish

FROM base AS final
RUN apt-get update && apt-get install -y iputils-ping \
    && apt-get install -y libc6-dev \
    && apt-get install -y libgdiplus \
    && apt-get install -y libx11-dev \    
    && apt-get install -y libfontconfig1
RUN mkdir /app/TaskExecutors/TestExecutor -p
WORKDIR /app
COPY --from=publish /app/publish .
WORKDIR /app/TaskExecutors/TestExecutor
COPY --from=publish /app/testexecutorpublish .
WORKDIR /app
ENTRYPOINT ["dotnet", "SenseNetTaskAgent.dll"]
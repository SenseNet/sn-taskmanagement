ARG TASKAGENT_VERSION=latest
FROM sensenetcsp/sn-taskagent:${TASKAGENT_VERSION} AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
WORKDIR "/src/TestExecutor"
RUN dotnet restore "TestExecutor.csproj"
RUN dotnet build "TestExecutor.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TestExecutor.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
RUN mkdir /app/TaskExecutors/TestExecutor -p
WORKDIR /app/TaskExecutors/TestExecutor
COPY --from=publish /app/publish .
WORKDIR /app
ENTRYPOINT ["dotnet", "SenseNetTaskAgent.dll"]
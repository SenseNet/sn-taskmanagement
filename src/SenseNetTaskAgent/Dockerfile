FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "TaskManagement.sln" 
WORKDIR "/src/SenseNetTaskAgent"
RUN dotnet build "SenseNetTaskAgent.csproj" -c Release -o /app/build
WORKDIR "/src/TestExecutor/"
RUN dotnet build "TestExecutor.csproj" -c Release -o /app/testexecutorbuild

FROM build AS publish
WORKDIR "/src/SenseNetTaskAgent/"
RUN dotnet publish "SenseNetTaskAgent.csproj" -c Release -o /app/publish /p:UseAppHost=false
WORKDIR "/src/TestExecutor/"
RUN dotnet publish "TestExecutor.csproj" -c Release -o /app/testexecutorpublish

FROM sensenetcsp/sn-preview-aspose:feature-containerization.2023.04.18 AS asposeexecutorpublish

FROM base AS final
RUN apt-get update && apt-get install -y iputils-ping \
    && apt-get install -y libc6-dev \
    && apt-get install -y libgdiplus \
    && apt-get install -y libx11-dev \    
    && apt-get install -y libfontconfig1
RUN mkdir /app/TaskExecutors/TestExecutor -p
RUN mkdir /app/TaskExecutors/AsposePreviewGenerator -p
WORKDIR /app
COPY --from=publish /app/publish .
WORKDIR /app/TaskExecutors/TestExecutor
COPY --from=publish /app/testexecutorpublish .
WORKDIR /app/TaskExecutors/AsposePreviewGenerator
COPY --from=asposeexecutorpublish /app .
WORKDIR /app
ENTRYPOINT ["dotnet", "SenseNetTaskAgent.dll"]
using System;
using System.Collections.Generic;
using System.IO;
// ReSharper disable InconsistentNaming

namespace SenseNet.TaskManagement.TaskAgent
{
    internal static class Hub
    {
        public const string Name = "AgentHub";
        public const string GetTaskMethod = "GetTask";
        public const string RefreshLockMethod = "RefreshLock";
        public const string HeartbeatMethod = "Heartbeat";
        public const string TaskFinished = "TaskFinished";
        public const string WriteProgressMethod = "WriteProgress";
        public const string StartSubtaskMethod = "StartSubtask";
        public const string FinishSubtaskMethod = "FinishSubtask";
    }

    public class AgentConfiguration
    {
        public string TaskManagementUrl { get; set; } = "https://localhost:5001";
        public int UpdateLockPeriodInSeconds { get; set; } = 15;
        public int ExecutorTimeoutInSeconds { get; set; } = 60;
        public int HeartbeatPeriodInSeconds { get; set; } = 30;
        public string TaskExecutorDirectory { get; set; } = 
            Path.Combine(Environment.CurrentDirectory, "TaskExecutors");

        //TODO: explicit executors feature has been removed temporarily
        // public Dictionary<string, string> Executors { get; set; }

        public List<ApplicationConfig> Applications { get; set; } = new List<ApplicationConfig>();
    }

    public class ApplicationConfig
    {
        public string AppId { get; set; }
        public string ClientId { get; set; }
        public string Secret { get; set; }
        public string ApiKey { get; set; }
    }
}

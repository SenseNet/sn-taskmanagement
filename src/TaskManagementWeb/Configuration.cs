using System;
using System.Configuration;
using SenseNet.TaskManagement.Core.Configuration;

namespace SenseNet.TaskManagement.Web
{
    public class TaskManagementConfiguration
    {
        //TODO: put all config values here
        public int TaskExecutionTimeoutInSeconds { get; set; }
    }

    //UNDONE: modernize configuration handling
    internal static class Configuration
    {
        //private static readonly string TaskDatabaseConnectrionStringKey = "TaskDatabase";
        //private static readonly string SignalRDatabaseConnectrionStringKey = "SignalRDatabase";
        private static readonly string SignalRSqlEnabledKey = "SignalRSqlEnabled";
        private static readonly string TaskExecutionTimeoutInSecondsKey = "TaskExecutionTimeoutInSeconds";

        internal static string ConnectionString { get; set; }

        private static bool? _signalRSqlEnabled;
        internal static bool SignalRSqlEnabled
        {
            get
            {
                if (!_signalRSqlEnabled.HasValue)
                {
                    bool value;
                    var setting = ConfigurationManager.AppSettings[SignalRSqlEnabledKey];
                    if (string.IsNullOrEmpty(setting) || !bool.TryParse(setting, out value))
                        value = false;
                    _signalRSqlEnabled = value;
                }
                return _signalRSqlEnabled.Value;
            }
        }
        
        private static int? _taskExecutionTimeoutInSeconds;
        private static int _defaultTaskExecutionTimeoutInSeconds = 30;
        /// <summary>After the the timeout the task lock will expire so any agent can claim the task.</summary>
        internal static int TaskExecutionTimeoutInSeconds
        {
            get
            {
                if (_taskExecutionTimeoutInSeconds == null)
                {
                    int value;
                    var setting = ConfigurationManager.AppSettings[TaskExecutionTimeoutInSecondsKey];
                    if (String.IsNullOrEmpty(setting) || !Int32.TryParse(setting, out value))
                        value = _defaultTaskExecutionTimeoutInSeconds;
                    _taskExecutionTimeoutInSeconds = value;
                }
                return _taskExecutionTimeoutInSeconds.Value;
            }
        }
    }
}
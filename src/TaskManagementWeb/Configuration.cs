using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using SenseNet.TaskManagement.Core.Configuration;

namespace SenseNet.TaskManagement.Web
{
    //UNDONE: modernize configuration handling
    internal static class Configuration
    {
        private static readonly string TaskDatabaseConnectrionStringKey = "TaskDatabase";
        private static readonly string SignalRDatabaseConnectrionStringKey = "SignalRDatabase";
        private static readonly string SignalRSqlEnabledKey = "SignalRSqlEnabled";
        private static readonly string TaskExecutionTimeoutInSecondsKey = "TaskExecutionTimeoutInSeconds";

        private static string _taskDatabaseConnectionString;
        internal static string ConnectionString
        {
            get
            {
                if (_taskDatabaseConnectionString == null)
                {
                    var setting = ConfigurationManager.ConnectionStrings[TaskDatabaseConnectrionStringKey];
                    _taskDatabaseConnectionString = setting == null ? string.Empty : setting.ConnectionString;
                }

                return _taskDatabaseConnectionString;
            }
            set => _taskDatabaseConnectionString = value;
        }

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

        private static string _signalrDatabaseConnectionString;
        internal static string SignalRDatabaseConnectionString
        {
            get
            {
                if (_signalrDatabaseConnectionString == null)
                {
                    var setting = ConfigurationManager.ConnectionStrings[SignalRDatabaseConnectrionStringKey];
                    _signalrDatabaseConnectionString = setting == null ? ConnectionString : setting.ConnectionString;
                }
                return _signalrDatabaseConnectionString;
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

        public static UserCredentials GetUserCredentials(string appId)
        {
            return AppAuthSection.GetUserCredentials(appId);
        }


        private static string _logName;
        internal static string LogName => _logName ?? (_logName = ConfigurationManager.AppSettings["LogName"] ?? "SnTask");

        private static string _logSourceName;
        internal static string LogSourceName => _logSourceName ?? (_logSourceName = ConfigurationManager.AppSettings["LogSourceName"] ?? "SnTaskWeb");

    }
}
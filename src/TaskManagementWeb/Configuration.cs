using System.Configuration;

// ReSharper disable once CheckNamespace
namespace SenseNet.TaskManagement.Web
{
    public class TaskManagementConfiguration
    {
        //TODO: put all config values here

        /// <summary>
        /// After this timeout the task lock will expire so any agent can claim the task.
        /// </summary>
        public int TaskExecutionTimeoutInSeconds { get; set; }
    }

    //UNDONE: modernize configuration handling
    internal static class Configuration
    {
        //private static readonly string TaskDatabaseConnectrionStringKey = "TaskDatabase";
        //private static readonly string SignalRDatabaseConnectrionStringKey = "SignalRDatabase";
        private static readonly string SignalRSqlEnabledKey = "SignalRSqlEnabled";

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
    }
}
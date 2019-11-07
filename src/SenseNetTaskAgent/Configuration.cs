using SenseNet.TaskManagement.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SenseNet.TaskManagement.TaskAgent
{
    internal static class Hub
    {
        public const string Name = "AgentHub";
        public const string GetTaskMethod = "GetTask";
        public const string RefreshLockMethod = "RefreshLock";
        public const string HeartbeatMethod = "Heartbeat";
        public const string IsUpdateAvailableMethod = "IsUpdateAvailable";
        public const string TaskFinished = "TaskFinished";
        public const string OnAgentConnected = "OnAgentConnected";
        public const string WriteProgressMethod = "WriteProgress";
        public const string StartSubtaskMethod = "StartSubtask";
        public const string FinishSubtaskMethod = "FinishSubtask";
    }

    internal class Configuration
    {
        private const string TASKMANAGEMENTURLKEY = "TaskManagementUrl";
        private const string DEFAULTTASKMANAGEMENTURL = "http://localhost";
        private static string _taskManagementUrl;
        public static string TaskManagementUrl
        {
            get
            {
                if (_taskManagementUrl == null)
                {
                    _taskManagementUrl = ConfigurationManager.AppSettings[TASKMANAGEMENTURLKEY];
                    if (string.IsNullOrEmpty(_taskManagementUrl))
                        _taskManagementUrl = DEFAULTTASKMANAGEMENTURL;
                }
                return _taskManagementUrl;
            }
        }

        private const string USERNAMEKEY = "Username";
        private static string _username;
        public static string Username
        {
            get
            {
                if (_username == null)
                {
                    _username = ConfigurationManager.AppSettings[USERNAMEKEY] ?? string.Empty;
                }
                return _username;
            }
        }

        private const string PASSWORDKEY = "Password";
        private static string _password;
        public static string Password
        {
            get
            {
                if (_password == null)
                {
                    _password = ConfigurationManager.AppSettings[PASSWORDKEY] ?? string.Empty;
                }
                return _password;
            }
        }

        private const string UPDATELOCKPERIODINSECONDSKEY = "UpdateLockPeriodInSeconds";
        private const int DEFAULTUPDATELOCKPERIODINSECONDS = 15;
        private static int? _updateLockPeriodInSeconds;
        public static int UpdateLockPeriodInSeconds
        {
            get
            {
                if (_updateLockPeriodInSeconds == null)
                {
                    int value;
                    if (!int.TryParse(ConfigurationManager.AppSettings[UPDATELOCKPERIODINSECONDSKEY], out value))
                        value = DEFAULTUPDATELOCKPERIODINSECONDS;
                    _updateLockPeriodInSeconds = value;
                }
                return _updateLockPeriodInSeconds.Value;
            }
        }

        private const string EXECUTORTIMEOUTINSECONDSKEY = "ExecutorTimeoutInSeconds";
        private const int DEFAULTEXECUTORTIMEOUTINSECONDS = 60;
        private static int? _executorTimeoutInSeconds;
        public static int ExecutorTimeoutInSeconds
        {
            get
            {
                if (_executorTimeoutInSeconds == null)
                {
                    int value;
                    if (!int.TryParse(ConfigurationManager.AppSettings[EXECUTORTIMEOUTINSECONDSKEY], out value))
                        value = DEFAULTEXECUTORTIMEOUTINSECONDS;
                    _executorTimeoutInSeconds = value;
                }
                return _executorTimeoutInSeconds.Value;
            }
        }

        private const string TASKEXECUTORDIRECTORYKEY = "TaskExecutorDirectory";
        private const string DEFAULTTASKEXECUTORDIRECTORY = "TaskExecutors";
        private static string _taskExecutorDirectory;
        public static string TaskExecutorDirectory
        {
            get
            {
                if (_taskExecutorDirectory == null)
                {
                    var setting = ConfigurationManager.AppSettings[TASKEXECUTORDIRECTORYKEY];
                    if (string.IsNullOrEmpty(setting))
                        setting = DEFAULTTASKEXECUTORDIRECTORY;
                    _taskExecutorDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(GetCodeBase()), setting));
                }
                return _taskExecutorDirectory;
            }
        }

        private const string HEARTBEATPERIODINSECONDSKEY = "HeartbeatPeriodInSeconds";
        private const int DEFAULTHEARTBEATPERIODINSECONDS = 30;
        private static int? _heartbeatPeriodInSeconds;
        public static int HeartbeatPeriodInSeconds
        {
            get
            {
                if (_heartbeatPeriodInSeconds == null)
                {
                    int value;
                    if (!int.TryParse(ConfigurationManager.AppSettings[HEARTBEATPERIODINSECONDSKEY], out value))
                        value = DEFAULTHEARTBEATPERIODINSECONDS;
                    _heartbeatPeriodInSeconds = value;
                }
                return _heartbeatPeriodInSeconds.Value;
            }
        }

        private const string EXPLICITEEXECUTORSSECTIONKEY = "taskManagement/executors";
        private static Dictionary<string, string> _expliciteExecutors;
        public static Dictionary<string, string> ExpliciteExecutors
        {
            get
            {
                if (_expliciteExecutors == null)
                {
                    var values = new Dictionary<string, string>();
                    var section = ConfigurationManager.GetSection(EXPLICITEEXECUTORSSECTIONKEY) as System.Collections.Specialized.NameValueCollection;
                    if (section != null)
                    {
                        var baseDir = Path.GetDirectoryName(GetCodeBase());
                        foreach (var key in section.AllKeys)
                            values.Add(key, Path.GetFullPath(Path.Combine(baseDir, section[key])));
                    }
                    _expliciteExecutors = values;
                }
                return _expliciteExecutors;
            }
        }

        public static UserCredentials GetUserCredentials(string appId)
        {
            return AppAuthSection.GetUserCredentials(appId);
        }

        private static string GetCodeBase(Assembly asm = null)
        {
            if (asm == null)
                asm = Assembly.GetExecutingAssembly();

            if (asm.IsDynamic)
                return string.Empty;
            return asm.CodeBase.Replace("file:///", "").Replace("file://", "//").Replace("/", "\\");
        }

        internal static string GetBasicAuthHeader(UserCredentials user)
        {
            if (user == null)
                return string.Empty;

            return "Basic " + Convert.ToBase64String(new ASCIIEncoding().GetBytes(user.UserName + ":" + user.Password));
        }

        private static string _logName;
        internal static string LogName => _logName ?? (_logName = ConfigurationManager.AppSettings["LogName"] ?? "SnTask");

        private static string _logSourceName;
        internal static string LogSourceName => _logSourceName ?? (_logSourceName = ConfigurationManager.AppSettings["LogSourceName"] ?? "SnTaskAgent");
    }
}

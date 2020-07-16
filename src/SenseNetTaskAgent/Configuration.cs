using SenseNet.TaskManagement.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Text;

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

    public class AgentConfiguration
    {
        public string TaskManagementUrl { get; set; } = "https://localhost:5001";
        public int UpdateLockPeriodInSeconds { get; set; } = 15;
        public int ExecutorTimeoutInSeconds { get; set; } = 60;
        public int HeartbeatPeriodInSeconds { get; set; } = 30;
        public string TaskExecutorDirectory { get; set; } = 
            Path.Combine(Environment.CurrentDirectory, "TaskExecutors");
    }
    internal class Configuration
    {
        //UNDONE: modernize authentication configuration

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
            //UNDONE: get user credentials or secret key using the new config api
            return null;
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
    }
}

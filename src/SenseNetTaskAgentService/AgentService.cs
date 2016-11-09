using System;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using SenseNet.Diagnostics;
using SenseNet.TaskManagement.Core;

namespace SenseNet.TaskManagement.TaskAgentService
{
    public partial class AgentService : ServiceBase
    {
        private static class AgentServiceConfig
        {

            private static string _logName;
            internal static string LogName => _logName ?? (_logName = ConfigurationManager.AppSettings["LogName"] ?? "SnTask");

            private static string _logSourceName;
            internal static string LogSourceName => _logSourceName ?? (_logSourceName = ConfigurationManager.AppSettings["LogSourceName"] ?? "SnTaskService");

        }

        private static bool _updateStarted;

        public AgentService()
        {
            InitializeComponent();

            this.AutoLog = false;
        }

        //====================================================================================== Service methods

        protected override void OnStart(string[] args)
        {
            _updateStarted = false;

            SnLog.Instance = new SnEventLogger(AgentServiceConfig.LogName, AgentServiceConfig.LogName);

            AgentManager.Startup(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), false);

            // TODO: handle updates
            // make sure that we subscribe to the event only once
            //AgentManager.OnTaskManagementUpdateStarted -= OnTaskManagementUpdateStarted;
            //AgentManager.OnTaskManagementUpdateStarted += OnTaskManagementUpdateStarted;

            SnLog.WriteInformation("TaskAgentService STARTED.", EventId.TaskManagement.Lifecycle);
        }

        protected override void OnStop()
        {
            if (_updateStarted)
            {
                // In case of an update the task agents will shut down themselves, this 
                // service should not stop them forcefully, simply stop itself.
                SnLog.WriteInformation("TaskAgentService STOPPED because the Task Management framework is being UPDATED.", EventId.TaskManagement.Lifecycle);
            }
            else
            {
                AgentManager.Shutdown();
                SnLog.WriteInformation("TaskAgentService STOPPED.", EventId.TaskManagement.Lifecycle);
            }
        }

        //====================================================================================== Event handlers

        private void OnTaskManagementUpdateStarted(object sender, EventArgs eventArgs)
        {
            // If an update process started, the service should stop itself, without closing the agents.
            _updateStarted = true;

            this.Stop();
        }
    }
}

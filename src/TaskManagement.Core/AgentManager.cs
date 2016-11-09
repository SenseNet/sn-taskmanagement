using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using SenseNet.Diagnostics;

namespace SenseNet.TaskManagement.Core
{
    /// <summary>
    /// Represents an AgentManager event argument.
    /// </summary>
    public class AgentManagerEventArgs : EventArgs
    {
        /// <summary>
        /// Creates a new instance of the AgentManagerEventArgs class.
        /// </summary>
        /// <param name="process">An agent process to track.</param>
        public AgentManagerEventArgs(Process process) { Process = process; }
        /// <summary>
        /// The agent process wrapped by this event argument.
        /// </summary>
        public Process Process { get; private set; }
    }

    /// <summary>
    /// Manages agent processes: monitors running processes and starts new ones as needed.
    /// </summary>
    public class AgentManager
    {
        /// <summary>
        /// Agent process name.
        /// </summary>
        public static readonly string AGENT_PROCESSNAME = "SenseNetTaskAgent";
        /// <summary>
        /// Agent name format for displaying a unique agent name on the UI.
        /// </summary>
        public static readonly string AGENT_NAMEFORMAT = "{0}-Agent#{1}";
        /// <summary>
        /// Name of the task management updater process (CURRENTLY NOT USED).
        /// </summary>
        public static readonly string UPDATER_PROCESSNAME = "SenseNetTaskManagementUpdater";
        /// <summary>
        /// Name of the compressed update package (CURRENTLY NOT USED).
        /// </summary>
        public static readonly string UPDATER_PACKAGENAME = "TaskManagementUpdate.zip";
        /// <summary>
        /// Content Repository path of update package parent folder (CURRENTLY NOT USED).
        /// </summary>
        public static readonly string UPDATER_PACKAGEPARENTPATH = "/Root/System/TaskManagement";
        /// <summary>
        /// Content Repository path of the update package (CURRENTLY NOT USED).
        /// </summary>
        public static readonly string UPDATER_PACKAGEPATH = UPDATER_PACKAGEPARENTPATH + "/" + UPDATER_PACKAGENAME;

        /// <summary>
        /// Exit code sent by the updater process when the update operation has already started (CURRENTLY NOT USED).
        /// </summary>
        public const int UPDATER_STATUSCODE_STARTED = -10000;

        private static Timer _agentTimer;
        private static int _counter;
        private static string _executionBasePath;
        private static string _agentPath;
        private static Process[] _agentProcesses;

        /// <summary>
        /// Event for the case when a new agent process is started.
        /// </summary>
        public static event EventHandler<AgentManagerEventArgs> ProcessStarted;
        /// <summary>
        /// Event for the case when a task management update process is started (CURRENTLY NOT USED).
        /// </summary>
        public static event EventHandler<EventArgs> OnTaskManagementUpdateStarted;

        /// <summary>
        /// Gets the full path of the agent executable.
        /// </summary>
        public static string AgentPath => _agentPath;

        //====================================================================================== Service methods

        /// <summary>
        /// Start monitoring and reviving task executor agents.
        /// </summary>
        /// <param name="executionBasePath">The absolute path of the folder where the code is executing. 
        /// This will be used for finding the agent executable if its configured path is relative.</param>
        /// <param name="delayedAgents">If set to true, agents will not start immediately, only when a new task arrive.
        /// Default is false. Use this parameter only in a development environment.</param>
        /// <param name="taskManagementFolderPath">Optional path of the TaskManagement folder. Default: current execution folder.</param>
        public static void Startup(string executionBasePath, bool delayedAgents, string taskManagementFolderPath = null)
        {
            _executionBasePath = executionBasePath;

            // TaskManagement path is different in case of Local and Distributed mode
            if (string.IsNullOrEmpty(taskManagementFolderPath))
            {
                // service mode: we look for the agent in the execution folder
                _agentPath = _executionBasePath;
            }
            else if (Path.IsPathRooted(taskManagementFolderPath))
            {
                _agentPath = taskManagementFolderPath;
            }
            else
            {
                _agentPath = Path.GetFullPath(Path.Combine(_executionBasePath, taskManagementFolderPath));
            }

            // add the agent executable name to the path
            _agentPath = Path.GetFullPath(Path.Combine(_agentPath, AGENT_PROCESSNAME + ".exe"));

            _agentProcesses = new Process[Configuration.TaskAgentCount];

            if (delayedAgents)
                Checker = new TaskChecker();
            else
                Checker = new AgentChecker();

            // We need a few seconds due time here, because if the heartbeat beats too soon the first time,
            // than there is a possibility that the Updater tool process (that starts the service as its
            // last step) is still running. That would lead to unwanted behavior, e.g. not starting agents.
            _agentTimer = new Timer(HeartBeatTimerElapsed, null, 3000, 5000);
        }

        /// <summary>
        /// Shuts down the agent processes started by this app domain.
        /// </summary>
        public static void Shutdown()
        {
            ShutDownAgentProcess();
        }

        //====================================================================================== Agent manager methods

        private static void EnsureAgentProcess()
        {
            var startedCount = 0;

            try
            {
                for (var i = 0; i < _agentProcesses.Length; i++)
                {
                    if (_agentProcesses[i] == null || _agentProcesses[i].HasExited)
                    {
                        // start a new process, but do not wait for it
                        _agentProcesses[i] = Process.Start(new ProcessStartInfo(AgentPath));
                        startedCount++;

                        // notify outsiders
                        ProcessStarted?.Invoke(null, new AgentManagerEventArgs(_agentProcesses[i]));
                    }
                }
            }
            catch (Exception ex)
            {
                SnLog.WriteException(ex, "Agent start error. Agent path: " + AgentPath + ".",
                    EventId.TaskManagement.Lifecycle);
                return;
            }

            if (startedCount > 0)
            {
                SnLog.WriteInformation($"{AGENT_PROCESSNAME} STARTED ({startedCount} new instance(s) from {AgentPath}).", EventId.TaskManagement.Lifecycle);
            }
            else if (++_counter >= 10)
            {
                _counter = 0;

                SnTrace.TaskManagement.Write("{0} is running ({1} instance(s) from {2}).", AGENT_PROCESSNAME, Configuration.TaskAgentCount, AgentPath);
            }
        }

        private static void ShutDownAgentProcess()
        {
            if (_agentProcesses == null)
                return;

            var stopCount = 0;

            foreach (var agentProcess in _agentProcesses.Where(p => p != null && !p.HasExited))
            {
                agentProcess.Kill();
                stopCount++;
            }

            SnTrace.TaskManagement.Write("{0} instances of the {1} process were killed during shutdown.", stopCount, AGENT_PROCESSNAME);
        }

        //====================================================================================== Agent methods

        /// <summary>
        /// Gets a unique agent name containing the machine name and process id. 
        /// Used from the GUI when displaying a list of running agents.
        /// </summary>
        public static string GetAgentName()
        {
            var thisProcess = Process.GetCurrentProcess();
            return string.Format(AGENT_NAMEFORMAT, Environment.MachineName, thisProcess.Id);
        }

        //====================================================================================== Helper methods

        private static void HeartBeatTimerElapsed(object o)
        {
            // if an update process has been started, stop the timer and notify clients
            if (IsUpdateStarted())
            {
                _agentTimer.Change(Timeout.Infinite, Timeout.Infinite);

                OnTaskManagementUpdateStarted?.Invoke(null, EventArgs.Empty);

                return;
            }

            Checker.Check();
        }

        private static bool IsUpdateStarted()
        {
            return Process.GetProcessesByName(UPDATER_PROCESSNAME).Length > 0;
        }

        private static class Configuration
        {
            private const string TASKAGENTCOUNTKEY = "TaskAgentCount";
            private const int DEFAULTTASKAGENTCOUNT = 1;
            private static int? _taskAgentCount;
            public static int TaskAgentCount
            {
                get
                {
                    if (!_taskAgentCount.HasValue)
                    {
                        int value;
                        if (!int.TryParse(ConfigurationManager.AppSettings[TASKAGENTCOUNTKEY], out value) || value < 1)
                            value = DEFAULTTASKAGENTCOUNT;
                        _taskAgentCount = value;
                    }

                    return _taskAgentCount.Value;
                }
            }
        }

        //====================================================================================== Delayed agent watching

        /// <summary>
        /// Sets the current agent check algoritm to the real one that actually starts agents.
        /// </summary>
        public static void AnyTaskRegistered()
        {
            AgentManager.Checker = new AgentChecker();
        }

        private static IChecker Checker = new TaskChecker();
        private interface IChecker
        {
            void Check();
        }
        private class AgentChecker : IChecker
        {
            public void Check()
            {
                EnsureAgentProcess();
            }
        }
        private class TaskChecker : IChecker
        {
            public void Check()
            {
                // do nothing
            }
        }
    }
}

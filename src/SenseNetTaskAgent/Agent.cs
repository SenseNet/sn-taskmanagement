using System.Reflection;
using Microsoft.AspNet.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SenseNet.Diagnostics;
using SenseNet.TaskManagement.Core;
using SenseNet.TaskManagement.Core.Configuration;

namespace SenseNet.TaskManagement.TaskAgent
{
    internal class Agent
    {
        private static string _agentName = Guid.NewGuid().ToString();
        internal static string AgentName { get { return _agentName; } }
        private static object _workingsync = new object();
        private static bool _working;
        private static bool _updateStarted;
        private static bool _updateWinner;

        private static SnTask _currentTask;
        internal static Dictionary<string, string> TaskExecutors { get; private set; }
        private static string[] _capabilities;
        private static ServerContext _serverContext = new ServerContext { ServerType = ServerType.Distributed };

        private static HubConnection _hubConnection;
        private static IHubProxy _hubProxy;

        private static Dictionary<string, string> _executorVersions;
        private static Dictionary<string, string> TaskExecutorVersions
        {
            get
            {
                if (_executorVersions == null)
                {
                    var versions = new Dictionary<string, string>();

                    foreach (var executor in TaskExecutors)
                    {
                        try
                        {
                            // load the executor's assembly name and get the version
                            var an = AssemblyName.GetAssemblyName(executor.Value);

                            versions.Add(executor.Key, an.Version.ToString());
                        }
                        catch
                        {
                            // error loading an executor, simply leave it out
                        }
                    }

                    _executorVersions = versions;
                }

                return _executorVersions;
            }
        }
 

        static void Main(string[] args)
        {
            SnLog.Instance = new SnEventLogger(Configuration.LogName, Configuration.LogSourceName);

            _agentName = AgentManager.GetAgentName();

            try
            {
                DiscoverCapabilities();

                _updateLockPeriodInMilliseconds = Configuration.UpdateLockPeriodInSeconds * 1000;
                _updateLockTimer = new Timer(new TimerCallback(UpdateLockTimerElapsed));

                _watchExecutorPeriodInMilliseconds = Configuration.ExecutorTimeoutInSeconds * 1000;
                _watchExecutorTimer = new Timer(new TimerCallback(WatchExecutorTimerElapsed));

                var started = StartSignalR();

                // check for updates before any other operation
                if (started && IsUpdateAvailable())
                {
                    StartUpdaterAndExit();

                    // exit only if the update really started (it is possible that there
                    // will be no update becaue the updater tool is missing)
                    if (_updateStarted)
                        return;
                }

                _heartBeatTimerPeriodInMilliseconds = Configuration.HeartbeatPeriodInSeconds * 1000;
                _heartbeatTimer = new Timer(new TimerCallback(HeartBeatTimerElapsed), null, _heartBeatTimerPeriodInMilliseconds, _heartBeatTimerPeriodInMilliseconds);

                // TODO: update mechanism
                //_updateTimer = new Timer(UpdateTimerElapsed, null, 500, 30000);

                if (started)
                    Work();
                else
                    _reconnecting = true;

                Console.WriteLine("Press <enter> to exit...");
                Console.ReadLine();

                _hubConnection.Dispose();
            }
            catch (Exception ex)
            {
                SnLog.WriteException(ex, String.Empty, EventId.TaskManagement.General);
            }
        }

        private static void DiscoverCapabilities()
        {
            var executors = new Dictionary<string, string>();
            
            foreach(var item in Configuration.ExpliciteExecutors)
                executors.Add(item.Key, item.Value);

            if (Directory.Exists(Configuration.TaskExecutorDirectory))
            {
                foreach (var executorDirectory in Directory.GetDirectories(Configuration.TaskExecutorDirectory))
                {
                    var dirName = Path.GetFileName(executorDirectory);
                    var exeName = GetExecutorExeName(dirName);
                    if (executors.ContainsKey(dirName))
                        continue;
                    var exe = Path.Combine(executorDirectory, exeName + ".exe");
                    if (File.Exists(exe))
                        executors.Add(dirName, exe);
                }
            }

            foreach (var item in executors)
                SnTrace.TaskManagement.Write("Agent {0} capability: {1}, {2}", AgentName, item.Key, item.Value);
            _capabilities = executors.Keys.ToArray();
            TaskExecutors = executors;
        }
        private static string GetExecutorExeName(string name)
        {
            return TaskManagement.TaskAgent.Tools.GetExecutorExeName(name);
        }
                
        private static bool StartSignalR()
        {
            _hubConnection = new HubConnection(Configuration.TaskManagementUrl);
            _hubConnection.DeadlockErrorTimeout = new TimeSpan(0, 1, 0); // needed to avoid SlowCallbackException
            _hubConnection.Closed += Connection_Closed;
            _hubConnection.ConnectionSlow += Connection_ConnectionSlow;
            _hubConnection.Error += Connection_Error;
            _hubConnection.Received += Connection_Received;
            _hubConnection.Reconnected += Connection_Reconnected;
            _hubConnection.Reconnecting += Connection_Reconnecting;
            _hubConnection.StateChanged += Connection_StateChanged;

            // TODO: agent --> taskmanagement authentication

            // set NTLM credentials (for Windows auth) or Authorization header (for basic auth)
            if (string.IsNullOrEmpty(Configuration.Username))
                _hubConnection.Credentials = CredentialCache.DefaultCredentials;
            else
                _hubConnection.Headers.Add("Authorization", Configuration.GetBasicAuthHeader(new UserCredentials
                {
                    UserName = Configuration.Username,
                    Password = Configuration.Password
                }));

            _hubProxy = _hubConnection.CreateHubProxy(Hub.Name);

            // register methods for incoming messages
            _hubProxy.On<SnTask>("newTask", (task) => NewTask(task));

            ServicePointManager.DefaultConnectionLimit = 10;

            SnTrace.TaskManagement.Write(string.Format("Agent {0} is CONNECTING to {1}...", AgentName, _hubConnection.Url));

            try
            {
                _hubConnection.Start().Wait();
                SnLog.WriteInformation(string.Format("Agent {0} is CONNECTED to {1}.", AgentName, _hubConnection.Url), EventId.TaskManagement.Communication);

                var msg = String.Format("Agent {0} works in {1} server context.", AgentName, _serverContext.ServerType.ToString().ToLower());
                SnTrace.TaskManagement.Write(msg);
                Console.WriteLine(msg);

                return true;
            }
            catch (Exception ex)
            {
                SnLog.WriteException(ex, "SignelR error.", EventId.TaskManagement.Communication);
                return false;
            }
        }

        private static object _reconnectLock = new object();
        private static void Reconnect()
        {
            SnTrace.TaskManagement.Write("Agent {0} RECONNECTING...", AgentName);
            _reconnecting = true;

            lock (_reconnectLock)
            {
                _hubConnection.Dispose();
                if (!StartSignalR())
                {
                    SnTrace.TaskManagement.Write("Agent {0} NOT RECONNECTED", AgentName);
                    return;
                }
            }

            SnTrace.TaskManagement.Write("Agent {0} RECONNECTED", AgentName);
            _reconnecting = false;
            Work();
        }

        static void Connection_StateChanged(StateChange obj)
        {
            SnTrace.TaskManagement.Write("Agent {0} connection state changed: {1} --> {2}", AgentName, obj.OldState, obj.NewState);
            if (obj.NewState == ConnectionState.Disconnected)
            {
                _reconnecting = true;
                return;
            }
            if (obj.NewState == ConnectionState.Connected)
            {
                _reconnecting = false;
                return;
            }
        }
        static void Connection_Reconnecting()
        {
            SnTrace.TaskManagement.Write("Agent {0}: connection reconnecing.", AgentName);
        }
        static void Connection_Reconnected()
        {
            SnLog.WriteInformation($"Agent {AgentName}: connection reconnected.", EventId.TaskManagement.Communication);
        }
        static void Connection_Received(string obj)
        {
            SnTrace.TaskManagement.Write("Agent {0}: received: {1}", AgentName, obj.Replace('\r', ' ').Replace('\n', ' '));
        }
        static void Connection_Error(Exception obj)
        {
            SnLog.WriteException(obj, "Connection error", EventId.TaskManagement.Communication);
            if (_serverContext.ServerType == ServerType.Distributed)
            {
                _reconnecting = true;
            }
            else
            {
                SnTrace.TaskManagement.Write("@@@@@@@@@@@@@@@@@@@@@@ Agent {0} now commits suicide.", AgentName);
                Thread.Sleep(1000);
                Process.GetCurrentProcess().Kill();
            }
        }
        static void Connection_ConnectionSlow()
        {
            SnLog.WriteWarning($"Agent {AgentName}: connection is slow.",
                EventId.TaskManagement.Communication);
        }
        static void Connection_Closed()
        {
            SnLog.WriteWarning($"Agent {AgentName}: connection is closed.", EventId.TaskManagement.Communication);
        }

        /*----------------------------------------------------- called by hub proxy */

        private static void NewTask(SnTask t)
        {
            if (_working)
                return;
            if (t != null && !TaskExecutors.ContainsKey(t.Type))
                return;

            if (t == null)
                SnTrace.TaskManagement.Write("Agent {0} handles a 'handle-dead-tasks' message.", AgentName);
            else
                SnTrace.TaskManagement.Write("Agent {0} handles a 'new-tasks' message.", AgentName);

            Work();
        }

        /*------------------------------------------------------------------------- */

        async static void Work()
        {
            lock (_workingsync)
            {
                if (_working || _updateStarted)
                    return;
                _working = true;
            }

            StartLockTimer();
            Console.WriteLine("_______________________________WORKING");

            try
            {
                var t = await GetTask();

                while (t != null)
                {
                    _currentTask = t;
                    var result = ExecuteTask(t);
                    _currentTask = null;
                    
                    // this will call the finalizers on the server side and delete the task from the database
                    SendResultAndDeleteTask(result);

                    // if an update process started in the meantime, do not get a new task
                    if (_updateStarted)
                        return;

                    // after finishing the previous one, try to get the next task
                    t = await GetTask();
                }
            }
            catch (Exception e)
            {
                SnLog.WriteException(e, "Agent error.", EventId.TaskManagement.General);
                _reconnecting = true;
            }
            finally
            {
                StopLockTimer();
                _working = false;
                Console.WriteLine("_______________________________WAITING");
            }
        }

        private static Task<SnTask> GetTask()
        {
            return InvokeProxy<SnTask>(Hub.GetTaskMethod, Environment.MachineName, AgentName, _capabilities);
        }

        private static SnTaskResult ExecuteTask(SnTask t)
        {
            Console.WriteLine("Start work on task#" + t.Id);


            var result = new SnTaskResult
            {
                MachineName = Environment.MachineName,
                AgentName = AgentName,
                Task = t
            };

            try
            {
                if (t.Type == "DoNotRunAnyExecutor")
                    using (var executor = new TestExecutor())
                        result.ResultCode = executor.Execute(t);
                else
                    using (var executor = new OutProcExecutor())
                        result.ResultCode = executor.Execute(t);
            }
            catch (Exception e)
            {
                result.Error = SnTaskError.Create(e);
            }
            Console.WriteLine("Execution finished.");

            result.ResultData = resultData;
            if (result.Error == null && resultError != null)
                result.Error = SnTaskError.Parse(resultError);

            resultData = null;
            resultError = null;

            return result;
        }
        private static void SendResultAndDeleteTask(SnTaskResult result)
        {
            InvokeProxy(Hub.TaskFinished, result);
        }

        // continuous lock support
        private static Timer _updateLockTimer;
        private static int _updateLockPeriodInMilliseconds;
        private static void StartLockTimer()
        {
            _updateLockTimer.Change(_updateLockPeriodInMilliseconds, _updateLockPeriodInMilliseconds);
        }
        private static void StopLockTimer()
        {
            _updateLockTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }
        private static void UpdateLockTimerElapsed(object o)
        {
            try
            {
                var taskId = _currentTask == null ? 0 : _currentTask.Id;
                if (taskId < 1)
                    return;
                //_hubProxy.Invoke(Hub.RefreshLockMethod, AgentName, taskId);
                InvokeProxy(Hub.RefreshLockMethod, Environment.MachineName, AgentName, taskId);
                Console.Write("*");
            }
            catch (Exception ex)
            {
                SnLog.WriteException(ex, "Agent.cs UpdateLockTimerElapsed failed.", EventId.TaskManagement.General);
            }
        }

        // watching executor support
        private static Timer _watchExecutorTimer;
        private static int _watchExecutorPeriodInMilliseconds;
        private static void StartWatcherTimer()
        {
            _watchExecutorTimer.Change(_watchExecutorPeriodInMilliseconds, _watchExecutorPeriodInMilliseconds);
        }
        private static void StopWatcherTimer()
        {
            _watchExecutorTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }
        private static void WatchExecutorTimerElapsed(object o)
        {
            if (DateTime.UtcNow.AddMilliseconds(-_watchExecutorPeriodInMilliseconds) > executionStateWritten)
            {
                var msg = string.Format( "EXECUTOR TERMINATED: {0}.", _executor.Task.Type);
                Console.WriteLine(msg);
                resultError = SnTaskError.Create("ExecutorTerminated", "ExecutorTerminated", msg, null).ToString();
                
                if (_executor != null)
                    _executor.Terminate();
            }
        }
        private static IExecutor _executor = null;
        private static string progressMessage = null;
        private static string resultData = null;
        private static string resultError = null;
        private static DateTime executionStateWritten = DateTime.MinValue;
        internal static void ExecutionStart(IExecutor executor)
        {
            _executor = executor;
            progressMessage = null;
            executionStateWritten = DateTime.UtcNow;

            StartWatcherTimer();
        }
        internal static void ExecutionEnd()
        {
            StopWatcherTimer();

            _executor = null;
            progressMessage = null;
            executionStateWritten = DateTime.MinValue;
        }
        internal static void OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e != null && e.Data != null)
            {
                // It does not matter what the executor wrote on the console, it means it is alive.
                // This 'resets' the timer we employ for monitoring executors for programmatic timeout.
                executionStateWritten = DateTime.UtcNow;

                if (e.Data.StartsWith("Progress:", StringComparison.OrdinalIgnoreCase))
                {
                    SnTrace.TaskManagement.Write("############ Progress received {0}: {1}", AgentName, progressMessage);                    

                    var progressRecord = GetProgressRecord(e.Data.Substring(9).Trim());
                    if (progressRecord != null)
                        InvokeProxy(Hub.WriteProgressMethod, Environment.MachineName, AgentName, progressRecord);
                }
                else if (e.Data.StartsWith("StartSubtask:", StringComparison.OrdinalIgnoreCase))
                {
                    var s = e.Data.Substring(13).Trim();
                    var subtask = JsonConvert.DeserializeObject<SnSubtask>(s);
                    InvokeProxy(Hub.StartSubtaskMethod, Environment.MachineName, AgentName, subtask, _currentTask);
                }
                else if (e.Data.StartsWith("FinishSubtask:", StringComparison.OrdinalIgnoreCase))
                {
                    var s = e.Data.Substring(14).Trim();
                    var subtask = JsonConvert.DeserializeObject<SnSubtask>(s);
                    InvokeProxy(Hub.FinishSubtaskMethod, Environment.MachineName, AgentName, subtask, _currentTask);
                }
                else if (e.Data.StartsWith("ResultData:", StringComparison.OrdinalIgnoreCase))
                {
                    resultData = e.Data.Substring(11).Trim();
                }
                else if (e.Data.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
                {
                    resultError = e.Data.Substring(6);
                }
                else if (e.Data.StartsWith("WARNING:", StringComparison.OrdinalIgnoreCase))
                {
                    SnLog.WriteWarning(e.Data.Substring(8), EventId.TaskManagement.General);
                }
                else if (resultError != null)
                {
                    resultError += e.Data;
                }
                Console.WriteLine(e.Data);
            }
        }

        // heartbeat support
        static PerformanceCounter cpuCounter = new PerformanceCounter() { CategoryName = "Processor", CounterName = "% Processor Time", InstanceName = "_Total" };
        static PerformanceCounter ramCounter = new PerformanceCounter("Memory", "Available MBytes");
        private static bool _reconnecting;
        private static Timer _heartbeatTimer;
        private static int _heartBeatTimerPeriodInMilliseconds;
        private static void HeartBeatTimerElapsed(object o)
        {
            try
            {
                if (_reconnecting)
                {
                    Reconnect();
                }
                else
                {
                    InvokeProxy(Hub.HeartbeatMethod, Environment.MachineName, AgentName, GetHealthRecord());
                }
            }
            catch (Exception ex)
            {
                SnLog.WriteException(ex, "Agent.cs HeartBeatTimerElapsed failed", EventId.TaskManagement.General);
            }
        }
        private static SnHealthRecord GetHealthRecord()
        {
            var p = Process.GetCurrentProcess();
            return new SnHealthRecord
            {
                Machine = Environment.MachineName,
                Agent = AgentName,
                EventTime = DateTime.Now,
                ProcessId = p.Id,
                CPU = cpuCounter.NextValue(),
                RAM = Convert.ToInt32(Math.Round(ramCounter.NextValue())),
                TotalRAM = GetTotalPhysicalMemory(),
                StartTime = p.StartTime.ToUniversalTime(),
                EventType = TaskEventType.Progress //TaskEventType.Idle
            };

        }
        private static SnProgressRecord GetProgressRecord(string progressMsg)
        {
            Progress progress;
            try
            {
                progress = JsonConvert.DeserializeObject<Progress>(progressMsg);
            }
            catch
            {
                // If deserializetion went wrong (e.g. because the executer wrote something with a 'Progress' prefix
                // to the console that is not a Progress object), we cannot return anything but null here.
                return null;
            }

            return new SnProgressRecord
            {
                AppId = _currentTask.AppId,
                Tag = _currentTask.Tag,
                TaskId = _currentTask.Id,
                Progress = progress
            };
        }

        // Task Management update
        //private static Timer _updateTimer;
        private static void UpdateTimerElapsed(object o)
        {
            // just to make sure we do not execute something twice, this may never be true here
            if (_updateStarted)
            {
                StopUpdateTimer();
                return;
            }

            if (!IsUpdateAvailable())
                return;

            // if an update started in the meantime
            if (_updateStarted)
            {
                StopUpdateTimer();
                return;
            }            

            // stop timer to avoid executing this again
            StopUpdateTimer();

            StartUpdaterAndExit();
        }
        private static void StopUpdateTimer()
        {
            //_updateTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private static bool IsUpdateAvailable()
        {
            // TODO: update mechanism
            return false;
        }

        private static void StartUpdaterAndExit()
        {
            // this switch is monitored by the Work method because it 
            // must not ask for a new task if an update has started
            _updateStarted = true;

            SnLog.WriteInformation($"Task#Starting update process on agent {AgentName}.", EventId.TaskManagement.General);

            var updaterToolName = AgentManager.UPDATER_PROCESSNAME + ".exe";
            var updaterToolPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), updaterToolName);
            var updaterAlreadyRunning = false;

            // the tool should be next to the agent executable
            if (File.Exists(updaterToolPath))
            {
                var startInfo = new ProcessStartInfo(updaterToolName)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    ErrorDialog = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                var updaterProcess = new Process
                {
                    EnableRaisingEvents = true,
                    StartInfo = startInfo
                };

                // listen to what the updater tool writes to the Console
                updaterProcess.OutputDataReceived += delegate(object sender, DataReceivedEventArgs args)
                {
                    if (args == null || args.Data == null)
                        return;

                    // the updater notified us that he won
                    if (string.CompareOrdinal(args.Data, "WINNER") == 0)
                        _updateWinner = true;
                };

                try
                {
                    updaterProcess.Start();
                    updaterProcess.BeginOutputReadLine();

                    SnLog.WriteInformation($"Task#Updater tool STARTED on agent {Agent.AgentName}",
                        EventId.TaskManagement.General);

                    // Wait while the updater process exits (because another updater is already running) 
                    // or it notifies us that he is the winner and will do the actual update soon.
                    do
                    {
                        updaterProcess.WaitForExit(1000);
                    } while (!updaterProcess.HasExited && !_updateWinner);

                    if (updaterProcess.HasExited)
                    {
                        if (updaterProcess.ExitCode == AgentManager.UPDATER_STATUSCODE_STARTED)
                        {
                            updaterAlreadyRunning = true;

                            // another agent already started the updater tool, simply exit
                            SnLog.WriteInformation($"Task#Updater tool EXITED on agent {AgentName} because another updater is already running.",
                                EventId.TaskManagement.General);
                        }
                        else
                        {
                            // unknown error code
                            SnLog.WriteWarning($"Task#Updater tool EXITED on agent {AgentName} with an unexpected code: {updaterProcess.ExitCode}.",
                                EventId.TaskManagement.General);
                        }
                    }
                    else if (_updateWinner)
                    {
                        // Download the package only if we started the one 
                        // and only true updater exe - that has not exited.
                        DownloadUpdatePackage();
                    }
                }
                catch (Exception ex)
                {
                    SnLog.WriteException(ex, "Agent update error.", EventId.TaskManagement.General);
                }
            }
            else
            {
                // the updater tool is missing
                SnLog.WriteError(string.Format("Task#Updater tool not found ({0}), but there is a new version on the server. Please update the TaskManagement folder manually.", updaterToolPath),
                    EventId.TaskManagement.General);

                // no update will be performed: switch back to working mode
                _updateStarted = false;

                // do not exit if there is no updater: the operator must handle 
                // this use-case manually (stop the service and copy the files)
                return;
            }

            // wait for the last task executor to finish
            while (_working)
            {
                Thread.Sleep(1000);
            }

            SnLog.WriteInformation(string.Format(updaterAlreadyRunning
                    ? "Task#Agent {0} exits before updating."
                    : "Task#Agent {0} exits before updating. This is Ripley, last survivor of the Nostromo, signing off.", AgentName), EventId.TaskManagement.General);

            // shut down this agent
            Environment.Exit(0);
        }

        private static void DownloadUpdatePackage()
        {
            SnLog.WriteInformation($"Task#Starting to download update package on agent {Agent.AgentName}.",
                EventId.TaskManagement.General);

            try
            {
                using (var client = new WebClient())
                {
                    var packageUrl = Configuration.TaskManagementUrl.TrimEnd('/') + AgentManager.UPDATER_PACKAGEPATH;
                    var folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    var targetFilePath = Path.Combine(folder, AgentManager.UPDATER_PACKAGENAME);

                    // set NTLM credentials (for Windows auth) or Authorization header (for basic auth)
                    if (string.IsNullOrEmpty(Configuration.Username))
                        client.Credentials = CredentialCache.DefaultCredentials;
                    else
                        client.Headers.Add("Authorization", Configuration.GetBasicAuthHeader(new UserCredentials
                        {
                            UserName = Configuration.Username,
                            Password = Configuration.Password
                        }));

                    // save the file to the local TaskManagement folder with the same name as the content
                    client.DownloadFile(packageUrl, targetFilePath);
                }

                SnLog.WriteInformation($"Task#Download update package FINISHED on agent {Agent.AgentName}.",
                    EventId.TaskManagement.General);
            }
            catch (Exception ex)
            {
                SnLog.WriteException(ex, "Agent update error.", EventId.TaskManagement.General);
            }
        }

        private static ulong GetTotalPhysicalMemory()
        {
            return new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory / (1024 * 1024);
        }


        internal static void InvokeProxy(string method, params object[] args)
        {
            try
            {
                _hubProxy.Invoke(method, args);
            }
            catch (Exception e)
            {
                SnTrace.TaskManagement.Write(e.ToString());
                _reconnecting = true;
            }
        }
        private static Task<T> InvokeProxy<T>(string method, params object[] args)
        {
            try
            {
                return _hubProxy.Invoke<T>(method, args);
            }
            catch (Exception e)
            {
                SnTrace.TaskManagement.Write(e.ToString());
                _reconnecting = true;
            }
            return null;
        }

    }
}

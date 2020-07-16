using System.Reflection;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using SenseNet.Diagnostics;
using SenseNet.TaskManagement.Core;
using SenseNet.TaskManagement.Core.Configuration;
using SenseNetTaskAgent;

namespace SenseNet.TaskManagement.TaskAgent
{
    internal class Agent
    {
        internal static string AgentName { get; private set; } = Guid.NewGuid().ToString();
        private static object _workingsync = new object();
        private static bool _working;
        private static bool _updateStarted;
        private static bool _updateWinner;

        private static SnTask _currentTask;
        internal static Dictionary<string, string> TaskExecutors { get; private set; }
        private static string[] _capabilities;
        private static ServerContext _serverContext = new ServerContext { ServerType = ServerType.Distributed };

        private static HubConnection _hubConnection;

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

        private  static AgentConfiguration AgentConfig { get; set; } = new AgentConfiguration();

        static async Task Main(string[] args)
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", true, true)
                .Build();
            
            config.GetSection("TaskManagement").Bind(AgentConfig);

            SnLog.Instance = new SnFileSystemEventLogger();
            SnTrace.SnTracers.Add(new SnFileSystemTracer());
            SnTrace.EnableAll();

            AgentName = AgentManager.GetAgentName();

            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            try
            {
                DiscoverCapabilities();

                _updateLockTimer = new Timer(UpdateLockTimerElapsed);
                _watchExecutorTimer = new Timer(WatchExecutorTimerElapsed);

                var started = await StartSignalR();

                // check for updates before any other operation
                if (started && IsUpdateAvailable())
                {
                    StartUpdaterAndExit();

                    // exit only if the update really started (it is possible that there
                    // will be no update because the updater tool is missing)
                    if (_updateStarted)
                        return;
                }

                _heartBeatTimerPeriodInMilliseconds = AgentConfig.HeartbeatPeriodInSeconds * 1000;
                _heartbeatTimer = new Timer(HeartBeatTimerElapsed, null, _heartBeatTimerPeriodInMilliseconds, _heartBeatTimerPeriodInMilliseconds);

                // TODO: update mechanism
                //_updateTimer = new Timer(UpdateTimerElapsed, null, 500, 30000);

#pragma warning disable 4014
                // start processing on a background thread
                if (started)
                    WorkAsync();
#pragma warning restore 4014
                
                Console.ReadLine();

                if (_hubConnection != null)
                    await _hubConnection.DisposeAsync();
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

            if (Directory.Exists(AgentConfig.TaskExecutorDirectory))
            {
                foreach (var executorDirectory in Directory.GetDirectories(AgentConfig.TaskExecutorDirectory))
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
            return Core.Tools.GetExecutorExeName(name);
        }
                
        private static async Task<bool> StartSignalR()
        {
            if (_hubConnection != null)
                await _hubConnection.DisposeAsync();

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(AgentConfig.TaskManagementUrl.TrimEnd('/') + "/" + Hub.Name)
                .WithAutomaticReconnect(new InfiniteRetryPolicy())
                .Build();

            _hubConnection.Closed += async error =>
            {
                SnLog.WriteWarning($"Agent {AgentName}: connection is CLOSED. Restarting...", 
                    EventId.TaskManagement.Communication);

                await Task.Delay(new Random().Next(0, 5) * 1000);
                await _hubConnection.StartAsync();

                SnLog.WriteInformation($"Agent {AgentName}: connection RESTARTED.");
            };
            _hubConnection.Reconnecting += exception =>
            {
                SnTrace.TaskManagement.Write("Agent {0}: connection RECONNECTING. Error: {1}", 
                    AgentName, exception?.Message);
                return Task.CompletedTask;
            };
            _hubConnection.Reconnected += connectionId =>
            {
                SnLog.WriteInformation($"Agent {AgentName}: connection RECONNECTED.", 
                    EventId.TaskManagement.Communication);

#pragma warning disable 4014
                // restart worker thread
                WorkAsync();
#pragma warning restore 4014

                return Task.CompletedTask;
            };

            //UNDONE: agent --> taskmanagement authentication

            // set NTLM credentials (for Windows auth) or Authorization header (for basic auth)
            //if (string.IsNullOrEmpty(Configuration.Username))
            //    _hubConnection.Credentials = CredentialCache.DefaultCredentials;
            //else
            //    _hubConnection.Headers.Add("Authorization", Configuration.GetBasicAuthHeader(new UserCredentials
            //    {
            //        UserName = Configuration.Username,
            //        Password = Configuration.Password
            //    }));

            // register methods for incoming messages
            _hubConnection.On<SnTask>("newTask", NewTask);

            ServicePointManager.DefaultConnectionLimit = 10;

            SnTrace.TaskManagement.Write($"Agent {AgentName} is CONNECTING to " +
                                         $"{AgentConfig.TaskManagementUrl}...");

            try
            {
                await _hubConnection.StartAsync().ConfigureAwait(false);

                SnLog.WriteInformation($"Agent {AgentName} is CONNECTED to {AgentConfig.TaskManagementUrl}.", 
                    EventId.TaskManagement.Communication);

                var msg = $"Agent {AgentName} works in {_serverContext.ServerType.ToString().ToLower()} " +
                          "server context.";

                SnTrace.TaskManagement.Write(msg);
                Console.WriteLine(msg);

                return true;
            }
            catch (Exception ex)
            {
                SnLog.WriteException(ex, "SignalR error.", EventId.TaskManagement.Communication);
                Console.WriteLine($"[{DateTime.UtcNow}] Connection could not be opened, waiting for heartbeat to retry the operation...");
                return false;
            }
        }

        private static readonly object ReconnectLock = new object();
        private static void Reconnect()
        {
            SnTrace.TaskManagement.Write("Agent {0} RECONNECTING...", AgentName);

            lock (ReconnectLock)
            {
                if (!StartSignalR().GetAwaiter().GetResult())
                {
                    SnTrace.TaskManagement.Write("Agent {0} NOT RECONNECTED", AgentName);
                    return;
                }
            }

#pragma warning disable 4014
            // start worker background thread
            WorkAsync();
#pragma warning restore 4014
        }

        /*----------------------------------------------------- called by hub proxy */

        private static void NewTask(SnTask t)
        {
            if (_working)
                return;
            if (t != null && !TaskExecutors.ContainsKey(t.Type))
                return;

            SnTrace.TaskManagement.Write(
                t == null
                    ? "Agent {0} handles a 'handle-dead-tasks' message."
                    : "Agent {0} handles a 'new-tasks' message.", AgentName);

#pragma warning disable 4014
            WorkAsync();
#pragma warning restore 4014
        }

        /*------------------------------------------------------------------------- */

        private static async Task WorkAsync()
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
                    await SendResultAndDeleteTask(result);

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
            return InvokeProxyAsync<SnTask>(Hub.GetTaskMethod, Environment.MachineName, AgentName, _capabilities);
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
        private static Task SendResultAndDeleteTask(SnTaskResult result)
        {
            return InvokeProxyAsync(Hub.TaskFinished, result);
        }

        // continuous lock support
        private static Timer _updateLockTimer;
        private static void StartLockTimer()
        {
            var updateLockPeriodInMilliseconds = AgentConfig.UpdateLockPeriodInSeconds * 1000;
            _updateLockTimer.Change(updateLockPeriodInMilliseconds, updateLockPeriodInMilliseconds);
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

                InvokeProxyAsync(Hub.RefreshLockMethod, Environment.MachineName, AgentName, taskId)
                    .GetAwaiter().GetResult();
                Console.Write("*");
            }
            catch (Exception ex)
            {
                SnLog.WriteException(ex, "Agent.cs UpdateLockTimerElapsed failed.", EventId.TaskManagement.General);
            }
        }

        // watching executor support
        private static Timer _watchExecutorTimer;
        private static void StartWatcherTimer()
        {
            var watchExecutorPeriodInMilliseconds = AgentConfig.ExecutorTimeoutInSeconds * 1000;
            _watchExecutorTimer.Change(watchExecutorPeriodInMilliseconds, watchExecutorPeriodInMilliseconds);
        }
        private static void StopWatcherTimer()
        {
            _watchExecutorTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }
        private static void WatchExecutorTimerElapsed(object o)
        {
            if (DateTime.UtcNow.AddMilliseconds(-(AgentConfig.ExecutorTimeoutInSeconds * 1000)) > executionStateWritten)
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
                        InvokeProxyAsync(Hub.WriteProgressMethod, Environment.MachineName, AgentName, 
                            progressRecord).GetAwaiter().GetResult();
                }
                else if (e.Data.StartsWith("StartSubtask:", StringComparison.OrdinalIgnoreCase))
                {
                    var s = e.Data.Substring(13).Trim();
                    var subtask = JsonConvert.DeserializeObject<SnSubtask>(s);
                    InvokeProxyAsync(Hub.StartSubtaskMethod, Environment.MachineName, AgentName, 
                        subtask, _currentTask).GetAwaiter().GetResult();
                }
                else if (e.Data.StartsWith("FinishSubtask:", StringComparison.OrdinalIgnoreCase))
                {
                    var s = e.Data.Substring(14).Trim();
                    var subtask = JsonConvert.DeserializeObject<SnSubtask>(s);
                    InvokeProxyAsync(Hub.FinishSubtaskMethod, Environment.MachineName, AgentName, 
                        subtask, _currentTask).GetAwaiter().GetResult();
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
        private static Timer _heartbeatTimer;
        private static int _heartBeatTimerPeriodInMilliseconds;
        private static void HeartBeatTimerElapsed(object o)
        {
            try
            {
                if (_hubConnection == null || _hubConnection.State == HubConnectionState.Disconnected)
                {
                    Reconnect();
                }
                else
                {
                    InvokeProxyAsync(Hub.HeartbeatMethod, Environment.MachineName, AgentName,
                        GetHealthRecord()).GetAwaiter().GetResult();
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
            // this switch is monitored by the WorkAsync method because it 
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
                    var packageUrl = AgentConfig.TaskManagementUrl.TrimEnd('/') + AgentManager.UPDATER_PACKAGEPATH;
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
            //UNDONE: get physical memory
            //return new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory / (1024 * 1024);
            return 0;
        }


        internal static async Task InvokeProxyAsync(string method, params object[] args)
        {
            try
            {
                if (_hubConnection.State != HubConnectionState.Connected)
                {
                    SnTrace.TaskManagement.Write("Cannot invoke hub methods when " +
                                                 $"the connection state is {_hubConnection.State}");
                    return;
                }

                switch (args.Length)
                {
                    case 0: await _hubConnection.InvokeAsync(method); break;
                    case 1: await _hubConnection.InvokeAsync(method, args[0]); break;
                    case 2: await _hubConnection.InvokeAsync(method, args[0], args[1]); break;
                    case 3: await _hubConnection.InvokeAsync(method, args[0], args[1], args[2]); break;
                    case 4: await _hubConnection.InvokeAsync(method, args[0], args[1], args[2], 
                        args[3]); break;
                    case 5: await _hubConnection.InvokeAsync(method, args[0], args[1], args[2], 
                        args[3], args[4]); break;
                    case 6: await _hubConnection.InvokeAsync(method, args[0], args[1], args[2], 
                        args[3], args[4], args[5]); break;
                    default:
                        throw new NotImplementedException("Too many parameters.");
                }
            }
            catch (Exception e)
            {
                SnTrace.TaskManagement.Write(e.ToString());
            }
        }
        private static async Task<T> InvokeProxyAsync<T>(string method, params object[] args)
        {
            try
            {
                if (_hubConnection.State != HubConnectionState.Connected)
                {
                    SnTrace.TaskManagement.Write("Cannot invoke hub methods when " +
                                                 $"the connection state is {_hubConnection.State}");
                    return default;
                }

                switch (args.Length)
                {
                    case 0: return await _hubConnection.InvokeAsync<T>(method);
                    case 1: return await _hubConnection.InvokeAsync<T>(method, args[0]);
                    case 2: return await _hubConnection.InvokeAsync<T>(method, args[0], args[1]);
                    case 3: return await _hubConnection.InvokeAsync<T>(method, args[0], args[1], args[2]);
                    case 4: return await _hubConnection.InvokeAsync<T>(method, args[0], args[1], args[2],
                            args[3]);
                    case 5: return await _hubConnection.InvokeAsync<T>(method, args[0], args[1], args[2],
                            args[3], args[4]);
                    case 6: return await _hubConnection.InvokeAsync<T>(method, args[0], args[1], args[2],
                            args[3], args[4], args[5]);
                    default:
                        throw new NotImplementedException("Too many parameters.");
                }
            }
            catch (Exception e)
            {
                SnTrace.TaskManagement.Write(e.ToString());
            }
            return default;
        }
    }
}

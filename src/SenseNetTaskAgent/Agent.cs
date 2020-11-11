using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using SenseNet.Diagnostics;
using SenseNet.TaskManagement.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SenseNet.TaskManagement.TaskAgent
{
    internal class Agent
    {
        internal static string AgentName { get; private set; } = Guid.NewGuid().ToString();
        private static readonly object WorkingSync = new object();
        private static bool _working;
        private static SnTask _currentTask;
        internal static Dictionary<string, string> TaskExecutors { get; private set; }
        private static string[] _capabilities;
        private static readonly ServerContext ServerContext = new ServerContext { ServerType = ServerType.Distributed };

        private static HubConnection _hubConnection;

        private  static AgentConfiguration AgentConfig { get; } = new AgentConfiguration();

        // ReSharper disable once UnusedParameter.Local
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
                
                _heartBeatTimerPeriodInMilliseconds = AgentConfig.HeartbeatPeriodInSeconds * 1000;
                _heartbeatTimer = new Timer(HeartBeatTimerElapsed, null, _heartBeatTimerPeriodInMilliseconds, _heartBeatTimerPeriodInMilliseconds);
                
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
                SnLog.WriteException(ex, string.Empty, EventId.TaskManagement.General);
            }

            _heartbeatTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _heartbeatTimer?.Dispose();
        }

        private static void DiscoverCapabilities()
        {
            var executors = new Dictionary<string, string>();

            //TODO: explicit executors feature has been removed temporarily
            //foreach(var item in AgentConfig.Executors)
            //    executors.Add(item.Key, item.Value);

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

                var msg = $"Agent {AgentName} works in {ServerContext.ServerType.ToString().ToLower()} " +
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
            lock (WorkingSync)
            {
                if (_working)
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
                {
                    using var executor = new TestExecutor();
                    result.ResultCode = executor.Execute(t);
                }
                else
                {
                    using var executor = new OutProcExecutor(AgentConfig);
                    result.ResultCode = executor.Execute(t);
                }
            }
            catch (Exception e)
            {
                result.Error = SnTaskError.Create(e);
            }
            Console.WriteLine("Execution finished.");

            result.ResultData = _resultData;
            if (result.Error == null && _resultError != null)
                result.Error = SnTaskError.Parse(_resultError);

            _resultData = null;
            _resultError = null;

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
            if (DateTime.UtcNow.AddMilliseconds(-(AgentConfig.ExecutorTimeoutInSeconds * 1000)) > _executionStateWritten)
            {
                var msg = string.Format( "EXECUTOR TERMINATED: {0}.", _executor.Task.Type);
                Console.WriteLine(msg);
                _resultError = SnTaskError.Create("ExecutorTerminated", "ExecutorTerminated", msg, null).ToString();
                
                if (_executor != null)
                    _executor.Terminate();
            }
        }
        private static IExecutor _executor;
        private static string _progressMessage;
        private static string _resultData;
        private static string _resultError;
        private static DateTime _executionStateWritten = DateTime.MinValue;
        internal static void ExecutionStart(IExecutor executor)
        {
            _executor = executor;
            _progressMessage = null;
            _executionStateWritten = DateTime.UtcNow;

            StartWatcherTimer();
        }
        internal static void ExecutionEnd()
        {
            StopWatcherTimer();

            _executor = null;
            _progressMessage = null;
            _executionStateWritten = DateTime.MinValue;
        }
        internal static void OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e != null && e.Data != null)
            {
                // It does not matter what the executor wrote on the console, it means it is alive.
                // This 'resets' the timer we employ for monitoring executors for programmatic timeout.
                _executionStateWritten = DateTime.UtcNow;

                if (e.Data.StartsWith("Progress:", StringComparison.OrdinalIgnoreCase))
                {
                    SnTrace.TaskManagement.Write("############ Progress received {0}: {1}", AgentName, _progressMessage);                    

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
                    _resultData = e.Data.Substring(11).Trim();
                }
                else if (e.Data.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
                {
                    _resultError = e.Data.Substring(6);
                }
                else if (e.Data.StartsWith("WARNING:", StringComparison.OrdinalIgnoreCase))
                {
                    SnLog.WriteWarning(e.Data.Substring(8), EventId.TaskManagement.General);
                }
                else if (_resultError != null)
                {
                    _resultError += e.Data;
                }
                Console.WriteLine(e.Data);
            }
        }

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
                CPU = 0,
                RAM = 0,
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
                // If deserialization went wrong (e.g. because the executor wrote something with a 'Progress' prefix
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
        private static ulong GetTotalPhysicalMemory()
        {
            //UNDONE: get physical memory (no official .Net Core solution yet)
            //return new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory / (1024 * 1024);
            return 0;
        }

        // hub proxy support
        private static async Task InvokeProxyAsync(string method, params object[] args)
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

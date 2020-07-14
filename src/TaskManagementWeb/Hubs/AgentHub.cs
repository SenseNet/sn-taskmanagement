using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using SenseNet.Diagnostics;
using SenseNet.TaskManagement.Core;
using SenseNet.TaskManagement.Data;
using SenseNet.TaskManagement.Web;

namespace SenseNet.TaskManagement.Hubs
{
    public static class AgentHubExtensions
    {
        public static async Task BroadcastNewTask(this IHubContext<AgentHub> hubContext, SnTask task)
        {
            try
            {
                await hubContext.Clients.All.SendAsync("newTask", task).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                SnLog.WriteException(ex, "AgentHub BroadcastNewTask failed.", EventId.TaskManagement.General);
            }
        }
    }

    //TODO: authentication/authorization
    //[SenseNetAuthorizeAttribute]
    public class AgentHub : Hub
    {
        private readonly IHubContext<TaskMonitorHub> _monitorHub;

        public AgentHub(IHubContext<TaskMonitorHub> monitorHub)
        {
            _monitorHub = monitorHub;
        }

        //===================================================================== Properties

        /// <summary>
        /// Number of connected agents.
        /// </summary>
        public static int ClientCount { get; private set; }

        //===================================================================== Hub API

        public SnTask GetTask(string machineName, string agentName, string[] capabilities)
        {
            SnTrace.TaskManagement.Write("AgentHub GetTask called. Agent: {0}, capabilities: {1}.", agentName, string.Join(", ", capabilities));

            try
            {
                var task = TaskDataHandler.GetNextAndLock(machineName, agentName, capabilities);
                SnTrace.TaskManagement.Write("AgentHub TaskDataHandler.GetNextAndLock returned: " + (task == null ? "null" : "task " + task.Id));

                // task details are not passed to the monitor yet
                if (task != null)
                    _monitorHub.OnTaskEvent(SnTaskEvent.CreateStartedEvent(task.Id, task.Title, null, 
                        task.AppId, task.Tag, machineName, agentName)).GetAwaiter().GetResult(); 

                return task;
            }
            catch (Exception ex)
            {
                SnLog.WriteException(ex, "AgentHub GetTask failed.", EventId.TaskManagement.General);
            }

            return null;
        }

        public void RefreshLock(string machineName, string agentName, int taskId)
        {
            SnTrace.TaskManagement.Write("AgentHub RefreshLock. Agent: {0}, task: {1}.", agentName, taskId);
            TaskDataHandler.RefreshLock(taskId);
        }

        public void Heartbeat(string machineName, string agentName, SnHealthRecord healthRecord)
        {
            SnTrace.TaskManagement.Write($"AgentHub Heartbeat. Machine: {machineName}, Agent: " +
                                         $"{agentName}, Process id: {healthRecord.ProcessId}, " +
                                         $"RAM: {healthRecord.RAM}, CPU: {healthRecord.CPU}.");
            try
            {
                _monitorHub.Heartbeat(machineName, agentName, healthRecord).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {                
                SnLog.WriteException(ex, "AgentHub Heartbeat failed.", EventId.TaskManagement.General);
            }
        }

        public void TaskFinished(SnTaskResult taskResult)
        {
            SnTrace.TaskManagement.Write("AgentHub TaskFinished called. Agent: {0} / {1}, taskId: {2}, code: {3}, error: {4}",
                       taskResult.MachineName, taskResult.AgentName, taskResult.Task.Id, taskResult.ResultCode,
                       taskResult.Error == null ? "" : taskResult.Error.Message);
            try
            {
                if (string.IsNullOrEmpty(taskResult.Task.AppId))
                {
                    SnLog.WriteWarning($"AppId is empty for task #{taskResult.Task.Id}.",
                        EventId.TaskManagement.Lifecycle);
                    return;
                }

                var doesApplicationNeedNotification = !string.IsNullOrWhiteSpace(taskResult.Task.GetFinalizeUrl());
                // first we make sure that the app is accessible by sending a ping request
                if (doesApplicationNeedNotification && !ApplicationHandler.SendPingRequest(taskResult.Task.AppId))
                {
                    var app = ApplicationHandler.GetApplication(taskResult.Task.AppId);

                    SnLog.WriteError(string.Format("Ping request to application {0} ({1}) failed when finalizing task #{2}. Task success: {3}, error: {4}",
                        taskResult.Task.AppId,
                        app == null ? "unknown app" : app.ApplicationUrl,
                        taskResult.Task.Id,
                        taskResult.Successful,
                        taskResult.Error == null ? "-" : taskResult.Error.ToString()),
                        EventId.TaskManagement.Communication);

                    doesApplicationNeedNotification = false;
                }

                // remove the task from the database first
                TaskDataHandler.FinalizeTask(taskResult);

                SnTrace.TaskManagement.Write("AgentHub TaskFinished: task {0} has been deleted.", taskResult.Task.Id);

                if (doesApplicationNeedNotification)
                {
                    // This method does not need to be awaited, because we do not want to do anything 
                    // with the result, only notify the app that the task has been finished.
#pragma warning disable 4014
                    ApplicationHandler.SendFinalizeNotificationAsync(taskResult);
#pragma warning restore 4014
                }

                // notify monitors
                var te = taskResult.Successful
                    ? SnTaskEvent.CreateDoneEvent(taskResult.Task.Id, taskResult.Task.Title,
                        taskResult.ResultData, taskResult.Task.AppId, taskResult.Task.Tag,
                        taskResult.MachineName, taskResult.AgentName)
                    : SnTaskEvent.CreateFailedEvent(taskResult.Task.Id, taskResult.Task.Title,
                        taskResult.ResultData, taskResult.Task.AppId, taskResult.Task.Tag,
                        taskResult.MachineName, taskResult.AgentName);

                _monitorHub.OnTaskEvent(te).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {                
                SnLog.WriteException(ex, "AgentHub TaskFinished failed.", EventId.TaskManagement.General);
            }
        }

        public void StartSubtask(string machineName, string agentName, SnSubtask subtask, SnTask task)
        {
            SnTrace.TaskManagement.Write("AgentHub StartSubtask. Task id:{0}, agent:{1}, title:{2}", 
                task.Id, agentName, subtask.Title);
            try
            { 
                TaskDataHandler.StartSubtask(machineName, agentName, subtask, task);

                _monitorHub.OnTaskEvent(SnTaskEvent.CreateSubtaskStartedEvent(task.Id, subtask.Title, subtask.Details, 
                    task.AppId, task.Tag, machineName, agentName, subtask.Id)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {                
                SnLog.WriteException(ex, "AgentHub StartSubtask failed.", EventId.TaskManagement.General);
            }
        }
        
        public void FinishSubtask(string machineName, string agentName, SnSubtask subtask, SnTask task)
        {
            SnTrace.TaskManagement.Write("AgentHub FinishSubtask. Task id:{0}, agent:{1}, title:{2}", task.Id, agentName, subtask.Title);
            try
            {                
                TaskDataHandler.FinishSubtask(machineName, agentName, subtask, task);
                
                _monitorHub.OnTaskEvent(SnTaskEvent.CreateSubtaskFinishedEvent(task.Id, subtask.Title, subtask.Details, 
                    task.AppId, task.Tag, machineName, agentName, subtask.Id)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {                
                SnLog.WriteException(ex, "AgentHub FinishSubtask failed.", EventId.TaskManagement.General);
            }
        }

        public void WriteProgress(string machineName, string agentName, SnProgressRecord progressRecord)
        {
            SnTrace.TaskManagement.Write("AgentHub WriteProgress. agent:{0}, progress:{1}", 
                agentName, progressRecord);               
            try
            {
                _monitorHub.WriteProgress(machineName, agentName, progressRecord).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {                
                SnLog.WriteException(ex, "AgentHub WriteProgress failed.", EventId.TaskManagement.General);
            }
        }
        
        //===================================================================== Overrides

        public override Task OnConnectedAsync()
        {
            ClientCount++;
            SnTrace.TaskManagement.Write("AgentHub OnConnected. Client count: " + ClientCount);

            var agentHub = Context.GetHttpContext()?.RequestServices.GetService(typeof(IHubContext<AgentHub>)) as
                IHubContext<AgentHub>;

            // This is here to have access to the agent hub service. Timer initialization
            // should happen only once.
            InitializeDeadTaskTimer(agentHub);

            return base.OnConnectedAsync();
        }
        public override Task OnDisconnectedAsync(Exception ex)
        {
            ClientCount--;
            SnTrace.TaskManagement.Write("AgentHub OnDisconnected(exception: {0}). Client count: {1}", 
                ex?.Message, ClientCount);

            return base.OnDisconnectedAsync(ex);
        }

        private static readonly int HandleDeadTaskPeriodInMilliseconds = 60 * 1000;
        private static Timer _deadTaskTimer;

        private static void InitializeDeadTaskTimer(IHubContext<AgentHub> agentHub)
        {
            if (agentHub == null)
                return;

            // initialize the timer only once
            if (_deadTaskTimer != null)
                return;

            SnTrace.TaskManagement.Write("Initializing dead task timer.");

            _deadTaskTimer = new Timer(state =>
                {
                    var ah = (IHubContext<AgentHub>)state;
                    var dtc = TaskDataHandler.GetDeadTaskCount();
                    
                    // if there is a dead task in the db, notify agents
                    if (dtc > 0)
                        ah.BroadcastNewTask(null).GetAwaiter().GetResult();
                }, 
                agentHub,
                HandleDeadTaskPeriodInMilliseconds,
                HandleDeadTaskPeriodInMilliseconds);
        }
    }
}
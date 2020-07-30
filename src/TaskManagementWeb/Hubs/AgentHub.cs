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
        private readonly ApplicationHandler _applicationHandler;
        private readonly TaskDataHandler _dataHandler;

        public AgentHub(IHubContext<TaskMonitorHub> monitorHub, ApplicationHandler appHandler, TaskDataHandler dataHandler)
        {
            _monitorHub = monitorHub;
            _applicationHandler = appHandler;
            _dataHandler = dataHandler;
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
                var task = _dataHandler.GetNextAndLock(machineName, agentName, capabilities);
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
            _dataHandler.RefreshLock(taskId);
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

                var app = _applicationHandler.GetApplication(taskResult.Task.AppId);
                var doesApplicationNeedNotification = !string.IsNullOrWhiteSpace(taskResult.Task.GetFinalizeUrl(app));

                // first we make sure that the app is accessible by sending a ping request
                if (doesApplicationNeedNotification && !_applicationHandler.SendPingRequest(taskResult.Task.AppId))
                {
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
                _dataHandler.FinalizeTask(taskResult);

                SnTrace.TaskManagement.Write("AgentHub TaskFinished: task {0} has been deleted.", taskResult.Task.Id);

                if (doesApplicationNeedNotification)
                {
                    // This method does not need to be awaited, because we do not want to do anything 
                    // with the result, only notify the app that the task has been finished.
#pragma warning disable 4014
                    _applicationHandler.SendFinalizeNotificationAsync(taskResult);
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
                _dataHandler.StartSubtask(machineName, agentName, subtask, task);

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
                _dataHandler.FinishSubtask(machineName, agentName, subtask, task);
                
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
            
            return base.OnConnectedAsync();
        }
        public override Task OnDisconnectedAsync(Exception ex)
        {
            ClientCount--;
            SnTrace.TaskManagement.Write("AgentHub OnDisconnected(exception: {0}). Client count: {1}", 
                ex?.Message, ClientCount);

            return base.OnDisconnectedAsync(ex);
        }
    }
}
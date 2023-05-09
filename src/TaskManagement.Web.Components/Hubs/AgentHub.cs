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
        private readonly ApplicationConnector _applicationConnector;

        public AgentHub(IHubContext<TaskMonitorHub> monitorHub, ApplicationHandler appHandler, TaskDataHandler dataHandler,
            ApplicationConnector applicationConnector)
        {
            _monitorHub = monitorHub;
            _applicationHandler = appHandler;
            _dataHandler = dataHandler;
            _applicationConnector = applicationConnector;

            if (_monitorHub == null)
                SnTrace.TaskManagement.WriteError($"AgentHub MonitorHub is null.");
            if (_applicationHandler == null)
                SnTrace.TaskManagement.WriteError($"AgentHub ApplicationHandler is null.");
            if (_dataHandler == null)
                SnTrace.TaskManagement.WriteError($"AgentHub DataHandler is null.");
        }

        //===================================================================== Properties

        /// <summary>
        /// Number of connected agents.
        /// </summary>
        public static int ClientCount { get; private set; }

        //===================================================================== Hub API

        public async Task<SnTask> GetTask(string machineName, string agentName, string[] capabilities)
        {
            SnTrace.TaskManagement.Write("AgentHub GetTask called. Agent: {0}, capabilities: {1}.", agentName, string.Join(", ", capabilities));

            try
            {
                var task = await _dataHandler.GetNextAndLock(machineName, agentName, capabilities, Context.ConnectionAborted)
                    .ConfigureAwait(false);

                SnTrace.TaskManagement.Write("AgentHub TaskDataHandler.GetNextAndLock returned: " + (task == null ? "null" : "task " + task.Id));

                // task details are not passed to the monitor yet
                if (task != null)
                {
                    await _monitorHub.OnTaskEvent(SnTaskEvent.CreateStartedEvent(task.Id, task.Title, null,
                        task.AppId, task.Tag, machineName, agentName)).ConfigureAwait(false);

                    // set authentication on the task object
                    var app = _applicationHandler.GetApplication(task.AppId);
                    if (app != null)
                    {
                        // select the authentication method based on the task type - or use the default one
                        var appAuth = app.GetAuthenticationForTask(task.Type);
                        if (appAuth != null)
                            task.Authentication = appAuth;
                    }
                }

                return task;
            }
            catch (Exception ex)
            {
                SnLog.WriteException(ex, "AgentHub GetTask failed.", EventId.TaskManagement.General);
            }

            return null;
        }

        public async Task RefreshLock(string machineName, string agentName, int taskId)
        {
            SnTrace.TaskManagement.Write("AgentHub RefreshLock. Agent: {0}, task: {1}.", agentName, taskId);
            await _dataHandler.RefreshLockAsync(taskId, Context.ConnectionAborted).ConfigureAwait(false); ;
        }

        public async Task Heartbeat(string machineName, string agentName, SnHealthRecord healthRecord)
        {
            SnTrace.TaskManagement.Write($"AgentHub Heartbeat. Machine: {machineName}, Agent: " +
                                         $"{agentName}, Process id: {healthRecord.ProcessId}, " +
                                         $"RAM: {healthRecord.RAM}, CPU: {healthRecord.CPU}.");
            try
            {
                await _monitorHub.Heartbeat(machineName, agentName, healthRecord).ConfigureAwait(false);
            }
            catch (Exception ex)
            {                
                SnLog.WriteException(ex, "AgentHub Heartbeat failed.", EventId.TaskManagement.General);
            }
        }

        public async Task TaskFinished(SnTaskResult taskResult)
        {
            SnTrace.TaskManagement.Write("AgentHub TaskFinished called. Agent: {0} / {1}, taskId: #{2}, code: {3}, error: {4}",
                       taskResult.MachineName, taskResult.AgentName, taskResult.Task?.Id, taskResult.ResultCode,
                       taskResult.Error == null ? "-" : taskResult.Error.Message);

            if (taskResult.Task == null)
            {
                SnTrace.TaskManagement.WriteError("Task not found.");
                return;
            }

            try
            {
                if (string.IsNullOrEmpty(taskResult.Task.AppId))
                {
                    SnLog.WriteWarning($"AppId is empty for task #{taskResult.Task.Id}.",
                        EventId.TaskManagement.Lifecycle);
                    return;
                }

                var app = _applicationHandler.GetApplication(taskResult.Task.AppId);
                if (app == null)
                    SnTrace.TaskManagement.Write($"App not found with id {taskResult.Task.AppId} for taskId #{taskResult.Task.Id}");

                var finalizeUrl = taskResult.Task.GetFinalizeUrl(app);
                var doesApplicationNeedNotification = !string.IsNullOrWhiteSpace(finalizeUrl);

                SnTrace.TaskManagement.Write(!doesApplicationNeedNotification
                    ? $"AgentHub Finalize url is empty for task #{taskResult.Task.Id}"
                    : $"AgentHub Finalizing task #{taskResult.Task.Id}");

                // first we make sure that the app is accessible by sending a ping request
                if (doesApplicationNeedNotification && !(await _applicationConnector.SendPingRequestAsync(taskResult.Task, 
                        Context?.ConnectionAborted ?? CancellationToken.None)
                        .ConfigureAwait(false)))
                {
                    SnLog.WriteError($"Ping request to application {taskResult.Task.AppId} " +
                                     $"({(app == null ? "unknown app" : app.ApplicationUrl)}) " +
                                     $"failed when finalizing task #{taskResult.Task.Id}. " +
                                     $"Task success: {taskResult.Successful}, " +
                                     $"error: {(taskResult.Error == null ? "-" : taskResult.Error.ToString())}",
                        EventId.TaskManagement.Communication);

                    doesApplicationNeedNotification = false;
                }

                // remove the task from the database first
                await _dataHandler.FinalizeTaskAsync(taskResult, 
                    Context?.ConnectionAborted ?? CancellationToken.None).ConfigureAwait(false);

                SnTrace.TaskManagement.Write("AgentHub TaskFinished: task {0} has been deleted.", taskResult.Task.Id);

                if (doesApplicationNeedNotification)
                {
                    SnTrace.TaskManagement.Write($"AgentHub TaskFinished: sending finalize notification for task #{taskResult.Task.Id} " +
                                                 $"to {finalizeUrl}.");

                    // This method does not need to be awaited, because we do not want to do anything 
                    // with the result, only notify the app that the task has been finished.
#pragma warning disable 4014
                    _applicationConnector.SendFinalizeNotificationAsync(taskResult, CancellationToken.None);
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

                await _monitorHub.OnTaskEvent(te).ConfigureAwait(false);
            }
            catch (Exception ex)
            {                
                SnLog.WriteException(ex, "AgentHub TaskFinished failed.", EventId.TaskManagement.General);
            }
        }

        public async Task StartSubtask(string machineName, string agentName, SnSubtask subtask, SnTask task)
        {
            SnTrace.TaskManagement.Write("AgentHub StartSubtask. Task id:{0}, agent:{1}, title:{2}", 
                task.Id, agentName, subtask.Title);
            try
            { 
                await _dataHandler.StartSubtask(machineName, agentName, subtask, task, Context.ConnectionAborted).ConfigureAwait(false);

                await _monitorHub.OnTaskEvent(SnTaskEvent.CreateSubtaskStartedEvent(task.Id, subtask.Title, subtask.Details, 
                    task.AppId, task.Tag, machineName, agentName, subtask.Id)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {                
                SnLog.WriteException(ex, "AgentHub StartSubtask failed.", EventId.TaskManagement.General);
            }
        }
        
        public async Task FinishSubtask(string machineName, string agentName, SnSubtask subtask, SnTask task)
        {
            SnTrace.TaskManagement.Write("AgentHub FinishSubtask. Task id:{0}, agent:{1}, title:{2}", task.Id, agentName, subtask.Title);
            try
            {
                await _dataHandler.FinishSubtask(machineName, agentName, subtask, task, Context.ConnectionAborted).ConfigureAwait(false);
                
                await _monitorHub.OnTaskEvent(SnTaskEvent.CreateSubtaskFinishedEvent(task.Id, subtask.Title, subtask.Details, 
                    task.AppId, task.Tag, machineName, agentName, subtask.Id)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {                
                SnLog.WriteException(ex, "AgentHub FinishSubtask failed.", EventId.TaskManagement.General);
            }
        }

        public async Task WriteProgress(string machineName, string agentName, SnProgressRecord progressRecord)
        {
            SnTrace.TaskManagement.Write("AgentHub WriteProgress. agent:{0}, progress:{1}", 
                agentName, progressRecord);               
            try
            {
                await _monitorHub.WriteProgress(machineName, agentName, progressRecord).ConfigureAwait(false);
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
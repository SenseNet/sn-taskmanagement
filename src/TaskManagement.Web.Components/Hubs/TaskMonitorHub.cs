using Microsoft.AspNetCore.SignalR;
using SenseNet.Diagnostics;
using SenseNet.TaskManagement.Core;
using SenseNet.TaskManagement.Data;

// ReSharper disable once CheckNamespace
namespace SenseNet.TaskManagement.Hubs
{
    public static class TaskMonitorHubExtensions
    {
        /// <summary>
        /// Calls the onTaskEvent client method when a task state event occurs (e.g. started, finished, etc.). 
        /// Only clients with the appropriate app id are called.
        /// </summary>
        public static async Task OnTaskEvent(this IHubContext<TaskMonitorHub> hubContext, SnTaskEvent taskEvent)
        {
            try
            {
                // Send events to clients with the same app id only. Monitor clients are 
                // registered to the appropriate group in the OnConnected event handler.
                await hubContext.Clients.Group(taskEvent.AppId).SendAsync("onTaskEvent", taskEvent)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                SnLog.WriteException(ex, "TaskMonitorHub OnTaskEvent failed.", EventId.TaskManagement.General);
            }
        }
        /// <summary>
        /// Periodically calls the Heartbeat client method for providing state information
        /// about task agents. The message is sent to all clients.
        /// </summary>
        public static async Task Heartbeat(this IHubContext<TaskMonitorHub> hubContext,
            string machineName, string agentName, SnHealthRecord healthRecord)
        {
            try
            {
                // the heartbeat is sent to every monitor client
                await hubContext.Clients.All.SendAsync("heartbeat", agentName, healthRecord)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                SnLog.WriteException(ex, "TaskMonitorHub Heartbeat failed.", EventId.TaskManagement.General);
            }
        }
        /// <summary>
        /// Calls the WriteProgress client method when a subtask progress event occurs.
        /// </summary>
        public static async Task WriteProgress(this IHubContext<TaskMonitorHub> hubContext,
            string machineName, string agentName, SnProgressRecord progressRecord)
        {
            try
            {
                // Send progress to clients with the same app id only. Monitor clients are 
                // registered to the appropriate group in the OnConnected event handler.
                await hubContext.Clients.Group(progressRecord.AppId).SendAsync("writeProgress", progressRecord)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                SnLog.WriteException(ex, "TaskMonitorHub WriteProgress failed.", EventId.TaskManagement.General);
            }
        }
    }

    public class TaskMonitorHub : Hub
    {
        private readonly TaskDataHandler _dataHandler;

        public TaskMonitorHub(TaskDataHandler dataHandler)
        {
            _dataHandler = dataHandler;
        }

        //===================================================================== Hub API

        /// <summary>
        /// Loads all tasks from the database that are registered, but not finished or failed. The real status of
        /// currently in progress tasks will be set with the next progress or event call.
        /// </summary>
        /// <param name="appId">Application id to identify the client application.</param>
        /// <param name="tag">If a tag is provided, events will be filtered by it.</param>
        /// <returns></returns>
        public Task<SnTaskEvent[]> GetUnfinishedTasks(string appId, string tag)
        {
            return _dataHandler.GetUnfinishedTasksAsync(appId, tag, Context.ConnectionAborted);
        }

        /// <summary>
        /// Loads all task and subtask events for a single task.
        /// </summary>
        /// <param name="appId">Application id to identify the client application.</param>
        /// <param name="tag">If a tag is provided, events will be filtered by it.</param>
        /// <param name="taskId">Id of the task to load events for.</param>
        /// <returns></returns>
        public Task<SnTaskEvent[]> GetDetailedTaskEvents(string appId, string tag, int taskId)
        {
            return _dataHandler.GetDetailedTaskEventsAsync(appId, tag, taskId, Context.ConnectionAborted);
        }

        //===================================================================== Overrides

        public override async Task OnConnectedAsync()
        {
            // Add this client to the appropriate group. Only clients connected 
            // with the same appid will receive messages about a certain task.
            var appid = Context.GetHttpContext().Request.Query["appid"].FirstOrDefault();
            if (!string.IsNullOrEmpty(appid))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, appid);                
            }

            SnTrace.TaskManagement.Write("TaskMonitorHub Client connected. AppId: {0}", appid ?? string.Empty);

            await base.OnConnectedAsync();
        }
    }
}
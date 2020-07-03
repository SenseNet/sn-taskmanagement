using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.SignalR;
using SenseNet.Diagnostics;
using SenseNet.TaskManagement.Core;
using SenseNet.TaskManagement.Data;
using SenseNet.TaskManagement.Web;

namespace SenseNet.TaskManagement.Hubs
{
    public class TaskMonitorHub : Hub
    {
        //===================================================================== Hub API

        /// <summary>
        /// Loads all tasks from the database that are registered, but not finished or failed. The real status of
        /// currently in progress tasks will be set with the next progress or event call.
        /// </summary>
        /// <param name="appId">Application id to identify the client application.</param>
        /// <param name="tag">If a tag is provided, events will be filtered by it.</param>
        /// <returns></returns>
        public SnTaskEvent[] GetUnfinishedTasks(string appId, string tag)
        {
            return TaskDataHandler.GetUnfinishedTasks(appId, tag);
        }

        /// <summary>
        /// Loads all task and subtask events for a single task.
        /// </summary>
        /// <param name="appId">Application id to identify the client application.</param>
        /// <param name="tag">If a tag is provided, events will be filtered by it.</param>
        /// <param name="taskId">Id of the task to load events for.</param>
        /// <returns></returns>
        public SnTaskEvent[] GetDetailedTaskEvents(string appId, string tag, int taskId)
        {
            return TaskDataHandler.GetDetailedTaskEvents(appId, tag, taskId);
        }

        //===================================================================== Static API

        /// <summary>
        /// Periodically calles the Heartbeat client method for providing state information about task agents. The message is sent to all clients.
        /// </summary>
        public static void Heartbeat(string machineName, string agentName, SnHealthRecord healthRecord)
        {          
            SnTrace.TaskManagement.Write("TaskMonitorHub Heartbeat. MachineName: {0}, agentName: {1}, healthRecord: {2}", machineName, agentName, healthRecord);

            //UNDONE: use new SignalR API
            // heartbeat is sent to every monitor client
            //var hubContext = GlobalHost.ConnectionManager.GetHubContext<TaskMonitorHub>();
            //hubContext.Clients.All.Heartbeat(agentName, healthRecord);
        }

        /// <summary>
        /// Calls the OnTaskEvent client method when a task state event occurs (e.g. started, finished, etc.). 
        /// Only clients with the appropriate app id are called.
        /// </summary>
        public static void OnTaskEvent(SnTaskEvent e)
        {
            SnTrace.TaskManagement.Write("TaskMonitorHub OnTaskEvent: {0}, taskId: {1}, agent: {2}", e.EventType, e.TaskId, e.Agent);

            //UNDONE: use new SignalR API
            //var hubContext = GlobalHost.ConnectionManager.GetHubContext<TaskMonitorHub>();

            //// Send events to clients with the same app id only. Monitor clients are 
            //// registered to the appropriate group in the OnConnected event handler.
            //hubContext.Clients.Group(e.AppId).OnTaskEvent(e);
        }

        /// <summary>
        /// Calls the WriteProgress client method when a subtask progress event occurs.
        /// </summary>
        public static void WriteProgress(string machineName, string agentName, SnProgressRecord progressRecord)
        {
            SnTrace.TaskManagement.Write("TaskMonitorHub WriteProgress: {0}, taskId: {1}, agent: {2}", progressRecord.Progress.OverallProgress, progressRecord.TaskId, agentName);

            //UNDONE: use new SignalR API
            //var hubContext = GlobalHost.ConnectionManager.GetHubContext<TaskMonitorHub>();

            //// Send progress to clients with the same app id only. Monitor clients are 
            //// registered to the appropriate group in the OnConnected event handler.
            //hubContext.Clients.Group(progressRecord.AppId).WriteProgress(progressRecord);
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
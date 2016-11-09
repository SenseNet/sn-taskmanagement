using System;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using SenseNet.Diagnostics;
using SenseNet.TaskManagement.Core;
using SenseNet.TaskManagement.Data;
using SenseNet.TaskManagement.Web;

namespace SenseNet.TaskManagement.Hubs
{
    //TODO: authentication/authorization
    //[SenseNetAuthorizeAttribute]
    public class AgentHub : Hub
    {
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
                SnTrace.TaskManagement.Write("AgentHub TaskDataHandler.GetNextAndLock returned with: " + (task == null ? "null" : "task " + task.Id.ToString()));

                // task details are not passed to the monitor yet
                if (task != null)
                    TaskMonitorHub.OnTaskEvent(SnTaskEvent.CreateStartedEvent(task.Id, task.Title, null, task.AppId, task.Tag, machineName, agentName)); 

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
            SnTrace.TaskManagement.Write("AgentHub Heartbeat. Agent: {0}, data: {1}.", agentName, healthRecord);
            try
            {
                TaskMonitorHub.Heartbeat(machineName, agentName, healthRecord);
            }
            catch (Exception ex)
            {                
                SnLog.WriteException(ex, "AgentHub RefreshLock failed.", EventId.TaskManagement.General);
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

                // first we make sure that the app is accessible by sending a ping request
                if (!ApplicationHandler.SendPingRequest(taskResult.Task.AppId))
                {
                    var app = ApplicationHandler.GetApplication(taskResult.Task.AppId);

                    SnLog.WriteError(string.Format("Ping request to application {0} ({1}) failed when finalizing task #{2}. Task success: {3}, error: {4}",
                        taskResult.Task.AppId,
                        app == null ? "unknown app" : app.ApplicationUrl,
                        taskResult.Task.Id,
                        taskResult.Successful,
                        taskResult.Error == null ? "-" : taskResult.Error.ToString()),
                        EventId.TaskManagement.Communication);

                    return;
                }

                // remove the task from the database first
                TaskDataHandler.FinalizeTask(taskResult);

                SnTrace.TaskManagement.Write("AgentHub TaskFinished: task {0} has been deleted.", taskResult.Task.Id);

                // This method does not need to be awaited, because we do not want to do anything 
                // with the result, only notify the app that the task has been finished.
                ApplicationHandler.SendFinalizeNotificationAsync(taskResult);

                // notify monitors
                TaskMonitorHub.OnTaskEvent(taskResult.Successful
                    ? SnTaskEvent.CreateDoneEvent(taskResult.Task.Id, taskResult.Task.Title, taskResult.ResultData, taskResult.Task.AppId, taskResult.Task.Tag, taskResult.MachineName, taskResult.AgentName)
                    : SnTaskEvent.CreateFailedEvent(taskResult.Task.Id, taskResult.Task.Title, taskResult.ResultData, taskResult.Task.AppId, taskResult.Task.Tag, taskResult.MachineName, taskResult.AgentName));
            }
            catch (Exception ex)
            {                
                SnLog.WriteException(ex, "AgentHub TaskFinished failed.", EventId.TaskManagement.General);
            }
        }

        public void StartSubtask(string machineName, string agentName, SnSubtask subtask, SnTask task)
        {
            SnTrace.TaskManagement.Write("AgentHub StartSubtask. Task id:{0}, agent:{1}, title:{2}", task.Id, agentName, subtask.Title);
            try
            { 
                TaskDataHandler.StartSubtask(machineName, agentName, subtask, task);
                TaskMonitorHub.OnTaskEvent(SnTaskEvent.CreateSubtaskStartedEvent(task.Id, subtask.Title, subtask.Details, task.AppId, task.Tag, machineName, agentName, subtask.Id));
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
                TaskMonitorHub.OnTaskEvent(SnTaskEvent.CreateSubtaskFinishedEvent(task.Id, subtask.Title, subtask.Details, task.AppId, task.Tag, machineName, agentName, subtask.Id));
            }
            catch (Exception ex)
            {                
                SnLog.WriteException(ex, "AgentHub FinishSubtask failed.", EventId.TaskManagement.General);
            }
        }

        public void WriteProgress(string machineName, string agentName, SnProgressRecord progressRecord)
        {
            SnTrace.TaskManagement.Write("AgentHub WriteProgress. agent:{0}, progress:{1}", agentName, progressRecord);               
            try
            {
                TaskMonitorHub.WriteProgress(machineName, agentName, progressRecord);
            }
            catch (Exception ex)
            {                
                SnLog.WriteException(ex, "AgentHub WriteProgress failed.", EventId.TaskManagement.General);
            }
        }

        //===================================================================== Static API

        public static void BroadcastMessage(SnTask task)
        {
            try
            {
                var hubContext = GlobalHost.ConnectionManager.GetHubContext<AgentHub>();
                hubContext.Clients.All.NewTask(task);
            }
            catch (Exception ex)
            {                
                SnLog.WriteException(ex, "AgentHub BroadcastMessage failed.", EventId.TaskManagement.General);
            }
        }

        //===================================================================== Overrides

        public override Task OnConnected()
        {
            //_connections.Add(this.Context.ConnectionId);
            ClientCount++;
            SnTrace.TaskManagement.Write("AgentHub OnConnected. Client count: " + ClientCount);

            return base.OnConnected();
        }
        public override Task OnReconnected()
        {
            ClientCount++;
            SnTrace.TaskManagement.Write("AgentHub OnReconnected. Client count: " + ClientCount);
            
            return base.OnReconnected();
        }
        public override Task OnDisconnected(bool stopCalled)
        {
            ClientCount--;
            SnTrace.TaskManagement.Write("AgentHub OnDisconnected(stop called: {0}). Client count: {1}", stopCalled, ClientCount);

            return base.OnDisconnected(stopCalled);
        }        
    }
}
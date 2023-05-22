using System.Text;
using SenseNet.TaskManagement.Core;
using SenseNet.TaskManagement.Data;
using SenseNet.TaskManagement.Hubs;
using SenseNet.TaskManagement.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SenseNet.Diagnostics;
using EventId = SenseNet.Diagnostics.EventId;

// ReSharper disable once CheckNamespace
namespace SenseNet.TaskManagement.Controllers
{
    public class TaskController : Controller
    {
        private readonly IHubContext<AgentHub> _agentHub;
        private readonly IHubContext<TaskMonitorHub> _monitorHub;
        private readonly ApplicationHandler _applicationHandler;
        private readonly TaskDataHandler _dataHandler;
        private readonly ILogger<TaskController> _logger;

        public TaskController(IHubContext<AgentHub> agentHub, IHubContext<TaskMonitorHub> monitorHub,
            ApplicationHandler appHandler, TaskDataHandler dataHandler, ILogger<TaskController> logger)
        {
            _agentHub = agentHub;
            _monitorHub = monitorHub;
            _applicationHandler = appHandler;
            _dataHandler = dataHandler;
            _logger = logger;
        }

        /// <summary>
        /// Registers a task.
        /// </summary>
        /// <param name="taskRequest">Contains the necessary information for registering a task.</param>
        /// <returns>Returns a RegisterTaskResult object containing information about the registered task.</returns>
        [HttpPost]
        public async Task<RegisterTaskResult> RegisterTask([FromBody]RegisterTaskRequest taskRequest)
        {
            Application app = null;

            try
            {
                // load the corresponding application to make sure the appid is valid
                app = _applicationHandler.GetApplication(taskRequest.AppId);
            }
            catch (Exception ex)
            {
                SnLog.WriteException(ex, "Error loading app for id " + taskRequest.AppId, EventId.TaskManagement.General);
            }

            // If we do not know the appid, we must not register the task. Client applications
            // must observe this response and try to re-register the application, before
            // trying to register the task again (this can happen if the TaskManagement Web
            // was unreachable when the client application tried to register the appid before).
            if (app == null)
            {
                return new RegisterTaskResult { Error = RegisterTaskRequest.ERROR_UNKNOWN_APPID };
            }

            RegisterTaskResult result;
            try
            {
                // calculate hash with the default algorithm if not given
                var hash = taskRequest.Hash == 0
                    ? ComputeTaskHash(taskRequest.Type + taskRequest.AppId + taskRequest.Tag + taskRequest.TaskData)
                    : taskRequest.Hash;

                result = await _dataHandler.RegisterTaskAsync(
                    taskRequest.Type,
                    taskRequest.Title,
                    taskRequest.Priority,
                    taskRequest.AppId,
                    taskRequest.Tag,
                    taskRequest.FinalizeUrl,
                    hash,
                    taskRequest.TaskData,
                    taskRequest.MachineName,
                    HttpContext.RequestAborted).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                var msg = $"Task registration failed. {ex.Message} AppId: {app.AppId}, " +
                          $"Task: {taskRequest.Type}, Title: {taskRequest.Title}";

                SnLog.WriteException(ex, msg);

                return new RegisterTaskResult { Error = RegisterTaskResult.ErrorTaskRegistrationFailed };
            }

            try
            {
                // notify agents
                await _agentHub.BroadcastNewTask(result.Task).ConfigureAwait(false);

                // notify monitor clients
                await _monitorHub.OnTaskEvent(SnTaskEvent.CreateRegisteredEvent(
                    result.Task.Id, result.Task.Title, string.Empty, result.Task.AppId,
                    result.Task.Tag, null, result.Task.Type, result.Task.Order,
                    result.Task.Hash, result.Task.TaskData)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // The task has been created successfully, this error is only about
                // notification, so client applications should not be notified.
                SnLog.WriteException(ex, "Error during agent or monitor notification after a task was registered.", EventId.TaskManagement.Communication);
            }

            return result;
        }

        /// <summary>
        /// Registers an application.
        /// </summary>
        /// <param name="appRequest">Contains the necessary information for registering an application.</param>
        [HttpPost]        
        public async Task<RegisterApplicationResult> RegisterApplication([FromBody]RegisterApplicationRequest appRequest)
        {
            try
            {
                var _ = await _dataHandler.RegisterApplicationAsync(appRequest, HttpContext.RequestAborted)
                    .ConfigureAwait(false);

                _logger.LogTrace("Application registered with app id {appId}, url: {url}, authentication: {auth}",
                    appRequest.AppId, appRequest.ApplicationUrl,
                    string.Join(", ",
                        appRequest.Authentication?.Select(a => a.TaskType + ": " + a.ApiKey[..5]) ??
                        Array.Empty<string>()));
            }
            catch (Exception ex)
            {
                SnLog.WriteException(ex, $"Error during app registration. AppId: {appRequest?.AppId}, " +
                                         $"Url: {appRequest?.ApplicationUrl}");

                return new RegisterApplicationResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }

            // invalidate app cache
            _applicationHandler.Reset();

            return new RegisterApplicationResult();
        }

        private static Int64 ComputeTaskHash(string data)
        {
            if (String.IsNullOrEmpty(data))
                return 0L;
            byte[] rawHash;
            using (var sha = new System.Security.Cryptography.SHA256CryptoServiceProvider())
                rawHash = sha.ComputeHash(Encoding.Unicode.GetBytes(data));
            return BitConverter.ToInt64(rawHash, 0) ^ BitConverter.ToInt64(rawHash, 8) ^ BitConverter.ToInt64(rawHash, 24);
        }
    }
}
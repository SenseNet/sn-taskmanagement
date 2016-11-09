using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using SenseNet.TaskManagement.Core;
using SenseNet.TaskManagement.Data;
using SenseNet.TaskManagement.Hubs;
using SenseNet.TaskManagement.Web;
using System.Net;
using SenseNet.Diagnostics;

namespace SenseNet.TaskManagement.Controllers
{
    public class TaskController : ApiController
    {
        /// <summary>
        /// Registers a task.
        /// </summary>
        /// <param name="taskRequest">Contains the necessary information for registering a task.</param>
        /// <returns>Returns a RegisterTaskResult object containing information about the registered task.</returns>
        [HttpPost]
        public RegisterTaskResult RegisterTask(RegisterTaskRequest taskRequest)
        {
            Application app = null;

            try
            {
                // load the corresponding application to make sure the appid is valid
                app = ApplicationHandler.GetApplication(taskRequest.AppId);
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
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, RegisterTaskRequest.ERROR_UNKNOWN_APPID));

            RegisterTaskResult result = null;

            try
            {
                // calculate hash with the default algrithm if not given
                var hash = taskRequest.Hash == 0
                    ? ComputeTaskHash(taskRequest.Type + taskRequest.AppId + taskRequest.Tag + taskRequest.TaskData)
                    : taskRequest.Hash;

                result = TaskDataHandler.RegisterTask(
                    taskRequest.Type,
                    taskRequest.Title,
                    taskRequest.Priority,
                    taskRequest.AppId,
                    taskRequest.Tag,
                    taskRequest.FinalizeUrl,
                    hash,
                    taskRequest.TaskData,
                    taskRequest.MachineName);
            }
            catch (Exception ex)
            {
                // the client app needs to be notified
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, ex));
            }

            try
            {
                // notify agents
                AgentHub.BroadcastMessage(result.Task);

                // notify monitor clients
                TaskMonitorHub.OnTaskEvent(SnTaskEvent.CreateRegisteredEvent(
                    result.Task.Id, result.Task.Title, string.Empty, result.Task.AppId,
                    result.Task.Tag, null, result.Task.Type, result.Task.Order,
                    result.Task.Hash, result.Task.TaskData));
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
        public void RegisterApplication(RegisterApplicationRequest appRequest)
        {
            try
            {
                var app = TaskDataHandler.RegisterApplication(appRequest);
            }
            catch (Exception ex)
            {
                // the client app needs to be notified
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, ex));
            }

            // invalidate app cache
            ApplicationHandler.Reset();
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
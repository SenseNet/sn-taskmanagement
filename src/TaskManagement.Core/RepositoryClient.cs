using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SenseNet.Diagnostics;

namespace SenseNet.TaskManagement.Core
{
    /// <summary>
    /// Client API for communicating with the Task Management web application. TaskManager implementations
    /// use it so that they do not have to send native HTTP requests.
    /// </summary>
    public static class RepositoryClient
    {
        private static class Error
        {
            internal static readonly string REGISTERTASK = "Register task request failed. ";
            internal static readonly string REGISTERTASK_MISSING_URL = "Task management url is missing.";
            internal static readonly string REGISTERTASK_MISSING_REQUESTDATA = "Request data is missing.";
            internal static readonly string REGISTERTASK_MISSING_APPID_OR_TASK = "Please fill at least the app id and task type.";
            
            internal static readonly string REGISTERAPP = "Register app request failed. ";
            internal static readonly string REGISTERAPP_MISSING_APPID = "Please fill the app id.";
        }

        private static readonly string APIURL_REGISTERAPP = "api/task/registerapplication";
        private static readonly string APIURL_REGISTERTASK = "api/task/registertask";

        /// <summary>
        /// Registers a task through the task management REST API as an asynchronous operation.
        /// </summary>
        /// <param name="taskManagementUrl">The url of the TaskManagement component.</param>
        /// <param name="requestData">Contains the necessary information for registering a task.</param>
        /// <returns>Returns a RegisterTaskResult object containing information about the registered task.</returns>
        public static async Task<RegisterTaskResult> RegisterTaskAsync(string taskManagementUrl, RegisterTaskRequest requestData)
        {
            // null checks
            if (string.IsNullOrEmpty(taskManagementUrl))
                throw new TaskManagementException(Error.REGISTERTASK_MISSING_URL);
            if (requestData == null)
                throw new TaskManagementException(Error.REGISTERTASK_MISSING_REQUESTDATA);
            if (string.IsNullOrEmpty(requestData.AppId) || string.IsNullOrEmpty(requestData.Type))
                throw new TaskManagementException(Error.REGISTERTASK_MISSING_APPID_OR_TASK);

            // fill the machine name if not provided
            if (string.IsNullOrEmpty(requestData.MachineName))
                requestData.MachineName = Environment.MachineName;

            HttpResponseMessage response = null;
            var responseText = string.Empty;

            try
            {
                response = await SendRequest(taskManagementUrl, requestData, APIURL_REGISTERTASK).ConfigureAwait(false);
                responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    return JsonConvert.DeserializeObject<RegisterTaskResult>(responseText);
                }
                else
                {
                    // Check if the error states that the appid is unknown. In that case we need to throw
                    // an exception so that the client can re-register the app and then the task again.
                    var message = GetResponseMessage(responseText);
                    if (string.CompareOrdinal(message, RegisterTaskRequest.ERROR_UNKNOWN_APPID) == 0)
                        throw new TaskManagementException(RegisterTaskRequest.ERROR_UNKNOWN_APPID, requestData.AppId, null);
                }
            }
            catch (TaskManagementException)
            {
                // simply throw it further: this is already a well-formed exception
                throw;
            }
            catch (Exception ex)
            {
                throw new TaskManagementException(Error.REGISTERTASK + GetResponsePhrase(response) + responseText,
                    requestData.AppId, taskType: requestData.Type, innerException: ex);
            }

            // the response did not throw an exception, but was not successful
            throw new TaskManagementException(Error.REGISTERTASK + GetResponsePhrase(response) + responseText, 
                requestData.AppId, taskType: requestData.Type);
        }

        /// <summary>
        /// Registers an application through the task management REST API as an asynchronous operation.
        /// </summary>
        /// <param name="taskManagementUrl">The url of the TaskManagement component.</param>
        /// <param name="requestData">Contains the necessary information for registering an application.</param>
        /// <returns>True if the request was successful.</returns>
        public static async Task RegisterApplicationAsync(string taskManagementUrl, RegisterApplicationRequest requestData)
        {
            // null checks
            if (string.IsNullOrEmpty(taskManagementUrl))
                throw new TaskManagementException(Error.REGISTERTASK_MISSING_URL);
            if (requestData == null)
                throw new TaskManagementException(Error.REGISTERTASK_MISSING_REQUESTDATA);
            if (string.IsNullOrEmpty(requestData.AppId))
                throw new TaskManagementException(Error.REGISTERAPP_MISSING_APPID);

            HttpResponseMessage response = null;
            var responseText = string.Empty;

            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            try
            {
                response = await SendRequest(taskManagementUrl, requestData, APIURL_REGISTERAPP).ConfigureAwait(false);
                responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                    return;
            }
            catch (Exception ex)
            {
                throw new TaskManagementException(Error.REGISTERAPP + GetResponsePhrase(response) + responseText, requestData.AppId, ex);
            }

            // the response did not throw an exception, but was not successful
            throw new TaskManagementException(Error.REGISTERAPP + GetResponsePhrase(response) + responseText, requestData.AppId, null);
        }

        //=========================================================================================== Helper methods

        private static async Task<HttpResponseMessage> SendRequest(string taskManagementUrl, object postData, string apiUrl)
        {            
            using (var client = new HttpClient())
            {
                // set client properties
                client.BaseAddress = new Uri(taskManagementUrl);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // serialize post data
                var content = new StringContent(JsonConvert.SerializeObject(postData), Encoding.UTF8, "application/json");

                // Call the task management REST API
                // ConfigureAwait(false) makes sure that synchronized callers will not encounter a deadlock
                return await client.PostAsync(apiUrl, content).ConfigureAwait(false);
            }
        }

        private static string GetResponsePhrase(HttpResponseMessage response)
        {
            if (response == null)
                return string.Empty;

            return response.ReasonPhrase + " ";
        }

        private static string GetResponseMessage(string responseText)
        {
            // empty or not JSON
            if (string.IsNullOrEmpty(responseText) || !responseText.StartsWith("{"))
                return string.Empty;

            try
            {
                // the response object hopefully contains a message
                dynamic responseObject = JsonConvert.DeserializeObject(responseText);

                return responseObject.message;
            }
            catch
            {
                SnTrace.TaskManagement.Write("Unknown message format when calling the Task Manager REST API: " + responseText);
            }

            return string.Empty;
        }
    }
}

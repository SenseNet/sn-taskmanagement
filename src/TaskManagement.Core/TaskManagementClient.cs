using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SenseNet.Diagnostics;

namespace SenseNet.TaskManagement.Core
{
    public class TaskManagementOptions
    {
        public string Url { get; set; }

        public string ApplicationUrl { get; set; }

        public string ApplicationId { get; set; }
    }

    public interface ITaskManagementClient
    {
        /// <summary>
        /// Registers a task through the task management REST API as an asynchronous operation.
        /// </summary>
        /// <param name="requestData">Contains the necessary information for registering a task.</param>
        /// <returns>Returns a RegisterTaskResult object containing information about the registered task.</returns>
        Task<RegisterTaskResult> RegisterTaskAsync(RegisterTaskRequest requestData);
        /// <summary>
        /// Registers an application through the task management REST API as an asynchronous operation.
        /// </summary>
        /// <param name="requestData">Contains the necessary information for registering an application.</param>
        /// <returns>True if the request was successful.</returns>
        Task RegisterApplicationAsync(RegisterApplicationRequest requestData);
    }

    public class TaskManagementClient : ITaskManagementClient
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly TaskManagementOptions _options;
        private readonly ILogger _logger;

        public TaskManagementClient(IOptions<TaskManagementOptions> options,
            IHttpClientFactory clientFactory,
            ILogger<TaskManagementClient> logger)
        {
            _clientFactory = clientFactory;
            _options = options?.Value ?? new TaskManagementOptions();
            _logger = (ILogger)logger ?? NullLogger.Instance;

            if (string.IsNullOrEmpty(_options.Url))
                _logger.LogWarning("TaskManagement url is not configured.");
        }

        /// <inheritdoc cref="ITaskManagementClient"/>
        public async Task<RegisterTaskResult> RegisterTaskAsync(RegisterTaskRequest requestData)
        {
            // null checks
            if (string.IsNullOrEmpty(_options.Url))
            {
                _logger.LogWarning("TaskManagement url is not configured.");
                return null;
            }

            if (requestData == null)
                throw new TaskManagementException(RepositoryClient.Error.REGISTERTASK_MISSING_REQUESTDATA);
            if (string.IsNullOrEmpty(requestData.AppId) || string.IsNullOrEmpty(requestData.Type))
                throw new TaskManagementException(RepositoryClient.Error.REGISTERTASK_MISSING_APPID_OR_TASK);

            // fill the machine name if not provided
            if (string.IsNullOrEmpty(requestData.MachineName))
                requestData.MachineName = Environment.MachineName;

            HttpResponseMessage response = null;
            var responseText = string.Empty;

            try
            {
                response = await SendRequest(requestData, RepositoryClient.APIURL_REGISTERTASK).ConfigureAwait(false);
                responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonConvert.DeserializeObject<RegisterTaskResult>(responseText);
                    if (!string.IsNullOrEmpty(result?.Error))
                        throw new TaskManagementException(result.Error, requestData.AppId, null);

                    _logger.LogTrace($"Task {requestData.Title} registered successfully.");

                    return result;
                }

                _logger.LogWarning($"Registering task {requestData.Title} failed.");

                // Check if the error states that the appid is unknown. In that case we need to throw
                // an exception so that the client can re-register the app and then the task again.
                var message = GetResponseMessage(responseText);
                if (string.CompareOrdinal(message, RegisterTaskRequest.ERROR_UNKNOWN_APPID) == 0)
                    throw new TaskManagementException(RegisterTaskRequest.ERROR_UNKNOWN_APPID, requestData.AppId, null);
            }
            catch (TaskManagementException)
            {
                // simply throw it further: this is already a well-formed exception
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during registering a task to {_options.Url}.");
                throw new TaskManagementException(RepositoryClient.Error.REGISTERTASK + GetResponsePhrase(response) + responseText,
                    requestData.AppId, taskType: requestData.Type, innerException: ex);
            }

            // the response did not throw an exception, but was not successful
            throw new TaskManagementException(RepositoryClient.Error.REGISTERTASK + GetResponsePhrase(response) + responseText,
                requestData.AppId, taskType: requestData.Type);
        }

        /// <inheritdoc cref="ITaskManagementClient"/>
        public async Task RegisterApplicationAsync(RegisterApplicationRequest requestData)
        {
            // null checks
            if (string.IsNullOrEmpty(_options.Url))
            {
                _logger.LogWarning("TaskManagement url is not configured.");
                return;
            }
            if (requestData == null)
                throw new TaskManagementException(RepositoryClient.Error.REGISTERTASK_MISSING_REQUESTDATA);
            if (string.IsNullOrEmpty(requestData.AppId))
                throw new TaskManagementException(RepositoryClient.Error.REGISTERAPP_MISSING_APPID);

            HttpResponseMessage response = null;
            var responseText = string.Empty;

            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            try
            {
                response = await SendRequest(requestData, RepositoryClient.APIURL_REGISTERAPP).ConfigureAwait(false);
                responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonConvert.DeserializeObject<RegisterApplicationResult>(responseText);
                    if (!string.IsNullOrEmpty(result?.Error))
                        throw new TaskManagementException(result.Error, requestData.AppId, null);

                    return;
                }
            }
            catch (Exception ex)
            {
                throw new TaskManagementException(RepositoryClient.Error.REGISTERAPP + GetResponsePhrase(response) + responseText, requestData.AppId, ex);
            }

            // the response did not throw an exception, but was not successful
            throw new TaskManagementException(RepositoryClient.Error.REGISTERAPP + GetResponsePhrase(response) + responseText, requestData.AppId, null);
        }

        #region Helper methods

        private async Task<HttpResponseMessage> SendRequest(object postData, string apiUrl)
        {
            var client = _clientFactory.CreateClient();

            // set client properties
            client.BaseAddress = new Uri(_options.Url);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // serialize post data
            var content = new StringContent(JsonConvert.SerializeObject(postData), Encoding.UTF8, 
                "application/json");

            // Call the task management REST API
            return await client.PostAsync(apiUrl, content).ConfigureAwait(false);
        }

        private static string GetResponsePhrase(HttpResponseMessage response)
        {
            if (response == null)
                return string.Empty;

            return response.ReasonPhrase + " ";
        }

        private string GetResponseMessage(string responseText)
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
                _logger.LogWarning("Unknown message format when calling the Task Manager REST API: " + responseText);
            }

            return string.Empty;
        }

        #endregion
    }
}

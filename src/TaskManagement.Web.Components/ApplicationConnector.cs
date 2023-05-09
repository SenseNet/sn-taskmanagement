using Newtonsoft.Json;
using SenseNet.Diagnostics;
using SenseNet.TaskManagement.Core;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;

namespace SenseNet.TaskManagement.Web
{
    public class ApplicationConnector
    {
        private readonly ApplicationHandler _applicationHandler;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ISnClientProvider _snClientProvider;
        private readonly ILogger<ApplicationHandler> _logger;

        public ApplicationConnector(ApplicationHandler applicationHandler, IHttpClientFactory httpClientFactory,
            ISnClientProvider snClientProvider, ILogger<ApplicationHandler> logger)
        {
            _applicationHandler = applicationHandler;
            _httpClientFactory = httpClientFactory;
            _snClientProvider = snClientProvider;
            _logger = logger;
        }

        internal async Task SendFinalizeNotificationAsync(SnTaskResult result, CancellationToken cancel)
        {
            if (result?.Task == null || string.IsNullOrEmpty(result.Task.AppId))
                return;

            // load the finalize url from the task or a global app setting
            var app = _applicationHandler.GetApplication(result.Task.AppId);
            var finalizeUrl = result.Task.GetFinalizeUrl(app);

            // cannot do much: no finalize url found for the task
            if (string.IsNullOrEmpty(finalizeUrl))
                return;

            _logger.LogTrace("Sending finalize notification. AppId: {appId}. " +
                             "Agent: {agentName}, Task: {taskId}, Type: {taskType}, " +
                             "task success: {taskSuccessful}",
                result.Task.AppId, result.AgentName, result.Task.Id, result.Task.Type, result.Successful);

            using var client = await GetHttpClient(app, result.Task, cancel).ConfigureAwait(false);

            // create post data
            var content = new StringContent(JsonConvert.SerializeObject(new { result }), Encoding.UTF8,
                "application/json");

            try
            {
                var response = await client.PostAsync(finalizeUrl, content, cancel).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var responseText = await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);

                    _logger.LogWarning("Error during finalize REST API call. Url: {finalizeUrl}, " +
                                       "Status code: {statusCode}. Response: {responseText}",
                        finalizeUrl, response.StatusCode, responseText);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during finalize REST API call. " +
                                     "Application: {appId}, Task: {taskId}, Type: {taskType}",
                    app.AppId, result.Task.Id, result.Task.Type);
            }
        }

        internal async Task<bool> SendPingRequestAsync(SnTask task, CancellationToken cancel)
        {
            var appId = task.AppId;
            var app = !string.IsNullOrEmpty(appId)
                ? _applicationHandler.GetApplication(appId)
                : null;

            // application url not found
            if (app == null || string.IsNullOrEmpty(app.ApplicationUrl))
            {
                SnTrace.TaskManagement.Write("SendPingRequest could not complete: no url found for appid {0}.", appId);
                return false;
            }

            using var client = await GetHttpClient(app, task, cancel).ConfigureAwait(false);

            try
            {
                // Send a simple ping request to the application and 
                // make sure it returns a 200 OK.
                var response = await client.GetAsync(app.ApplicationUrl, cancel).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                SnTrace.TaskManagement.Write("AgentHub SendPingRequest completed successfully for appid {0}.", appId);

                return true;
            }
            catch (Exception ex)
            {
                SnTrace.TaskManagement.Write($"AgentHub SendPingRequest FAILED for appid {appId}. {ex.Message}");
                return false;
            }
        }

        //============================================================================ Helper methods

        private async Task<HttpClient> GetHttpClient(Application app, SnTask task, CancellationToken cancel)
        {
            // repo app request authentication: get auth token for appId and set it in a header
            var client = _httpClientFactory.CreateClient();

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            await _snClientProvider.SetAuthenticationAsync(client, app.ApplicationUrl, cancel);

            //TODO: move this to sn client provider above
            var appAuth = app.GetAuthenticationForTask(task.Type);
            if (appAuth?.ApiKey != null)
            {
                client.DefaultRequestHeaders.Add("apikey", appAuth.ApiKey);
            }

            return client;
        }
    }
}

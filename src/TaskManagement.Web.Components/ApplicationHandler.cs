using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SenseNet.Client.Authentication;
using SenseNet.Diagnostics;
using SenseNet.TaskManagement.Core;
using SenseNet.TaskManagement.Data;
using EventId = SenseNet.Diagnostics.EventId;

// ReSharper disable once CheckNamespace
namespace SenseNet.TaskManagement.Web
{
    public class ApplicationHandler
    {
        private List<Application>? _applications;
        private readonly object _appLock = new();
        
        private IEnumerable<Application> Applications
        {
            get
            {
                if (_applications != null) 
                    return _applications;

                lock (_appLock)
                {
                    if (_applications != null) 
                        return _applications;

                    _applications = _dataHandler.GetApplications().ToList();

                    var message = _applications.Count == 0 
                        ? "No apps found."
                        : "Applications loaded: " + string.Join(" | ", _applications.Select(a => $"{a.AppId} ({a.ApplicationUrl})"));
                            
                    _logger.LogInformation(message);
                }

                return _applications;
            }
        }

        private readonly ITokenStore _tokenStore;
        private readonly TaskDataHandler _dataHandler;
        private readonly ILogger<ApplicationHandler> _logger;

        public ApplicationHandler(ITokenStore tokenStore, TaskDataHandler dataHandler, ILogger<ApplicationHandler> logger)
        {
            _tokenStore = tokenStore;
            _dataHandler = dataHandler;
            _logger = logger;
        }

        /// <summary>
        /// Invalidates the application cache.
        /// </summary>
        public void Reset()
        {
            _applications = null;

            SnTrace.TaskManagement.Write("Application cache reset.");
        }

        public void Initialize()
        {
            Reset();

            // reload apps
            var _ = Applications;
        }
        /// <summary>
        ///  Get one application by app id.
        /// </summary>
        /// <param name="appId">Application id</param>
        public Application GetApplication(string appId)
        {
            if (string.IsNullOrEmpty(appId))
                return null;

            var app = Applications.FirstOrDefault(a => string.Compare(a.AppId, appId, StringComparison.InvariantCulture) == 0);
            if (app == null)
            { 
                // try to reload apps from the db: workaround for load balanced behavior
                Reset();
                app = Applications.FirstOrDefault(a => string.Compare(a.AppId, appId, StringComparison.InvariantCulture) == 0);
            }

            return app;
        }

        //============================================================================ Communication

        internal async Task SendFinalizeNotificationAsync(SnTaskResult result, CancellationToken cancellationToken)
        {
            if (result?.Task == null || string.IsNullOrEmpty(result.Task.AppId))
                return;

            // load the finalize url from the task or a global app setting
            var app = GetApplication(result.Task.AppId);
            var finalizeUrl = result.Task.GetFinalizeUrl(app);

            // cannot do much: no finalize url found for the task
            if (string.IsNullOrEmpty(finalizeUrl))
                return;

            _logger.LogTrace("Sending finalize notification. AppId: {appId}." +
                             "Agent: {agentName}, Task: {taskId}, Type: {taskType}, " +
                             "task success: {taskSuccessful}",
                result.Task.AppId, result.AgentName, result.Task.Id, result.Task.Type, result.Successful);

            using var client = await GetHttpClient(app.ApplicationUrl).ConfigureAwait(false);

            // create post data
            var content = new StringContent(JsonConvert.SerializeObject(new {result}), Encoding.UTF8,
                "application/json");

            try
            {
                var response = await client.PostAsync(finalizeUrl, content, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

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

        internal async Task<bool> SendPingRequestAsync(string appId, CancellationToken cancellationToken)
        {
            var app = !string.IsNullOrEmpty(appId)
                ? GetApplication(appId)
                : null;

            // application url not found
            if (app == null || string.IsNullOrEmpty(app.ApplicationUrl))
            {
                SnTrace.TaskManagement.Write("SendPingRequest could not complete: no url found for appid {0}.", appId);
                return false;
            }

            using var client = await GetHttpClient(app.ApplicationUrl).ConfigureAwait(false);

            try
            {
                // Send a simple ping request to the application and 
                // make sure it returns a 200 OK.
                var response = await client.GetAsync(app.ApplicationUrl, cancellationToken).ConfigureAwait(false);
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

        private async Task<HttpClient> GetHttpClient(string appUrl)
        {
            // repo app request authentication: get auth token for appId and set it in a header
            var client = new HttpClient();

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            //UNDONE: IsTrusted only in dev environment
            var server = new Client.ServerContext
            {
                Url = appUrl,
                IsTrusted = true,
            };
            
            //UNDONE: get app-specific secret from configuration
            var accessToken = await _tokenStore.GetTokenAsync(server, "client", "secret", CancellationToken.None)
                .ConfigureAwait(false);

            // add auth header
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            return client;
        }
    }
}
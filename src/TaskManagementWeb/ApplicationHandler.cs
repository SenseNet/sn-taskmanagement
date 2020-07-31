using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SenseNet.Client.Authentication;
using SenseNet.Diagnostics;
using SenseNet.TaskManagement.Core;
using SenseNet.TaskManagement.Data;

// ReSharper disable once CheckNamespace
namespace SenseNet.TaskManagement.Web
{
    public class ApplicationHandler
    {
        private List<Application> _applications;
        private readonly object _appLock = new object();
        private IEnumerable<Application> Applications
        {
            get
            {
                if (_applications == null)
                {
                    lock (_appLock) 
                    {
                        if (_applications == null)
                        {
                            _applications = _dataHandler.GetApplications().ToList();

                            SnLog.WriteInformation($"Applications reloaded. {Environment.NewLine}{(_applications.Count == 0 ? "No apps found." : string.Join(Environment.NewLine, _applications.Select(a => $"{a.AppId} ({a.ApplicationUrl})")))}",
                                EventId.TaskManagement.Lifecycle);
                        }
                    }
                }

                return _applications;
            }
        }

        private readonly TokenStore _tokenStore;
        private readonly TaskDataHandler _dataHandler;

        public ApplicationHandler(TokenStore tokenStore, TaskDataHandler dataHandler)
        {
            _tokenStore = tokenStore;
            _dataHandler = dataHandler;
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

            SnTrace.TaskManagement.Write($"Sending finalize notification. AppId: {result.Task.AppId}." +
                                         $"Agent: {result.AgentName}, " +
                                         $"Task: {result.Task.Id}, Type: {result.Task.Type}, " +
                                         $"task success: {result.Successful}");

            using var client = await GetHttpClient(app.ApplicationUrl);

            // create post data
            var content = new StringContent(JsonConvert.SerializeObject(new {result}), Encoding.UTF8,
                "application/json");

            try
            {
                var response = await client.PostAsync(finalizeUrl, content, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    SnLog.WriteWarning($"Error during finalize REST API call. Url: {finalizeUrl}, Status code: {response.StatusCode}. Response: {responseText}",
                        EventId.TaskManagement.General);
                }
            }
            catch (Exception ex)
            {
                SnLog.WriteException(ex, "Error during finalize REST API call.", EventId.TaskManagement.General);
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
            var accessToken = await _tokenStore.GetTokenAsync(server, "secret").ConfigureAwait(false);

            // add auth header
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            return client;
        }
    }
}
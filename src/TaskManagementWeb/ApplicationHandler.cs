using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SenseNet.Diagnostics;
using SenseNet.TaskManagement.Core;
using SenseNet.TaskManagement.Core.Configuration;
using SenseNet.TaskManagement.Data;

namespace SenseNet.TaskManagement.Web
{
    public static class ApplicationHandler
    {
        private static List<Application> _applications;
        private static readonly object _appLock = new object();
        private static IEnumerable<Application> Applications
        {
            get
            {
                if (_applications == null)
                {
                    lock (_appLock) 
                    {
                        if (_applications == null)
                        {
                            _applications = TaskDataHandler.Getapplications().ToList();

                            SnLog.WriteInformation($"Applications reloaded. {Environment.NewLine}{(_applications.Count == 0 ? "No apps found." : string.Join(Environment.NewLine, _applications.Select(a => $"{a.AppId} ({a.ApplicationUrl})")))}",
                                EventId.TaskManagement.Lifecycle);
                        }
                    }
                }

                return _applications;
            }
        }

        /// <summary>
        /// Invalidates the application cache.
        /// </summary>
        public static void Reset()
        {
            _applications = null;

            SnTrace.TaskManagement.Write("Application cache reset.");
        }

        public static void Initialize()
        {
            Reset();

            // reload apps
            var apps = Applications;
        }

        /// <summary>
        ///  Get one application by app id.
        /// </summary>
        /// <param name="appId">Aplication id</param>
        public static Application GetApplication(string appId)
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

        internal static async Task SendFinalizeNotificationAsync(SnTaskResult result)
        {
            if (result == null || result.Task == null || string.IsNullOrEmpty(result.Task.AppId))
                return;

            // load the finalize url from the task or a global app setting
            var finalizeUrl = result.Task.GetFinalizeUrl();

            // cannot do much: no finalize url found for the task
            if (string.IsNullOrEmpty(finalizeUrl))
                return;

            SnTrace.TaskManagement.Write($"Sending finalize notification. AppId: {result.Task.AppId}." +
                                         $"Agent: {result.AgentName}, " +
                                         $"Task: {result.Task.Id}, Type: {result.Task.Type}, " +
                                         $"task success: {result.Successful}");

            using (var client = GetHttpClient(result.Task.AppId))
            {
                // create post data
                var content = new StringContent(JsonConvert.SerializeObject(new
                {
                    result = result
                }),
                Encoding.UTF8, "application/json");

                try
                {
                    var response = await client.PostAsync(finalizeUrl, content);

                    if (!response.IsSuccessStatusCode)
                    {
                        var responseText = await response.Content.ReadAsStringAsync();

                        SnLog.WriteWarning($"Error during finalize REST API call. Url: {finalizeUrl}, Status code: {response.StatusCode}. Response: {responseText}",
                            EventId.TaskManagement.General);
                    }
                }
                catch (Exception ex)
                {
                    SnLog.WriteException(ex, "Error during finalize REST API call.", EventId.TaskManagement.General);
                }
            }
        }

        internal static bool SendPingRequest(string appId)
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

            using (var client = GetHttpClient(appId))
            {
                try
                {
                    // Send a simple ping request to the application and 
                    // make sure it returns a 200 OK.
                    var response = client.GetAsync(app.ApplicationUrl).Result;
                    response.EnsureSuccessStatusCode();

                    SnTrace.TaskManagement.Write("AgentHub SendPingRequest completed successfully for appid {0}.", appId);

                    return true;
                }
                catch (Exception ex)
                {
                    SnTrace.TaskManagement.Write("AgentHub SendPingRequest FAILED for appid {0}.", appId);
                    return false;
                }
            }
        }

        //============================================================================ Helper methods

        private static HttpClient GetHttpClient(string appId)
        {
            // get user name and password if configured
            var user = Configuration.GetUserCredentials(appId);

            SnTrace.TaskManagement.Write("AgentHub GetHttpClient user credentials for appid {0}: {1}.", appId, user == null ? "null" : "basic");

            // basic or windows authentication, based on the configured user
            var clientHandler = user == null
                ? new HttpClientHandler { UseDefaultCredentials = true }
                : new HttpClientHandler();

            var client = new HttpClient(clientHandler);

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // add basic auth header if necessary
            if (user != null)
                client.DefaultRequestHeaders.Authorization = GetBasicAuthenticationHeader(user);

            return client;
        }

        private static AuthenticationHeaderValue GetBasicAuthenticationHeader(UserCredentials user)
        {
            if (user == null)
                return new AuthenticationHeaderValue("Basic", string.Empty);

            return new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format(@"{0}:{1}",
                user.UserName,
                user.Password))));
        }
    }
}
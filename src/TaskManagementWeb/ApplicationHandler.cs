using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using IdentityModel.Client;
using Newtonsoft.Json;
using SenseNet.Client;
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

            var app = GetApplication(result.Task.AppId);

            using (var client = await GetHttpClient(app.ApplicationUrl))
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

            //UNDONE: make this method async
            using (var client = GetHttpClient(app.ApplicationUrl).GetAwaiter().GetResult())
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

        private static async Task<HttpClient> GetHttpClient(string appUrl)
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

            var authority = await GetAuthorityUrl(server).ConfigureAwait(false);
            if (string.IsNullOrEmpty(authority))
                throw new SecurityException($"Authority could not be found for repository {appUrl}.");

            var accessToken = await GetTokenAsync(authority).ConfigureAwait(false);

            // add auth header
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

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



        //UNDONE: remove temp token methods and use the official client library when available

        private static async Task<string> GetTokenAsync(string authority)
        {
            var client = new System.Net.Http.HttpClient();
            var disco = await client.GetDiscoveryDocumentAsync(authority).ConfigureAwait(false);
            if (disco.IsError)
            {
                //TODO: log
                return string.Empty;
            }

            // request token
            var tokenResponse = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
            {
                Address = disco.TokenEndpoint,

                //UNDONE: obtain repository-specific secret
                ClientId = "client",
                ClientSecret = "secret",
                Scope = "sensenet"
            });

            if (tokenResponse.IsError)
            {
                //TODO: log
                return string.Empty;
            }

            return tokenResponse.AccessToken;
        }

        private static async Task<string> GetAuthorityUrl(Client.ServerContext server)
        {
            var req = new ODataRequest(server)
            {
                Path = "/Root",
                ActionName = "GetClientRequestParameters"
            };

            //UNDONE: maybe the client type should be configurable
            req.Parameters.Add("clientType", "client");

            try
            {
                dynamic response = await RESTCaller.GetResponseJsonAsync(req, server)
                    .ConfigureAwait(false);

                return response.authority;
            }
            catch (Exception ex)
            {
                SnTrace.System.WriteError($"Could not access repository {server.Url} for getting the authority url. {ex.Message}");
            }

            return string.Empty;
        }
    }
}
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SenseNet.TaskManagement.Core;
using SNaaS.Client.Security;
using ServerContext = SenseNet.Client.ServerContext;

namespace SenseNet.TaskManagement.Web
{
    public class SNaaSClientProvider : ISnClientProvider
    {
        private readonly ISecretStore _secretStore;
        private readonly ILogger<SNaaSClientProvider> _logger;

        public SNaaSClientProvider(ISecretStore secretStore, ILogger<SNaaSClientProvider> logger)
        {
            _secretStore = secretStore;
            _logger = logger;
        }

        public async Task SetAuthenticationAsync(HttpClient client, string appUrl, CancellationToken cancel)
        {
            var server = await GetServerContextAsync(appUrl, cancel).ConfigureAwait(false);
            if (server == null) 
                return;

            // client/secret authentication
            if (!string.IsNullOrEmpty(server.Authentication.AccessToken))
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", server.Authentication.AccessToken);
            }

            // api key authentication
            if (!string.IsNullOrEmpty(server.Authentication.ApiKey))
            {
                client.DefaultRequestHeaders.Add("apikey", server.Authentication.ApiKey);
            }
        }

        public async Task<ServerContext> GetServerContextAsync(string appUrl, CancellationToken cancel)
        {
            var server = await _secretStore.GetServerContextAsync(appUrl, cancel).ConfigureAwait(false);

            if (server == null)
            {
                _logger.LogTrace("Warning: no repository found for app url {appUrl}", appUrl);
            }
            else
            {
                _logger.LogTrace("Server context loaded for app url {appUrl}. " +
                                 "Access token: {accessToken} Api key: {apiKey}", appUrl,
                    string.IsNullOrEmpty(server.Authentication.AccessToken) ? "null" : "[hidden]",
                    string.IsNullOrEmpty(server.Authentication.ApiKey) ? "null" : "[hidden]");
            }

            return server;
        }
    }
}

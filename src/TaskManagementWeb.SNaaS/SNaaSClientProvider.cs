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
            {
                _logger.LogTrace("Warning: no repository configured for app url {appUrl}", appUrl);
                return;
            }

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

        public Task<ServerContext> GetServerContextAsync(string appUrl, CancellationToken cancel)
        {
            return _secretStore.GetServerContextAsync(appUrl, cancel);
        }
    }
}

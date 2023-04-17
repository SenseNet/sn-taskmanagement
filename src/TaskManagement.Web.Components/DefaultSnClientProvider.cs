using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SenseNet.Client.Authentication;
using SenseNet.TaskManagement.Core;

namespace SenseNet.TaskManagement.Web
{
    internal class DefaultSnClientProvider : ISnClientProvider
    {
        private readonly ITokenStore _tokenStore;
        private readonly ILogger<DefaultSnClientProvider> _logger;
        private readonly TaskManagementWebOptions _taskManagementWebOptions;

        public DefaultSnClientProvider(ITokenStore tokenStore, IOptions<TaskManagementWebOptions> taskManagementWebOptions,
            ILogger<DefaultSnClientProvider> logger)
        {
            _tokenStore = tokenStore;
            _logger = logger;
            _taskManagementWebOptions = taskManagementWebOptions.Value;
        }

        public async Task SetAuthenticationAsync(HttpClient client, string appUrl, CancellationToken cancel)
        {
            var server = await GetServerContextAsync(appUrl, cancel).ConfigureAwait(false);

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

        public async Task<Client.ServerContext> GetServerContextAsync(string appUrl, CancellationToken cancel)
        {
            //TODO: IsTrusted only in dev environment
            var server = new Client.ServerContext
            {
                Url = appUrl,
                IsTrusted = true,
            };

            // get app-specific secret from configuration
            var repositoryOptions = _taskManagementWebOptions.Applications.FirstOrDefault(r => r.Url == appUrl);
            if (repositoryOptions == null)
            {
                _logger.LogTrace("Warning: no repository configured for app url {appUrl}", appUrl);
                return server;
            }

            // client/secret authentication
            if (!string.IsNullOrEmpty(repositoryOptions.Authentication.ClientId) &&
                !string.IsNullOrEmpty(repositoryOptions.Authentication.ClientSecret))
            {
                var accessToken = await _tokenStore.GetTokenAsync(server, repositoryOptions.Authentication.ClientId,
                    repositoryOptions.Authentication.ClientSecret, cancel).ConfigureAwait(false);
                
                server.Authentication.AccessToken = accessToken;
            }

            // api key authentication
            if (!string.IsNullOrEmpty(repositoryOptions.Authentication.ApiKey))
            {
                server.Authentication.ApiKey = repositoryOptions.Authentication.ApiKey;
            }

            return server;
        }
    }
}

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SenseNet.TaskManagement.Core
{
    /// <summary>
    /// Defines methods for connecting to a sensenet repository service. Implementations can be created for
    /// different environments - e.g. a standalone or a SNaaS environment, where clients may come from different sources.
    /// </summary>
    public interface ISnClientProvider
    {
        /// <summary>
        /// Sets authentication headers on an existing HttpClient instance.
        /// </summary>
        Task SetAuthenticationAsync(HttpClient client, string appUrl, CancellationToken cancel);
        /// <summary>
        /// Gets an authenticated server context instance that connects to the provided url.
        /// </summary>
        Task<Client.ServerContext> GetServerContextAsync(string appUrl, CancellationToken cancel);
    }
}
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SenseNet.TaskManagement.Core
{
    public interface ISnClientProvider
    {
        Task SetAuthenticationAsync(HttpClient client, string appUrl, CancellationToken cancel);
    }
}

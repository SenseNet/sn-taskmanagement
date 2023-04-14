using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SenseNet.TaskManagement.Data;
using SenseNet.TaskManagement.Hubs;

// ReSharper disable once CheckNamespace
namespace SenseNet.TaskManagement.Web
{
    internal class DeadTaskHostedService : IHostedService, IDisposable
    {
        private static readonly int HandleDeadTaskPeriodInMilliseconds = 60 * 1000;

        private readonly IServiceProvider _services;
        private readonly ILogger<DeadTaskHostedService> _logger;
        private readonly TaskDataHandler _dataHandler;
        private Timer _timer;

        public DeadTaskHostedService(IServiceProvider services, ILogger<DeadTaskHostedService> logger, TaskDataHandler dataHandler)
        {
            _services = services;
            _logger = logger;
            _dataHandler = dataHandler;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Initializing dead task timer.");

            _timer = new Timer(DoWork, null, 5000, HandleDeadTaskPeriodInMilliseconds);

            return Task.CompletedTask;
        }

        private void DoWork(object state)
        {
            using var scope = _services.CreateScope();
            var agentHub = scope.ServiceProvider.GetRequiredService<IHubContext<AgentHub>>();
            var dtc = _dataHandler.GetDeadTaskCount();

            // if there is a dead task in the db, notify agents
            if (dtc > 0)
                agentHub.BroadcastNewTask(null).GetAwaiter().GetResult();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Dead task timer is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}

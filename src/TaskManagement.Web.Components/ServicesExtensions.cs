using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SenseNet.Diagnostics;
using SenseNet.TaskManagement.Core;
using SenseNet.TaskManagement.Data;
using SenseNet.TaskManagement.Web;

namespace SenseNet.Extensions.DependencyInjection
{
    public static class ServicesExtensions
    {
        public static IServiceCollection AddSenseNetTaskManagementWebServices(this IServiceCollection services,
            IConfiguration configuration)
        {
            SnLog.Instance = new SnFileSystemEventLogger();
            SnTrace.SnTracers.Add(new SnFileSystemTracer());
            SnTrace.EnableAll();

            services.Configure<TaskManagementWebOptions>(options => 
                    configuration.GetSection("TaskManagement").Bind(options));

            return services
                .AddSingleton<TaskDataHandler>()
                .AddSenseNetClientTokenStore()
                .AddSingleton<ApplicationHandler>()
                .AddHostedService<DeadTaskHostedService>()
                .AddSingleton<ISnClientProvider, DefaultSnClientProvider>()
                .AddHttpClient();
        }
    }
}

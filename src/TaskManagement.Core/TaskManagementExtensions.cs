using Microsoft.Extensions.DependencyInjection;
using SenseNet.TaskManagement.Core;

namespace SenseNet.Extensions.DependencyInjection
{
    public static class TaskManagementExtensions
    {
        /// <summary>
        /// Registers the provided task management client service that is responsible for communicating
        /// with the task management REST API:
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddTaskManagementClient<T>(this IServiceCollection services) where T: class, ITaskManagementClient
        {
            services.AddHttpClient()
                .Configure<TaskManagementOptions>(options => {})
                .AddSingleton<ITaskManagementClient, T>();

            return services;
        }

        /// <summary>
        /// Registers the default task management client service that is responsible for communicating
        /// with the task management REST API:
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddTaskManagementClient(this IServiceCollection services)
        {
            services.AddTaskManagementClient<TaskManagementClient>();

            return services;
        }
    }
}

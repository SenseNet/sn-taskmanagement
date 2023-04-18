using SenseNet.TaskManagement.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SenseNet.Extensions.DependencyInjection;
using SNaaS.Extensions.DependencyInjection;
using SenseNet.TaskManagement.Core;

namespace SenseNet.TaskManagement.Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();
            services.AddSignalR();
            
            //TODO: inject allowed origins dynamically (do not allow everything)
            services.AddCors(c =>
            {
                c.AddPolicy("AllowAllOrigins", options =>
                {
                    options.AllowAnyOrigin();
                    options.AllowAnyHeader();
                    options.AllowAnyMethod();
                });
            });

            services.AddSenseNetTaskManagementWebServices(Configuration)

                // SNaaS-related services
                .ConfigureSnaasOptions(Configuration)
                .AddSingleton<ISnClientProvider, SNaaSClientProvider>()
                .AddSnaasSecretStore();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ApplicationHandler appHandler)
        {
            //var logger = app.ApplicationServices.GetService<ILogger<Program>>();

            // This will set the global SnLog and SnTrace instances to route log messages to the
            // official .Net Core ILogger API.
            app.ApplicationServices.AddSenseNetILogger();

            // make Web API use the standard ASP.NET error configuration
            //ConfigureErrorHandling();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            //TODO: inject allowed origins dynamically (do not allow everything)
            app.UseCors("AllowAllOrigins");

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "TaskManagementApi",
                    pattern: "api/{controller=Task}/{action}/{id?}");

                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");

                endpoints.MapHub<AgentHub>("/agenthub");
                endpoints.MapHub<TaskMonitorHub>("/monitorhub");
            });

            //UNDONE: add Redis backend if SignalR scale out is enabled
            //if (SenseNet.TaskManagement.Web.Configuration.SignalRSqlEnabled)
            //    GlobalHost.DependencyResolver.UseSqlServer(SenseNet.TaskManagement.Web.Configuration.SignalRDatabaseConnectionString);
            
            //TODO: configure global error logging
            //httpConfiguration.Services.Add(typeof(IExceptionLogger), new WebExceptionLogger());

            // load apps
            appHandler.Initialize();
        }
    }
}
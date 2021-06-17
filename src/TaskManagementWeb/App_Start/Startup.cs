using SenseNet.TaskManagement.Data;
using SenseNet.TaskManagement.Hubs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SenseNet.Diagnostics;
using SenseNet.Extensions.DependencyInjection;
using SNaaS.Extensions.DependencyInjection;
using EventId = SenseNet.Diagnostics.EventId;

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

            SnLog.Instance = new SnFileSystemEventLogger();
            SnTrace.SnTracers.Add(new SnFileSystemTracer());
            SnTrace.EnableAll();

            services.Configure<TaskManagementConfiguration>(Configuration.GetSection("TaskManagement"));
            services.ConfigureSnaasOptions(Configuration);
            
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

            services.AddSingleton<TaskDataHandler>();
            services.AddSnaasSecretStore();
            services.AddSingleton<ApplicationHandler>();
            services.AddHostedService<DeadTaskHostedService>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ApplicationHandler appHandler)
        {
            // This will set the global SnLog and SnTrace instances to route log messages to the
            // official .Net Core ILogger API.
            app.ApplicationServices.AddSenseNetILogger();

            SnLog.WriteInformation("Starting TaskManagement.Web", EventId.TaskManagement.Lifecycle);

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
            
            SnLog.WriteInformation("SenseNet TaskManagement app started.", EventId.TaskManagement.Lifecycle);

            // load apps
            appHandler.Initialize();
        }
    }
}
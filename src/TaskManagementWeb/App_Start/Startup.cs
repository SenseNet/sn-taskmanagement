using System;
using System.Collections.Generic;
using System.Linq;
//using System.Net.Http.Formatting;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Threading;
using SenseNet.TaskManagement.Data;
using SenseNet.TaskManagement.Hubs;
using SenseNet.TaskManagement.Web;
using SenseNet.TaskManagement.Core;
//using System.Web.Configuration;
using System.Configuration;
using Microsoft.AspNetCore.Builder;
//using System.Web.Http.ExceptionHandling;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SenseNet.Diagnostics;
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

            //UNDONE: use config binding
            Web.Configuration.ConnectionString = Configuration.GetConnectionString("TaskDatabase");

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
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
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

            //UNDONE: add SQL server backend if SignalR SQL is enabled
            //if (SenseNet.TaskManagement.Web.Configuration.SignalRSqlEnabled)
            //    GlobalHost.DependencyResolver.UseSqlServer(SenseNet.TaskManagement.Web.Configuration.SignalRDatabaseConnectionString);

            SnLog.WriteInformation(
                $"SignalR SQL backplane is{(Web.Configuration.SignalRSqlEnabled ? string.Empty : " NOT")} enabled.",
                EventId.TaskManagement.Lifecycle);

            //var httpConfiguration = new HttpConfiguration();

            //httpConfiguration.Formatters.Clear();
            //httpConfiguration.Formatters.Add(new JsonMediaTypeFormatter());

            //httpConfiguration.Formatters.JsonFormatter.SerializerSettings =
            //    new JsonSerializerSettings
            //    {
            //        ContractResolver = new CamelCasePropertyNamesContractResolver()
            //    };
            
            //TODO: configure global error logging
            //httpConfiguration.Services.Add(typeof(IExceptionLogger), new WebExceptionLogger());
            
            SnLog.WriteInformation("SenseNet TaskManagement app started.", EventId.TaskManagement.Lifecycle);

            // load apps
            ApplicationHandler.Initialize();
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Formatting;
using System.Web;
using System.Web.Http;
using Microsoft.AspNet.SignalR;
using Microsoft.Owin.Cors;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Owin;
using System.Threading;
using SenseNet.TaskManagement.Data;
using SenseNet.TaskManagement.Hubs;
using SenseNet.TaskManagement.Web;
using SenseNet.TaskManagement.Core;
using System.Web.Configuration;
using System.Configuration;
using System.Web.Http.ExceptionHandling;
using SenseNet.Diagnostics;

[assembly: Microsoft.Owin.OwinStartup(typeof(Startup))]

namespace SenseNet.TaskManagement.Web
{
    public class Startup
    {
        public void Configuration(IAppBuilder appBuilder)
        {
            SnLog.Instance = new SnEventLogger(Web.Configuration.LogName, Web.Configuration.LogSourceName);
            SnLog.WriteInformation("Starting TaskManagement.Web", EventId.TaskManagement.Lifecycle);

            // make Web API use the standard ASP.NET error configuration
            ConfigureErrorHandling();

            // Branch the pipeline here for requests that start with "/signalr"
            appBuilder.Map("/signalr", map =>
            {
                // Setup the CORS middleware to run before SignalR.
                // By default this will allow all origins. You can 
                // configure the set of origins and/or http verbs by
                // providing a cors options with a different policy.
                map.UseCors(CorsOptions.AllowAll);

                var hubConfiguration = new HubConfiguration
                {
                    // You can enable JSONP by uncommenting line below.
                    // JSONP requests are insecure but some older browsers (and some
                    // versions of IE) require JSONP to work cross domain
                    // EnableJSONP = true
                };

                // add SQL server backend if SignalR SQL is enabled
                if (SenseNet.TaskManagement.Web.Configuration.SignalRSqlEnabled)
                    GlobalHost.DependencyResolver.UseSqlServer(SenseNet.TaskManagement.Web.Configuration.SignalRDatabaseConnectionString);

                SnLog.WriteInformation(
                    $"SignalR SQL backplane is{(SenseNet.TaskManagement.Web.Configuration.SignalRSqlEnabled ? string.Empty : " NOT")} enabled.",
                    EventId.TaskManagement.Lifecycle);

                // Run the SignalR pipeline. We're not using MapSignalR
                // since this branch already runs under the "/signalr"
                // path.
                map.RunSignalR(hubConfiguration);
            });

            var httpConfiguration = new HttpConfiguration();

            httpConfiguration.Formatters.Clear();
            httpConfiguration.Formatters.Add(new JsonMediaTypeFormatter());

            httpConfiguration.Formatters.JsonFormatter.SerializerSettings =
                new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                };

            // Web API routes (moved here from WebApiConfig)
            httpConfiguration.MapHttpAttributeRoutes();

            httpConfiguration.Routes.MapHttpRoute(
                name: "TaskManagementRegisterTaskApi",
                routeTemplate: "api/{controller}/{action}/{taskRequest}",
                defaults: new
                {
                    taskRequest = RouteParameter.Optional,
                    controller = "Task",
                    action = "RegisterTask"
                }
            );

            httpConfiguration.Routes.MapHttpRoute(
                name: "TaskManagementRegisterAppApi",
                routeTemplate: "api/{controller}/{action}/{appRequest}",
                defaults: new
                {
                    taskRequest = RouteParameter.Optional,
                    controller = "Task",
                    action = "RegisterApplication"
                }
            );

            httpConfiguration.Routes.MapHttpRoute(
                name: "TaskManagementApi",
                routeTemplate: "api/{controller}/{action}/{id}",
                defaults: new { id = RouteParameter.Optional });

            // configure global error logging
            httpConfiguration.Services.Add(typeof(IExceptionLogger), new WebExceptionLogger());

            appBuilder.UseWebApi(httpConfiguration);

            // initialize dead task timer
            InitializeDeadTaskTimer();

            SnLog.WriteInformation("SenseNet TaskManagement app started.", EventId.TaskManagement.Lifecycle);

            // load apps
            ApplicationHandler.Initialize();
        }

        //===================================================================== Application initialization

        private static int _handleDeadTaskPeriodInMilliseconds = 60 * 1000;
        private static Timer _deadTaskTimer;

        private static void InitializeDeadTaskTimer()
        {
            _deadTaskTimer = new Timer(new TimerCallback(DeadTaskTimerElapsed), null, 
                _handleDeadTaskPeriodInMilliseconds, 
                _handleDeadTaskPeriodInMilliseconds);
        }
        private static void DeadTaskTimerElapsed(object o)
        {
            if (TaskDataHandler.GetDeadTaskCount() > 0)
                AgentHub.BroadcastMessage(null);
        }

        private static void ConfigureErrorHandling()
        {
            // load the ASP.NET setting from the web.config
            var config = (CustomErrorsSection)ConfigurationManager.GetSection("system.web/customErrors");
            if (config == null)
                return;

            IncludeErrorDetailPolicy errorDetailPolicy;

            switch (config.Mode)
            {
                case CustomErrorsMode.RemoteOnly:
                    errorDetailPolicy = IncludeErrorDetailPolicy.LocalOnly;
                    break;
                case CustomErrorsMode.On:
                    errorDetailPolicy = IncludeErrorDetailPolicy.Never;
                    break;
                case CustomErrorsMode.Off:
                    errorDetailPolicy = IncludeErrorDetailPolicy.Always;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // configure Web API according to the ASP.NET configuration
            GlobalConfiguration.Configuration.IncludeErrorDetailPolicy = errorDetailPolicy;
        }
    }
}
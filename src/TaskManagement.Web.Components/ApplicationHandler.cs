using Microsoft.Extensions.Logging;
using SenseNet.Client;
using SenseNet.Diagnostics;
using SenseNet.TaskManagement.Data;

// ReSharper disable once CheckNamespace
namespace SenseNet.TaskManagement.Web
{
    public class ApplicationHandler
    {
        private List<Application>? _applications;
        private readonly object _appLock = new();
        
        private IEnumerable<Application> Applications
        {
            get
            {
                if (_applications != null) 
                    return _applications;

                lock (_appLock)
                {
                    if (_applications != null) 
                        return _applications;

                    _applications = _dataHandler.GetApplications().ToList();

                    var message = _applications.Count == 0 
                        ? "No apps found."
                        : "Applications loaded: " + string.Join(" | ", _applications.Select(a => $"{a.AppId} ({a.ApplicationUrl})"));
                            
                    _logger.LogInformation(message);
                }

                return _applications;
            }
        }

        private readonly TaskDataHandler _dataHandler;
        private readonly ILogger<ApplicationHandler> _logger;

        public ApplicationHandler(TaskDataHandler dataHandler, ILogger<ApplicationHandler> logger)
        {
            _dataHandler = dataHandler;
            _logger = logger;
        }

        /// <summary>
        /// Invalidates the application cache.
        /// </summary>
        public void Reset()
        {
            _applications = null;

            SnTrace.TaskManagement.Write("Application cache reset.");
        }

        public void Initialize()
        {
            Reset();

            // reload apps
            var _ = Applications;
        }
        /// <summary>
        ///  Get one application by app id.
        /// </summary>
        /// <param name="appId">Application id</param>
        public Application GetApplication(string appId)
        {
            if (string.IsNullOrEmpty(appId))
                return null;

            var app = Applications.FirstOrDefault(a => string.Compare(a.AppId, appId, StringComparison.InvariantCulture) == 0);
            if (app == null)
            { 
                // try to reload apps from the db: workaround for load balanced behavior
                Reset();
                app = Applications.FirstOrDefault(a => string.Compare(a.AppId, appId, StringComparison.InvariantCulture) == 0);
            }

            return app;
        }

        public Application GetApplicationByUrl(string appUrl)
        {
            if (string.IsNullOrEmpty(appUrl))
                return null;

            appUrl = appUrl.TrimSchema();

            var app = Applications.FirstOrDefault(a => 
                string.Compare(a.ApplicationUrl.TrimSchema(), appUrl, StringComparison.InvariantCulture) == 0);

            if (app == null)
            {
                // try to reload apps from the db: workaround for load balanced behavior
                Reset();
                app = Applications.FirstOrDefault(a =>
                    string.Compare(a.ApplicationUrl.TrimSchema(), appUrl, StringComparison.InvariantCulture) == 0);
            }

            return app;
        }
    }
}
using SenseNet.TaskManagement.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using SenseNet.Diagnostics;

namespace SenseNet.TaskManagement.Web
{
    public static class Extensions
    {
        public static string GetFinalizeUrl(this SnTask task)
        {
            var app = ApplicationHandler.GetApplication(task.AppId);
            var combinedUrl = CombineUrls(app, task.FinalizeUrl, task.Id);

            // there is no local url: try the global
            if (string.IsNullOrEmpty(combinedUrl) && app != null)
                combinedUrl = CombineUrls(app, app.TaskFinalizeUrl);

            return combinedUrl;
        }

        //============================================================================ Helper methods

        /// <summary>
        /// Helper method for combining two urls. Returns the local url if it is absolute. If it is relative, 
        /// tries to combine it with the global app url. Otherwise (or any of the urls are invalid) returns an empty string.
        /// </summary>
        /// <param name="app">The application.</param>
        /// <param name="localUri">Absolute or relative local url to add to the global one.</param>
        /// <param name="taskId">Id of the task for logging purposes.</param>
        private static string CombineUrls(Application app, string localUri, int taskId = 0)
        {
            if (!string.IsNullOrEmpty(localUri))
            {
                Uri uri;

                // check the given task-specific url
                if (Uri.TryCreate(localUri, UriKind.RelativeOrAbsolute, out uri))
                {
                    if (uri.IsAbsoluteUri)
                    {
                        // use this task-specific absolute url
                        return localUri;
                    }
                    else
                    {
                        // the task-specific url is relative, but there is no global base url
                        if (app == null || string.IsNullOrEmpty(app.ApplicationUrl))
                            return string.Empty;

                        // try to combine the global and the local (relative) urls
                        if (Uri.TryCreate(new Uri(app.ApplicationUrl), localUri, out uri))
                            return uri.ToString();
                        else
                            return string.Empty;
                    }
                }
                else
                {
                    // invalid local url
                    if (taskId > 0)
                        SnLog.WriteWarning(
                            $"Invalid url: {localUri}, AppId: {(app == null ? string.Empty : app.AppId)}, Task: {taskId}", EventId.TaskManagement.General);
                    else
                        SnLog.WriteWarning(
                            $"Invalid url: {localUri}, AppId: {(app == null ? string.Empty : app.AppId)}", EventId.TaskManagement.General);

                    return string.Empty;
                }
            }

            return string.Empty;
        }
    }
}
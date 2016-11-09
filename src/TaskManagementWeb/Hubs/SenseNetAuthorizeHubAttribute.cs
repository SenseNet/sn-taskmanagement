using Microsoft.AspNet.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Web;

namespace SenseNet.TaskManagement.Hubs
{
    /// <summary>
    /// Responsible for authorizing access for hub clients.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    internal class SenseNetAuthorizeHubAttribute : AuthorizeAttribute
    {
        protected override bool UserAuthorized(System.Security.Principal.IPrincipal user)
        {
            // TODO: authentication/authorization

            var princ = user as WindowsPrincipal;
            if (princ == null || princ.Identity == null)
                throw new ArgumentNullException("user");

            if (!princ.Identity.IsAuthenticated)
                return false;

            return false;
        }
    }
}
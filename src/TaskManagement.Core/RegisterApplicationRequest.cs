using System;

namespace SenseNet.TaskManagement.Core
{
    /// <summary>
    /// Holds all the information needed for registering an application.
    /// </summary>
    public class RegisterApplicationRequest
    {
        /// <summary>
        /// The application id to identify the different client app instances.
        /// </summary>
        public string AppId { get; set; }

        /// <summary>
        /// The base url of the client application for callback API calls.
        /// </summary>
        public string ApplicationUrl { get; set; }

        /// <summary>
        /// Relative or absolute url of a global finalizer callback in the client application.
        /// </summary>
        public string TaskFinalizeUrl { get; set; }

        /// <summary>
        /// Relative or absolute url of an authentication callback in the client application (NOT IMPLEMENTED).
        /// </summary>
        public string AuthenticationUrl { get; set; }

        /// <summary>
        /// Relative or absolute url of an authorization callback in the client application (NOT IMPLEMENTED).
        /// </summary>
        public string AuthorizationUrl { get; set; }

        /// <summary>
        /// Authentication options for the client application.
        /// </summary>
        public TaskAuthenticationOptions[] Authentication { get; set; } = Array.Empty<TaskAuthenticationOptions>();
    }
}

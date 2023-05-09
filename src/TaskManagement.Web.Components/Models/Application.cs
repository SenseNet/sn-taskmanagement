using SenseNet.Client.Authentication;
using SenseNet.TaskManagement.Core;

// ReSharper disable once CheckNamespace
namespace SenseNet.TaskManagement.Web
{
    public class Application
    {
        public string AppId { get; set; }
        public string ApplicationUrl { get; set; }
        public string TaskFinalizeUrl { get; set; }
        public string AuthenticationUrl { get; set; }
        public string AuthorizationUrl { get; set; }
        public DateTime RegistrationDate { get; set; }
        public DateTime LastUpdateDate { get; set; }
        public TaskAuthenticationOptions[] Authentication { get; set; } = Array.Empty<TaskAuthenticationOptions>();

        public AuthenticationOptions? GetAuthenticationForTask(string taskType)
        {
            if (!(Authentication?.Any() ?? false))
                return null;

            var appAuth = Authentication.FirstOrDefault(authOptions => authOptions.TaskType == taskType) ??
                          Authentication.FirstOrDefault(authOptions =>
                              string.IsNullOrEmpty(authOptions.TaskType) ||
                              string.Equals(authOptions.TaskType, TaskAuthenticationOptions.DefaultTaskType,
                                  StringComparison.InvariantCultureIgnoreCase));

            return appAuth;
        }
    }
}
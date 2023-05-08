using System;
using SenseNet.Client.Authentication;

namespace SenseNet.TaskManagement.Core
{
    public class TaskAuthenticationOptions : AuthenticationOptions
    {
        public const string DefaultTaskType = "Default";

        public string TaskType { get; set; }
        public DateTime ApiKeyExpiration { get; set; }
    }
}

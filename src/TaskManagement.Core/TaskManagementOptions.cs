using System.Collections.Generic;
using SenseNet.Tools.Configuration;

namespace SenseNet.TaskManagement.Core
{
    public class TaskAuthenticationInfo
    {
        public string User { get; set; }
    }

    [OptionsClass(sectionName: "sensenet:TaskManagement")]
    public class TaskManagementOptions
    {
        public string Url { get; set; }

        public string ApplicationUrl { get; set; }

        public string ApplicationId { get; set; }

        public int ApiKeyExpirationHours { get; set; } = 24;

        public IDictionary<string, TaskAuthenticationInfo> Authentication { get; set; } =
            new Dictionary<string, TaskAuthenticationInfo>();
    }
}

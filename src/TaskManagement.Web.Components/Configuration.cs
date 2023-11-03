using SenseNet.Client;
using SenseNet.Tools.Configuration;

namespace SenseNet.TaskManagement.Web
{
    [OptionsClass(sectionName: "TaskManagement")]
    public class TaskManagementWebOptions
    {
        /// <summary>
        /// After this timeout the task lock will expire so any agent can claim the task.
        /// </summary>
        public int TaskExecutionTimeoutInSeconds { get; set; }

        public RepositoryOptions[] Applications { get; set; } = Array.Empty<RepositoryOptions>();
    }
}
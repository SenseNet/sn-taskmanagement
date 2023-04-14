namespace SenseNet.TaskManagement.Web
{
    public class TaskManagementConfiguration
    {
        /// <summary>
        /// After this timeout the task lock will expire so any agent can claim the task.
        /// </summary>
        public int TaskExecutionTimeoutInSeconds { get; set; }
    }
}
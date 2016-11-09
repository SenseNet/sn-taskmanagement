namespace SenseNet.TaskManagement.Core
{
    /// <summary>
    /// Holds all the information needed for registering a task.
    /// </summary>
    public class RegisterTaskRequest
    {
        /// <summary>
        /// Error id for unknown client applications.
        /// </summary>
        public static readonly string ERROR_UNKNOWN_APPID = "UnknownAppId";

        /// <summary>
        /// The application id to identify the different client app instances.
        /// </summary>
        public string AppId { get; set; }

        /// <summary>
        /// Task type name for identifying the appropriate task executor. Mandatory.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// User friendly title of the task that will be displayed on task monitors.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Determines the order of the execution of tasks. Agents will get immediate tasks first.
        /// </summary>
        public TaskPriority Priority { get; set; }

        /// <summary>
        /// Optional value for categorizing tasks. Task monitor user interface may use this to display only certain task events.
        /// </summary>
        public string Tag { get; set; }

        /// <summary>
        /// Optional finalize callback url of the task. If it is relative, the system will 
        /// try to complete it using the application base url provided during app registration.
        /// </summary>
        public string FinalizeUrl { get; set; }

        /// <summary>
        /// Custom data that will be passed over to the agent and to the tak executor eventually. The recommended format is JSON.
        /// </summary>
        public string TaskData { get; set; }

        /// <summary>
        /// Optional hash code for identifying tasks. If left empty, we will generate a default hash from the task data.
        /// </summary>
        public long Hash { get; set; }

        /// <summary>
        /// Optional machine name of the task origin for logging purposes.
        /// </summary>
        public string MachineName { get; set; }
    }
}

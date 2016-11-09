namespace SenseNet.TaskManagement.Core
{
    /// <summary>
    /// Represents a task execution result.
    /// </summary>
    public class SnTaskResult
    {
        /// <summary>
        /// Agent machine name.
        /// </summary>
        public string MachineName { get; set; }
        /// <summary>
        /// Agent name.
        /// </summary>
        public string AgentName { get; set; }
        /// <summary>
        /// Task details.
        /// </summary>
        public SnTask Task { get; set; }
        /// <summary>
        /// Execution result code.
        /// </summary>
        public int ResultCode { get; set; }
        /// <summary>
        /// Result data.
        /// </summary>
        public string ResultData { get; set; }
        /// <summary>
        /// Task error data.
        /// </summary>
        public SnTaskError Error { get; set; }

        /// <summary>
        /// Gets whether the task execution was successful.
        /// </summary>
        public bool Successful => ResultCode == 0 && Error == null;
    }
}

using System;

namespace SenseNet.TaskManagement.Core
{
    /// <summary>
    /// Represents a task finish event data.
    /// </summary>
    public class TaskFinishedEventArgs : EventArgs
    {
        /// <summary>
        /// Task execution result.
        /// </summary>
        public SnTaskResult TaskResult { get; set; }
    }
}

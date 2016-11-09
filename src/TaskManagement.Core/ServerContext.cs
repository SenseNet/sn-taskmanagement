using System;

namespace SenseNet.TaskManagement.Core
{
    /// <summary>
    /// Task Management deploy type (local or distributed) (NOT USED).
    /// </summary>
    public enum ServerType
    {
        /// <summary>
        /// Local mode.
        /// </summary>
        Local,
        /// <summary>
        /// Distributed mode.
        /// </summary>
        Distributed
    }

    /// <summary>
    /// Describes the context the task management is running.
    /// </summary>
    [Serializable]
    public class ServerContext
    {
        /// <summary>
        /// Task Management deploy type (local or distributed) (NOT USED).
        /// </summary>
        public ServerType ServerType { get; set; }
    }
}

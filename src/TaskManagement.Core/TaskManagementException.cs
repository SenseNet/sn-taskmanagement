using System;
using System.Runtime.Serialization;

namespace SenseNet.TaskManagement.Core
{
    /// <summary>
    /// Represents an error in the Task Management component.
    /// </summary>
    [Serializable]
    public class TaskManagementException : Exception
    {
        private static readonly string PROPNAME_APPID = "AppId";
        private static readonly string PROPNAME_MACHINENAME = "MachineName";
        private static readonly string PROPNAME_TASKID = "TaskId";
        private static readonly string PROPNAME_TASKTYPE = "TaskType";

        /// <summary>
        /// Client application id.
        /// </summary>
        public string AppId { get; }
        /// <summary>
        /// Agent machine name.
        /// </summary>
        public string MachineName { get; }
        /// <summary>
        /// Task identifier.
        /// </summary>
        public int TaskId { get; }
        /// <summary>
        /// Task type name.
        /// </summary>
        public string TaskType { get; }

        //=========================================================== The usual exception constructors

        /// <summary>
        /// Initializes an instance of the TaskManagementException class.
        /// </summary>
        public TaskManagementException() : this("Task Management error.") { }
        /// <summary>
        /// Initializes an instance of the TaskManagementException class.
        /// </summary>
        /// <param name="message">Exception message.</param>
        public TaskManagementException(string message) : base(message) { }
        /// <summary>
        /// Initializes an instance of the TaskManagementException class.
        /// </summary>
        /// <param name="message">Exception message.</param>
        /// <param name="innerException">Inner exception.</param>
        public TaskManagementException(string message, Exception innerException) : base(message, innerException) { }

        //=========================================================== Custom constructors

        /// <summary>
        /// Initializes an instance of the TaskManagementException class.
        /// </summary>
        /// <param name="message">Exception message.</param>
        /// <param name="appId">Client app id.</param>
        /// <param name="innerException">Inner exception.</param>
        public TaskManagementException(string message, string appId, Exception innerException = null)
            : this(message, appId, 0, null, innerException) { }
        /// <summary>
        /// Initializes an instance of the TaskManagementException class.
        /// </summary>
        /// <param name="message">Exception message.</param>
        /// <param name="appId">Client app id.</param>
        /// <param name="taskId">Task id.</param>
        /// <param name="taskType">Task type name.</param>
        /// <param name="innerException">Inner exception.</param>
        public TaskManagementException(string message, string appId, int taskId = 0, string taskType = null, 
            Exception innerException = null) : base(message, innerException)
        {
            AppId = appId;
            MachineName = Environment.MachineName;
            TaskId = taskId;
            TaskType = taskType;
        }
        /// <summary>
        /// Initializes an instance of the TaskManagementException class.
        /// </summary>
        protected TaskManagementException(SerializationInfo info, StreamingContext context) : base(info, context) 
        {
            if (info != null)
            {
                this.AppId = info.GetString(PROPNAME_APPID);
                this.MachineName = info.GetString(PROPNAME_MACHINENAME);
                this.TaskId = info.GetInt32(PROPNAME_TASKID);
                this.TaskType = info.GetString(PROPNAME_TASKTYPE);
            }
        }

        //=========================================================== ISerializable implementation

        /// <summary>
        /// Sets the SerializationInfo with information about the exception.
        /// </summary>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info != null)
            {
                info.AddValue(PROPNAME_APPID, this.AppId);
                info.AddValue(PROPNAME_MACHINENAME, this.MachineName);
                info.AddValue(PROPNAME_TASKID, this.TaskId);
                info.AddValue(PROPNAME_TASKTYPE, this.TaskType);
            }

            base.GetObjectData(info, context);
        }
    }
}

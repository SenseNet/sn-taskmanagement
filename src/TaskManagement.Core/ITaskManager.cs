using System.Threading.Tasks;

namespace SenseNet.TaskManagement.Core
{
    /// <summary>
    /// Describes the interface of a task manager implementation that is able to register
    /// client applications and tasks.
    /// </summary>
    public interface ITaskManager
    {
        /// <summary>
        /// Register a task asynchronously.
        /// </summary>
        /// <param name="taskManagementUrl">Url of the central Task Management web application.</param>
        /// <param name="requestData">Task registration details.</param>
        /// <returns>A result object containing details of the registered task.</returns>
        Task<RegisterTaskResult> RegisterTaskAsync(string taskManagementUrl, RegisterTaskRequest requestData);
        /// <summary>
        /// Registers a new client application in the Task Management database.
        /// </summary>
        /// <param name="taskManagementUrl">Url of the central Task Management web application.</param>
        /// <param name="requestData">Application registration details.</param>
        /// <returns>True if the registration was sucessful.</returns>
        Task<bool> RegisterApplicationAsync(string taskManagementUrl, RegisterApplicationRequest requestData);
        
        /// <summary>
        /// An event handler that is fired whenever a task is finished. 
        /// This must be called by the client application from the task finalizer 
        /// method when that is called by the Task Management web app.
        /// </summary>
        /// <param name="result">Task result received in the finalizer.</param>
        void OnTaskFinished(SnTaskResult result);
    }
}

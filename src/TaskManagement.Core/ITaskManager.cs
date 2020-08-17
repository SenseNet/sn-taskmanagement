using System.Threading;
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
        /// <param name="requestData">Task registration details.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A result object containing details of the registered task.</returns>
        Task<RegisterTaskResult> RegisterTaskAsync(RegisterTaskRequest requestData, CancellationToken cancellationToken);
        /// <summary>
        /// Registers a new client application in the Task Management database.
        /// </summary>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>True if the registration was successful.</returns>
        Task<bool> RegisterApplicationAsync(CancellationToken cancellationToken);

        /// <summary>
        /// An event handler that is fired whenever a task is finished. 
        /// This must be called by the client application from the task finalizer 
        /// method when that is called by the Task Management web app.
        /// </summary>
        /// <param name="result">Task result received in the finalizer.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        Task OnTaskFinishedAsync(SnTaskResult result, CancellationToken cancellationToken);
    }
}

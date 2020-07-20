using Newtonsoft.Json;
using System;

namespace SenseNet.TaskManagement.Core
{
    /// <summary>
    /// Contains progress information about a task. The progress and maximum values are for
    /// letting the UI display progress information to the user easily.
    /// </summary>
    public class Progress
    {
        /// <summary>
        /// Subtask identifier.
        /// </summary>
        [JsonProperty("subtaskId")]
        public Guid? SubtaskId { get; set; }
        /// <summary>
        /// Subtask progress.
        /// </summary>
        [JsonProperty("p")]
        public int? SubtaskProgress { get; set; }
        /// <summary>
        /// Subtask progress maximum.
        /// </summary>
        [JsonProperty("pm")]
        public int? SubtaskProgressMax { get; set; }
        /// <summary>
        /// Overall task progress.
        /// </summary>
        [JsonProperty("op")]
        public int? OverallProgress { get; set; }
        /// <summary>
        /// Overall task progress maximum.
        /// </summary>
        [JsonProperty("opm")]
        public int? OverallProgressMax { get; set; }
        /// <summary>
        /// Subtask details.
        /// </summary>
        [JsonProperty("d")]
        public string Details { get; set; }

        /// <summary>
        /// Serializes this object to JSON.
        /// </summary>
        public override string ToString() => JsonConvert.SerializeObject(this);
    }

    /// <summary>
    /// Represents part of a task that is being executed (e.g. downloading a file or querying necessary content).
    /// Provides progress information to the UI.
    /// </summary>
    public class SnSubtask
    {
        /// <summary>
        /// Subtask identifier.
        /// </summary>
        [JsonProperty("id")]
        public Guid Id { get; set; }
        /// <summary>
        /// Subtask title.
        /// </summary>
        [JsonProperty("t")]
        public string Title { get; set; }
        /// <summary>
        /// Subtask details.
        /// </summary>
        [JsonProperty("d")]
        public string Details { get; set; }

        /// <summary>
        /// Initializes an instance of the SnSubtask class.
        /// </summary>
        public SnSubtask() {}
        /// <summary>
        /// Initializes an instance of the SnSubtask class.
        /// </summary>
        /// <param name="title">Subtask title.</param>
        /// <param name="details">Optional subtask details.</param>
        public SnSubtask(string title, string details = null)
        {
            if (title == null)
                throw new ArgumentNullException(nameof(title));
            if (title.Length == 0)
                throw new ArgumentException("Value of the 'title' cannot be empty.");

            Id = Guid.NewGuid();
            Title = title;
            Details = details;
        }
        /// <summary>
        /// Serializes this object to JSON.
        /// </summary>
        public override string ToString() => JsonConvert.SerializeObject(this);

        /// <summary>
        /// Starts the subtask by sending a message to the agent.
        /// </summary>
        public void Start()
        {
            Console.WriteLine("StartSubtask:" + this);
        }

        /// <summary>
        /// Sends progress information to the agent about this subtask.
        /// </summary>
        /// <param name="subtaskProgress">Subtask progress to aid a progress bar.</param>
        /// <param name="subtaskProgressMax">Subtask progress maximum.</param>
        /// <param name="overallProgress">Overall progress of the whole task.</param>
        /// <param name="overallProgressMax">Overall progress maximum of the whole task.</param>
        /// <param name="details">Subtask details.</param>
        public void Progress(int subtaskProgress, int subtaskProgressMax, int overallProgress, int overallProgressMax, string details = null)
        {
            Console.WriteLine("Progress:" + new Progress
            {
                SubtaskId = Id,
                SubtaskProgress = subtaskProgress,
                SubtaskProgressMax = subtaskProgressMax,
                OverallProgress = overallProgress,
                OverallProgressMax = overallProgressMax,
                Details = details
            });
        }
        /// <summary>
        /// Finalizes the subtask by sending a message to the agent.
        /// </summary>
        /// <param name="details">Subtask details.</param>
        public void Finish(string details = null)
        {
            Details = details;
            Console.WriteLine("FinishSubtask:" + this);
        }
    }
}

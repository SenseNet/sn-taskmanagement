using Newtonsoft.Json;
using System;

namespace SenseNet.TaskManagement.Core
{
    /// <summary>
    /// Contains constants for different task events.
    /// </summary>
    public static class TaskEventType
    { 
        /// <summary>
        /// Registered event.
        /// </summary>
        public static readonly string Registered = "Registered";
        /// <summary>
        /// Updated event.
        /// </summary>
        public static readonly string Updated = "Updated";
        /// <summary>
        /// Started event.
        /// </summary>
        public static readonly string Started = "Started";
        /// <summary>
        /// Done event.
        /// </summary>
        public static readonly string Done = "Done";
        /// <summary>
        /// Failed event.
        /// </summary>
        public static readonly string Failed = "Failed";
        /// <summary>
        /// SubtaskStarted event.
        /// </summary>
        public static readonly string SubtaskStarted = "SubtaskStarted";
        /// <summary>
        /// SubtaskFinished event.
        /// </summary>
        public static readonly string SubtaskFinished = "SubtaskFinished";
        /// <summary>
        /// Progress event.
        /// </summary>
        public static readonly string Progress = "Progress";
        /// <summary>
        /// Idle event.
        /// </summary>
        public static readonly string Idle = "Idle";
    }

    /// <summary>
    /// Represents an event in the task life cycle (e.g.Registered or Updated).
    /// </summary>
    public class SnTaskEvent
    {
        /// <summary>
        /// Event identifier.
        /// </summary>
        public int? Id { get; set; }
        /// <summary>
        /// Event type.
        /// </summary>
        public string EventType { get; set; }
        /// <summary>
        /// Time of occurence.
        /// </summary>
        public DateTime EventTime { get; set; }
        /// <summary>
        /// Event title.
        /// </summary>
        public string Title { get; set; }
        /// <summary>
        /// Event details.
        /// </summary>
        public string Details { get; set; }
        /// <summary>
        /// Client application id.
        /// </summary>
        public string AppId { get; set; }
        /// <summary>
        /// Event tag.
        /// </summary>
        public string Tag { get; set; }
        /// <summary>
        /// Machine name where the task was executed.
        /// </summary>
        public string Machine { get; set; }
        /// <summary>
        /// Agent name.
        /// </summary>
        public string Agent { get; set; }
        /// <summary>
        /// Task identifier.
        /// </summary>
        public int TaskId { get; set; }
        /// <summary>
        /// Subtask identifier.
        /// </summary>
        public Guid? SubtaskId { get; set; }
        /// <summary>
        /// Task type identifier.
        /// </summary>
        public string TaskType { get; set; }
        /// <summary>
        /// Task priority.
        /// </summary>
        public double? TaskOrder { get; set; }
        /// <summary>
        /// Task hash.
        /// </summary>
        public long? TaskHash { get; set; }
        /// <summary>
        /// Task data.
        /// </summary>
        public string TaskData { get; set; }

        /// <summary>
        /// Creates a task event for task registration.
        /// </summary>
        /// <param name="taskId">Task id.</param>
        /// <param name="title">Event title.</param>
        /// <param name="details">Event details.</param>
        /// <param name="appId">Client app id.</param>
        /// <param name="tag">Task tag.</param>
        /// <param name="machine">Agent machine.</param>
        /// <param name="taskType">Task type name.</param>
        /// <param name="taskOrder">Task priority.</param>
        /// <param name="taskHash">Task hash.</param>
        /// <param name="taskData">Task data.</param>
        public static SnTaskEvent CreateRegisteredEvent(int taskId, string title, string details, string appId, string tag, string machine, string taskType, double taskOrder, long taskHash, string taskData)
        {
            return CreateTaskEvent(null, taskId, TaskEventType.Registered, DateTime.Now, title, details, appId, tag, machine, null, null, taskType, taskOrder, taskHash, taskData);
        }
        /// <summary>
        /// Creates a task event for updating a task.
        /// </summary>
        /// <param name="taskId">Task id.</param>
        /// <param name="title">Event title.</param>
        /// <param name="details">Event details.</param>
        /// <param name="appId">Client app id.</param>
        /// <param name="tag">Task tag.</param>
        /// <param name="machine">Agent machine.</param>
        /// <param name="taskType">Task type name.</param>
        /// <param name="taskOrder">Task priority.</param>
        /// <param name="taskHash">Task hash.</param>
        /// <param name="taskData">Task data.</param>
        public static SnTaskEvent CreateUpdatedEvent(int taskId, string title, string details, string appId, string tag, string machine, string taskType, double taskOrder, long taskHash, string taskData)
        {
            return CreateTaskEvent(null, taskId, TaskEventType.Updated, DateTime.Now, title, details, appId, tag, machine, null, null, taskType, taskOrder, taskHash, taskData);
        }

        /// <summary>
        /// Creates a task event for starting a task.
        /// </summary>
        /// <param name="taskId">Task id.</param>
        /// <param name="title">Event title.</param>
        /// <param name="details">Event details.</param>
        /// <param name="appId">Client app id.</param>
        /// <param name="tag">Task tag.</param>
        /// <param name="machine">Agent machine.</param>
        /// <param name="agent">Agent name.</param>
        public static SnTaskEvent CreateStartedEvent(int taskId, string title, string details, string appId, string tag, string machine, string agent)
        {
            return CreateTaskEvent(null, taskId, TaskEventType.Started, DateTime.Now, title, details, appId, tag, machine, agent, null, null, null, null, null);
        }
        /// <summary>
        /// Creates a task event for finishing the task.
        /// </summary>
        /// <param name="taskId">Task id.</param>
        /// <param name="title">Event title.</param>
        /// <param name="details">Event details.</param>
        /// <param name="appId">Client app id.</param>
        /// <param name="tag">Task tag.</param>
        /// <param name="machine">Agent machine.</param>
        /// <param name="agent">Agent name.</param>
        public static SnTaskEvent CreateDoneEvent(int taskId, string title, string details, string appId, string tag, string machine, string agent)
        {
            return CreateTaskEvent(null, taskId, TaskEventType.Done, DateTime.Now, title, details, appId, tag, machine, agent, null, null, null, null, null);
        }
        /// <summary>
        /// Creates a task event for a failed task.
        /// </summary>
        /// <param name="taskId">Task id.</param>
        /// <param name="title">Event title.</param>
        /// <param name="details">Event details.</param>
        /// <param name="appId">Client app id.</param>
        /// <param name="tag">Task tag.</param>
        /// <param name="machine">Agent machine.</param>
        /// <param name="agent">Agent name.</param>
        public static SnTaskEvent CreateFailedEvent(int taskId, string title, string details, string appId, string tag, string machine, string agent)
        {
            return CreateTaskEvent(null, taskId, TaskEventType.Failed, DateTime.Now, title, details, appId, tag, machine, agent, null, null, null, null, null);
        }

        /// <summary>
        /// Creates a task event for starting a subtask.
        /// </summary>
        /// <param name="taskId">Task id.</param>
        /// <param name="title">Event title.</param>
        /// <param name="details">Event details.</param>
        /// <param name="appId">Client app id.</param>
        /// <param name="tag">Task tag.</param>
        /// <param name="machine">Agent machine.</param>
        /// <param name="agent">Agent name.</param>
        /// <param name="subtaskId">Subtask id.</param>
        public static SnTaskEvent CreateSubtaskStartedEvent(int taskId, string title, string details, string appId, string tag, string machine, string agent, Guid subtaskId)
        {
            return CreateTaskEvent(null, taskId, TaskEventType.SubtaskStarted, DateTime.Now, title, details, appId, tag, machine, agent, subtaskId, null, null, null, null);
        }
        /// <summary>
        /// Creates a task event for finishing a subtask.
        /// </summary>
        /// <param name="taskId">Task id.</param>
        /// <param name="title">Event title.</param>
        /// <param name="details">Event details.</param>
        /// <param name="appId">Client app id.</param>
        /// <param name="tag">Task tag.</param>
        /// <param name="machine">Agent machine.</param>
        /// <param name="agent">Agent name.</param>
        /// <param name="subtaskId">Subtask id.</param>
        public static SnTaskEvent CreateSubtaskFinishedEvent(int taskId, string title, string details, string appId, string tag, string machine, string agent, Guid subtaskId)
        {
            return CreateTaskEvent(null, taskId, TaskEventType.SubtaskFinished, DateTime.Now, title, details, appId, tag, machine, agent, subtaskId, null, null, null, null);
        }

        /// <summary>
        /// Creates a custom task event.
        /// </summary>
        /// <param name="id">Event id.</param>
        /// <param name="taskId">Task id.</param>
        /// <param name="eventType">Custom event type.</param>
        /// <param name="eventTime">Event time.</param>
        /// <param name="title">Event title.</param>
        /// <param name="details">Event details.</param>
        /// <param name="appId">Client app id.</param>
        /// <param name="tag">Task tag.</param>
        /// <param name="machine">Agent machine.</param>
        /// <param name="agent">Agent name.</param>
        /// <param name="subtaskId">Subtask id.</param>
        /// <param name="taskType"></param>
        /// <param name="taskOrder"></param>
        /// <param name="taskHash"></param>
        /// <param name="taskData"></param>
        public static SnTaskEvent CreateTaskEvent(int? id, int taskId, string eventType, DateTime eventTime,
            string title, string details, string appId, string tag,
            string machine, string agent, Guid? subtaskId,
            string taskType, double? taskOrder, long? taskHash, string taskData)
                {
                    return new SnTaskEvent
                    {
                        Id = id,
                        EventType = eventType,
                        EventTime = eventTime,
                        Title = title,
                        Details = details,
                        AppId = appId,
                        Tag = tag,
                        Machine = machine,
                        Agent = agent,
                        TaskId = taskId,
                        SubtaskId = subtaskId,
                        TaskType = taskType,
                        TaskOrder = taskOrder,
                        TaskHash = taskHash,
                        TaskData = taskData,
                    };
                }
    }

    /// <summary>
    /// Contains information about the environment on the task agent machine.
    /// </summary>
    public class SnHealthRecord
    {
        /// <summary>
        /// Machine name.
        /// </summary>
        public string Machine { get; set; }
        /// <summary>
        /// Agent name.
        /// </summary>
        public string Agent { get; set; }
        /// <summary>
        /// Event time.
        /// </summary>
        public DateTime EventTime { get; set; }
        /// <summary>
        /// Agent process id.
        /// </summary>
        public int ProcessId { get; set; }
        /// <summary>
        /// CPU performance counter value.
        /// </summary>
        public double CPU { get; set; }
        /// <summary>
        /// RAM performance counter value.
        /// </summary>
        public int RAM { get; set; }
        /// <summary>
        /// Physical memory size on the agent machine.
        /// </summary>
        public ulong TotalRAM { get; set; }
        /// <summary>
        /// Agent process start time.
        /// </summary>
        public DateTime StartTime { get; set; }
        /// <summary>
        /// Event type.
        /// </summary>
        public string EventType { get; set; }

        /// <summary>
        /// Serializes this object to JSON.
        /// </summary>
        public override string ToString() => JsonConvert.SerializeObject(this);
    }

    /// <summary>
    /// Task execution progress record for client applications.
    /// </summary>
    public class SnProgressRecord
    {
        /// <summary>
        /// Progress title.
        /// </summary>
        public string Title { get; set; }
        /// <summary>
        /// Progress details.
        /// </summary>
        public string Details { get; set; }
        /// <summary>
        /// Client application id.
        /// </summary>
        public string AppId { get; set; }
        /// <summary>
        /// Task tag.
        /// </summary>
        public string Tag { get; set; }
        /// <summary>
        /// Task id.
        /// </summary>
        public int TaskId { get; set; }
        /// <summary>
        /// Progress data.
        /// </summary>
        public Progress Progress { get; set; }

        /// <summary>
        /// Serializes this object to JSON.
        /// </summary>
        public override string ToString() => JsonConvert.SerializeObject(this);
    }
}

namespace SenseNet.TaskManagement.Core
{
    /// <summary>
    /// Enum for possible task priority values. Tasks with a higher priority will always be performed first.
    /// </summary>
    public enum TaskPriority
    {
        /// <summary>
        /// System task, will be performed ahead of everything else.
        /// </summary>
        System,
        /// <summary>
        /// To be performed as soon as possible.
        /// </summary>
        Immediately,
        /// <summary>
        /// Important task.
        /// </summary>
        Important,
        /// <summary>
        /// Normal task.
        /// </summary>
        Normal,
        /// <summary>
        /// Can be performed later.
        /// </summary>
        Unimportant
    }
}

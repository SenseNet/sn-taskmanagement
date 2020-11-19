namespace TestExecutor
{
    public static class Scripts
    {
        /// <summary>
        /// Creates a dummy task directly in the task management database.
        /// </summary>
        public const string CreateTask =
@"INSERT INTO [dbo].[Tasks] VALUES ('TestExecutor', 'Test task title', 
1, null, GETUTCDATE(), 'localhost', null, null, null, 0, '')";

        /// <summary>
        /// Creates a dummy task that FAILS directly in the task management database.
        /// </summary>
        public const string CreateTaskFail =
@"INSERT INTO [dbo].[Tasks] VALUES ('TestExecutor', 'Test task title', 
1, null, GETUTCDATE(), 'localhost', null, null, null, 0, '{ FailOnPurpose: true }')";
    }
}

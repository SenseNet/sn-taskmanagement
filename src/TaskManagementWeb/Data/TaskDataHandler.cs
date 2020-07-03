using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Data.SqlClient;
using System.Data;
using System.Diagnostics;
using System.Text;
using SenseNet.Diagnostics;
using SenseNet.TaskManagement.Core;
using SenseNet.TaskManagement.Web;

namespace SenseNet.TaskManagement.Data
{
    public static class TaskDataHandler
    {
        #region SQL SCRIPTS

        private const string REGISTERAPPLICATIONSQL = @"DECLARE @Created bit SET @Created = 0
DECLARE @Updated bit SET @Updated = 0
DECLARE @Id AS INT
DECLARE @RegistrationDate AS datetime2
DECLARE @LastUpdateDate AS datetime2
DECLARE @T TABLE (Id int, RegistrationDate datetime2, LastUpdateDate datetime2)

SELECT @Id = Id FROM [Applications] (NOLOCK) WHERE [AppId] = @AppId
IF @Id IS NULL BEGIN
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE
    BEGIN TRANSACTION
        SELECT @Id = Id FROM [Applications] (NOLOCK) WHERE [AppId] = @AppId
        IF @Id IS NULL BEGIN
            INSERT INTO [Applications] ([AppId], [AppData], [RegistrationDate], [LastUpdateDate])
	            OUTPUT INSERTED.Id, INSERTED.RegistrationDate, INSERTED.LastUpdateDate INTO @T
	            VALUES (@AppId, @AppData, GETUTCDATE(), GETUTCDATE())
            SELECT @Id = Id, @RegistrationDate = RegistrationDate, @LastUpdateDate = LastUpdateDate FROM @T
            SET @Created = 1
        END
    COMMIT TRANSACTION
END

IF @Created = 0 BEGIN
	UPDATE [Applications] SET [AppData] = @AppData, [LastUpdateDate] = GETUTCDATE()
		OUTPUT INSERTED.Id, INSERTED.RegistrationDate, INSERTED.LastUpdateDate INTO @T
		WHERE [AppId] = @AppId
	IF @@ROWCOUNT > 0
		SET @Updated = 1
	SELECT TOP 1 @Id = Id, @RegistrationDate = RegistrationDate, @LastUpdateDate = LastUpdateDate FROM @T
END

SELECT @Id Id, @RegistrationDate RegistrationDate, @LastUpdateDate LastUpdateDate";

        private const string LOADAPPLICATIONSSQL = @"SELECT * FROM [Applications]";

        private const string REGISTERTASKSQL = @"DECLARE @Created bit SET @Created = 0
DECLARE @Updated bit SET @Updated = 0
DECLARE @Id AS INT
DECLARE @T TABLE (Id int)

SELECT @Id = Id FROM Tasks (NOLOCK) WHERE [Hash] = @Hash AND [TaskData] = @TaskData
IF @Id IS NULL BEGIN
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE
    BEGIN TRANSACTION
        SELECT @Id = Id FROM Tasks (NOLOCK) WHERE [Hash] = @Hash AND [TaskData] = @TaskData
        IF @Id IS NULL BEGIN
            INSERT INTO Tasks ([Type],  Title, [Order], Tag, RegisteredAt, AppId, FinalizeUrl, [Hash], TaskData)
	            OUTPUT INSERTED.Id INTO @T
	            VALUES (@Type, @Title, @Order, @Tag, GETUTCDATE(), @AppId, @FinalizeUrl, @Hash, @TaskData)
            SELECT @Id = Id FROM @T
            SET @Created = 1
        END
    COMMIT TRANSACTION
END

IF @Created = 0 BEGIN
	UPDATE Tasks SET [Order] = @Order
		OUTPUT INSERTED.Id INTO @T
		WHERE [Hash] = @Hash AND [TaskData] = @TaskData AND [Order] > @Order
	IF @@ROWCOUNT > 0
		SET @Updated = 1
	SELECT TOP 1 @Id = Id FROM @T
END

SELECT @Id TaskId, @Created Created, @Updated Updated
";

        const string DELETETASKSQL = @"DELETE FROM Tasks WHERE Id = @Id";

        const string REFRESHLOCKSQL = @"UPDATE Tasks SET LastLockUpdate = GETUTCDATE() WHERE Id = @Id";

        static readonly string GETANDLOCKSQL = @"UPDATE T SET LockedBy = @LockedBy, LastLockUpdate = GETUTCDATE()
OUTPUT inserted.*
FROM (SELECT TOP 1 * FROM Tasks
	  WHERE (LockedBy IS NULL OR LastLockUpdate < DATEADD(second, -@ExecutionTimeoutInSeconds, GETUTCDATE())) AND Type IN ('{0}')
	  ORDER BY [Order], RegisteredAt) T
";

        static readonly string GETDEADTASKSSQL = @"SELECT COUNT(1) FROM Tasks WHERE LockedBy IS NULL OR LastLockUpdate < DATEADD(second, -@ExecutionTimeoutInSeconds, GETUTCDATE())";

        const string GETRUNNINGTASKS = @"SELECT * FROM TaskEvents WHERE EventType = 'Registered' AND TaskId IN (
	SELECT TaskId FROM TaskEvents        WHERE EventTime > @TimeLimit AND EventType = 'Registered'
	EXCEPT SELECT TaskId FROM TaskEvents WHERE EventTime > @TimeLimit AND EventType IN ( 'Done', 'Failed' ) )
UNION ALL
SELECT Id, SubTaskId, 'Failed', EventTime, Title, Tag, Details, AppId, Machine, Agent, TaskId, TaskType, TaskOrder, TaskHash, TaskData
	FROM TaskEvents               WHERE EventTime > @TimeLimit AND EventType = 'Registered' AND TaskId IN (
	SELECT TaskId FROM TaskEvents WHERE EventTime > @TimeLimit AND EventType = 'Failed' )
";
        const string GETRUNNINGTASKS_BYAPPID = @"SELECT * FROM TaskEvents WHERE EventType = 'Registered' AND TaskId IN (
	SELECT TaskId FROM TaskEvents        WHERE EventTime > @TimeLimit AND AppId = @AppId AND EventType = 'Registered'
	EXCEPT SELECT TaskId FROM TaskEvents WHERE EventTime > @TimeLimit AND AppId = @AppId AND EventType IN ( 'Done', 'Failed' ) )
UNION ALL
SELECT Id, SubTaskId, 'Failed', EventTime, Title, Tag, Details, AppId, Machine, Agent, TaskId, TaskType, TaskOrder, TaskHash, TaskData
	FROM TaskEvents               WHERE EventTime > @TimeLimit AND AppId = @AppId AND EventType = 'Registered' AND TaskId IN (
	SELECT TaskId FROM TaskEvents WHERE EventTime > @TimeLimit AND AppId = @AppId AND EventType = 'Failed' )
";
        const string GETRUNNINGTASKS_BYTAG = @"SELECT * FROM TaskEvents WHERE EventType = 'Registered' AND TaskId IN (
	SELECT TaskId FROM TaskEvents        WHERE EventTime > @TimeLimit AND Tag = @Tag AND EventType = 'Registered'
	EXCEPT SELECT TaskId FROM TaskEvents WHERE EventTime > @TimeLimit AND Tag = @Tag AND EventType IN ( 'Done', 'Failed' ) )
UNION ALL
SELECT Id, SubTaskId, 'Failed', EventTime, Title, Tag, Details, AppId, Machine, Agent, TaskId, TaskType, TaskOrder, TaskHash, TaskData
	FROM TaskEvents               WHERE EventTime > @TimeLimit AND Tag = @Tag AND EventType = 'Registered' AND TaskId IN (
	SELECT TaskId FROM TaskEvents WHERE EventTime > @TimeLimit AND Tag = @Tag AND EventType = 'Failed' )
";
        const string GETRUNNINGTASKS_BYAPPIDANDTAG = @"SELECT * FROM TaskEvents WHERE EventType = 'Registered' AND TaskId IN (
	SELECT TaskId FROM TaskEvents        WHERE EventTime > @TimeLimit AND AppId = @AppId AND Tag = @Tag AND EventType = 'Registered'
	EXCEPT SELECT TaskId FROM TaskEvents WHERE EventTime > @TimeLimit AND AppId = @AppId AND Tag = @Tag AND EventType IN ( 'Done', 'Failed' ) )
UNION ALL
SELECT Id, SubTaskId, 'Failed', EventTime, Title, Tag, Details, AppId, Machine, Agent, TaskId, TaskType, TaskOrder, TaskHash, TaskData
	FROM TaskEvents               WHERE EventTime > @TimeLimit AND AppId = @AppId AND Tag = @Tag AND EventType = 'Registered' AND TaskId IN (
	SELECT TaskId FROM TaskEvents WHERE EventTime > @TimeLimit AND AppId = @AppId AND Tag = @Tag AND EventType = 'Failed' )
";

        const string GETEVENTSBYTASKID = "SELECT * FROM TaskEvents WHERE TaskId = @TaskId";
        const string GETEVENTSBYTASKID_ANDAPPID = "SELECT * FROM TaskEvents WHERE TaskId = @TaskId AND AppId = @AppId";
        const string GETEVENTSBYTASKID_ANDTAG = "SELECT * FROM TaskEvents WHERE TaskId = @TaskId AND Tag = @Tag";
        const string GETEVENTSBYTASKID_ANDAPPIDANDTAG = "SELECT * FROM TaskEvents WHERE TaskId = @TaskId AND AppId = @AppId AND Tag = @Tag";

        #endregion

        //================================================================================= Manage tasks

        public static SnTaskEvent[] GetUnfinishedTasks(string appId, string tag)
        {
            SnTrace.TaskManagement.Write("TaskDataHandler GetUnfinishedTasks: appId: " + (appId ?? "") + ", tag: " + (tag ?? ""));

            try
            {
                using (var cn = new SqlConnection(Web.Configuration.ConnectionString))
                using (var cm = cn.CreateCommand())
                {
                    cm.CommandType = CommandType.Text;

                    cm.CommandText = (appId == null)
                        ? (tag == null ? GETRUNNINGTASKS : GETRUNNINGTASKS_BYTAG)
                        : (tag == null ? GETRUNNINGTASKS_BYAPPID : GETRUNNINGTASKS_BYAPPIDANDTAG);

                    cm.Parameters.Add("@TimeLimit", SqlDbType.DateTime).Value = DateTime.UtcNow.AddDays(-1);
                    if (appId != null)
                        cm.Parameters.Add("@AppId", SqlDbType.NVarChar, 50).Value = appId;
                    if (tag != null)
                        cm.Parameters.Add("@Tag", SqlDbType.NVarChar, 450).Value = tag;

                    cn.Open();
                    var reader = cm.ExecuteReader();
                    var result = new List<SnTaskEvent>();

                    while (reader.Read())
                        result.Add(GetTaskEventFromReader(reader));

                    return result.ToArray();
                }
            }
            catch (Exception ex)
            {
                throw new TaskManagementException("Error during getting unfinished tasks.", ex);
            }
        }
        public static SnTaskEvent[] GetDetailedTaskEvents(string appId, string tag, int taskId)
        {
            SnTrace.TaskManagement.Write("TaskDataHandler GetDetailedTaskEvents: appId: " + (appId ?? "") + ", tag: " + (tag ?? "") + ", taskId: " + taskId);

            try
            {
                using (var cn = new SqlConnection(Web.Configuration.ConnectionString))
                using (var cm = cn.CreateCommand())
                {
                    cm.CommandType = CommandType.Text;

                    cm.CommandText = (appId == null)
                        ? (tag == null ? GETEVENTSBYTASKID : GETEVENTSBYTASKID_ANDTAG)
                        : (tag == null ? GETEVENTSBYTASKID_ANDAPPID : GETEVENTSBYTASKID_ANDAPPIDANDTAG);

                    cm.Parameters.Add("@TaskId", SqlDbType.Int).Value = taskId;
                    if (appId != null)
                        cm.Parameters.Add("@AppId", SqlDbType.NVarChar, 50).Value = appId;
                    if (tag != null)
                        cm.Parameters.Add("@Tag", SqlDbType.NVarChar, 450).Value = tag;

                    cn.Open();
                    var reader = cm.ExecuteReader();
                    var result = new List<SnTaskEvent>();

                    while (reader.Read())
                        result.Add(GetTaskEventFromReader(reader));

                    return result.ToArray();
                }
            }
            catch (Exception ex)
            {
                throw new TaskManagementException("Error during getting task events.", ex);
            }
        }
        private static SnTaskEvent GetTaskEventFromReader(SqlDataReader reader)
        {
            return new SnTaskEvent
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                SubtaskId = GetSafeGuid(reader, reader.GetOrdinal("SubtaskId")),
                EventType = reader.GetString(reader.GetOrdinal("EventType")),
                EventTime = reader.GetDateTime(reader.GetOrdinal("EventTime")),
                Title = reader.GetString(reader.GetOrdinal("Title")),
                AppId = GetSafeString(reader, reader.GetOrdinal("AppId")),
                Tag = GetSafeString(reader, reader.GetOrdinal("Tag")),
                Details = GetSafeString(reader, reader.GetOrdinal("Details")),
                Machine = GetSafeString(reader, reader.GetOrdinal("Machine")),
                Agent = GetSafeString(reader, reader.GetOrdinal("Agent")),
                TaskId = GetSafeInt(reader, reader.GetOrdinal("TaskId")) ?? 0,
                TaskType = GetSafeString(reader, reader.GetOrdinal("TaskType")),
                TaskOrder = GetSafeDouble(reader, reader.GetOrdinal("TaskOrder")),
                TaskHash = GetSafeLong(reader, reader.GetOrdinal("TaskHash")),
                TaskData = GetSafeString(reader, reader.GetOrdinal("TaskData")),
            };
        }

        public static RegisterTaskResult RegisterTask(string type, string title, TaskPriority priority, string appId, string tag, string finalizeUrl, long hash, string taskDataSerialized, string machineName)
        {
            double order = double.NaN;
            switch (priority)
            {
                case TaskPriority.System: order = 0.0; break;
                case TaskPriority.Immediately: order = 1.0; break;
                case TaskPriority.Important: order = 10.0; break;
                case TaskPriority.Normal: order = 100.0; break;
                case TaskPriority.Unimportant: order = 1000.0; break;
                default:
                    throw new TaskManagementException("Unknown TaskPriority: " + priority, appId, taskType: type);
            }

            RegisterTaskResult result;

            try
            {
                result = RegisterTask(type, title, order, appId, tag, finalizeUrl, hash, taskDataSerialized, machineName);
            }
            catch (TaskManagementException)
            {
                // bubble up
                throw;
            }
            catch (Exception ex)
            {
                // wrap the exception
                throw new TaskManagementException("Task registration error.", appId, taskType: type, innerException: ex);
            }

            if (result != null)
            {
                SnTrace.TaskManagement.Write("Task registered. Id:{0}, AppId:{1}, Type:{2}, New:{3}, Updated:{4}",
                    result.Task.Id, result.Task.AppId, result.Task.Type, result.NewlyCreated, result.Updated);
            }
            else
            {
                //TODO: task registration was not successful. A retry logic should be implemented here.
            }

            return result;
        }
        private static RegisterTaskResult RegisterTask(string type, string title, double order, string appId, string tag, string finalizeUrl, long hash, string taskDataSerialized, string machineName)
        {
            using (var cn = new SqlConnection(Web.Configuration.ConnectionString))
            {
                cn.Open();
                var tran = cn.BeginTransaction();
                var cm1 = cn.CreateCommand();
                SqlCommand cm2 = null;
                cm1.CommandText = REGISTERTASKSQL;
                cm1.Connection = cn;
                cm1.Transaction = tran;
                cm1.CommandType = CommandType.Text;

                try
                {
                    cm1.Parameters.Add("@Type", SqlDbType.NVarChar).Value = type;
                    cm1.Parameters.Add("@Title", SqlDbType.NVarChar).Value = title;
                    cm1.Parameters.Add("@Order", SqlDbType.Float).Value = order;
                    cm1.Parameters.Add("@Tag", SqlDbType.NVarChar).Value = tag;
                    cm1.Parameters.Add("@AppId", SqlDbType.NVarChar).Value = (object)appId ?? DBNull.Value;
                    cm1.Parameters.Add("@FinalizeUrl", SqlDbType.NVarChar).Value = finalizeUrl;
                    cm1.Parameters.Add("@Hash", SqlDbType.BigInt).Value = hash;
                    cm1.Parameters.Add("@TaskData", SqlDbType.NVarChar).Value = taskDataSerialized;

                    var result = new RegisterTaskResult();
                    var id = 0;
                    using (var reader = cm1.ExecuteReader())
                    {
                        // there must be only one row
                        reader.Read();
                        var o = reader[0];

                        // task registration was not successful
                        if (o == DBNull.Value)
                            return null;

                        id = Convert.ToInt32(o);
                        result.NewlyCreated = reader.GetBoolean(1);
                        result.Updated = reader.GetBoolean(2);
                    }

                    result.Task = new SnTask
                    {
                        Id = id,
                        Title = title,
                        Order = order,
                        Tag = tag,
                        TaskData = taskDataSerialized,
                        RegisteredAt = DateTime.UtcNow,
                        Hash = hash,
                        Type = type,
                        AppId = appId,
                        FinalizeUrl = finalizeUrl
                    };

                    if (result.NewlyCreated || result.Updated)
                    {
                        cm2 = cn.CreateCommand();
                        cm2.Transaction = tran;
                        WriteRegisterTaskEvent(result, machineName, cm2);
                    }

                    tran.Commit();
                    return result;
                }
                catch
                {
                    tran.Rollback();
                    throw;
                }
                finally
                {
                    cm1.Dispose();
                    if (cm2 != null)
                        cm2.Dispose();
                }
            }
        }

        public static void FinalizeTask(SnTaskResult taskResult)
        {
            SnTrace.TaskManagement.Write("TaskDataHandler FinalizeTask: " + (taskResult.Successful ? "Done" : "Error") + ", Id: " + taskResult.Task.Id);

            using (var cn = new SqlConnection(Web.Configuration.ConnectionString))
            using (var cm = new SqlCommand(DELETETASKSQL, cn) { CommandType = System.Data.CommandType.Text })
            {
                cm.Parameters.Add("@Id", SqlDbType.Int).Value = taskResult.Task.Id;
                cn.Open();
                cm.ExecuteNonQuery();
            }
            WriteFinishExecutionEvent(taskResult);
        }

        public static void RefreshLock(int taskId)
        {
            using (var cn = new SqlConnection(Web.Configuration.ConnectionString))
            using (var cm = new SqlCommand(REFRESHLOCKSQL, cn) { CommandType = System.Data.CommandType.Text })
            {
                cm.Parameters.Add("@Id", SqlDbType.Int).Value = taskId;

                cn.Open();
                cm.ExecuteNonQuery();
            }
        }
        public static SnTask GetNextAndLock(string machineName, string agentName, string[] capabilities)
        {
            var sql = String.Format(GETANDLOCKSQL, String.Join("', '", capabilities));
            using (var cn = new SqlConnection(Web.Configuration.ConnectionString))
            using (var cm = new SqlCommand(sql, cn) { CommandType = System.Data.CommandType.Text })
            {
                cm.Parameters.Add("@LockedBy", SqlDbType.NVarChar, 450).Value = agentName;
                cm.Parameters.AddWithValue("@ExecutionTimeoutInSeconds", Web.Configuration.TaskExecutionTimeoutInSeconds);

                cn.Open();
                var reader = cm.ExecuteReader();
                while (reader.Read())
                {
                    var task = new SnTask
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("Id")),
                        Type = reader.GetString(reader.GetOrdinal("Type")),
                        Title = reader.GetString(reader.GetOrdinal("Title")),
                        Order = reader.GetDouble(reader.GetOrdinal("Order")),
                        Tag = GetSafeString(reader, reader.GetOrdinal("Tag")),
                        RegisteredAt = reader.GetDateTime(reader.GetOrdinal("RegisteredAt")),
                        AppId = GetSafeString(reader, reader.GetOrdinal("AppId")),
                        FinalizeUrl = GetSafeString(reader, reader.GetOrdinal("FinalizeUrl")),
                        LastLockUpdate = GetSafeDateTime(reader, reader.GetOrdinal("LastLockUpdate")),
                        LockedBy = GetSafeString(reader, reader.GetOrdinal("LockedBy")),
                        Hash = reader.GetInt64(reader.GetOrdinal("Hash")),
                        TaskData = GetSafeString(reader, reader.GetOrdinal("TaskData")),
                    };
                    WriteStartExecutionEvent(task, machineName, agentName);
                    return task;
                }
                return null;
            }
        }
        public static int GetDeadTaskCount()
        {
            using (var cn = new SqlConnection(Web.Configuration.ConnectionString))
            using (var cm = new SqlCommand(GETDEADTASKSSQL, cn) { CommandType = System.Data.CommandType.Text })
            {
                cm.Parameters.AddWithValue("@ExecutionTimeoutInSeconds", Web.Configuration.TaskExecutionTimeoutInSeconds);
                cn.Open();
                var result = (int)cm.ExecuteScalar();
                SnTrace.TaskManagement.Write("TaskDataHandler GetDeadTasks. Count: " + result);
                return result;
            }
        }

        public static void StartSubtask(string machineName, string agentName, SnSubtask subtask, SnTask task)
        {
            WriteStartSubtaskEvent(subtask, task, machineName, agentName);
        }
        public static void FinishSubtask(string machineName, string agentName, SnSubtask subtask, SnTask task)
        {
            WriteFinishSubtaskEvent(subtask, task, machineName, agentName);
        }

        //================================================================================= Manage applications

        public static Application RegisterApplication(RegisterApplicationRequest request)
        {
            using (var cn = new SqlConnection(Web.Configuration.ConnectionString))
            {
                SqlCommand cm1 = null;

                try
                {
                    cn.Open();

                    cm1 = cn.CreateCommand();
                    cm1.CommandText = REGISTERAPPLICATIONSQL;
                    cm1.Connection = cn;
                    cm1.CommandType = CommandType.Text;

                    var appData = JsonConvert.SerializeObject(new
                    {
                        ApplicationUrl = request.ApplicationUrl,
                        TaskFinalizeUrl = request.TaskFinalizeUrl,
                        AuthenticationUrl = request.AuthenticationUrl,
                        AuthorizationUrl = request.AuthorizationUrl
                    });

                    var resultApp = new Application
                    {
                        AppId = request.AppId,
                        ApplicationUrl = request.ApplicationUrl,
                        AuthenticationUrl = request.AuthenticationUrl,
                        AuthorizationUrl = request.AuthorizationUrl
                    };

                    cm1.Parameters.Add("@AppId", SqlDbType.NVarChar).Value = request.AppId;
                    cm1.Parameters.Add("@AppData", SqlDbType.NVarChar).Value = appData;

                    using (var reader = cm1.ExecuteReader())
                    {
                        // there must be only one row
                        reader.Read();

                        resultApp.RegistrationDate = reader.GetDateTime(reader.GetOrdinal("RegistrationDate"));
                        resultApp.LastUpdateDate = reader.GetDateTime(reader.GetOrdinal("LastUpdateDate"));
                    }

                    return resultApp;
                }
                catch (Exception ex)
                {
                    SnLog.WriteException(ex, "Error during app registration.", EventId.TaskManagement.General);

                    throw new TaskManagementException("Error during application registration.", request.AppId, ex);
                }
                finally
                {
                    if (cm1 != null)
                        cm1.Dispose();
                }
            }
        }

        public static Application[] Getapplications()
        {
            using var cn = new SqlConnection(Web.Configuration.ConnectionString);
            using var cm = cn.CreateCommand();

            cm.CommandType = CommandType.Text;
            cm.CommandText = LOADAPPLICATIONSSQL;

            cn.Open();

            var reader = cm.ExecuteReader();
            var result = new List<Application>();

            while (reader.Read())
                result.Add(GetApplicationFromReader(reader));

            return result.ToArray();
        }

        private static Application GetApplicationFromReader(SqlDataReader reader)
        {
            var app = new Application
            {
                AppId = GetSafeString(reader, reader.GetOrdinal("AppId")),
                RegistrationDate = reader.GetDateTime(reader.GetOrdinal("RegistrationDate")),
                LastUpdateDate = reader.GetDateTime(reader.GetOrdinal("LastUpdateDate"))
            };

            var appData = GetSafeString(reader, reader.GetOrdinal("AppData"));

            if (!string.IsNullOrEmpty(appData))
            {
                dynamic appDataObj = JsonConvert.DeserializeObject(appData);

                app.ApplicationUrl = appDataObj.ApplicationUrl;
                app.AuthenticationUrl = appDataObj.AuthenticationUrl;
                app.AuthorizationUrl = appDataObj.AuthorizationUrl;
                app.TaskFinalizeUrl = appDataObj.TaskFinalizeUrl;
            }

            return app;
        }

        //================================================================================= Write events

        private static void WriteRegisterTaskEvent(RegisterTaskResult result, string machineName, SqlCommand command)
        {
            var task = result.Task;
            WriteEvent(command, result.NewlyCreated ? TaskEventType.Registered : TaskEventType.Updated, task.Id,
                null, task.Title, null,
                task.AppId, machineName, null,
                task.Tag, task.Type, task.Order, task.Hash, task.TaskData);
        }
        private static void WriteStartExecutionEvent(SnTask task, string machineName, string agentName)
        {
            var message = "Agent " + task.LockedBy + " started a task execution.";
            WriteEvent(null, TaskEventType.Started, task.Id, null, task.Title, null, task.AppId,
                 machineName, agentName, task.Tag,
                 null, null, null, null);
        }
        private static void WriteFinishExecutionEvent(SnTaskResult taskResult)
        {
            var task = taskResult.Task;
            WriteEvent(null,
                taskResult.Successful ? TaskEventType.Done : TaskEventType.Failed,
                task.Id,
                null,
                task.Title,
                taskResult.Error == null ? (string)null : taskResult.Error.ToString(),
                task.AppId,
                taskResult.MachineName,
                taskResult.AgentName,
                task.Tag,
                null, null, null, null);
        }
        private static void WriteStartSubtaskEvent(SnSubtask subtask, SnTask task, string machine, string agent)
        {
            WriteEvent(null, TaskEventType.SubtaskStarted, task.Id, subtask.Id, subtask.Title, subtask.Details, task.AppId, machine, agent, task.Tag
                , null, null, null, null);
        }
        private static void WriteFinishSubtaskEvent(SnSubtask subtask, SnTask task, string machine, string agent)
        {
            WriteEvent(null, TaskEventType.SubtaskFinished, task.Id, subtask.Id, subtask.Title, subtask.Details, task.AppId, machine, agent, task.Tag
                , null, null, null, null);
        }

        private static void WriteEvent(SqlCommand cm, string eventType, int taskId, Guid? subtaskId, string title, string details,
            string appId, string machine, string agent, string tag,
            string taskType, double? taskOrder, long? taskHash, string taskData)
        {
            SqlConnection cn = null;
            if (cm == null)
            {
                cn = new SqlConnection(Web.Configuration.ConnectionString);
                cm = cn.CreateCommand();
                cm.Connection = cn;
                cn.Open();
            }

            try
            {
                cm.CommandType = CommandType.Text;

                cm.CommandText = @"INSERT INTO TaskEvents ( EventType,  EventTime,  SubtaskId,  Title,  Details,  AppId,  Machine,  Agent,  Tag,  TaskId,  TaskType,  TaskOrder,   TaskHash,  TaskData ) VALUES
                                                          (@EventType, GETUTCDATE(), @SubtaskId, @Title, @Details, @AppId, @Machine, @Agent, @Tag, @TaskId, @TaskType, @TaskOrder,  @TaskHash, @TaskData )";

                cm.Parameters.Add("@EventType", SqlDbType.NVarChar).Value = eventType.ToString();
                cm.Parameters.Add("@SubTaskId", SqlDbType.UniqueIdentifier).Value = subtaskId.HasValue ? (object)subtaskId.Value : DBNull.Value;
                cm.Parameters.Add("@Title", SqlDbType.NVarChar).Value = title;
                cm.Parameters.Add("@Details", SqlDbType.NVarChar).Value = (object)details ?? DBNull.Value;
                cm.Parameters.Add("@AppId", SqlDbType.NVarChar).Value = (object)appId ?? DBNull.Value;
                cm.Parameters.Add("@Machine", SqlDbType.NVarChar).Value = (object)machine ?? DBNull.Value;
                cm.Parameters.Add("@Agent", SqlDbType.NVarChar).Value = (object)agent ?? DBNull.Value;
                cm.Parameters.Add("@Tag", SqlDbType.NVarChar).Value = (object)tag ?? DBNull.Value;
                cm.Parameters.Add("@TaskId", SqlDbType.Int).Value = taskId;

                cm.Parameters.Add("@TaskType", SqlDbType.NVarChar).Value = (object)taskType ?? DBNull.Value;
                cm.Parameters.Add("@TaskOrder", SqlDbType.Float).Value = taskOrder.HasValue ? (object)taskOrder.Value : DBNull.Value;
                cm.Parameters.Add("@TaskHash", SqlDbType.BigInt).Value = taskHash.HasValue ? (object)taskHash.Value : DBNull.Value;
                cm.Parameters.Add("@TaskData", SqlDbType.NVarChar).Value = (object)taskData ?? DBNull.Value;

                cm.ExecuteNonQuery();
            }
            finally
            {
                if (cn != null)
                {
                    cn.Close();
                    cm.Dispose();
                    cn.Dispose();
                }
            }

        }

        //================================================================================= Helper methods

        internal static string Serialize(object data)
        {
            if (data != null)
                return null;

            var serializer = new JsonSerializer();
            serializer.Converters.Add(new JavaScriptDateTimeConverter());
            serializer.NullValueHandling = NullValueHandling.Ignore;

            using (var sw = new StringWriter())
            {
                using (JsonWriter writer = new JsonTextWriter(sw))
                    serializer.Serialize(writer, data);
                return sw.GetStringBuilder().ToString();
            }
        }

        private static string GetSafeString(SqlDataReader reader, int index)
        {
            if (reader.IsDBNull(index))
                return null;
            return reader.GetString(index);
        }
        private static DateTime? GetSafeDateTime(SqlDataReader reader, int index)
        {
            if (reader.IsDBNull(index))
                return null;
            return reader.GetDateTime(index);
        }
        private static int? GetSafeInt(SqlDataReader reader, int index)
        {
            if (reader.IsDBNull(index))
                return null;
            return reader.GetInt32(index);
        }
        private static long? GetSafeLong(SqlDataReader reader, int index)
        {
            if (reader.IsDBNull(index))
                return null;
            return reader.GetInt64(index);
        }
        private static double? GetSafeDouble(SqlDataReader reader, int index)
        {
            if (reader.IsDBNull(index))
                return null;
            return reader.GetDouble(index);
        }
        private static Guid? GetSafeGuid(SqlDataReader reader, int index)
        {
            if (reader.IsDBNull(index))
                return null;
            return reader.GetGuid(index);
        }

    }
}
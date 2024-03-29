﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Timers;
using SenseNet.Diagnostics;
using SenseNet.TaskManagement.Core;

namespace SenseNet.TaskManagement.TaskAgent
{
    internal interface IExecutor
    {
        SnTask Task { get; }
        int Execute(SnTask task);
        void Terminate();
    }

    internal class TestExecutor : IExecutor, IDisposable
    {
        private readonly Timer _timer = new Timer(1000.0);

        public SnTask Task { get; private set; }

        public TestExecutor()
        {
            _timer.Elapsed += Timer_Elapsed;
        }

        public void Dispose()
        {
            _timer.Dispose();
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Console.Write("#");
        }

        public int Execute(SnTask task)
        {
            Task = task;

            var t = 10000 + new Random().Next(5000);
            Console.Write("t: " + t + " ");

            _timer.Start();
            System.Threading.Thread.Sleep(t);
            _timer.Stop();

            Console.WriteLine(".");
            return 0;
        }

        public void Terminate()
        {
        }
    }

    internal class OutProcExecutor : IExecutor, IDisposable
    {
        private readonly AgentConfiguration _config;
        private Process _process;
        public SnTask Task { get; private set; }

        public OutProcExecutor(AgentConfiguration config)
        {
            _config = config;
        }

        public int Execute(SnTask task)
        {
            Task = task;
            Agent.ExecutionStart(this);
            try
            {
                return ExecuteInner(task);
            }
            finally
            {
                Agent.ExecutionEnd();
            }
        }
        private int ExecuteInner(SnTask task)
        {
            var workerExe = GetWorkerExePath(task);
            if (!AgentTools.ExecutorExists(workerExe))
                throw new TaskManagementException("Task executor command was not found", task.AppId, task.Id, task.Type);

            var app = _config.Applications.FirstOrDefault(a =>
                string.Equals(a.AppId, task.AppId, StringComparison.OrdinalIgnoreCase));

            if (app == null)
            {
                SnTrace.TaskManagement.Write($"Application was NOT found in configuration for app id {task.AppId} " +
                                             $"and task {task.Id}");
            }
            else
            {
                SnTrace.TaskManagement.Write($"Application was found in configuration for app id {task.AppId} " +
                                             $"and task {task.Id}");
            }

            var userValue = app?.ClientId ?? (task.Authentication?.ClientId ?? string.Empty);
            var passwordValue = app?.Secret ?? task.Authentication?.ClientSecret;
            var apiKeyValue = app?.ApiKey ?? task.Authentication?.ApiKey;

            SnTrace.TaskManagement.Write($"Task executor parameters: task type: {task.Type} User: {userValue} " +
                                         $"Password: {(string.IsNullOrEmpty(passwordValue) ? "null" : passwordValue[..3] )} " +
                                         $"ApiKey: {(string.IsNullOrEmpty(apiKeyValue) ? "null" : apiKeyValue[..3])}");

            var userParameter = "USERNAME:\"" + userValue + "\"";
            var passwordParameter = "PASSWORD:\"" + passwordValue + "\"";
            var dataParameter = "DATA:\"" + EscapeArgument(task.TaskData) + "\"";
            var apiKeyParameter = "APIKEY:\"" + apiKeyValue + "\"";

            //if RuntimeInformation.IsOSPlatform(OSPlatform.Windows)

            var startInfo = new ProcessStartInfo(workerExe)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(workerExe),
                CreateNoWindow = true,
                ErrorDialog = false,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            startInfo.ArgumentList.Add(userParameter);
            startInfo.ArgumentList.Add(passwordParameter);
            startInfo.ArgumentList.Add(dataParameter);
            startInfo.ArgumentList.Add(apiKeyParameter);

            _process = new Process
            {
                EnableRaisingEvents = true, 
                StartInfo = startInfo
            };
            _process.OutputDataReceived += Process_OutputDataReceived;

            SnLog.WriteInformation(string.Format(
                "Task#{1} execution STARTED on agent {0}:\r\n    id: {1},\r\n    type: {2},\r\n    " +
                "hash: {3},\r\n    order: {4},\r\n    registered: {5},\r\n    key: {6},\r\n    data: {7},\r\n    " +
                "appid: {8},\r\n    ClientId: {9}"
                , Agent.AgentName, task.Id, task.Type, task.Hash, task.Order, task.RegisteredAt, task.TaskKey,
                task.TaskData, app?.AppId, app?.ClientId), 
                EventId.TaskManagement.Lifecycle);

            _process.Start();
            _process.BeginOutputReadLine();
            _process.WaitForExit();

            _process.OutputDataReceived -= Process_OutputDataReceived;
            var result = _process.ExitCode;

            if (result != 0)
                SnLog.WriteWarning($"Task#{task.Id} execution TERMINATED with error. Result:{result}, task type: {task.Type}, agent: {Agent.AgentName}",
                    EventId.TaskManagement.General);
            else
                SnLog.WriteInformation($"Task#{task.Id} execution FINISHED: type: {task.Type}, agent: {Agent.AgentName}",
                    EventId.TaskManagement.Lifecycle);

            _process.Dispose();
            _process = null;
            return result;
        }

        public void Terminate()
        {
            if (_process != null && !_process.HasExited)
                _process.Kill();
        }

        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Agent.OutputDataReceived(sender, e);
        }

        private string GetWorkerExePath(SnTask task)
        {
            return Agent.TaskExecutors.TryGetValue(task.Type, out var exePath) ? exePath : null;
        }

        private static string EscapeArgument(string arg)
        {
            return arg.Replace("\"", "\"\"");
        }

        #region IDisposable Pattern
        private bool _disposed;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        ~OutProcExecutor()
        {
            Dispose(false);
        }
        private void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!this._disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources here.
                    if (_process != null && !_process.HasExited)
                    {
                        _process.Kill();
                        _process.Dispose();
                    }
                }
                // Clean up unmanaged resources here.
            }
            _disposed = true;
        }
        #endregion
    }
}

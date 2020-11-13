using System;
using System.Net;
using System.Threading;
using Newtonsoft.Json;
using SenseNet.Client;
using SenseNet.TaskManagement.Core;

namespace TestExecutor
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = ParseParameters(args);

            Console.WriteLine("Test executor is running...");
            Thread.Sleep(5000);

            var subtask = new SnSubtask("Thinking hard", "Pretending to do something.");
            subtask.Start();

            // throw exception if that was the wish of the caller
            if (config.FailOnPurpose)
            {
                WriteError();
                return;
            }

            var count = new Random().Next(10, 15);
            for (var i = 0; i < count; i++)
            {
                Thread.Sleep(1000);
                subtask.Progress(i, count, i, count,
                    $"Still pretending: {i}...");
            }

            subtask.Finish("Cannot pretend anymore.");
        }

        private static void WriteError()
        {
            var ex1 = new ClientException("watiz", HttpStatusCode.Conflict, new InvalidOperationException("This is not allowed."));
            var te = SnTaskError.Create(ex1, new { Prop1 = "pppp1" });
            //var s = te.ToString();

            Console.WriteLine("ERROR:" + te);
        }

        private static ExecutorConfig ParseParameters(string[] args)
        {
            foreach (var arg in args)
            {
                if (arg.StartsWith("USERNAME:", StringComparison.OrdinalIgnoreCase))
                {
                    //Username = GetParameterValue(arg);
                }
                else if (arg.StartsWith("PASSWORD:", StringComparison.OrdinalIgnoreCase))
                {
                    //Password = GetParameterValue(arg);
                }
                else if (arg.StartsWith("DATA:", StringComparison.OrdinalIgnoreCase))
                {
                    var data = GetParameterValue(arg).Replace("\"\"", "\"");
                    if (string.IsNullOrWhiteSpace(data))
                        return new ExecutorConfig();

                    return JsonConvert.DeserializeObject<ExecutorConfig>(data);
                }
            }

            return new ExecutorConfig();
        }

        private static string GetParameterValue(string arg)
        {
            return arg.Substring(arg.IndexOf(":", StringComparison.Ordinal) + 1).TrimStart('\'', '"').TrimEnd('\'', '"');
        }
    }
}

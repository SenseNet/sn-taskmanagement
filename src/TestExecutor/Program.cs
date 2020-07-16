using System;
using System.Threading;
using SenseNet.TaskManagement.Core;

namespace TestExecutor
{
    class Program
    {
        static void Main(string[] args)
        {
            var subtask = new SnSubtask("Thinking hard", "Pretending to do something.");
            subtask.Start();

            var count = new Random().Next(10, 15);
            for (var i = 0; i < count; i++)
            {
                Thread.Sleep(1000);
                subtask.Progress(i, count, i, count,
                    $"Still pretending: {i}...");
            }

            subtask.Finish("Cannot pretend anymore.");
        }
    }
}

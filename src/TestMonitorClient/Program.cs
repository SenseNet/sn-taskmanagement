using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using SenseNet.TaskManagement.Core;

namespace TestMonitorClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var connection = new HubConnectionBuilder()
                .WithUrl("https://localhost:5001/monitorhub?appid=SenseNet")
                .Build();

            connection.Closed += async error =>
            {
                await Task.Delay(new Random().Next(0, 5) * 1000);
                await connection.StartAsync();
            };
            connection.On<SnTaskEvent>("onTaskEvent", taskEvent =>
            {
                Console.WriteLine($"Agent: {taskEvent.Agent}, " +
                                  $"Event: {taskEvent.EventType}, Title: {taskEvent.Title}");
            });
            connection.On<string, SnHealthRecord>("heartbeat", (agentName, healthRecord) =>
            {
                Console.WriteLine($"HEARTBEAT Agent: {agentName}, RAM: {healthRecord.RAM}");
            });
            connection.On<SnProgressRecord>("writeProgress", progressRecord =>
            {
                Console.WriteLine($"PROGRESS {progressRecord.Progress.SubtaskProgress}, " +
                                  $"Details: {progressRecord.Progress.Details}");
            });

            try
            {
                Console.WriteLine("Connecting to server...");

                await connection.StartAsync();

                Console.WriteLine("Monitor started.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.ReadLine();
        }
    }
}

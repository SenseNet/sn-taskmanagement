using System;
using Microsoft.AspNetCore.SignalR.Client;
using SenseNet.Diagnostics;

namespace SenseNetTaskAgent
{
    /// <summary>
    /// Tells SignalR to infinitely retry reconnecting to the server.
    /// </summary>
    internal class InfiniteRetryPolicy : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            SnTrace.TaskManagement.Write($"Connection retry attempt failed. Elapsed time: {retryContext.ElapsedTime}");

            // retry infinitely
            return TimeSpan.FromSeconds(10);
        }
    }
}

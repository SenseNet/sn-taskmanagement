using System.IO;

namespace SenseNet.TaskManagement.TaskAgent
{
    internal static class AgentTools
    {
        public static bool ExecutorExists(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            // Accept both Linux and Windows-style executors:
            // with or without the exe extension.
            return File.Exists(path) || File.Exists(path + ".exe");
        }
    }
}

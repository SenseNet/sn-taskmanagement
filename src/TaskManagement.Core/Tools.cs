using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SenseNet.TaskManagement.Core
{
    public class Tools
    {
        private static readonly Regex ExecutorExeNameRegex = new Regex("([.][vV][0-9]+([.][1-9]+){0,3})$");

        public static string GetExecutorExeName(string taskType)
        {
            // anything.v11.222.333.444
            var match = ExecutorExeNameRegex.Match(taskType);
            if (match.Length > 0)
                return taskType.Substring(0, taskType.Length - match.Length);

            return taskType;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SenseNet.TaskManagement.TaskAgent
{
    internal class Tools
    {
        private static Regex _executorExeNameRegex = new Regex("([.][vV][0-9]+([.][1-9]+){0,3})$");

        public static string GetExecutorExeName(string taskType)
        {
            // anything.v11.222.333.444
            var match = _executorExeNameRegex.Match(taskType);
            if (match.Length > 0)
                return taskType.Substring(0, taskType.Length - match.Length);

            return taskType;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http.ExceptionHandling;
using SenseNet.Diagnostics;

namespace SenseNet.TaskManagement.Web
{
    public class WebExceptionLogger : ExceptionLogger
    {
        public override void Log(ExceptionLoggerContext context)
        {
            SnLog.WriteException(context.ExceptionContext.Exception, context.ExceptionContext.Exception.Message, 1);
        }
    }
}
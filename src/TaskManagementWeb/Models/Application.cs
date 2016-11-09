using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SenseNet.TaskManagement.Web
{
    public class Application
    {
        public string AppId { get; set; }
        public string ApplicationUrl { get; set; }
        public string TaskFinalizeUrl { get; set; }
        public string AuthenticationUrl { get; set; }
        public string AuthorizationUrl { get; set; }
        public DateTime RegistrationDate { get; set; }
        public DateTime LastUpdateDate { get; set; }
    }
}
using System.Configuration;

namespace SenseNet.TaskManagement.Core.Configuration
{
    /// <summary>
    /// Represents a user/password pair for accessing the client application.
    /// </summary>
    public class UserCredentials
    {
        /// <summary>
        /// Username for accessing the client application.
        /// </summary>
        public string UserName { get; set; }
        /// <summary>
        /// Password for the user.
        /// </summary>
        public string Password { get; set; }
    }

    /// <summary>
    /// Represents a client application configuration element.
    /// </summary>
    public class AppAuthElement : ConfigurationElement
    {
        private const string ATTRIBUTE_USERNAME = "userName";
        private const string ATTRIBUTE_PASSWORD = "password";
        private const string ATTRIBUTE_APPID = "appId";

        /// <summary>
        /// Gets the app id.
        /// </summary>
        [ConfigurationProperty(ATTRIBUTE_APPID, DefaultValue = "", IsRequired = true)]
        public string AppId
        {
            get
            {
                return (string)this[ATTRIBUTE_APPID];
            }
            set
            {
                this[ATTRIBUTE_APPID] = value;
            }
        }

        /// <summary>
        /// Username for the client application.
        /// </summary>
        [ConfigurationProperty(ATTRIBUTE_USERNAME, DefaultValue = "", IsRequired = false)]
        public string UserName
        {
            get
            {
                return (string)this[ATTRIBUTE_USERNAME];
            }
            set
            {
                this[ATTRIBUTE_USERNAME] = value;
            }
        }

        /// <summary>
        /// Password for the user.
        /// </summary>
        [ConfigurationProperty(ATTRIBUTE_PASSWORD, DefaultValue = "", IsRequired = false)]
        public string Password
        {
            get
            {
                return (string)this[ATTRIBUTE_PASSWORD];
            }
            set
            {
                this[ATTRIBUTE_PASSWORD] = value;
            }
        }
    }
}

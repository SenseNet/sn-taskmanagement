using System.Configuration;

namespace SenseNet.TaskManagement.Core.Configuration
{
    /// <summary>
    /// Defines a configuration section for holding user credentials to connect to different client applications.
    /// </summary>
    public class AppAuthSection : ConfigurationSection
    {
        //UNDONE: refactor or remove old auth config section

        /// <summary>
        /// Configuration section xpath.
        /// </summary>
        public static readonly string SECTIONFULLNAME = "taskManagement/appAuth";

        private const string ATTRIBUTE_USERNAME = "userName";
        private const string ATTRIBUTE_PASSWORD = "password";

        /// <summary>
        /// Gets a global (fallback) username for client applications that have no user configured.
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
        /// Password for the global user.
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

        /// <summary>
        /// 
        /// </summary>
        [ConfigurationProperty("", IsDefaultCollection = true, IsKey = false, IsRequired = true)]
        public AppAuthCollection AppAuthEntries
        {
            get
            {
                return base[""] as AppAuthCollection;
            }
            set
            {
                base[""] = value;
            }
        }

        //========================================================================== Configuration API

        /// <summary>
        /// Gets a user name and password configured for the provided application or 
        /// (if it is not configured) a global one.
        /// </summary>
        /// <param name="appId">An application id to get credentials for.</param>
        /// <returns>A username/password pair.</returns>
        public static UserCredentials GetUserCredentials(string appId)
        {
            var config = ConfigurationManager.GetSection(SECTIONFULLNAME) as AppAuthSection;
            if (config != null)
            {
                if (!string.IsNullOrEmpty(appId))
                {
                    var aae = config.AppAuthEntries[appId];
                    if (!string.IsNullOrEmpty(aae?.UserName))
                    {
                        // user is configured for a specific app
                        return new UserCredentials
                        {
                            UserName = aae.UserName,
                            Password = aae.Password
                        };
                    }
                }

                // user is configured globally for all apps
                if (!string.IsNullOrEmpty(config.UserName))
                {
                    return new UserCredentials
                    {
                        UserName = config.UserName,
                        Password = config.Password
                    };
                }
            }

            // no configured user found
            return null;
        }
    }
}

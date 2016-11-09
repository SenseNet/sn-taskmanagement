using System.Configuration;

namespace SenseNet.TaskManagement.Core.Configuration
{
    /// <summary>
    /// Represents a collection of configured client applications that connect to this Task Management application.
    /// </summary>
    public class AppAuthCollection : ConfigurationElementCollection
    {
        private const string ELEMENTNAME = "add";

        /// <summary>
        /// Creates a new AppAuthCollection element.
        /// </summary>
        /// <returns>A newly created AppAuthCollection element.</returns>
        protected override ConfigurationElement CreateNewElement()
        {
            return new AppAuthElement();
        }
        /// <summary>
        /// Gets the element key for a specified AppAuth element.
        /// </summary>
        /// <param name="element">The System.Configuration.ConfigurationElement to return the key for.</param>
        /// <returns>AppId of the application represented by the config element.</returns>
        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((AppAuthElement)element).AppId;
        }
        /// <summary>
        /// Gets the name used to identify child configuration elements in the collection.
        /// </summary>
        protected override string ElementName => ELEMENTNAME;
        /// <summary>
        /// Indicates whether the specified element exists in this collection.
        /// </summary>
        /// <param name="elementName">The name of the element to verify.</param>
        /// <returns>true if the element name is the child element name (add).</returns>
        protected override bool IsElementName(string elementName)
        {
            return !string.IsNullOrEmpty(elementName) && elementName == ELEMENTNAME;
        }
        /// <summary>
        /// Gets the type of the configuration collection: BasicMap.
        /// </summary>
        public override ConfigurationElementCollectionType CollectionType => ConfigurationElementCollectionType.BasicMap;

        /// <summary>
        /// Gets the client app config element at the specified index location.
        /// </summary>
        /// <param name="index">Index of the application element.</param>
        /// <returns>The application at the specified index.</returns>
        public AppAuthElement this[int index] => BaseGet(index) as AppAuthElement;
        /// <summary>
        /// Returns the client app config element with the specified key.
        /// </summary>
        /// <param name="key">The key of the app element to return.</param>
        /// <returns>The app element with the specified key; otherwise null.</returns>
        public new AppAuthElement this[string key] => BaseGet(key) as AppAuthElement;
    }
}

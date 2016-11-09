using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SenseNet.TaskManagement.Core
{
    /// <summary>
    /// Represents an error that occured during task execution.
    /// </summary>
    public class SnTaskError
    {
        /// <summary>
        /// Error code.
        /// </summary>
        public string ErrorCode { get; set; }
        /// <summary>
        /// Exception type name or a custom error type.
        /// </summary>
        public string ErrorType { get; set; }
        /// <summary>
        /// Error message.
        /// </summary>
        public string Message { get; set; }
        /// <summary>
        /// Error details.
        /// </summary>
        public string Details { get; set; }
        /// <summary>
        /// Custom error data serialized in JSON format.
        /// </summary>
        public string CallingContext { get; set; }

        /// <summary>
        /// Creates a task execution error object based on an exception and a custom context data.
        /// </summary>
        /// <param name="e">Exception thrown when trying to execute a task.</param>
        /// <param name="callingContext">Custom object that will be serialized to the error object.</param>
        public static SnTaskError Create(Exception e, object callingContext = null)
        {
            return new SnTaskError
            {
                ErrorCode = null,
                ErrorType = e.GetType().Name,
                Message = e.Message,
                Details = SerializeException(e),
                CallingContext = callingContext == null ? null : JsonConvert.SerializeObject(callingContext)
            };
        }
        /// <summary>
        /// Creates a task execution error object.
        /// </summary>
        /// <param name="errorCode">Error code.</param>
        /// <param name="errorType">Error type (e.g. exception type name).</param>
        /// <param name="message">Error message.</param>
        /// <param name="details">Error details.</param>
        public static SnTaskError Create(string errorCode, string errorType, string message, string details)
        {
            return new SnTaskError
            {
                ErrorCode = errorCode,
                ErrorType = errorType,
                Message = message,
                Details = details
            };
        }
        /// <summary>
        /// Deserializes an error sent by the executor in JSON format.
        /// </summary>
        /// <param name="src">Error text in JSON.</param>
        /// <returns>An SnTaskError object.</returns>
        public static SnTaskError Parse(string src)
        {
            var result = new SnTaskError();
            try
            {
                var jErr = JObject.Parse(src);
                foreach (var prop in jErr.Properties())
                {
                    var val = prop.Value.ToString();
                    switch (prop.Name)
                    {
                        case "ErrorCode": result.ErrorCode = val; break;
                        case "Message": result.Message = val; break;
                        case "ErrorType": result.ErrorType = val; break;
                        case "Details": result.Details = val; break;
                        case "CallingContext": result.CallingContext = val; break;
                    }
                }
            }
            catch //compensation
            {
                result.ErrorCode = "unknown";
                result.Message = "An error occured during error parsing. The Details property contains the raw error data of the tast executor.";
                result.ErrorType = "unknown";
                result.Details = src;
            }
            return result;
        }

        /// <summary>
        /// Serializes this object to JSON.
        /// </summary>
        public override string ToString()
        {
            var writer = new StringWriter();
            JsonSerializer.Create(new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })
                .Serialize(writer, this);
            return writer.GetStringBuilder().ToString();
        }

        private static string SerializeException(Exception ex)
        {
            if (ex == null)
                return string.Empty;

            try
            {
                var writer = new StringWriter();
                JsonSerializer.Create(new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })
                    .Serialize(writer, ex);
                return writer.GetStringBuilder().ToString();
            }
            catch
            {
                // Most likely the exception is not serializable.
                return ex.ToString();
            }
        }
    }
}

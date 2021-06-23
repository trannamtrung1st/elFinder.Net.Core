using elFinder.Net.Core.Models.Response;
using System;
using System.Net;
using System.Runtime.Serialization;

namespace elFinder.Net.Core.Exceptions
{
    public abstract class ConnectorException : Exception
    {
        protected ConnectorException()
        {
        }

        protected ConnectorException(string message) : base(message)
        {
        }

        protected ConnectorException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        protected ConnectorException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public virtual ErrorResponse ErrorResponse { get; protected set; }

        public virtual HttpStatusCode? StatusCode { get; }
    }
}

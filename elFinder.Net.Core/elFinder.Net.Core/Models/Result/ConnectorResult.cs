using elFinder.Net.Core.Models.Response;
using System.Collections.Generic;
using System.Net;

namespace elFinder.Net.Core.Models.Result
{
    public class ConnectorResult
    {
        public ConnectorResult()
        {
        }

        public virtual object Response { get; set; }
        public virtual IDictionary<string, string> Cookies { get; set; }
        public virtual string ContentType { get; set; }
        public virtual HttpStatusCode StatusCode { get; set; }
        public virtual ResultType ResultType { get; set; }

        public static ConnectorResult Success(object response)
        {
            return new ConnectorResult()
            {
                Response = response,
                ContentType = ContentTypeNames.Application.Json,
                StatusCode = HttpStatusCode.OK,
                ResultType = ResultType.Success
            };
        }

        public static ConnectorResult File(FileResponse fileResponse)
        {
            return new ConnectorResult()
            {
                Response = fileResponse,
                ContentType = fileResponse.ContentType,
                StatusCode = HttpStatusCode.OK,
                ResultType = ResultType.File
            };
        }

        public static ConnectorResult Error(object response, HttpStatusCode statusCode = HttpStatusCode.InternalServerError)
        {
            return new ConnectorResult()
            {
                Response = response,
                ContentType = ContentTypeNames.Application.Json,
                StatusCode = statusCode,
                ResultType = ResultType.Error
            };
        }

        public static ConnectorResult NoContent(object response)
        {
            return new ConnectorResult()
            {
                Response = response,
                StatusCode = HttpStatusCode.NoContent,
                ResultType = ResultType.Success
            };
        }

        #region Constants
        public const string Cookie_Elfdl = "elfdl";
        #endregion
    }
}

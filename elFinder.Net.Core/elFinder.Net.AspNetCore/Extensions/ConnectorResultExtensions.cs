using elFinder.Net.AspNetCore.Results;
using elFinder.Net.Core;
using elFinder.Net.Core.Models.Response;
using elFinder.Net.Core.Models.Result;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;

namespace elFinder.Net.AspNetCore.Extensions
{
    public static class ConnectorResultExtensions
    {
        public static IActionResult ToActionResult(this ConnectorResult result, HttpContext httpContext)
        {
            if (result.Cookies?.Any() == true)
            {
                foreach (var cookie in result.Cookies)
                    httpContext.Response.Cookies.Append(cookie.Key, cookie.Value);
            }

            switch (result.ResultType)
            {
                case ResultType.Success:
                    if (result.StatusCode == System.Net.HttpStatusCode.NoContent)
                        return new NoContentResult();

                    switch (result.ContentType)
                    {
                        case ContentTypeNames.Application.Json:
                            return new JsonResult(result.Response)
                            {
                                StatusCode = (int)result.StatusCode
                            };
                    }
                    break;
                case ResultType.File:
                    {
                        if (result.Response is FileResponse fileResp)
                            return new ElFinderFileResult(fileResp);

                        throw new ArgumentException("Not a valid FileResponse");
                    }
                case ResultType.Error:
                    return new JsonResult(result.Response)
                    {
                        StatusCode = (int)result.StatusCode
                    };
            }

            throw new NotImplementedException();
        }
    }
}

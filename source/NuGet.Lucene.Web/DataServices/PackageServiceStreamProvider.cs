using System;
using System.Data.Services;
using System.IO;
using System.Web;
using System.Web.Routing;

namespace NuGet.Lucene.Web.DataServices
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    public class PackageServiceStreamProvider : DefaultServiceStreamProvider
    {
        public PackageServiceStreamProvider()
        {
            ContentType = "application/zip";
        }

        public override Uri GetReadStreamUri(object entity, DataServiceOperationContext operationContext)
        {
            var package = (DataServicePackage)entity;
            
            var vpath = GetPackageDownloadPath(package);

            return new Uri(operationContext.AbsoluteRequestUri, vpath);
        }

        public string GetPackageDownloadPath(DataServicePackage package)
        {
            var route = RouteTable.Routes[RouteNames.Packages.Download];

            var routeValues = new { id = package.Id, version = package.Version, httproute = true };

            var routeValueDictionary = new RouteValueDictionary(routeValues);

            this.AddMissingRouteDataFromCurrentRequest(routeValueDictionary);

            var virtualPathData = route.GetVirtualPath(RequestContext, routeValueDictionary);
            if (virtualPathData != null)
            {
                var path = virtualPathData.VirtualPath;
                return VirtualPathUtility.ToAbsolute("~/" + path);
            }
            
            throw new InvalidOperationException("Can't calculate valid route for package!");
        }

        private string[] packagesDownloadRoutePlaceholders = null;

        private void AddMissingRouteDataFromCurrentRequest(RouteValueDictionary routeValueDictionary)
        {
            var webRoute = RouteTable.Routes[RouteNames.Packages.Download] as Route;

            if (webRoute != null)
            {
                if (this.packagesDownloadRoutePlaceholders == null)
                {
                    this.packagesDownloadRoutePlaceholders = this.GetPlaceholderKeys(webRoute.Url).ToArray();
                }

                var missingKeys = this.packagesDownloadRoutePlaceholders.Where(k => !routeValueDictionary.ContainsKey(k));

                var serviceRouteData = HttpContext.Current.Request.RequestContext.RouteData;

                foreach (var missingKey in missingKeys.Where(missingKey => serviceRouteData.Values.ContainsKey(missingKey)))
                {
                    routeValueDictionary.Add(missingKey, serviceRouteData.Values[missingKey]);
                }
            }
        }

        private readonly Regex placeholderRegex = new Regex(@"\{([^\}]*)}");

        private IEnumerable<string> GetPlaceholderKeys(string url)
        {
            return placeholderRegex.Matches(url).Cast<Match>().Select(m => m.Groups[1].Value);
        }

        private static RequestContext RequestContext
        {
            get
            {
                var httpContext = HttpContext.Current;
                var request = new EmptyInputStreamHttpRequestWrapper(httpContext.Request);
                return new RequestContext(new HttpContextWrapperWithRequest(httpContext, request), new RouteData());
            }
        }
    }

    /// <summary>
    /// Allow HttpContext.Request to be replaced with an arbitrary HttpRequestBase instance.
    /// </summary>
    class HttpContextWrapperWithRequest : HttpContextWrapper
    {
        private readonly HttpRequestBase request;

        public HttpContextWrapperWithRequest(HttpContext httpContext, HttpRequestBase request) : base(httpContext)
        {
            this.request = request;
        }

        public override HttpRequestBase Request
        {
            get
            {
                return request;
            }
        }
    }

    /// <summary>
    /// Prevents "System.Web.HttpException (0x80004005): This method or property is not
    /// supported after HttpRequest.GetBufferlessInputStream has been invoked." from being
    /// thrown at System.Web.HttpRequest.get_InputStream().
    /// </summary>
    class EmptyInputStreamHttpRequestWrapper : HttpRequestWrapper
    {
        public EmptyInputStreamHttpRequestWrapper(HttpRequest httpRequest) : base(httpRequest)
        {
        }

        public override Stream InputStream
        {
            get
            {
                return new MemoryStream();
            }
        }
    }
}
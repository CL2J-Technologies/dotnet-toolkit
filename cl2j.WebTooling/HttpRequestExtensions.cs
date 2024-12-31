using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;

namespace cl2j.WebTooling
{
    public static class HttpRequestExtensions
    {
        public static string GetInfo(this HttpRequest request)
        {
            return $"{request.Method} {request.Scheme}://{request.Host}{request.Path}{request.QueryString}";
        }

        public static string GetIp(this HttpContext context)
        {
            var connection = context.Features.Get<IHttpConnectionFeature>();
            if (connection != null)
            {
                var clientIp = connection.RemoteIpAddress;
                if (clientIp != null)
                    return clientIp.ToString();
            }

            return string.Empty;
        }

        public static string GetUrl(this HttpContext context)
        {
            var request = context.Request;
            return $"{request.Method} {request.Scheme}://{request.Host}{request.Path}{request.QueryString}";
        }

        public static string GetUserAgent(this HttpContext context)
        {
            var agent = context.Request.Headers.UserAgent.ToString();
            return agent;
        }

        public static string GetBrowserLanguage(this HttpRequest request)
        {
            //Mettre la liste dans le AppSettings.json
            var languagesSupported = new List<string> { "fr", "en" };

            var languages = request.GetTypedHeaders().AcceptLanguage.OrderByDescending(x => x.Quality);
            foreach (var language in languages)
            {
                var lang = language.Value.ToString();
                if (languagesSupported.Contains(lang))
                    return lang;
            }

            return "en";
        }

        public static string GetRequestLanguage(this HttpRequest request)
        {
            if (request.Path.ToString().StartsWith("/en", StringComparison.CurrentCultureIgnoreCase))
                return "en";
            return "fr";
        }

        public static IReadOnlyDictionary<string, object?> GetDataTokens(this HttpContext context)
        {
            var endpoint = context.GetEndpoint();
            if (endpoint != null)
            {
                var dataTokensDescriptor = endpoint.Metadata.GetMetadata<IDataTokensMetadata>();
                if (dataTokensDescriptor != null)
                    return dataTokensDescriptor.DataTokens;
            }

            return new Dictionary<string, object?>();
        }

        public static string GetQueryString(this HttpContext httpContext)
        {
            return httpContext.Request.QueryString.ToString();
        }

        public static string? GetRouteValueString(this HttpContext context, string key)
        {
            return context.Request.RouteValues[key] as string;
        }

        public static string? GetRouteValueString(this HttpRequest request, string key) => request.RouteValues[key] as string;

        public static string GetCurrentUrl(this HttpRequest request)
        {
            return request.FormatCanonicalUrl(request.Path + request.QueryString.ToString());
        }

        public static string FormatCanonicalUrl(this HttpRequest request, string? canonicalUrl)
        {
            if (string.IsNullOrEmpty(canonicalUrl))
                return string.Empty;

            StringBuilder sb = new();
            sb.Append(request.Scheme);
            sb.Append("://");
            sb.Append(request.Host);
            sb.Append(canonicalUrl);

            return sb.ToString();
        }
    }
}

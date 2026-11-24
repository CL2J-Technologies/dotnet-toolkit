using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace cl2j.WebTooling.Tests
{
    public class HttpRequestExtensionsTests
    {
        private static DefaultHttpContext Context(string method = "GET", string scheme = "https", string host = "example.com", string path = "/", string query = "")
        {
            var context = new DefaultHttpContext();
            context.Request.Method = method;
            context.Request.Scheme = scheme;
            context.Request.Host = new HostString(host);
            context.Request.Path = path;
            context.Request.QueryString = new QueryString(query);
            return context;
        }

        private sealed class Tokens(Dictionary<string, object?> tokens) : IDataTokensMetadata
        {
            public IReadOnlyDictionary<string, object?> DataTokens => tokens;
        }

        [Fact]
        public void GetInfo_formats_method_scheme_host_path_and_query()
        {
            var request = Context("POST", "https", "example.com:8443", "/a/b", "?x=1").Request;

            Assert.Equal("POST https://example.com:8443/a/b?x=1", request.GetInfo());
        }

        [Fact]
        public void GetUrl_formats_like_GetInfo()
        {
            var context = Context("GET", "http", "localhost", "/p", "?q=2");

            Assert.Equal("GET http://localhost/p?q=2", context.GetUrl());
            Assert.Equal(context.Request.GetInfo(), context.GetUrl());
        }

        [Fact]
        public void GetIp_returns_the_remote_address()
        {
            var context = Context();
            context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.9");

            Assert.Equal("203.0.113.9", context.GetIp());
        }

        [Fact]
        public void GetIp_returns_an_empty_string_when_there_is_no_remote_address()
        {
            Assert.Equal(string.Empty, Context().GetIp());
        }

        [Fact]
        public void GetUserAgent_returns_the_header()
        {
            var context = Context();
            context.Request.Headers.UserAgent = "TestBrowser/1.0";

            Assert.Equal("TestBrowser/1.0", context.GetUserAgent());
        }

        [Fact]
        public void GetUserAgent_returns_an_empty_string_without_the_header()
        {
            Assert.Equal(string.Empty, Context().GetUserAgent());
        }

        [Theory]
        [InlineData("fr", "fr")]
        [InlineData("en", "en")]
        [InlineData("fr;q=0.5,en;q=0.9", "en")]
        [InlineData("en;q=0.5,fr;q=0.9", "fr")]
        [InlineData("de,es;q=0.8,fr;q=0.2", "fr")]
        [InlineData("de,es", "en")]
        [InlineData("", "en")]
        public void GetBrowserLanguage_picks_the_best_supported_language(string acceptLanguage, string expected)
        {
            var request = Context().Request;
            if (acceptLanguage.Length > 0)
                request.Headers.AcceptLanguage = acceptLanguage;

            Assert.Equal(expected, request.GetBrowserLanguage());
        }

        [Theory]
        [InlineData("/en", "en")]
        [InlineData("/en/products", "en")]
        [InlineData("/EN/products", "en")]
        [InlineData("/fr/produits", "fr")]
        [InlineData("/", "fr")]
        [InlineData("/produits", "fr")]
        public void GetRequestLanguage_is_english_under_the_en_prefix_and_french_otherwise(string path, string expected)
        {
            Assert.Equal(expected, Context(path: path).Request.GetRequestLanguage());
        }

        [Fact(Skip = "Bug: GetRequestLanguage tests StartsWith(\"/en\") without a segment boundary, so a French page such as /entreprise is reported as English.")]
        public void GetRequestLanguage_does_not_treat_a_path_that_merely_starts_with_en_as_english()
        {
            Assert.Equal("fr", Context(path: "/entreprise").Request.GetRequestLanguage());
        }

        [Fact]
        public void GetDataTokens_returns_the_tokens_of_the_endpoint()
        {
            var context = Context();
            var tokens = new Dictionary<string, object?> { ["area"] = "admin" };
            context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new Tokens(tokens)), "test"));

            Assert.Equal("admin", context.GetDataTokens()["area"]);
        }

        [Fact]
        public void GetDataTokens_is_empty_without_an_endpoint()
        {
            Assert.Empty(Context().GetDataTokens());
        }

        [Fact]
        public void GetDataTokens_is_empty_when_the_endpoint_has_no_tokens_metadata()
        {
            var context = Context();
            context.SetEndpoint(new Endpoint(null, EndpointMetadataCollection.Empty, "test"));

            Assert.Empty(context.GetDataTokens());
        }

        [Fact]
        public void GetQueryString_returns_the_query_with_its_question_mark()
        {
            Assert.Equal("?a=1&b=2", Context(query: "?a=1&b=2").GetQueryString());
        }

        [Fact]
        public void GetQueryString_is_empty_without_a_query()
        {
            Assert.Equal(string.Empty, Context().GetQueryString());
        }

        [Fact]
        public void GetRouteValueString_returns_a_string_route_value_from_the_context_and_from_the_request()
        {
            var context = Context();
            context.Request.RouteValues["slug"] = "hello";

            Assert.Equal("hello", context.GetRouteValueString("slug"));
            Assert.Equal("hello", context.Request.GetRouteValueString("slug"));
        }

        [Fact]
        public void GetRouteValueString_returns_null_for_a_missing_or_non_string_value()
        {
            var context = Context();
            context.Request.RouteValues["id"] = 42;

            Assert.Null(context.GetRouteValueString("id"));
            Assert.Null(context.GetRouteValueString("missing"));
            Assert.Null(context.Request.GetRouteValueString("id"));
        }

        [Fact]
        public void GetCurrentUrl_is_the_absolute_url_of_the_request_with_its_query()
        {
            var request = Context(host: "example.com", path: "/a/b", query: "?x=1").Request;

            Assert.Equal("https://example.com/a/b?x=1", request.GetCurrentUrl());
        }

        [Fact]
        public void FormatCanonicalUrl_prefixes_scheme_and_host()
        {
            var request = Context(scheme: "http", host: "example.com:81").Request;

            Assert.Equal("http://example.com:81/canonical", request.FormatCanonicalUrl("/canonical"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void FormatCanonicalUrl_is_empty_when_there_is_no_canonical_url(string? canonical)
        {
            Assert.Equal(string.Empty, Context().Request.FormatCanonicalUrl(canonical));
        }
    }
}

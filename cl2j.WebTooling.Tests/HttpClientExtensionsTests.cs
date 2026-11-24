using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace cl2j.WebTooling.Tests
{
    public class HttpClientExtensionsTests
    {
        private sealed record Result(string Status, int Count);

        private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
        {
            public HttpRequestMessage? Request { get; private set; }
            public string? RequestBody { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Request = request;
                RequestBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            }
        }

        private static FormFileCollection Files(params (string Name, string Content)[] files)
        {
            var collection = new FormFileCollection();
            foreach (var (name, content) in files)
            {
                var bytes = Encoding.UTF8.GetBytes(content);
                collection.Add(new FormFile(new MemoryStream(bytes), 0, bytes.Length, "ignored", name));
            }
            return collection;
        }

        [Fact]
        public async Task PostFiles_sends_every_file_as_multipart_and_parses_the_response()
        {
            var handler = new StubHandler(HttpStatusCode.OK, """{"Status":"ok","Count":2}""");
            using var client = new HttpClient(handler);

            var result = await client.PostFiles<Result>("http://example.invalid/upload", Files(("a.txt", "alpha"), ("b.txt", "bravo")));

            Assert.Equal(new Result("ok", 2), result);
            Assert.Equal(HttpMethod.Post, handler.Request!.Method);
            Assert.Equal("http://example.invalid/upload", handler.Request.RequestUri!.ToString());
            Assert.StartsWith("multipart/form-data", handler.Request.Content!.Headers.ContentType!.MediaType);
            Assert.Contains("filename=a.txt", handler.RequestBody);
            Assert.Contains("alpha", handler.RequestBody);
            Assert.Contains("filename=b.txt", handler.RequestBody);
            Assert.Contains("bravo", handler.RequestBody);
        }

        [Fact]
        public async Task PostFiles_names_every_part_file()
        {
            var handler = new StubHandler(HttpStatusCode.OK, "{}");
            using var client = new HttpClient(handler);

            await client.PostFiles<Result>("http://example.invalid/upload", Files(("a.txt", "alpha")));

            Assert.Contains("name=file", handler.RequestBody);
        }

        [Fact]
        public async Task PostFiles_throws_when_the_server_answers_with_an_error()
        {
            using var client = new HttpClient(new StubHandler(HttpStatusCode.InternalServerError, "{}"));

            await Assert.ThrowsAsync<HttpRequestException>(() => client.PostFiles<Result>("http://example.invalid/upload", Files(("a.txt", "alpha"))));
        }

        [Fact]
        public async Task PostFiles_returns_the_default_value_for_an_empty_response_body()
        {
            using var client = new HttpClient(new StubHandler(HttpStatusCode.OK, ""));

            var result = await client.PostFiles<Result>("http://example.invalid/upload", Files(("a.txt", "alpha")));

            Assert.Null(result);
        }

        [Fact]
        public async Task PostFiles_sends_an_empty_multipart_body_for_no_files()
        {
            var handler = new StubHandler(HttpStatusCode.OK, """{"Status":"none","Count":0}""");
            using var client = new HttpClient(handler);

            var result = await client.PostFiles<Result>("http://example.invalid/upload", new FormFileCollection());

            Assert.Equal(new Result("none", 0), result);
            Assert.DoesNotContain("filename=", handler.RequestBody);
        }
    }
}

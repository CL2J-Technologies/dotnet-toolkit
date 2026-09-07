using System.Net;
using System.Net.Http.Headers;
using System.Text;
using cl2j.Tooling;
using Xunit;

namespace cl2j.Tooling.Tests
{
    /// <summary>
    ///     <see cref="RestClient"/> against a handler that answers in the process rather than a
    ///     server on a port. Nothing here binds a socket, so the tests are as fast and as
    ///     repeatable as any other, and they can assert on the request that was actually built —
    ///     which is the part a caller cannot see and the part that breaks.
    /// </summary>
    public class RestClientTests
    {
        /// <summary>
        ///     Records what was sent and answers with what the test says. The requests are captured
        ///     before the response is produced, so a test can assert on a request even when the
        ///     call it belongs to threw.
        /// </summary>
        private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
        {
            public List<HttpRequestMessage> Requests { get; } = [];
            public List<string?> Bodies { get; } = [];

            public static StubHandler Returning(string body, HttpStatusCode status = HttpStatusCode.OK)
            {
                return new StubHandler(_ => new HttpResponseMessage(status)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                });
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                Bodies.Add(request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
                return respond(request);
            }
        }

        /// <summary>
        ///     Waits, and honours the cancellation token while it does — which is what a real
        ///     handler does and what gives <see cref="HttpClient.Timeout"/> something to cancel.
        /// </summary>
        private sealed class SlowHandler(TimeSpan delay) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                await Task.Delay(delay, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
            }
        }

        private sealed class Payload
        {
            public string? FirstName { get; set; }
        }

        private sealed class Result
        {
            public string? Message { get; set; }
        }

        private const string BaseUrl = "https://example.invalid/api/";

        [Fact]
        public async Task A_get_returns_the_object_the_server_described()
        {
            var handler = StubHandler.Returning("{\"message\":\"hello\"}");
            using var client = new RestClient(handler, BaseUrl);

            var result = await client.GetAsync<Result>("things");

            Assert.Equal("hello", result?.Message);
        }

        [Fact]
        public async Task A_get_that_is_not_found_is_absence_rather_than_failure()
        {
            //The one status this class treats as data instead of an error, and the reason a caller
            //can write `?? CreateIt()` instead of catching.
            var handler = StubHandler.Returning(string.Empty, HttpStatusCode.NotFound);
            using var client = new RestClient(handler, BaseUrl);

            Assert.Null(await client.GetAsync<Result>("missing"));
        }

        [Fact]
        public async Task Any_other_failure_is_raised()
        {
            var handler = StubHandler.Returning("{}", HttpStatusCode.InternalServerError);
            using var client = new RestClient(handler, BaseUrl);

            await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync<Result>("broken"));
        }

        [Fact]
        public async Task A_relative_url_is_resolved_against_the_base_address()
        {
            var handler = StubHandler.Returning("{}");
            using var client = new RestClient(handler, BaseUrl);

            await client.GetAsync<Result>("things/42");

            Assert.Equal("https://example.invalid/api/things/42", Assert.Single(handler.Requests).RequestUri?.ToString());
        }

        [Fact]
        public async Task Json_is_the_accepted_response_format()
        {
            var handler = StubHandler.Returning("{}");
            using var client = new RestClient(handler, BaseUrl);

            await client.GetAsync<Result>("things");

            Assert.Contains(
                Assert.Single(handler.Requests).Headers.Accept,
                h => h.MediaType == "application/json");
        }

        [Fact]
        public async Task Default_headers_are_sent_on_every_request()
        {
            var handler = StubHandler.Returning("{}");
            using var client = new RestClient(handler, BaseUrl, new Dictionary<string, string> { ["X-Api-Key"] = "secret" });

            await client.GetAsync<Result>("one");
            await client.GetAsync<Result>("two");

            Assert.Equal(2, handler.Requests.Count);
            Assert.All(handler.Requests, r => Assert.Equal("secret", r.Headers.GetValues("X-Api-Key").Single()));
        }

        [Fact]
        public async Task An_authorization_scheme_and_value_reach_the_request()
        {
            var handler = StubHandler.Returning("{}");
            using var client = new RestClient(handler, BaseUrl);
            client.SetAuthorization("Bearer", "a-token");

            await client.GetAsync<Result>("things");

            var authorization = Assert.Single(handler.Requests).Headers.Authorization;
            Assert.Equal("Bearer", authorization?.Scheme);
            Assert.Equal("a-token", authorization?.Parameter);
        }

        [Fact]
        public async Task A_post_sends_the_payload_as_camel_case_json_and_reads_the_answer()
        {
            var handler = StubHandler.Returning("{\"message\":\"created\"}");
            using var client = new RestClient(handler, BaseUrl);

            var result = await client.PostAsync<Payload, Result>("things", new Payload { FirstName = "Renée" });

            Assert.Equal("created", result?.Message);
            Assert.Contains("\"firstName\": \"Renée\"", Assert.Single(handler.Bodies), StringComparison.Ordinal);
        }

        [Fact]
        public void A_client_built_without_a_handler_gets_one_of_its_own()
        {
            //The constructor every real caller uses. It opens no connection until something is
            //requested, so this only asserts that it builds and disposes — which is the part the
            //handler overload could have broken when it was added.
            using var client = new RestClient(BaseUrl, new Dictionary<string, string> { ["X-Api-Key"] = "secret" });

            Assert.NotNull(client);
        }

        [Fact]
        public async Task A_post_with_no_expected_answer_sends_the_payload()
        {
            var handler = StubHandler.Returning(string.Empty, HttpStatusCode.NoContent);
            using var client = new RestClient(handler, BaseUrl);

            await client.PostAsync("things", new Payload { FirstName = "Renée" });

            Assert.Contains("\"firstName\": \"Renée\"", Assert.Single(handler.Bodies), StringComparison.Ordinal);
        }

        [Fact]
        public async Task A_post_with_no_expected_answer_still_raises_a_failure()
        {
            var handler = StubHandler.Returning(string.Empty, HttpStatusCode.BadRequest);
            using var client = new RestClient(handler, BaseUrl);

            await Assert.ThrowsAsync<HttpRequestException>(() => client.PostAsync("things", new Payload()));
        }

        [Fact]
        public async Task A_post_of_prepared_content_sends_it_unchanged()
        {
            var handler = StubHandler.Returning("{\"message\":\"ok\"}");
            using var client = new RestClient(handler, BaseUrl);

            var result = await client.PostAsync<Result>("things", new StringContent("raw", Encoding.UTF8, "text/plain"));

            Assert.Equal("ok", result?.Message);
            Assert.Equal("raw", Assert.Single(handler.Bodies));
        }

        [Fact]
        public async Task Files_are_posted_as_multipart_with_one_part_each()
        {
            var handler = StubHandler.Returning("{\"message\":\"stored\"}");
            using var client = new RestClient(handler, BaseUrl);

            var result = await client.PostFiles<Result>("upload",
            [
                ("first.txt", "one"u8.ToArray()),
                ("second.txt", "two"u8.ToArray())
            ]);

            Assert.Equal("stored", result?.Message);

            var request = Assert.Single(handler.Requests);
            Assert.Equal("multipart/form-data", request.Content?.Headers.ContentType?.MediaType);

            var body = Assert.Single(handler.Bodies)!;
            Assert.Contains("name=file", body.Replace("\"", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
            Assert.Contains("first.txt", body, StringComparison.Ordinal);
            Assert.Contains("second.txt", body, StringComparison.Ordinal);
            Assert.Contains("one", body, StringComparison.Ordinal);
            Assert.Contains("two", body, StringComparison.Ordinal);
        }

        [Fact]
        public async Task A_timeout_that_elapses_cancels_the_call()
        {
            using var handler = new SlowHandler(TimeSpan.FromSeconds(30));
            using var client = new RestClient(handler, BaseUrl);
            client.SetTimeout(TimeSpan.FromMilliseconds(20));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync<Result>("slow"));
        }

        [Fact]
        public void An_injected_handler_belongs_to_whoever_passed_it()
        {
            //A handler is meant to be shared and long-lived — that is the whole reason
            //IHttpClientFactory exists. Disposing a RestClient must not take the caller's handler
            //with it, or the second client built on it fails.
            var handler = StubHandler.Returning("{}");

            using (var client = new RestClient(handler, BaseUrl))
            {
                //Nothing to do: the point is what Dispose does at the closing brace.
            }

            using var second = new RestClient(handler, BaseUrl);
            Assert.NotNull(second);
        }

        [Fact]
        public async Task The_handler_of_a_disposed_client_is_still_usable()
        {
            var handler = StubHandler.Returning("{\"message\":\"still here\"}");
            using (var first = new RestClient(handler, BaseUrl))
            {
                await first.GetAsync<Result>("things");
            }

            using var second = new RestClient(handler, BaseUrl);

            Assert.Equal("still here", (await second.GetAsync<Result>("things"))?.Message);
        }
    }
}

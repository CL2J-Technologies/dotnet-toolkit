using System.Net;
using System.Text;
using cl2j.Tooling;
using Newtonsoft.Json;
using Xunit;

namespace cl2j.Tooling.Tests
{
    /// <summary>
    ///     The two halves of every call <see cref="RestClient"/> makes: what goes on the wire, and
    ///     what comes back off it. Neither needs a socket — one builds an
    ///     <see cref="HttpContent"/>, the other reads an <see cref="HttpResponseMessage"/> — so the
    ///     part of this package that talks to the network was untested only because nobody had
    ///     separated it from the part that connects.
    ///
    ///     <para>
    ///     What is pinned here is the shape of the payload. A serializer setting is invisible in
    ///     review and changes every field name on the wire.
    ///     </para>
    /// </summary>
    public class PrepareRequestBodyTests
    {
        private sealed class Payload
        {
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public int ItemCount { get; set; }
        }

        private static async Task<string> BodyOf<T>(T value) => await value.PrepareRequestBody().ReadAsStringAsync();

        [Fact]
        public async Task Property_names_go_out_in_camel_case()
        {
            //The receiving end is a JSON API, not another .NET caller. Change this and every field
            //name on the wire changes with it.
            var body = await BodyOf(new Payload { FirstName = "Renée", ItemCount = 3 });

            Assert.Contains("\"firstName\"", body, StringComparison.Ordinal);
            Assert.Contains("\"itemCount\"", body, StringComparison.Ordinal);
            Assert.DoesNotContain("\"FirstName\"", body, StringComparison.Ordinal);
        }

        [Fact]
        public async Task A_null_property_is_left_out_entirely()
        {
            //NullValueHandling.Ignore. It is the difference between "do not change this field" and
            //"set this field to null" for any API that distinguishes the two.
            var body = await BodyOf(new Payload { FirstName = "Renée", LastName = null });

            Assert.Contains("\"firstName\"", body, StringComparison.Ordinal);
            Assert.DoesNotContain("lastName", body, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task A_value_that_is_not_null_is_still_written_when_it_is_a_default()
        {
            //Ignore applies to null, not to zero: an int of 0 is a value and stays.
            var body = await BodyOf(new Payload { FirstName = "x", ItemCount = 0 });

            Assert.Contains("\"itemCount\": 0", body, StringComparison.Ordinal);
        }

        [Fact]
        public void The_content_is_declared_as_utf8_json()
        {
            using var content = new Payload { FirstName = "Renée" }.PrepareRequestBody();

            Assert.Equal("application/json", content.Headers.ContentType?.MediaType);
            Assert.Equal("utf-8", content.Headers.ContentType?.CharSet);
        }

        [Fact]
        public async Task An_accented_value_survives_the_encoding()
        {
            var body = await BodyOf(new Payload { FirstName = "Ærøskøbing" });

            Assert.Contains("Ærøskøbing", body, StringComparison.Ordinal);
        }

        [Fact]
        public async Task A_null_payload_serialises_to_null_rather_than_throwing()
        {
            Assert.Equal("null", await BodyOf<Payload?>(null));
        }
    }

    public class ParseResponseAsyncTests
    {
        private sealed class Result
        {
            public string? FirstName { get; set; }
            public int ItemCount { get; set; }
        }

        private static HttpResponseMessage Responding(string body, HttpStatusCode status = HttpStatusCode.OK)
        {
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }

        [Fact]
        public async Task A_json_body_becomes_the_object_it_describes()
        {
            using var response = Responding("{\"firstName\":\"Renée\",\"itemCount\":3}");

            var result = await response.ParseResponseAsync<Result>();

            Assert.Equal("Renée", result?.FirstName);
            Assert.Equal(3, result?.ItemCount);
        }

        [Fact]
        public async Task An_empty_body_is_the_default_rather_than_an_error()
        {
            //The branch that makes GetAsync usable against an API that answers 200 with no body.
            using var response = Responding(string.Empty);

            Assert.Null(await response.ParseResponseAsync<Result>());
        }

        [Fact]
        public async Task A_field_the_type_does_not_declare_is_ignored()
        {
            using var response = Responding("{\"firstName\":\"Renée\",\"somethingElse\":true}");

            Assert.Equal("Renée", (await response.ParseResponseAsync<Result>())?.FirstName);
        }

        [Fact]
        public async Task The_status_code_is_not_consulted()
        {
            //Worth knowing: this parses whatever body it is handed. Every caller in RestClient
            //calls EnsureSuccessStatusCode first, which is what actually rejects a failure — this
            //method would happily deserialise an error payload.
            using var response = Responding("{\"firstName\":\"from an error body\"}", HttpStatusCode.InternalServerError);

            Assert.Equal("from an error body", (await response.ParseResponseAsync<Result>())?.FirstName);
        }

        [Fact]
        public async Task What_was_sent_can_be_read_back()
        {
            //The round trip is the contract that matters: camelCase out, and the same values in.
            using var content = new Result { FirstName = "Renée", ItemCount = 7 }.PrepareRequestBody();
            using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = content };

            var result = await response.ParseResponseAsync<Result>();

            Assert.Equal("Renée", result?.FirstName);
            Assert.Equal(7, result?.ItemCount);
        }

        [Fact]
        public async Task A_malformed_body_is_reported_rather_than_returning_null()
        {
            //Characterisation with a caveat, and the caveat is the point: in a DEBUG build this
            //method catches the failure, prints it to the console and retries through an untyped
            //round trip, which can succeed. In a RELEASE build it throws.
            //
            //CI and every published package are Release; a developer's F5 is Debug. So this
            //assertion is written to hold in both, and the divergence itself is reported in the
            //issue raised alongside these tests: the outputFileName parameter, which RestClient
            //exposes publicly on GetAsync, is silently a no-op in every build that ships.
            using var response = Responding("{ this is not json");

            await Assert.ThrowsAnyAsync<JsonException>(() => response.ParseResponseAsync<Result>());
        }
    }
}

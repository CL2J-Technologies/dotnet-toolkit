using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace cl2j.Tooling
{
    public static class HttpClientExtensions
    {
        /// <summary>
        ///     Deserialises the body of the response. An empty body is the default value.
        ///
        ///     <para>
        ///     When <paramref name="outputFileName"/> is given, the raw body is written to that file
        ///     first, in every build configuration, so that a payload that does not fit the type
        ///     can still be inspected. A body that cannot be deserialised throws.
        ///     </para>
        /// </summary>
        public static async Task<TResponse?> ParseResponseAsync<TResponse>(this HttpResponseMessage response, string? outputFileName = null)
        {
            var value = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(value))
                return default;

            if (!string.IsNullOrEmpty(outputFileName))
                await File.WriteAllTextAsync(outputFileName, value);

            return JsonConvert.DeserializeObject<TResponse>(value);
        }

        public static StringContent PrepareRequestBody<TIn>(this TIn content)
        {
            var contractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() };

            var data = JsonConvert.SerializeObject(content, new JsonSerializerSettings
            {
                ContractResolver = contractResolver,
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
            });
            var httpContent = new StringContent(data, Encoding.UTF8, "application/json");
            return httpContent;
        }
    }
}

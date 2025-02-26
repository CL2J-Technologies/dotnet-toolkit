using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace cl2j.Tooling
{
    public static class HttpClientExtensions
    {
        public static async Task<TResponse?> ParseResponseAsync<TResponse>(this HttpResponseMessage response, string? outputFileName = null)
        {
            var value = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrEmpty(value))
            {
#if DEBUG
                if (!string.IsNullOrEmpty(outputFileName))
                    File.WriteAllText(outputFileName, value);

                try
                {
#endif
                    return JsonConvert.DeserializeObject<TResponse>(value);
#if DEBUG
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());

                    //Get the real line to debug more easily
                    var obj = JsonConvert.DeserializeObject(value);
                    value = JsonConvert.SerializeObject(obj, Formatting.Indented);
                    return JsonConvert.DeserializeObject<TResponse>(value);
                }
#endif
            }

            return default;
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

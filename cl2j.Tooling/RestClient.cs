using System.Net.Http.Headers;

namespace cl2j.Tooling
{
    public class RestClient : IDisposable
    {
        private readonly HttpClient client;

        public RestClient(string baseUrl, Dictionary<string, string>? defaultHeaders = null)
        {
            client = new HttpClient
            {
                BaseAddress = new Uri(baseUrl)
            };
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (defaultHeaders != null)
            {
                foreach (var pair in defaultHeaders)
                    client.DefaultRequestHeaders.Add(pair.Key, pair.Value);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                client.Dispose();
            }
        }

        public void SetAuthorization(string key, string value)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(key, value);
        }

        public void SetTimeout(TimeSpan timeout)
        {
            client.Timeout = timeout;
        }

        public async Task<TOut?> PostAsync<TIn, TOut>(string url, TIn? data) where TOut : new()
        {
            var body = data.PrepareRequestBody();
            using HttpResponseMessage response = await client.PostAsync(url, body);
            response.EnsureSuccessStatusCode();
            return await response.ParseResponseAsync<TOut>();
        }

        public async Task PostAsync<TIn>(string url, TIn? data)
        {
            var body = data.PrepareRequestBody();
            using HttpResponseMessage response = await client.PostAsync(url, body);
            response.EnsureSuccessStatusCode();
        }

        public async Task<TOut?> PostAsync<TOut>(string url, HttpContent content)
        {
            using HttpResponseMessage response = await client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
            return await response.ParseResponseAsync<TOut>();
        }

        public async Task<TOut?> PostFiles<TOut>(string url, List<(string, byte[])> files)
        {
            using var content = new MultipartFormDataContent();
            foreach (var file in files)
                content.Add(new ByteArrayContent(file.Item2), "file", file.Item1);

            using HttpResponseMessage response = await client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
            return await response.ParseResponseAsync<TOut>();
        }

        public async Task<T?> GetAsync<T>(string url, string? outputFileName = null) where T : new()
        {
            using HttpResponseMessage response = await client.GetAsync(url);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return default;
            response.EnsureSuccessStatusCode();
            return await response.ParseResponseAsync<T>(outputFileName);
        }
    }
}

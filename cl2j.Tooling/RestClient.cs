﻿using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Net.Http.Headers;
using System.Text;

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
            var body = PrepareRequestBody(data);
            using HttpResponseMessage response = await client.PostAsync(url, body);
            response.EnsureSuccessStatusCode();
            return await ParseResponseAsync<TOut>(response);
        }

        public async Task PostAsync<TIn>(string url, TIn? data)
        {
            var body = PrepareRequestBody(data);
            using HttpResponseMessage response = await client.PostAsync(url, body);
            response.EnsureSuccessStatusCode();
        }

        public async Task<TOut?> PostAsync<TOut>(string url, HttpContent content)
        {
            using HttpResponseMessage response = await client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
            return await ParseResponseAsync<TOut>(response);
        }

        public async Task<TOut?> PostFiles<TOut>(string url, IFormFileCollection files)
        {
            using var content = new MultipartFormDataContent();
            foreach (var file in files)
            {
                byte[] data;
                using (var br = new BinaryReader(file.OpenReadStream()))
                    data = br.ReadBytes((int)file.OpenReadStream().Length);
                content.Add(new ByteArrayContent(data), "file", file.FileName);
            }

            using HttpResponseMessage response = await client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
            return await ParseResponseAsync<TOut>(response);
        }

        public async Task<TOut?> PostFiles<TOut>(string url, List<(string, byte[])> files)
        {
            using var content = new MultipartFormDataContent();
            foreach (var file in files)
                content.Add(new ByteArrayContent(file.Item2), "file", file.Item1);

            using HttpResponseMessage response = await client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
            return await ParseResponseAsync<TOut>(response);
        }

        public async Task<T?> GetAsync<T>(string url) where T : new()
        {
            using HttpResponseMessage response = await client.GetAsync(url);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return default;
            response.EnsureSuccessStatusCode();
            return await ParseResponseAsync<T>(response);

        }

        private static async Task<TResponse?> ParseResponseAsync<TResponse>(HttpResponseMessage response)
        {
            var value = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrEmpty(value))
                return JsonConvert.DeserializeObject<TResponse>(value);

            return default;
        }

        private static StringContent PrepareRequestBody<TIn>(TIn content)
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

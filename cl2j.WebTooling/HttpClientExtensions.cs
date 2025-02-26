using cl2j.Tooling;
using Microsoft.AspNetCore.Http;

namespace cl2j.WebTooling
{
    public static class HttpClientExtensions
    {
        public static async Task<TOut?> PostFiles<TOut>(this HttpClient client, string url, IFormFileCollection files)
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
            return await response.ParseResponseAsync<TOut>();
        }
    }
}

using System.Text;
using cl2j.FileStorage.Core;
using cl2j.Tooling;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace cl2j.FileStorage.Extensions
{
    public static class JsonExtensions
    {
        public static async Task<T> ReadJsonObjectAsync<T>(this IFileStorageProvider fileStorageProvider, string fileName, Encoding? encoding = null) where T : new()
        {
            var decompress = fileName.EndsWith(".json.gz");

            string? data = null;
            if (decompress)
            {
                var stream = await fileStorageProvider.ReadObjectAsync(fileName);
                if (stream != null)
                {
                    var bytes = stream.ToBytes();
                    if (bytes != null)
                        data = CompressionUtils.Decompress(bytes);
                }
            }
            else
                data = await fileStorageProvider.ReadTextAsync(fileName, encoding);

            if (string.IsNullOrWhiteSpace(data))
                return new T();

            var options = CreateSerializerOptions();

            var value = JsonConvert.DeserializeObject<T>(data, options);
            return value == null ? throw new JsonException($"Unable to deserialize {typeof(T).Name} from value '{data}'") : value;
        }

        public static async Task WriteJsonObjectAsync<T>(this IFileStorageProvider fileStorageProvider, string fileName, T obj, bool indented = false, Encoding? encoding = null)
        {
            var data = SerializeJsonObject(obj, indented);

            var compress = fileName.EndsWith(".json.gz");
            if (compress)
                await fileStorageProvider.WriteBytesAsync(fileName, CompressionUtils.Compress(data));
            else
                await fileStorageProvider.WriteTextAsync(fileName, data, encoding);
        }

        public static string SerializeJsonObject<T>(T obj, bool indented)
        {
            var options = CreateSerializerOptions();
            options.Formatting = indented ? Formatting.Indented : Formatting.None;

            return JsonConvert.SerializeObject(obj, options);
        }

        private static JsonSerializerSettings CreateSerializerOptions()
        {
            var options = new JsonSerializerSettings()
            {
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                DefaultValueHandling = DefaultValueHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                TypeNameHandling = TypeNameHandling.Auto
            };
            var camelCaseResolver = new CamelCasePropertyNamesContractResolver();
            camelCaseResolver.NamingStrategy.ProcessDictionaryKeys = false;
            options.ContractResolver = camelCaseResolver;
            options.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
            return options;
        }
    }
}
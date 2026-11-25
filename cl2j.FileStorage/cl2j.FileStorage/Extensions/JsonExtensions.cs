using System.Text;
using cl2j.FileStorage.Core;
using cl2j.Tooling;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace cl2j.FileStorage.Extensions
{
    public static class JsonExtensions
    {
        private static readonly string byteOrderMarkUtf8 = Encoding.UTF8.GetString(Encoding.UTF8.GetPreamble());

        public static Task<T> ReadJsonObjectAsync<T>(this IFileStorageProvider fileStorageProvider, string fileName, Encoding? encoding = null) where T : new()
            => ReadJsonObjectAsync<T>(fileStorageProvider, fileName, (JsonStorageOptions?)null, encoding);

        public static async Task<T> ReadJsonObjectAsync<T>(this IFileStorageProvider fileStorageProvider, string fileName, JsonStorageOptions? options, Encoding? encoding = null) where T : new()
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

            var settings = CreateSerializerOptions(options);

            if (data.StartsWith(byteOrderMarkUtf8, StringComparison.Ordinal))
                data = data[byteOrderMarkUtf8.Length..];

            var value = JsonConvert.DeserializeObject<T>(data, settings);
            return value == null ? throw new JsonException($"Unable to deserialize {typeof(T).Name} from value '{data}'") : value;
        }

        public static Task WriteJsonObjectAsync<T>(this IFileStorageProvider fileStorageProvider, string fileName, T obj, bool indented = false, Encoding? encoding = null)
            => WriteJsonObjectAsync(fileStorageProvider, fileName, obj, null, indented, encoding);

        public static async Task WriteJsonObjectAsync<T>(this IFileStorageProvider fileStorageProvider, string fileName, T obj, JsonStorageOptions? options, bool indented = false, Encoding? encoding = null)
        {
            var data = SerializeJsonObject(obj, indented, options);

            var compress = fileName.EndsWith(".json.gz");
            if (compress)
                await fileStorageProvider.WriteBytesAsync(fileName, CompressionUtils.Compress(data));
            else
                await fileStorageProvider.WriteTextAsync(fileName, data, encoding);
        }

        public static string SerializeJsonObject<T>(T obj, bool indented)
            => SerializeJsonObject(obj, indented, null);

        public static string SerializeJsonObject<T>(T obj, bool indented, JsonStorageOptions? options)
        {
            var settings = CreateSerializerOptions(options);
            settings.Formatting = indented ? Formatting.Indented : Formatting.None;

            return JsonConvert.SerializeObject(obj, settings);
        }

        private static JsonSerializerSettings CreateSerializerOptions(JsonStorageOptions? storageOptions)
        {
            var allowedTypes = storageOptions?.AllowedTypes ?? [];
            var options = new JsonSerializerSettings()
            {
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                DefaultValueHandling = DefaultValueHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                //A $type is neither written nor followed unless the caller listed the types it expects.
                //Auto with no binder let a stored file choose the type the reader instantiated.
                TypeNameHandling = allowedTypes.Count > 0 ? TypeNameHandling.Auto : TypeNameHandling.None,
                //Always set, even with an empty list: a member marked [JsonProperty(TypeNameHandling = ...)]
                //turns type names back on for itself, and must still be held to the list.
                SerializationBinder = new AllowListSerializationBinder(allowedTypes)
            };
            var camelCaseResolver = new CamelCasePropertyNamesContractResolver();
            camelCaseResolver.NamingStrategy?.ProcessDictionaryKeys = false;
            options.ContractResolver = camelCaseResolver;
            options.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
            return options;
        }
    }
}
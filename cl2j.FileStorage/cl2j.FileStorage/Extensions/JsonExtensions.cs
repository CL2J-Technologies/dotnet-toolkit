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
                //Auto, as it always was, but with a binder that refuses every type the caller did not
                //list, and the list is empty by default. A $type in a file, or a value whose runtime
                //type differs from its declared one (the only case Auto writes a $type for), throws
                //instead of being followed or quietly dropped: a stored file never chooses the type
                //the reader instantiates, and no derived property is lost without a word. A file
                //with no $type reads exactly as before.
                TypeNameHandling = TypeNameHandling.Auto,
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
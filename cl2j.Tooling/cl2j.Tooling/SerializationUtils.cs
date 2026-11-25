using Newtonsoft.Json;

namespace cl2j.Tooling
{
    public class SerializationUtils
    {
        public static TOut DeserializeData<TOut>(string? data) where TOut : new()
        {
            if (data == null)
                return new TOut();
            return JsonConvert.DeserializeObject<TOut?>(data) ?? new TOut();
        }
    }
}

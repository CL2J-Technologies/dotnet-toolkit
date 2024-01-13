using cl2j.FileStorage.Core;
using cl2j.FileStorage.Extensions;

namespace cl2j.DataStore.Json
{
    public class UniqueIdSequenceGenerator
    {
        private readonly IFileStorageProvider fileStorageProvider;
        public readonly Dictionary<string, int> lastIds;

        public UniqueIdSequenceGenerator(IFileStorageProvider fileStorageProvider)
        {
            this.fileStorageProvider = fileStorageProvider;

            lastIds = fileStorageProvider.ReadJsonObjectAsync<Dictionary<string, int>>("uniqueids.json").Result;
        }

        public async Task<int> NewIdAsync(string collectionName)
        {
            int id;
            if (lastIds.TryGetValue(collectionName, out var currentValue))
            {
                id = ++currentValue;
                lastIds[collectionName] = id;
            }
            else
            {
                id = 1;
                lastIds.Add(collectionName, id);
            }

            await fileStorageProvider.WriteJsonObjectAsync("uniqueids.json", lastIds);

            return id;
        }
    }
}
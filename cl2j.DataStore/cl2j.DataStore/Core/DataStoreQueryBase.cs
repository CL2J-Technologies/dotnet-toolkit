namespace cl2j.DataStore
{
    public abstract class DataStoreQueryBase<TKey, TValue> : IDataStoreQuery<TKey, TValue>
    {
        protected readonly Func<TValue, TKey> getKeyPredicate;

        public DataStoreQueryBase(Func<TValue, TKey> getKeyPredicate)
        {
            this.getKeyPredicate = getKeyPredicate;
        }

        public abstract Task<List<TValue>> GetAllAsync();

        public abstract Task<TValue?> GetByIdAsync(TKey key);

        protected TValue? FirstOrDefault(IEnumerable<TValue> list, TKey key)
        {
            return list.FirstOrDefault(item => EqualityComparer<TKey>.Default.Equals(getKeyPredicate(item), key));
        }

        protected int FindIndex(List<TValue> list, TValue entity)
        {
            var key = getKeyPredicate(entity);
            return FindIndex(list, key);
        }

        protected int FindIndex(List<TValue> list, TKey key)
        {
            return list.FindIndex(item => EqualityComparer<TKey>.Default.Equals(getKeyPredicate(item), key));
        }
    }
}
namespace cl2j.DataStore.List
{
    public abstract class DataStoreListCommandAndQueryBase<TKey, TValue> : IDataStoreListCommandAndQuery<TKey, TValue>
    {
        protected readonly Func<TValue, TKey> getKeyPredicate;

        public DataStoreListCommandAndQueryBase(Func<TValue, TKey> getKeyPredicate)
        {
            this.getKeyPredicate = getKeyPredicate;
        }

        public abstract Task<List<TValue>> GetAllAsync();

        public abstract Task<TValue?> GetByIdAsync(TKey key);

        public abstract Task DeleteAsync(TKey key);

        public abstract Task InsertAsync(TValue entity);

        public abstract Task UpdateAsync(TValue entity);

        public abstract Task ReplaceAllByAsync(ICollection<TValue> items);

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
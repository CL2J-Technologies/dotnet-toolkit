namespace cl2j.DataStore
{
    public abstract class DataStoreBase<TKey, TValue> : DataStoreQueryBase<TKey, TValue>, IDataStore<TKey, TValue>
    {
        public DataStoreBase(Func<TValue, TKey> getKeyPredicate)
            : base(getKeyPredicate)
        {
        }

        public abstract Task DeleteAsync(TKey key);

        public abstract Task InsertAsync(TValue entity);

        public abstract Task UpdateAsync(TValue entity);

        public abstract Task ReplaceAllByAsync(IDictionary<TKey, TValue> items);

        protected int RemoveAll(List<TValue> list, TKey key)
        {
            return list.RemoveAll(item => EqualityComparer<TKey>.Default.Equals(getKeyPredicate(item), key));
        }
    }
}
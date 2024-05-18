using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace cl2j.Tooling
{
#pragma warning disable CA1710 // Identifiers should have correct suffix

    public class Localized<T> : IDictionary<string, T>
#pragma warning restore CA1710 // Identifiers should have correct suffix
    {
        private readonly Dictionary<string, T> data = new();

#pragma warning disable CA2211 // Non-constant fields should not be visible
        public static string DefaultLanguage = "en";
#pragma warning restore CA2211 // Non-constant fields should not be visible

        public ICollection<string> Keys => ((IDictionary<string, T>)data).Keys;

        public ICollection<T> Values => ((IDictionary<string, T>)data).Values;

        public int Count => ((ICollection<KeyValuePair<string, T>>)data).Count;

        public bool IsReadOnly => ((ICollection<KeyValuePair<string, T>>)data).IsReadOnly;

        public T this[string key]
        {
            get
            {
                TryGetValue(key, out var value);
#pragma warning disable CS8603 // Possible null reference return.
                return value;
#pragma warning restore CS8603 // Possible null reference return.
            }

            set => ((IDictionary<string, T>)data)[key] = value;
        }

        public void Add(string key, T value)
        {
            ((IDictionary<string, T>)data).Add(key, value);
        }

        public bool ContainsKey(string key)
        {
            return ((IDictionary<string, T>)data).ContainsKey(key);
        }

        public bool Remove(string key)
        {
            return ((IDictionary<string, T>)data).Remove(key);
        }

        public T Get(string language, T defaultValue)
        {
            if (TryGetValue(language, out var value))
                return value;
            if (Count > 0)
                return Values.First();
            return defaultValue;
        }

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out T value)
        {
            //Return the value for the requested language
            if (((IDictionary<string, T>)data).TryGetValue(key, out value))
                return true;

            //Return the default language value
            if (((IDictionary<string, T>)data).TryGetValue(DefaultLanguage, out value))
                return true;

            //Take the first value
            var first = data.Values.FirstOrDefault();
            if (first != null)
            {
                value = first;
                return true;
            }

            //No values
            value = default;
            return false;
        }

        public T? GetOrDefault(string key)
        {
            if (TryGetValue(key, out var value)) return value;
            return default;
        }

        public void Add(KeyValuePair<string, T> item)
        {
            ((ICollection<KeyValuePair<string, T>>)data).Add(item);
        }

        public void Clear()
        {
            ((ICollection<KeyValuePair<string, T>>)data).Clear();
        }

        public bool Contains(KeyValuePair<string, T> item)
        {
            return ((ICollection<KeyValuePair<string, T>>)data).Contains(item);
        }

        public void CopyTo(KeyValuePair<string, T>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<string, T>>)data).CopyTo(array, arrayIndex);
        }

        public bool Remove(KeyValuePair<string, T> item)
        {
            return ((ICollection<KeyValuePair<string, T>>)data).Remove(item);
        }

        public IEnumerator<KeyValuePair<string, T>> GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<string, T>>)data).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)data).GetEnumerator();
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"{DefaultLanguage}={data[DefaultLanguage]}");
            foreach (KeyValuePair<string, T> item in data)
            {
                if (item.Key != DefaultLanguage)
                    sb.Append($", {item.Key}={item.Value}");
            }
            return sb.ToString();
        }

        public static Localized<T> CreateFrEn(T fr, T en)
        {
            var dict = new Dictionary<string, T>
            {
                { "en", en },
                { "fr", fr }
            };
            return FromDict(dict);
        }

        public static Localized<T> Default(T value)
        {
            return new Localized<T>
            {
                { "*", value }
            };
        }

        public static Localized<string> EmptyString()
        {
            return Localized<string>.Default(string.Empty);
        }

        public static Localized<T> FromDict(IDictionary<string, T> dict)
        {
            var localize = new Localized<T>();
            foreach (var kvp in dict)
                localize.Add(kvp.Key, kvp.Value);
            return localize;
        }

        public static Localized<T> FromDictNullable(IDictionary<string, T?> dict)
        {
            var localize = new Localized<T>();
            foreach (var kvp in dict)
            {
                if (kvp.Value != null)
                    localize.Add(kvp.Key, kvp.Value);
            }
            return localize;
        }

        public Localized<T> Clone()
        {
            Localized<T> clone = new();

            foreach (var (key, value) in this)
                clone[key] = value;

            return clone;
        }

        public override bool Equals(object? obj)
        {
            if (obj is not Localized<T> casted)
                return false;

            if (Count != casted.Count)
                return false;

            foreach (var kvp in this)
            {
                if (!casted.TryGetValue(kvp.Key, out var castedValue))
                    return false;

                if (kvp.Value == null)
                {
                    if (castedValue != null)
                        return false;
                }
                else if (!kvp.Value.Equals(castedValue))
                    return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            var hash = 13;
            var orderedKVPList = this.OrderBy(kvp => kvp.Key);
            foreach (var kvp in orderedKVPList)
            {
                hash = hash * 7 + kvp.Key.GetHashCode();
                if (kvp.Value != null)
                    hash = hash * 7 + kvp.Value.GetHashCode();
            }
            return hash;
        }
    }
}
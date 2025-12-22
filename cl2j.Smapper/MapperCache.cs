using System.Collections.Concurrent;

namespace cl2j.Smapper
{
    internal sealed class MapperCache
    {
        private static readonly ConcurrentDictionary<string, TypeMapper> cache = new();

        public static TypeMapper GetOrCreateMapper<TSource, TDestination>()
        {
            return GetOrCreateMapper(typeof(TSource), typeof(TDestination));
        }

        public static TypeMapper GetOrCreateMapper(Type source, Type destination)
        {
            var key = $"{source.FullName}->{destination.FullName}";
            if (cache.TryGetValue(key, out var mapper))
                return mapper;

            mapper = TypeMapper.Create(source, destination);
            return cache.GetOrAdd(key, mapper);
        }
    }
}
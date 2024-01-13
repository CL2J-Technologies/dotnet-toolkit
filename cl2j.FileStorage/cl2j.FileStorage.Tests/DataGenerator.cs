using System;
using System.IO;
using System.Linq;

namespace cl2j.FileStorage.Tests
{
    internal static class DataGenerator
    {
        private static readonly Random random = new();

        public static string GenerateFileName(string type, string container)
        {
            return Path.Combine($"{type}{container}", $"{random.Next(100_000, 1_000_000)}.txt");
        }

        public static string GenerateText()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var n = random.Next(100, 200);
            return new string(Enumerable.Repeat(chars, n).Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}
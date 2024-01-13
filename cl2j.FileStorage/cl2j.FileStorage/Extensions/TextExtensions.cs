using cl2j.FileStorage.Core;
using System.Text;

namespace cl2j.FileStorage.Extensions
{
    public static class TextExtensions
    {
        private static readonly Encoding DefaultEncoding = Encoding.UTF8;

        public static async Task<string> ReadTextAsync(this IFileStorageProvider fileStorageProvider, string fileName, Encoding? encoding = null)
        {
            using var stream = new MemoryStream();
            if (!await fileStorageProvider.ReadAsync(fileName, stream))
                return string.Empty;
            return stream.ToText(encoding ?? DefaultEncoding);
        }

        public static async Task WriteTextAsync(this IFileStorageProvider fileStorageProvider, string fileName, string text, Encoding? encoding = null)
        {
            using var stream = text.ToStream(encoding ?? DefaultEncoding);
            await fileStorageProvider.WriteAsync(fileName, stream);
        }

        public static async Task AppendTextAsync(this IFileStorageProvider fileStorageProvider, string fileName, string text, Encoding? encoding = null)
        {
            using var stream = text.ToStream(encoding ?? DefaultEncoding);
            await fileStorageProvider.AppendAsync(fileName, stream);
        }

        #region Private

        private static Stream ToStream(this string text, Encoding encoding)
        {
            if (text == null)
                return new MemoryStream();

            var ms = new MemoryStream(encoding.GetBytes(text));
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        private static string ToText(this MemoryStream stream, Encoding encoding)
        {
            return encoding.GetString(stream.GetBuffer(), 0, (int)stream.Length);
        }

        #endregion Private
    }
}
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using cl2j.FileStorage.Core;
using cl2j.FileStorage.Extensions;
using Xunit;

namespace cl2j.FileStorage.Tests
{
    internal class FileStorageTests
    {
        private readonly string name;

        public FileStorageTests(string name, IFileStorageProvider fileStorageProvider)
        {
            this.name = name;
            FileStorageProvider = fileStorageProvider;
        }

        public IFileStorageProvider FileStorageProvider { get; set; }

        public async Task WriteAsync_IfExists_DoNotThrow()
        {
            var fileName = DataGenerator.GenerateFileName(name, nameof(WriteAsync_IfExists_DoNotThrow));
            var text = DataGenerator.GenerateText();

            // Setup
            await FileStorageProvider.ClearDirectoryAsync(Path.GetDirectoryName(fileName));

            //Act
            await FileStorageProvider.WriteTextAsync(fileName, text);

            //Assert
            Assert.True(await FileStorageProvider.ExistsAsync(fileName));

            var text1Read = await FileStorageProvider.ReadTextAsync(fileName);
            Assert.Equal(text, text1Read);
        }

        public async Task AppendAsync_IfExists_DoNotThrow()
        {
            var fileName = DataGenerator.GenerateFileName(name, nameof(AppendAsync_IfExists_DoNotThrow));
            var text1 = DataGenerator.GenerateText();
            var text2 = DataGenerator.GenerateText();

            // Setup
            await FileStorageProvider.ClearDirectoryAsync(Path.GetDirectoryName(fileName));

            //Act
            await FileStorageProvider.AppendTextAsync(fileName, text1);
            await FileStorageProvider.AppendTextAsync(fileName, text2);

            //Assert
            Assert.True(await FileStorageProvider.ExistsAsync(fileName));

            var text1Read = await FileStorageProvider.ReadTextAsync(fileName);
            Assert.Equal(text1 + text2, text1Read);
        }

        public async Task ListAsync_IfExists_DoNotThrow()
        {
            var fileName1 = DataGenerator.GenerateFileName(name, nameof(ListAsync_IfExists_DoNotThrow));
            var fileName2 = DataGenerator.GenerateFileName(name, nameof(ListAsync_IfExists_DoNotThrow));
            var text1 = DataGenerator.GenerateText();
            var text2 = DataGenerator.GenerateText();

            // Setup
            await FileStorageProvider.ClearDirectoryAsync(Path.GetDirectoryName(fileName1));

            //Act
            await FileStorageProvider.WriteTextAsync(fileName1, text1);
            await FileStorageProvider.WriteTextAsync(fileName2, text2);

            //Assert
            var rootFiles = await FileStorageProvider.ListFilesAsync(Path.GetDirectoryName(fileName1));
            Assert.Equal(2, rootFiles.Count());
        }

        public async Task DeleteAsync_IfNotExists_DoNotThrow()
        {
            var fileName = DataGenerator.GenerateFileName(name, nameof(DeleteAsync_IfNotExists_DoNotThrow));
            var text = DataGenerator.GenerateText();

            // Setup
            await FileStorageProvider.ClearDirectoryAsync(Path.GetDirectoryName(fileName));

            //Act
            await FileStorageProvider.WriteTextAsync(fileName, text);
            await FileStorageProvider.DeleteAsync(fileName);

            //Assert
            Assert.False(await FileStorageProvider.ExistsAsync(fileName));
        }
    }
}
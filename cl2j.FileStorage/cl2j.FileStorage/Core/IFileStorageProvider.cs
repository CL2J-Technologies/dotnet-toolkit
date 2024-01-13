using Microsoft.Extensions.Configuration;

namespace cl2j.FileStorage.Core
{
    /// <summary>
    /// File store abstraction
    /// </summary>
    public interface IFileStorageProvider
    {
        /// <summary>
        /// The name of the FileStore.
        /// Used to query a specific instance of a IFileStore.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Initialize itself using the configuration
        /// </summary>
        /// <param name="configuration"></param>
        /// <returns></returns>
        void Initialize(string providerName, IConfigurationSection configuration);

        /// <summary>
        /// Determine if the specified key exists
        /// </summary>
        /// <param name="name">Name of the file to check</param>
        /// <returns></returns>
        Task<bool> ExistsAsync(string name);

        /// <summary>
        /// Retreive the information related to the file
        /// </summary>
        /// <param name="name">Name of the file to return the information</param>
        /// <returns>The file information of the name. null if the name doesn't exists</returns>
        Task<FileStoreFileInfo?> GetInfoAsync(string name);

        /// <summary>
        /// Return the list of all files contained in the path
        /// </summary>
        /// <param name="path">The path to list the objects</param>
        /// <returns>The list of names contained in the path</returns>
        Task<IEnumerable<string>> ListFilesAsync(string path);

        /// <summary>
        /// Return the list of all directory contained in the path
        /// </summary>
        /// <param name="path">The path to list the objects</param>
        /// <returns>The list of names contained in the path</returns>
        Task<IEnumerable<string>> ListFoldersAsync(string path);

        /// <summary>
        /// Get the specified file as a Stream. The caller is responsible to instance and dispose the stream.
        /// </summary>
        /// <param name="name">The name of the file to read</param>
        /// <param name="stream">The stream that will receive the file</param>
        /// <returns>true if the file was read properly otherwise false.</returns>
        Task<bool> ReadAsync(string name, Stream stream);

        /// <summary>
        /// Write the stream content to the file.
        /// If the file exists, it is overwritten.
        /// </summary>
        /// <param name="name">The name of the file to write to</param>
        /// <param name="stream">The content of the file to be written</param>
        Task WriteAsync(string name, Stream stream, string? contentType = null);

        /// <summary>
        /// Append the stream to the existing file.
        /// If the file doesn't exists it is created.
        /// </summary>
        /// <param name="name">The name of the file to append to</param>
        /// <param name="stream">The content to be added to the file</param>
        Task AppendAsync(string name, Stream stream);

        /// <summary>
        /// Delete the specified file
        /// </summary>
        /// <param name="name">The name of the file to be deleted</param>
        Task DeleteAsync(string name);
    }
}
using System;

namespace cl2j.FileStorage.Core
{
    public class FileStoreFileInfo
    {
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public DateTime Created { get; set; }
    }
}
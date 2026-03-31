namespace EIMSNext.Print.Pdf
{
    internal sealed class PdfTemporaryFileSession : IDisposable
    {
        private readonly string _rootDirectory;
        private readonly string _sessionDirectory;
        private readonly List<string> _filePaths = new();
        private bool _disposed;

        public PdfTemporaryFileSession(string rootDirectory)
        {
            _rootDirectory = rootDirectory;
            _sessionDirectory = Path.Combine(_rootDirectory, Guid.NewGuid().ToString("N"));
        }

        public IReadOnlyList<string> FilePaths => _filePaths;

        public string CreateFile(string extension, byte[] content)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PdfTemporaryFileSession));
            }

            var sanitizedExtension = string.IsNullOrWhiteSpace(extension) ? "bin" : extension.Trim().TrimStart('.');
            Directory.CreateDirectory(_sessionDirectory);
            var filePath = Path.Combine(_sessionDirectory, $"eimsnext-print-{Guid.NewGuid():N}.{sanitizedExtension}");
            File.WriteAllBytes(filePath, content);
            _filePaths.Add(filePath);
            return filePath;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            foreach (var filePath in _filePaths)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                }
                catch
                {
                }
            }

            _filePaths.Clear();

            try
            {
                if (Directory.Exists(_sessionDirectory))
                {
                    Directory.Delete(_sessionDirectory, true);
                }

                if (Directory.Exists(_rootDirectory) && !Directory.EnumerateFileSystemEntries(_rootDirectory).Any())
                {
                    Directory.Delete(_rootDirectory, false);
                }
            }
            catch
            {
            }

            _disposed = true;
        }
    }
}

namespace EIMSNext.Print.Pdf
{
    internal sealed class PdfTemporaryFileSession : IDisposable
    {
        private readonly string _rootDirectory;
        private readonly List<string> _filePaths = new();
        private bool _disposed;

        public PdfTemporaryFileSession(string rootDirectory)
        {
            _rootDirectory = rootDirectory;
        }

        public IReadOnlyList<string> FilePaths => _filePaths;

        public string CreateFile(string extension, byte[] content)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PdfTemporaryFileSession));
            }

            var sanitizedExtension = string.IsNullOrWhiteSpace(extension) ? "bin" : extension.Trim().TrimStart('.');
            Directory.CreateDirectory(_rootDirectory);
            var filePath = Path.Combine(_rootDirectory, $"eimsnext-print-{Guid.NewGuid():N}.{sanitizedExtension}");
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
                    // Best-effort cleanup. Rendering should not fail because temp files cannot be deleted.
                }
            }

            _filePaths.Clear();

            try
            {
                if (Directory.Exists(_rootDirectory) && !Directory.EnumerateFileSystemEntries(_rootDirectory).Any())
                {
                    Directory.Delete(_rootDirectory, false);
                }
            }
            catch
            {
                // Best-effort cleanup. Rendering should not fail because temp directories cannot be deleted.
            }

            _disposed = true;
        }
    }
}

namespace EIMSNext.Print.Abstractions
{
    public sealed class PrintResult : IDisposable, IAsyncDisposable
    {
        public Stream Content { get; internal set; } = Stream.Null;
        public string FileName { get; internal set; } = string.Empty;

        public void Dispose()
        {
            Content.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (Content is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else
            {
                Content.Dispose();
            }
        }
    }
}

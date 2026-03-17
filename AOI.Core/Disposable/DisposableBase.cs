namespace AOI.Core.Disposable;

public abstract class DisposableBase : IDisposable
{
    private bool _disposed;

    public void Dispose()
    {
        if (!_disposed)
        {
            DisposeManaged();

            _disposed = true;
        }
    }

    protected virtual void DisposeManaged()
    {
    }
}
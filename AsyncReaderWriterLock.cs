public class AsyncReaderWriterLock
{
    private const int MaxReaders = 10000; // Can be adjusted based on requirements
    private readonly SemaphoreSlim _readSemaphore = new SemaphoreSlim(MaxReaders, MaxReaders);
    private readonly SemaphoreSlim _writeSemaphore = new SemaphoreSlim(1, 1);
    private int _readCount = 0;
    private int _writeWaiters = 0;

    public async Task<IDisposable> ReaderLockAsync()
    {
        await WaitForReadLockAsync();
        return new AsyncDisposable(ReleaseReaderLock);
    }

    public async Task<IDisposable> WriterLockAsync()
    {
        Interlocked.Increment(ref _writeWaiters);
        try
        {
            await _writeSemaphore.WaitAsync();
            await _readSemaphore.WaitAsync(MaxReaders);
            return new AsyncDisposable(ReleaseWriterLock);
        }
        finally
        {
            Interlocked.Decrement(ref _writeWaiters);
        }
    }

    private async Task WaitForReadLockAsync()
    {
        while (true)
        {
            await _readSemaphore.WaitAsync();
            if (Interlocked.CompareExchange(ref _writeWaiters, 0, 0) == 0)
            {
                Interlocked.Increment(ref _readCount);
                if (_readCount == 1)
                {
                    try
                    {
                        await _writeSemaphore.WaitAsync();
                    }
                    catch
                    {
                        Interlocked.Decrement(ref _readCount);
                        _readSemaphore.Release();
                        throw;
                    }
                }
                return;
            }
            _readSemaphore.Release();
            await Task.Yield();
        }
    }

    private void ReleaseReaderLock()
    {
        if (Interlocked.Decrement(ref _readCount) == 0)
        {
            _writeSemaphore.Release();
        }
        _readSemaphore.Release();
    }

    private void ReleaseWriterLock()
    {
        int releaseCount = MaxReaders - _readSemaphore.CurrentCount;
        if (releaseCount > 0)
        {
            _readSemaphore.Release(releaseCount);
        }
        _writeSemaphore.Release();
    }

    private class AsyncDisposable : IDisposable
    {
        private readonly Action _disposeAction;

        public AsyncDisposable(Action disposeAction)
        {
            _disposeAction = disposeAction;
        }

        public void Dispose()
        {
            _disposeAction();
        }
    }
}

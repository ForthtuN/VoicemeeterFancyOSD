using System;
using System.Threading;
using System.Threading.Tasks;

namespace AtgDev.Utils;

internal sealed class AsyncDebouncer : IDisposable
{
    private readonly TimeSpan m_delay;
    private readonly object m_lock = new();
    private readonly SemaphoreSlim m_executionGate = new(1, 1);
    private CancellationTokenSource m_pending;
    private bool m_disposed;

    internal AsyncDebouncer(TimeSpan delay)
    {
        m_delay = delay;
    }

    internal void Schedule(Func<CancellationToken, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        CancellationToken token;
        lock (m_lock)
        {
            ObjectDisposedException.ThrowIf(m_disposed, this);
            m_pending?.Cancel();
            m_pending?.Dispose();
            m_pending = new CancellationTokenSource();
            token = m_pending.Token;
        }

        _ = RunAsync(action, token);
    }

    internal void CancelPending()
    {
        lock (m_lock)
        {
            if (m_disposed) return;
            m_pending?.Cancel();
            m_pending?.Dispose();
            m_pending = null;
        }
    }

    private async Task RunAsync(Func<CancellationToken, Task> action, CancellationToken token)
    {
        try
        {
            await Task.Delay(m_delay, token).ConfigureAwait(false);
            await m_executionGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                await action(token).ConfigureAwait(false);
            }
            finally
            {
                m_executionGate.Release();
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    public void Dispose()
    {
        lock (m_lock)
        {
            if (m_disposed) return;
            m_disposed = true;
            m_pending?.Cancel();
            m_pending?.Dispose();
            m_pending = null;
        }

        m_executionGate.Dispose();
    }
}

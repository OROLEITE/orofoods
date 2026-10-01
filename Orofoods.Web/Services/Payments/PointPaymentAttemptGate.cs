using System.Collections.Concurrent;

namespace Orofoods.Web.Services.Payments;

/// <summary>Serializes short in-process database phases independently for each Point order.</summary>
public sealed class PointPaymentAttemptGate
{
    private readonly ConcurrentDictionary<int, GateEntry> _gates = new();

    internal Action? BeforeEntryRetirementForTests { get; set; }

    public async ValueTask<IDisposable> AcquireAsync(int orderId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(orderId);

        while (true)
        {
            var entry = _gates.GetOrAdd(orderId, static _ => new GateEntry());
            var registered = false;
            lock (entry.Sync)
            {
                if (!entry.Retired
                    && _gates.TryGetValue(orderId, out var current)
                    && ReferenceEquals(current, entry))
                {
                    entry.ReferenceCount++;
                    registered = true;
                }
            }
            if (!registered)
            {
                continue;
            }

            try
            {
                await entry.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                return new Lease(this, orderId, entry);
            }
            catch
            {
                ReleaseReference(orderId, entry);
                throw;
            }
        }
    }

    private void Release(int orderId, GateEntry entry)
    {
        entry.Semaphore.Release();
        ReleaseReference(orderId, entry);
    }

    private void ReleaseReference(int orderId, GateEntry entry)
    {
        lock (entry.Sync)
        {
            entry.ReferenceCount--;
            if (entry.ReferenceCount == 0)
            {
                BeforeEntryRetirementForTests?.Invoke();
                entry.Retired = true;
                ((ICollection<KeyValuePair<int, GateEntry>>)_gates).Remove(new KeyValuePair<int, GateEntry>(orderId, entry));
            }
        }
    }

    private sealed class GateEntry
    {
        public object Sync { get; } = new();
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int ReferenceCount;
        public bool Retired;
    }

    private sealed class Lease(PointPaymentAttemptGate owner, int orderId, GateEntry entry) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.Release(orderId, entry);
            }
        }
    }
}

using Orofoods.Web.Services.Payments;

namespace Orofoods.Web.Tests.Services;

public sealed class PointPaymentAttemptGateTests
{
    [Fact]
    public async Task Acquiring_a_different_order_does_not_wait_for_the_first_order()
    {
        var gate = new PointPaymentAttemptGate();
        using var firstOrder = await gate.AcquireAsync(100);

        using var secondOrder = await gate.AcquireAsync(101).AsTask().WaitAsync(TimeSpan.FromMilliseconds(250));
    }

    [Fact]
    public async Task Concurrent_acquisitions_for_the_same_order_wait_for_the_current_holder()
    {
        var gate = new PointPaymentAttemptGate();
        using var first = await gate.AcquireAsync(100);
        var secondTask = gate.AcquireAsync(100).AsTask();

        Assert.False(secondTask.IsCompleted);
        first.Dispose();
        using var second = await secondTask.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Cancelled_waiter_does_not_leave_the_order_gate_locked()
    {
        var gate = new PointPaymentAttemptGate();
        using var first = await gate.AcquireAsync(100);
        using var cancellation = new CancellationTokenSource();
        var wait = gate.AcquireAsync(100, cancellation.Token).AsTask();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
        first.Dispose();
        using var next = await gate.AcquireAsync(100).AsTask().WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Reacquisition_during_gate_entry_retirement_never_creates_two_holders()
    {
        var gate = new PointPaymentAttemptGate();
        var activeHolders = 0;
        var concurrentHolderViolations = 0;

        var workers = Enumerable.Range(0, 24).Select(async _ =>
        {
            for (var iteration = 0; iteration < 250; iteration++)
            {
                using (await gate.AcquireAsync(100))
                {
                    if (Interlocked.Increment(ref activeHolders) != 1)
                    {
                        Interlocked.Increment(ref concurrentHolderViolations);
                    }
                    await Task.Yield();
                    Interlocked.Decrement(ref activeHolders);
                }
                if (iteration % 8 == 0) await Task.Yield();
            }
        });

        await Task.WhenAll(workers);

        Assert.Equal(0, concurrentHolderViolations);
    }

    [Fact]
    public async Task Waiter_cannot_join_an_entry_after_its_last_reference_begins_retirement()
    {
        var gate = new PointPaymentAttemptGate();
        using var retirementStarted = new ManualResetEventSlim();
        using var finishRetirement = new ManualResetEventSlim();
        gate.BeforeEntryRetirementForTests = () =>
        {
            retirementStarted.Set();
            finishRetirement.Wait(TimeSpan.FromSeconds(5));
        };
        var first = await gate.AcquireAsync(100);

        var releaseFirst = Task.Run(first.Dispose);
        Assert.True(retirementStarted.Wait(TimeSpan.FromSeconds(5)));
        var secondTask = Task.Run(async () => await gate.AcquireAsync(100));
        var secondJoinedRetiringEntry = await Task.WhenAny(secondTask, Task.Delay(TimeSpan.FromMilliseconds(100))) == secondTask;
        finishRetirement.Set();
        await releaseFirst.WaitAsync(TimeSpan.FromSeconds(5));
        using var second = await secondTask.WaitAsync(TimeSpan.FromSeconds(5));

        var thirdTask = gate.AcquireAsync(100).AsTask();
        await Task.Delay(TimeSpan.FromMilliseconds(50));
        Assert.False(secondJoinedRetiringEntry);
        Assert.False(thirdTask.IsCompleted);
        second.Dispose();
        using var third = await thirdTask.WaitAsync(TimeSpan.FromSeconds(5));
    }
}

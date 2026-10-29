using Orofoods.Web.Models.Integrations;

namespace Orofoods.Web.Tests.Integrations;

public sealed class WmcJsonOrderWorkerRuntimeTests
{
    [Fact]
    public void Runtime_distinguishes_hosted_service_from_active_cycle()
    {
        var runtimeType = typeof(WmcActivationDiagnostics).Assembly.GetType("Orofoods.Web.Integrations.Erp.Wmc.WmcJsonOrderWorkerRuntime");
        Assert.NotNull(runtimeType);
        var runtime = Activator.CreateInstance(runtimeType)!;
        var startedAt = DateTime.UtcNow;

        Invoke(runtime, "MarkServiceStarted", startedAt);
        var startedSnapshot = Get(runtime, "Snapshot");
        Assert.True((bool)Get(startedSnapshot, "IsServiceRunning"));
        Assert.Null(Get(startedSnapshot, "CurrentCycleStartedAtUtc"));

        Invoke(runtime, "BeginCycle", startedAt.AddMinutes(5));
        var cycleSnapshot = Get(runtime, "Snapshot");
        Assert.Equal(startedAt.AddMinutes(5), Get(cycleSnapshot, "CurrentCycleStartedAtUtc"));

        var assembly = typeof(WmcActivationDiagnostics).Assembly;
        var selectionType = assembly.GetType("Orofoods.Web.Models.Integrations.WmcJsonOrderCandidateSelectionSummary");
        Assert.NotNull(selectionType);
        var selection = Activator.CreateInstance(selectionType, 23, 20, 1, 1, 1, 3)!;
        Invoke(runtime, "SetCandidateSelection", selection);
        Invoke(runtime, "RecordAttempt", true);
        Invoke(runtime, "RecordAttempt", false);
        Invoke(runtime, "RecordReservationRejected");
        Invoke(runtime, "RecordCycleError", "NpgsqlException");

        Invoke(runtime, "FinishCycle", startedAt.AddMinutes(5).AddSeconds(20));
        var completedSnapshot = Get(runtime, "Snapshot");
        var summary = Get(completedSnapshot, "LastCycle");
        Assert.Equal(23, Get(summary, "EligibleCount"));
        Assert.Equal(20, Get(summary, "CandidateCount"));
        Assert.Equal(3, Get(summary, "ProcessedCount"));
        Assert.Equal(1, Get(summary, "SucceededCount"));
        Assert.Equal(1, Get(summary, "FailedCount"));
        Assert.Equal(1, Get(summary, "ReservationRejectedCount"));
        Assert.Equal(1, Get(summary, "CycleErrorCount"));
        Assert.Equal("NpgsqlException", Get(summary, "LastSafeErrorType"));

        Invoke(runtime, "BeginCycle", startedAt.AddMinutes(6));
        Invoke(runtime, "MarkServiceStopped", startedAt.AddMinutes(6).AddSeconds(1));
        var stoppedSnapshot = Get(runtime, "Snapshot");
        Assert.False((bool)Get(stoppedSnapshot, "IsServiceRunning"));
        Assert.Null(Get(stoppedSnapshot, "CurrentCycleStartedAtUtc"));
    }

    private static void Invoke(object instance, string method, params object?[] args)
    {
        var target = instance.GetType().GetMethods().SingleOrDefault(candidate => candidate.Name == method && candidate.GetParameters().Length == args.Length);
        Assert.NotNull(target);
        target.Invoke(instance, args);
    }

    private static object Get(object instance, string property)
    {
        var target = instance.GetType().GetProperty(property);
        Assert.NotNull(target);
        return target.GetValue(instance)!;
    }
}

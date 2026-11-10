namespace Orofoods.Web.Services.Commercial;

public static class WhatsAppServiceWindow
{
    private static readonly TimeSpan WindowDuration = TimeSpan.FromHours(24);

    public static bool IsOpen(DateTime? lastInboundAt, DateTime nowUtc)
    {
        if (lastInboundAt is null)
            return false;

        var inboundUtc = AsUtc(lastInboundAt.Value);
        var currentUtc = AsUtc(nowUtc);
        return currentUtc < inboundUtc.Add(WindowDuration);
    }

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}

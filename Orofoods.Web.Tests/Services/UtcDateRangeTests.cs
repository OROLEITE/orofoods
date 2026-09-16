using Orofoods.Web.Services;

namespace Orofoods.Web.Tests.Services;

public class UtcDateRangeTests
{
    [Fact]
    public void Month_start_is_a_utc_timestamp()
    {
        var result = UtcDateRange.MonthStart(new DateTime(2026, 8, 30, 14, 30, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }
}

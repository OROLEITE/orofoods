using Microsoft.Extensions.Logging;

namespace Orofoods.Web.Infrastructure.Logging;

public static class LoggingBuilderExtensions
{
    public static ILoggingBuilder AddDailyFile(
        this ILoggingBuilder builder,
        string directoryPath,
        LogLevel minimumLevel)
        => builder.AddProvider(new DailyFileLoggerProvider(directoryPath, minimumLevel));
}

using System.Diagnostics.Tracing;
using System.Text.RegularExpressions;
using Azure.Core.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Orofoods.Web.Infrastructure.Diagnostics;

internal static partial class AzureIdentityDiagnostics
{
    private static readonly object SyncRoot = new();
    private static AzureEventSourceListener? listener;

    internal static bool IsEnabled(IHostEnvironment environment, IConfiguration configuration) =>
        environment.IsStaging() && configuration.GetValue<bool>("Diagnostics:AzureIdentity");

    internal static void EnableIfConfigured(
        IHostEnvironment environment,
        IConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        if (!IsEnabled(environment, configuration))
        {
            return;
        }

        lock (SyncRoot)
        {
            listener ??= new AzureEventSourceListener(
                eventData => LogEvent(eventData, loggerFactory.CreateLogger("AzureSdkDiagnostics")),
                EventLevel.Informational);
        }
    }

    internal static string Redact(string message)
    {
        var redacted = SensitiveAssignmentRegex().Replace(message, "$1[REDACTED]");
        return JwtRegex().Replace(redacted, "[REDACTED]");
    }

    private static void LogEvent(EventWrittenEventArgs eventData, ILogger logger)
    {
        if (!IsRelevantSource(eventData.EventSource.Name) || string.IsNullOrWhiteSpace(eventData.Message))
        {
            return;
        }

        logger.LogInformation(
            "Azure SDK diagnostic {EventSource} {EventName}: {Message}",
            eventData.EventSource.Name,
            eventData.EventName,
            Redact(eventData.Message));
    }

    private static bool IsRelevantSource(string sourceName) =>
        sourceName is "Azure-Identity" or "Azure-Core" or "Azure-Storage-Blobs";

    [GeneratedRegex(@"(?i)(authorization|access[_-]?token|refresh[_-]?token|client[_-]?secret|identity[_-]?header|msi[_-]?secret|password|sig|sharedaccesssignature)\s*([:=])\s*([^\s,;&]+)")]
    private static partial Regex SensitiveAssignmentRegex();

    [GeneratedRegex(@"eyJ[a-zA-Z0-9_-]+\.[a-zA-Z0-9_-]+\.[a-zA-Z0-9_-]+")]
    private static partial Regex JwtRegex();
}
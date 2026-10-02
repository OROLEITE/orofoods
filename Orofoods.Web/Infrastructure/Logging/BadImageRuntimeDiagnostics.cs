using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.AspNetCore.Http;

namespace Orofoods.Web.Infrastructure.Logging;

internal static class BadImageRuntimeDiagnostics
{
    public static readonly EventId WmcOptionsEvent = new(7101, "BadImage.WmcOptions");
    public static readonly EventId HttpRequestEvent = new(7102, "BadImage.HttpRequest");
    public static readonly EventId EfMetadataEvent = new(7103, "BadImage.EfMetadata");

    public static BadImageFormatException? FindBadImageException(Exception exception)
    {
        var pending = new Stack<Exception>();
        var visited = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        pending.Push(exception);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!visited.Add(current))
            {
                continue;
            }

            if (current is BadImageFormatException badImage)
            {
                return badImage;
            }

            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    pending.Push(inner);
                }
            }
            else if (current.InnerException is not null)
            {
                pending.Push(current.InnerException);
            }
        }

        return null;
    }

    public static AssemblyDiagnostic Describe(Assembly assembly)
    {
        var name = assembly.GetName();
        var loadContext = AssemblyLoadContext.GetLoadContext(assembly);
        string? location;
        try
        {
            location = string.IsNullOrEmpty(assembly.Location) ? null : assembly.Location;
        }
        catch (NotSupportedException)
        {
            location = null;
        }

        return new AssemblyDiagnostic(
            name.Name,
            name.Version?.ToString(),
            location,
            loadContext?.Name,
            loadContext?.IsCollectible,
            ReferenceEquals(loadContext, AssemblyLoadContext.Default));
    }

    public static void LogWmcOptionsFailure(
        ILogger logger,
        Exception exception,
        Type optionsType)
    {
        var badImage = FindBadImageException(exception);
        if (badImage is null)
        {
            return;
        }

        var assembly = Describe(optionsType.Assembly);
        var details = DescribeException(badImage);
        logger.LogError(
            WmcOptionsEvent,
            "BadImageFormatException during {Operation} in {Worker}. ExceptionType={ExceptionType}; ExceptionMessage={ExceptionMessage}; HResult={HResult}; ExceptionSource={ExceptionSource}; TargetType={TargetType}; TargetMethod={TargetMethod}; TopApplicationFrame={TopApplicationFrame}; TopFrameworkFrame={TopFrameworkFrame}; InnerExceptionType={InnerExceptionType}; InnerExceptionMessage={InnerExceptionMessage}; AssemblyName={AssemblyName}; AssemblyVersion={AssemblyVersion}; AssemblyLocation={AssemblyLocation}; LoadContextName={LoadContextName}; IsCollectible={IsCollectible}; IsDefaultContext={IsDefaultContext}",
            "options resolution / options construction",
            "ErpRetryBackgroundService",
            badImage.GetType().FullName,
            badImage.Message,
            badImage.HResult,
            badImage.Source,
            details.TargetType,
            details.TargetMethod,
            details.TopApplicationFrame,
            details.TopFrameworkFrame,
            details.InnerExceptionType,
            details.InnerExceptionMessage,
            assembly.Name,
            assembly.Version,
            assembly.Location,
            assembly.LoadContextName,
            assembly.IsCollectible,
            assembly.IsDefaultContext);
    }

    public static void LogRequestFailure(
        ILogger logger,
        HttpContext context,
        Exception exception)
    {
        var badImage = FindBadImageException(exception);
        if (badImage is null)
        {
            return;
        }

        var details = DescribeException(badImage);
        var targetAssembly = badImage.TargetSite?.DeclaringType?.Assembly;
        var assembly = targetAssembly is null ? null : Describe(targetAssembly);
        var isEfMetadata = details.Frames.Any(frame =>
            frame.StartsWith("Microsoft.EntityFrameworkCore.", StringComparison.Ordinal));
        var dbContextType = isEfMetadata
            ? "Orofoods.Web.Data.PostgreSqlApplicationDbContext"
            : null;

        var eventId = isEfMetadata ? EfMetadataEvent : HttpRequestEvent;
        var operationCategory = isEfMetadata ? GetEfOperationCategory(details.Frames) : null;
        logger.LogError(
            eventId,
            "BadImageFormatException during HTTP request. TimestampUtc={TimestampUtc}; TraceIdentifier={TraceIdentifier}; ActivityTraceId={ActivityTraceId}; ActivitySpanId={ActivitySpanId}; HttpMethod={HttpMethod}; RequestPath={RequestPath}; ResponseStatusCode={ResponseStatusCode}; Endpoint={Endpoint}; ExceptionType={ExceptionType}; ExceptionMessage={ExceptionMessage}; HResult={HResult}; ExceptionSource={ExceptionSource}; TargetType={TargetType}; TargetMethod={TargetMethod}; TopApplicationFrame={TopApplicationFrame}; TopFrameworkFrame={TopFrameworkFrame}; InnerExceptionType={InnerExceptionType}; InnerExceptionMessage={InnerExceptionMessage}; AssemblyName={AssemblyName}; AssemblyVersion={AssemblyVersion}; AssemblyLocation={AssemblyLocation}; LoadContextName={LoadContextName}; IsCollectible={IsCollectible}; IsDefaultContext={IsDefaultContext}; DbContextType={DbContextType}; OperationCategory={OperationCategory}",
            DateTimeOffset.UtcNow,
            context.TraceIdentifier,
            Activity.Current?.TraceId.ToString(),
            Activity.Current?.SpanId.ToString(),
            context.Request.Method,
            context.Request.Path.Value,
            context.Response.HasStarted ? context.Response.StatusCode : null,
            context.GetEndpoint()?.DisplayName,
            badImage.GetType().FullName,
            badImage.Message,
            badImage.HResult,
            badImage.Source,
            details.TargetType,
            details.TargetMethod,
            details.TopApplicationFrame,
            details.TopFrameworkFrame,
            details.InnerExceptionType,
            details.InnerExceptionMessage,
            assembly?.Name,
            assembly?.Version,
            assembly?.Location,
            assembly?.LoadContextName,
            assembly?.IsCollectible,
            assembly?.IsDefaultContext,
            dbContextType,
            operationCategory);
    }

    private static ExceptionDiagnostic DescribeException(Exception exception)
    {
        var frames = new StackTrace(exception, fNeedFileInfo: false)
            .GetFrames()?
            .Select(frame => frame.GetMethod())
            .Where(method => method is not null)
            .Select(method =>
            {
                var declaringType = method!.DeclaringType;
                return declaringType is null
                    ? method.Name
                    : $"{declaringType.FullName}.{method.Name}";
            })
            .ToArray() ?? [];

        var target = exception.TargetSite;
        var targetType = target?.DeclaringType;
        var inner = exception.InnerException;

        return new ExceptionDiagnostic(
            targetType?.FullName,
            target?.Name,
            frames.FirstOrDefault(frame => frame.StartsWith("Orofoods.", StringComparison.Ordinal)),
            frames.FirstOrDefault(frame =>
                frame.StartsWith("Microsoft.", StringComparison.Ordinal) ||
                frame.StartsWith("System.", StringComparison.Ordinal)),
            inner?.GetType().FullName,
            inner?.Message,
            frames);
    }

    private static string GetEfOperationCategory(IReadOnlyList<string> frames)
    {
        if (frames.Any(frame => frame.Contains("SaveChanges", StringComparison.Ordinal)))
        {
            return "save";
        }

        if (frames.Any(frame => frame.Contains("Query", StringComparison.Ordinal) ||
                                frame.Contains("Execute", StringComparison.Ordinal)))
        {
            return "query";
        }

        if (frames.Any(frame => frame.Contains("CreateModel", StringComparison.Ordinal) ||
                                frame.Contains("Model", StringComparison.Ordinal)))
        {
            return "model-building";
        }

        return "unknown";
    }

    internal sealed record AssemblyDiagnostic(
        string? Name,
        string? Version,
        string? Location,
        string? LoadContextName,
        bool? IsCollectible,
        bool? IsDefaultContext);

    private sealed record ExceptionDiagnostic(
        string? TargetType,
        string? TargetMethod,
        string? TopApplicationFrame,
        string? TopFrameworkFrame,
        string? InnerExceptionType,
        string? InnerExceptionMessage,
        string[] Frames);
}

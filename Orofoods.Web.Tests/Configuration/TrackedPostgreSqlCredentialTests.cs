using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Orofoods.Web.Tests.Configuration;

public class TrackedPostgreSqlCredentialTests
{
    private static readonly Regex PostgreSqlConnectionLine = new(
        @"^(?=.*(?:host|server)\s*=)(?=.*database\s*=)(?=.*(?:username|user\s+id)\s*=)(?=.*(?:password|pwd)\s*=).+$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex PasswordField = new(
        @"(?:password|pwd)\s*=\s*(?<password>[^;\r\n\""'`]*)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ExplicitPlaceholder = new(
        @"^<[^>]+>$|^\$\{?[A-Z][A-Z0-9_]*\}?$",
        RegexOptions.CultureInvariant);

    private static readonly Regex DocumentationPlaceholder = new(
        @"^(?=[A-Z][A-Z0-9_-]*$)(?=.*(?:PASSWORD|SENHA|SECRET|CHANGE|REPLACE)).+$",
        RegexOptions.CultureInvariant);

    [Fact]
    public void Tracked_repository_has_no_secret_bearing_postgresql_connection_strings()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var findings = GetTrackedFiles(repositoryRoot)
            .SelectMany(path => FindSecretBearingConnectionStrings(repositoryRoot, path))
            .ToArray();

        Assert.True(
            findings.Length == 0,
            $"Tracked PostgreSQL credential findings:{Environment.NewLine}{string.Join(Environment.NewLine, findings)}");
    }

    private static IEnumerable<string> GetTrackedFiles(string repositoryRoot)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("ls-files");
        startInfo.ArgumentList.Add("-z");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to enumerate tracked repository files.");
        var output = process.StandardOutput.ReadToEnd();
        _ = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Unable to enumerate tracked repository files (exit {process.ExitCode}).");
        }

        return output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
    }

    private static IEnumerable<string> FindSecretBearingConnectionStrings(string repositoryRoot, string relativePath)
    {
        var fullPath = Path.Combine(repositoryRoot, relativePath);
        byte[] bytes;

        try
        {
            bytes = File.ReadAllBytes(fullPath);
        }
        catch (IOException)
        {
            yield break;
        }

        if (bytes.Contains((byte)0))
        {
            yield break;
        }

        var lines = Encoding.UTF8.GetString(bytes).Split('\n');
        var isDocumentation = relativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
        var requiresExplicitPlaceholder = relativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || relativePath.Equals(
                "docs/superpowers/plans/2026-08-30-postgresql-and-transactional-layout.md",
                StringComparison.Ordinal);

        for (var index = 0; index < lines.Length; index++)
        {
            if (!PostgreSqlConnectionLine.IsMatch(lines[index]))
            {
                continue;
            }

            foreach (Match match in PasswordField.Matches(lines[index]))
            {
                var password = match.Groups["password"].Value.Trim();
                if (IsSafePlaceholder(password, isDocumentation, requiresExplicitPlaceholder))
                {
                    continue;
                }

                yield return $"{relativePath}:{index + 1} POSTGRESQL_SECRET_BEARING";
            }
        }
    }

    private static bool IsSafePlaceholder(
        string password,
        bool isDocumentation,
        bool requiresExplicitPlaceholder) =>
        string.IsNullOrEmpty(password)
        || ExplicitPlaceholder.IsMatch(password)
        || !requiresExplicitPlaceholder && isDocumentation && DocumentationPlaceholder.IsMatch(password);
}

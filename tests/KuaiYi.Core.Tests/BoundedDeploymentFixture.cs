namespace KuaiYi.Core.Tests;

// Test-only path selection for one externally supervised ordinary case. No
// production installer, receipt or source authority reads this environment key.
internal static class BoundedDeploymentFixture
{
    internal const string Variable = "KUAIYI_BOUNDED_DEPLOYMENT_ROOT";
    private static int claimed;

    internal static (string Root, string Pipe) CreatePaths()
    {
        string? selected = Environment.GetEnvironmentVariable(Variable);
        if (selected is null)
            return (Path.Combine(Path.GetTempPath(), "kuaiyi-upgrade-tests-" + Guid.NewGuid().ToString("N")),
                "ky-deployment-session-" + Guid.NewGuid().ToString("N"));

        if (!OperatingSystem.IsLinux() || string.IsNullOrWhiteSpace(selected) ||
            !Path.IsPathFullyQualified(selected) || Path.GetFullPath(selected) != selected)
            throw new InvalidDataException("Bounded fixture requires a canonical Linux directory.");
        const string prefix = "kuaiyi-upgrade-tests-";
        string name = Path.GetFileName(selected);
        if (!name.StartsWith(prefix, StringComparison.Ordinal) ||
            !Guid.TryParseExact(name[prefix.Length..], "N", out _))
            throw new InvalidDataException("Bounded fixture root name is invalid.");
        string pipe = Path.Combine(selected, "peer.sock");
        if (System.Text.Encoding.UTF8.GetByteCount(pipe) > 100)
            throw new InvalidDataException("Bounded fixture pipe exceeds its Unix path allowance.");
        for (DirectoryInfo? current = new(selected); current is not null; current = current.Parent)
            if (!current.Exists || current.LinkTarget is not null ||
                (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Bounded fixture ancestor is missing or linked.");
        if (File.GetUnixFileMode(selected) !=
            (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute) ||
            Directory.EnumerateFileSystemEntries(selected).Any())
            throw new InvalidDataException("Bounded fixture must be precreated, private and empty.");
        if (Interlocked.CompareExchange(ref claimed, 1, 0) != 0)
            throw new InvalidOperationException("Only one bounded fixture may be claimed by this test process.");
        return (selected, pipe);
    }
}

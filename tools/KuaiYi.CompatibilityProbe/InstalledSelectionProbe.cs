using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using KuaiYi.Core;
using KuaiYi.Contracts;
using KuaiYi.NativeHost;
using KuaiYi.Windows;
namespace KuaiYi.CompatibilityProbe;

internal sealed record SelectionCaseEvidence(int Index, string Kind, string Outcome, bool Captured, bool Matches, bool ForegroundStable, bool MarkerStable, bool BrokerSettled,SelectionBrokerObservation? BrokerObservation=null,int? BrokerExitCode=null,SelectionReachedBoundary? LastReachedBoundary=null,string BoundaryMarkerSetup="Disabled",System.Collections.Immutable.ImmutableArray<SelectionPhaseTiming>? PhaseTimings=null);
internal sealed record SelectionViewEvidence(string Application, string Version, string View, string Status, string Reason, bool SequenceCompleted, int Planned, int Correct, int Mismatches, bool BrokerCleanup, bool ApplicationCleanup, bool ProfileDeleted, SelectionCaseEvidence[] Cases);
internal sealed record SelectionProbeReceipt(string Kind, string SourceCommit, string Scope, string Accessibility, string PhysicalSelection, string Windows11Acceptance, int ModelCalls, int Planned, SelectionViewEvidence[] Views,string Backend="ManagedUia");
internal static class InstalledSelectionProbe
{
    internal static async Task<int> RunAsync(string broker, string receipt, string chrome, string edge,bool diagnosticOnly=false,bool comDiagnostic=false,bool timingDiagnostic=false)
    {
        if(timingDiagnostic&&(!diagnosticOnly||!comDiagnostic))return 2;
        if(comDiagnostic&&(!diagnosticOnly||Path.GetFileName(broker)!="KuaiYi.ComCaptureProbe.exe"))return 2;
        if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("CI") != "true" ||
            Environment.GetEnvironmentVariable("GITHUB_SHA") is not { } source || !Regex.IsMatch(source, "^[0-9a-f]{40}$") || !File.Exists(broker)) return 2;
        var views = new List<SelectionViewEvidence>(); bool continueAllowed = true;
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(8));
        foreach (var app in new[] { (Name: "Chrome", Path: chrome), (Name: "Edge", Path: edge) })
        {
            if(timingDiagnostic&&app.Name!="Chrome")continue;
            bool installed = IsFixedInstalledPath(app.Name, app.Path);
            string version = installed ? FileVersionInfo.GetVersionInfo(app.Path).FileVersion ?? "Unknown" : "Unknown";
            if (!Regex.IsMatch(version, "^[0-9]+(\\.[0-9]+){1,3}$")) version = "Unknown";
            foreach (string view in PublicSelectionFixtures.Views)
            {
                if(timingDiagnostic&&view!="Html")continue;
                var cases = PublicSelectionFixtures.Cases().Take(diagnosticOnly?1:20).ToArray();
                var results = cases.Select((c, i) => new SelectionCaseEvidence(i, c.Kind, "NotRun", false, false, false, false, false)).ToArray();
                if (!installed || !continueAllowed || deadline.IsCancellationRequested)
                {
                    views.Add(new(app.Name, version, view, "Blocked", !installed ? "UnavailableOrUnverified" : "PreviousCleanupOrDeadline", false, cases.Length, 0, 0, false, false, false, results));
                    continue;
                }
                string profile = Path.Combine(Path.GetTempPath(), "KuaiYi-public-selection-" + Guid.NewGuid().ToString("N"));
                string marker = Guid.NewGuid().ToString("N"); string prefix = "KYSEL-" + marker + "-";
                OwnedApplicationJob? application = null; SelectionBrokerClient? client = null;
                bool brokerCleanup = true, applicationCleanup = false, profileDeleted = false, sequenceCompleted = false;
                string reason = "Completed";
                try
                {
                    Directory.CreateDirectory(profile);
                    Directory.CreateDirectory(Path.Combine(profile, "local")); Directory.CreateDirectory(Path.Combine(profile, "roaming"));
                    string file = Path.Combine(profile, "public.html");
                    await File.WriteAllTextAsync(file, PublicSelectionFixtures.Html(view, marker,cases.Length), deadline.Token);
                    application = await OwnedApplicationJob.StartAsync(app.Path,
                        ["--user-data-dir=" + Path.Combine(profile, "browser"), "--no-first-run", "--no-default-browser-check", "--disable-background-networking", "--app=" + new Uri(file).AbsoluteUri], profile);
                    client = new SelectionBrokerClient(Path.GetFullPath(broker),boundaryDiagnostic:diagnosticOnly,timingDiagnostic:timingDiagnostic);
                    var watch = Stopwatch.StartNew(); int previous = -1;
                    while (watch.Elapsed < TimeSpan.FromSeconds(80) && !deadline.IsCancellationRequested)
                    {
                        nint window = application.FindWindow(prefix);
                        string? value = application.CaseMarker(window, prefix);
                        if (value == "DO") { sequenceCompleted = true; break; }
                        if (int.TryParse(value, out int index) && index >=0 && index<cases.Length && index > previous)
                        {
                            previous = index;
                            if (NativeDesktop.GetForegroundWindow() != window) application.Activate(window);
                            await Task.Delay(120, deadline.Token);
                            if (!application.Owns(window) || application.CaseMarker(window, prefix) != value || NativeDesktop.GetForegroundWindow() != window || !NativeDesktop.GetWindowRect(window, out var rect))
                            { results[index] = results[index] with { Outcome = "SourceNotCurrent" }; continue; }
                            PixelPoint point = rect.ToPixels().Center;
                            SelectionWindow? sourceWindow = SelectionDesktop.Resolve(window.ToInt64());
                            if (sourceWindow == null || !application.OwnsSource(sourceWindow) || !SelectionDesktop.IsCurrent(sourceWindow) || application.CaseMarker(window, prefix) != value)
                            { results[index] = results[index] with { Outcome = "SourceNotCurrent" }; continue; }
                            // This tests explicit UIA capture, not injected mouse-hook admission or translation.
                            var captured = await client.CaptureAsync(new(sourceWindow, point, point, Environment.TickCount64, 0, true), deadline.Token);
                            bool foreground = application.OwnsSource(sourceWindow) && SelectionDesktop.IsCurrent(sourceWindow);
                            bool stable = application.CaseMarker(window, prefix) == value;
                            bool matches = captured.Status == SelectionCaptureStatus.Success && foreground && stable && (comDiagnostic?string.Equals(captured.Text,cases[index].Expected,StringComparison.Ordinal):PublicSelectionFixtures.Matches(captured.Text, cases[index].Expected));
                            var cleanupWatch = Stopwatch.StartNew();
                            while (client.ExitState != null && cleanupWatch.Elapsed < TimeSpan.FromSeconds(2)) await Task.Delay(20);
                            bool settled = client.ExitState == null;
                            results[index] = new(index, cases[index].Kind, captured.Status.ToString(), captured.Status == SelectionCaptureStatus.Success, matches && settled, foreground, stable, settled,client.LastObservation,client.LastProcessExitCode,client.LastReachedBoundary,client.BoundaryMarkerSetup,client.LastPhaseTimings);
                            captured = null!;
                            if (!settled) { brokerCleanup = false; reason = "BrokerCleanupUnconfirmed"; break; }
                        }
                        await Task.Delay(30, deadline.Token);
                    }
                    if (!sequenceCompleted && reason == "Completed") reason = deadline.IsCancellationRequested ? "ProbeDeadline" : "SequenceUnconfirmed";
                }
                catch (OperationCanceledException) when (deadline.IsCancellationRequested) { reason = "ProbeDeadline"; }
                catch (Exception error) when (error is IOException or System.ComponentModel.Win32Exception or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
                { reason = "FixtureOrSourceFailure"; }
                finally
                {
                    if (client != null)
                    {
                        try { await client.DisposeAsync(); brokerCleanup &= client.ExitState == null; }
                        catch (IOException) { brokerCleanup = false; reason = "BrokerCleanupUnconfirmed"; }
                    }
                    if (application != null) applicationCleanup = await application.StopAsync();
                    // A failed suspended launch is finalized by StartAsync; an unconfirmed launch throws IOException.
                    if (application == null) { applicationCleanup = reason != "BrokerCleanupUnconfirmed" && reason != "FixtureOrSourceFailure"; }
                    if (brokerCleanup && applicationCleanup)
                    {
                        try { Directory.Delete(profile, true); profileDeleted = !Directory.Exists(profile); }
                        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { reason = "ProfileDeletionFailed"; }
                    }
                    continueAllowed = brokerCleanup && applicationCleanup && profileDeleted;
                }
                int correct = results.Count(c => c.Matches);
                int mismatches = results.Count(c => c.Captured && c.ForegroundStable && c.MarkerStable && !c.Matches);
                bool pass = reason == "Completed" && sequenceCompleted && correct >= 19 && mismatches == 0 && brokerCleanup && applicationCleanup && profileDeleted;
                bool observed = reason=="Completed"&&sequenceCompleted&&brokerCleanup&&applicationCleanup&&profileDeleted&&results.All(c=>c.BrokerSettled);
                views.Add(new(app.Name, version, view, diagnosticOnly?(observed?"Observed":"Fail"):pass?"Pass":"Fail", reason, sequenceCompleted, cases.Length, correct, mismatches, brokerCleanup, applicationCleanup, profileDeleted, results));
            }
        }
        var report = new SelectionProbeReceipt(timingDiagnostic?"HostedInstalledSelectionTimingDiagnostic":diagnosticOnly?"HostedInstalledSelectionDiagnostic":"HostedInstalledSelection", source, timingDiagnostic?"TimingDiagnosticOnly":diagnosticOnly?"DiagnosticOnly":"ExplicitProductionUiaOnly", "DefaultOnDemand", "NotRun", "NotRun", 0, timingDiagnostic?1:diagnosticOnly?6:120, views.ToArray(),comDiagnostic?"SdkCom":"ManagedUia");
        await File.WriteAllTextAsync(receipt, JsonSerializer.Serialize(report));
        return views.Any(v => v.Status == "Fail") ? 3 : views.Any(v => v.Status == "Blocked") ? 2 : 0;
    }
    internal static bool IsFixedInstalledPath(string application, string path)
    {
        string relative = application switch { "Chrome" => "Google/Chrome/Application/chrome.exe", "Edge" => "Microsoft/Edge/Application/msedge.exe", _ => "" };
        return relative.Length > 0 && File.Exists(path) && new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) }
            .Where(p => p.Length > 0).Any(root => string.Equals(Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar))), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));
    }
}

using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using KuaiYi.Core;
using KuaiYi.OleTransfer;
using KuaiYi.Windows;

namespace KuaiYi.App;

// Explicit CI-only native export/import contract. It never calls RegisterDragDrop or enables capture.
internal static class HostedOleMarshalProbe
{
    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };
    public static int Run(string receipt)
    {
        receipt = Path.GetFullPath(receipt); Directory.CreateDirectory(Path.GetDirectoryName(receipt)!);
        var app = new Application(); var window = new Window { Title = "KuaiYi public marshal contract", Width = 80, Height = 80, ShowInTaskbar = false };
        var importer = new ContractImporter(); OleReceiverClient? client = null; OleReceiverHello? hello = null;
        Task? run = null, work = null; bool imported = false, ownedFinalized = false; OleStartupDiagnostic? failure = null;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        window.Loaded += (_, _) =>
        {
            nint hwnd = new WindowInteropHelper(window).Handle;
            importer.PresentationThread = NativeDesktop.GetCurrentThreadId();
            work = Task.Run(async () =>
            {
                OleSharedActivation? untransferred = null;
                try
                {
                    Guid instance = Guid.NewGuid(); untransferred = new OleSharedActivation(instance);
                    client = new(Path.Combine(AppContext.BaseDirectory, "KuaiYi.OleReceiver.exe"), importer, untransferred);
                    hello = new(instance, 1, Environment.ProcessId, hwnd.ToInt64(), RandomNumberGenerator.GetBytes(32), untransferred.Name);
                    untransferred = null; // The original Client now retains the mapping until actual finalization.
                    var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    run = client.RunAsync(hello, item => { if (item.Phase == "Ready") ready.TrySetResult(); },
                        (_, _, _, _) => throw new InvalidOperationException("No text delivery in marshal contract."), deadline.Token);
                    Task first = await Task.WhenAny(ready.Task, run).WaitAsync(TimeSpan.FromSeconds(12), deadline.Token);
                    if (first == run) await run; // Preserve the actual failed production startup.
                    await ready.Task.WaitAsync(TimeSpan.FromSeconds(1), deadline.Token);
                    if(run.IsCompleted||!client.IsReady)throw new InvalidOperationException("Receiver pump ended before imported binding admission.");
                    imported = importer.Imported && importer.IndependentSta;
                    if (!imported) throw new InvalidOperationException("Native import did not complete on independent STA.");
                }
                catch (Exception error) { failure = OleStartupDiagnostics.Project(OleStartupStage.ParentRegister, error); }
                finally
                {
                    try
                    {
                        untransferred?.Dispose(); client?.Cancel();
                        if (run is not null)
                            try { await run.WaitAsync(TimeSpan.FromSeconds(5)); }
                            catch (Exception error) { if (!imported) failure ??= OleStartupDiagnostics.Project(OleStartupStage.ParentShutdown, error); }
                        ownedFinalized = run is { IsCompleted: true } && client is not null && await client.TryFinalizeAsync(true);
                    }
                    catch (Exception error) { failure ??= OleStartupDiagnostics.Project(OleStartupStage.ParentShutdown, error); }
                    finally
                    {
                        if (hello is not null) CryptographicOperations.ZeroMemory(hello.Nonce);
                        await window.Dispatcher.InvokeAsync(window.Close);
                    }
                }
            });
        };
        app.Run(window);
        try { work?.WaitAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult(); }
        catch (Exception error) { failure ??= OleStartupDiagnostics.Project(OleStartupStage.ParentShutdown, error); }
        bool pass = imported && ownedFinalized && importer.IsSettledAndRevoked && importer.Failure is null && client?.DiagnosticReadSettled == true && client.DiagnosticReadFailure == OleExceptionCode.None && failure is null;
        int exit = pass ? 0 : 3;
        var result = new
        {
            Kind = "OwnedNativeMarshalContract", Status = pass ? "Pass" : "Fail", ModelCalls = 0,
            ExportSource = "ActualNativeTargetInOwnedReceiver", ProductionValidator = true, ProductionImport = true, ImportAttempted = importer.Attempted,
            Imported = imported, IndependentParentSta = importer.IndependentSta, ImporterReleased = importer.IsSettledAndRevoked,
            OwnedFinalized = ownedFinalized, ReceiverExitCode = client?.OwnedExitCode,
            DiagnosticReadSettled = client?.DiagnosticReadSettled ?? false, DiagnosticReadFailure = client?.DiagnosticReadFailure,
            ExportObservations = client?.ExportObservations ?? [], ParentImportObservation = importer.Observation,
            StartupFailure = client?.StartupFailure, ContractFailure = failure, ImportFailure = importer.Failure,
            RegistrationAttempted = false, ActualDrop = "NotRun", GetData = "NotRun", CloseRecovery = "NotRun",
            PhysicalE06 = "NotRun", Windows11Acceptance = "NotRun", ExitCode = exit
        };
        File.WriteAllText(receipt, JsonSerializer.Serialize(result, Json)); return exit;
    }

    private sealed class ContractImporter : IOleTargetRegistrar
    {
        private readonly object sync = new(); private Dispatcher? pump; private Task? original;
        private bool stopping, released = true; private int started;
        public uint PresentationThread;
        public bool Attempted { get; private set; }
        public bool Imported { get; private set; }
        public bool IndependentSta { get; private set; }
        public OleExportObservation? Observation { get; private set; }
        public OleStartupDiagnostic? Failure { get; private set; }
        public bool IsSettledAndRevoked { get { lock (sync) return original is not { IsCompleted: false } && released; } }
        public Task RegisterAsync(long hwnd, byte[] ownedPacket, CancellationToken cancellation)
        {
            // Test adapter for the real Import only: no HWND registration is claimed.
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var settled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (Interlocked.Exchange(ref started, 1) != 0) { CryptographicOperations.ZeroMemory(ownedPacket); throw new InvalidOperationException("One import per contract."); }
            lock (sync) { original = settled.Task; released = false; }
            var thread = new Thread(() =>
            {
                nint target = 0; bool cleanup = false; OleApartment? apartment = null;
                try
                {
                    cancellation.ThrowIfCancellationRequested(); apartment = new OleApartment(); pump = Dispatcher.CurrentDispatcher;
                    IndependentSta = Thread.CurrentThread.GetApartmentState() == ApartmentState.STA && NativeDesktop.GetCurrentThreadId() != PresentationThread;
                    if (!IndependentSta) throw new InvalidOperationException("Independent STA required.");
                    Observation = OleExportContract.Inspect(OleExportSource.ParentImport, (ulong)ownedPacket.Length, (ulong)ownedPacket.Length, ownedPacket);
                    OleReceiverWire.ValidateExport(ownedPacket); Attempted = true; target = OleTargetMarshal.Import(ownedPacket);
                    if (target == 0) throw new InvalidOperationException("Missing actual imported pointer.");
                    Imported = true; CryptographicOperations.ZeroMemory(ownedPacket);
                    lock (sync) if (stopping || cancellation.IsCancellationRequested) throw new OperationCanceledException();
                    ready.TrySetResult(); using var stop = cancellation.Register(Stop); Dispatcher.Run();
                }
                catch (Exception error) { Failure = OleStartupDiagnostics.Project(OleStartupStage.ParentRegister, error); ready.TrySetException(error); }
                finally
                {
                    CryptographicOperations.ZeroMemory(ownedPacket);
                    try { try { if (target != 0) Marshal.Release(target); } finally { apartment?.Dispose(); } cleanup = true; }
                    catch (Exception error) { Failure = OleStartupDiagnostics.Project(OleStartupStage.ParentShutdown, error); }
                    lock (sync) released = cleanup; settled.TrySetResult();
                }
            }) { IsBackground = true, Name = "KuaiYi public native import STA" };
            try { thread.SetApartmentState(ApartmentState.STA); thread.Start(); }
            catch (Exception error) { CryptographicOperations.ZeroMemory(ownedPacket); Failure = OleStartupDiagnostics.Project(OleStartupStage.ParentRegister, error); lock (sync) released = true; ready.TrySetException(error); settled.TrySetResult(); }
            return ready.Task;
        }
        private void Stop()
        {
            Dispatcher? dispatcher; lock (sync) { stopping = true; dispatcher = pump; }
            if (dispatcher is null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) return;
            try { dispatcher.BeginInvokeShutdown(DispatcherPriority.Send); }
            catch (InvalidOperationException error) { if (!dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished) Failure = OleStartupDiagnostics.Project(OleStartupStage.ParentShutdown, error); }
        }
        public async ValueTask<bool> TryFinalizeAsync()
        {
            Stop(); Task? pending; lock (sync) pending = original;
            if (pending is not null) try { await pending.WaitAsync(TimeSpan.FromSeconds(2)); } catch (TimeoutException) { return false; }
            return IsSettledAndRevoked;
        }
    }
}

using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using KuaiYi.Core;
using KuaiYi.Windows;
using KuaiYi.OleTransfer;
namespace KuaiYi.OleReceiver;
    internal sealed class NativeDropTarget(ReceiverHost owner) : IOleDropTarget
    {
        private readonly OleDropTransaction transaction = new();
        private Guid id;
        private bool advertised, active;private SelectionWindow? source;private OleTextFormat format;
        private long hoverEpoch;
        public bool HasPendingTransfer=>active;
        public void Invalidate() { transaction.Leave(); advertised = false; }
        private bool Clean(OleCleanupScope scope, Guid operation, long generation, bool earlierCleanupFailed = false)
        {
            try { scope.Dispose(); }
            catch (Exception error) { owner.WorkerMetadata(operation, generation, "CleanupFailed", error.GetType().Name); return false; }
            if (earlierCleanupFailed) { owner.WorkerMetadata(operation, generation, "CleanupFailed", "Earlier native cleanup unconfirmed"); return false; }
            return scope.CleanupCompleted;
        }
        public int DragEnter(nint data, uint keys, OlePoint point, ref uint effect)
        {
            Guid query = transaction.Enter(); id = query; advertised = false;
            uint offeredEffect=effect;
            if (!OleTargetDiagnostics.AdmitEnter(()=>owner.Enabled,()=>active,()=>owner.Hit(point),()=>((OleEffect)offeredEffect & OleEffect.Copy)!=0,owner.Observe)) { effect = 0; return 0; }
            source=owner.Production?owner.CaptureSource():null;format=OleTextFormat.None;if(owner.Production&&source==null){owner.Observe(OleTargetObservation.EnterSourceRejected);effect=0;return 0;}
            long generation = owner.Epoch; hoverEpoch = generation; nint foreground = NativeDesktop.GetForegroundWindow();
            owner.Emit(query, "QueryBegin", "Format query and cleanup deadline",source:source,point:new(point.X,point.Y),format:OleTextFormat.None);
            var scope = new OleCleanupScope(); string status = "Format query and native cleanup completed";
            try
            {
                var reference = OleDataReference.FromBorrowed(data,owner.Observations);
                scope.Own(() => owner.ReleaseOwned(query, generation, "QueryRelease", reference.Dispose));
                if(owner.CanCommit(query,generation)){format=owner.Production?reference.OfferedContent(owner.AllowImageFiles):reference.OffersUnicode()?OleTextFormat.Unicode:OleTextFormat.None;advertised=format!=OleTextFormat.None;}
            }
            catch (Exception error) { advertised = false; status = error.GetType().Name; }
            if (!Clean(scope, query, generation)) { effect = 0; advertised = false; active = true; return 0; }
            effect = (uint)OleTransferPolicy.Preview(owner.Enabled && generation == owner.Epoch, (OleEffect)effect, advertised);
            owner.Feedback(query,generation,effect!=0,point,source,format);
            owner.Emit(query, "QueryEnd", status, (OleEffect)effect, foreground: NativeDesktop.GetForegroundWindow() == foreground, cleanupCompleted: scope.CleanupCompleted,source:source,point:new(point.X,point.Y),format:format);
            return 0;
        }
        public int DragOver(uint keys, OlePoint point, ref uint effect) { effect = (uint)OleTransferPolicy.Preview(owner.Enabled && hoverEpoch == owner.Epoch && !active&&owner.Hit(point)&&(!owner.Production||source!=null&&owner.SourceCurrent(source)), (OleEffect)effect, advertised);owner.Feedback(id,hoverEpoch,effect!=0,point,source,format);return 0; }
        public int DragLeave() { Invalidate(); if(id!=Guid.Empty)owner.WorkerMetadata(id,hoverEpoch,"Leave"); return 0; }
        public int Drop(nint data, uint keys, OlePoint point, ref uint effect)
        {
            if (active || hoverEpoch != owner.Epoch || !owner.Hit(point)||owner.Production&&(source==null||!owner.SourceCurrent(source)) || !transaction.TryConsume(id, owner.Enabled, (OleEffect)effect, advertised)) { effect = 0; return 0; }
            Guid operation = id; long generation = owner.Epoch; hoverEpoch = generation; nint foreground = NativeDesktop.GetForegroundWindow(); active = true; effect = 0;
            owner.Emit(operation, "DropBegin", "Extraction, EndOperation and all cleanup deadline",owner.Production?OleEffect.Copy:OleEffect.None,source:source,point:new(point.X,point.Y),format:format);
            var scope = new OleCleanupScope(); var transfer = new AsyncTransfer(scope);
            string? text = null; string status = "Synchronous copy and all cleanup completed"; bool priorCleanupFailed = false;
            try
            {
                var reference = OleDataReference.FromBorrowed(data,owner.Observations);
                var dataLease = scope.Own(() => owner.ReleaseOwned(operation, generation, "DropRelease", reference.Dispose));
                if(!owner.CanCommit(operation,generation))throw new OperationCanceledException();
                var capability = reference.QueryAsync(); bool mode = false, started = false;
                if (capability is not null)
                {
                    scope.Own(() => owner.ReleaseOwned(operation, generation, "AsyncRelease", capability.Dispose));
                    try { mode = capability.Enabled; if (mode&&owner.CanCommit(operation,generation)) started = capability.StartOperation() >= 0; }
                    catch (COMException error) { owner.Emit(operation, "NegotiationFailed", error.GetType().Name); }
                    if (started)
                    {
                        scope.Own(() =>
                        {
                            if (!transfer.EndAttempted) { transfer.EndAttempted = true; Marshal.ThrowExceptionForHR(capability.EndOperation(OleNative.Failure, OleEffect.None)); transfer.EndSucceeded = true; }
                            if (!transfer.EndSucceeded) throw new InvalidOperationException("EndOperation was not confirmed; no retry.");
                        });
                    }
                }
                owner.Emit(operation, "Negotiated", "Optional async capability", available: capability is not null, mode: mode, started: started);
                if (OleTransferPolicy.Extraction(capability is not null, mode, started) == OleExtractionPath.NegotiatedAsync)
                {
                    transfer.Stream = reference.MarshalToStream();
                    // Two owned stream references: origin cleanup and the future unmarshal call.
                    scope.Own(() => ReleaseStream(transfer));
                    Marshal.AddRef(transfer.Stream); transfer.ExtraReference = true;
                    dataLease.Dispose(); // A remote Release may pump messages: do it before queuing.
                    owner.Emit(operation, "DropReturningAsync", "Owned transfer pending; deadline remains armed", OleEffect.Copy);
                    owner.Dispatcher.BeginInvoke(() => LaunchWorker(transfer, operation, generation, foreground));
                    effect = (uint)OleEffect.Copy; return 0; // The origin scope remains owned until FinishWorker.
                }
                if(!owner.CanCommit(operation,generation))throw new OperationCanceledException();
                owner.Observe(OleTargetObservation.ExtractionBegin);text = owner.Production?(format==OleTextFormat.ImageFile?reference.ReadImageFile():reference.ReadText(format)):reference.ReadUnicode();owner.Observe(OleTargetObservation.ExtractionReturned);
            }
            catch (Exception error) { text = null; status = error.GetType().Name; priorCleanupFailed = error is OleCleanupException; }
            if (!Clean(scope, operation, generation, priorCleanupFailed)) return 0;
            if(!owner.CanCommit(operation,generation))text=null;
            effect = text is null ? 0 : (uint)OleEffect.Copy;
            active = false; owner.Result(operation, generation, text, status, foreground); return 0;
        }
        private sealed class AsyncTransfer(OleCleanupScope origin)
        {
            public OleCleanupScope Origin { get; } = origin;
            public nint Stream;
            public bool ExtraReference, StreamReleased, Unmarshaled, EndAttempted, EndSucceeded;
        }
        private static void ReleaseStream(AsyncTransfer transfer)
        {
            try { if (!transfer.Unmarshaled) OleNative.ReleaseUnusedMarshalPacket(transfer.Stream); }
            finally
            {
                try { if (transfer.ExtraReference && !transfer.StreamReleased) Marshal.Release(transfer.Stream); }
                finally { Marshal.Release(transfer.Stream); }
            }
        }
        private void LaunchWorker(AsyncTransfer transfer, Guid operation, long generation, nint foreground)
        {
            var worker = new Thread(() =>
            {
                string? text = null; string status = "Async copy and all cleanup completed";
                bool nativeCleanupFailed = false; var apartmentScope = new OleCleanupScope();
                try
                {
                    var apartment = new OleApartment(); apartmentScope.Own(apartment.Dispose);
                    var dispatcher = Dispatcher.CurrentDispatcher;
                    dispatcher.BeginInvoke(() =>
                    {
                        var scope = new OleCleanupScope();
                        try
                        {
                            owner.WorkerMetadata(operation, generation, "WorkerUnmarshalBegin");
                            Guid iid = OleNative.DataObjectId; transfer.StreamReleased = true;
                            int hr = OleNative.CoGetInterfaceAndReleaseStream(transfer.Stream, ref iid, out nint pointer);
                            transfer.Unmarshaled = hr >= 0; Marshal.ThrowExceptionForHR(hr);
                            var reference = new OleDataReference(pointer,owner.Observations); scope.Own(reference.Dispose);
                            var capability = reference.QueryAsync() ?? throw new COMException("Async capability disappeared."); scope.Own(capability.Dispose);
                            int extractionHr = OleNative.Failure;
                            try { if(!owner.CanCommit(operation,generation))throw new OperationCanceledException();owner.WorkerMetadata(operation, generation, "WorkerReadBegin"); owner.Observe(OleTargetObservation.ExtractionBegin);text = owner.Production?(format==OleTextFormat.ImageFile?reference.ReadImageFile():reference.ReadText(format)):reference.ReadUnicode();owner.Observe(OleTargetObservation.ExtractionReturned); extractionHr = 0; }
                            catch (Exception error) { text = null; status = error.GetType().Name; nativeCleanupFailed = error is OleCleanupException; }
                            finally
                            {
                                owner.WorkerMetadata(operation, generation, "WorkerEndAttempt"); transfer.EndAttempted = true;
                                int end = capability.EndOperation(extractionHr, text is null ? OleEffect.None : OleEffect.Copy);
                                transfer.EndSucceeded = end >= 0;
                                if (!transfer.EndSucceeded) { text = null; status = "EndOperation failed"; nativeCleanupFailed = true; }
                            }
                        }
                        catch (Exception error) { text = null; status = error.GetType().Name; nativeCleanupFailed |= error is OleCleanupException; }
                        finally { if (!Clean(scope, operation, generation)) { text = null; nativeCleanupFailed = true; } dispatcher.BeginInvokeShutdown(DispatcherPriority.Send); }
                    });
                    Dispatcher.Run();
                }
                catch (Exception error) { text = null; status = error.GetType().Name; nativeCleanupFailed |= error is OleCleanupException; }
                finally { if (!Clean(apartmentScope, operation, generation)) { text = null; nativeCleanupFailed = true; } }
                // Queuing establishes visibility of worker state; all origin COM calls remain on the origin STA.
                owner.Dispatcher.BeginInvoke(() => FinishWorker(transfer, operation, generation, foreground, text, status, nativeCleanupFailed));
            }) { IsBackground = true, Name = "KuaiYi marshaled OLE STA" };
            try { worker.SetApartmentState(ApartmentState.STA); worker.Start(); }
            catch (Exception error) { FinishWorker(transfer, operation, generation, foreground, null, error.GetType().Name, false); }
        }
        private void FinishWorker(AsyncTransfer transfer, Guid operation, long generation, nint foreground, string? text, string status, bool workerCleanupFailed)
        {
            if (!Clean(transfer.Origin, operation, generation, workerCleanupFailed)) return;
            active = false; owner.Result(operation, generation, text, status, foreground);
        }
    }

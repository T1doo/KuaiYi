using System.Security.Cryptography;
using System.Text.Json;
using KuaiYi.Core.Installation;
using KuaiYi.NativeHost;
using Xunit;
namespace KuaiYi.Core.Tests;

// Actual Core/adapter operations over generated isolated stores and synthetic registry. No Windows APIs or runnable payload claim.
public sealed partial class InstallationRegistrationWithdrawalTests(InstallationRegistrationWithdrawalTests.Sources sources):IClassFixture<InstallationRegistrationWithdrawalTests.Sources>
{
    public sealed class Sources:IDisposable
    {
        internal readonly string Root=Path.Combine(Path.GetTempPath(),"ky-withdrawal-sources-"+Guid.NewGuid().ToString("N"));
        internal readonly MaterializedPayloadSource G;
        internal readonly BootstrapSource B,Development;
        internal bool Keep;
        public Sources()
        {
            Directory.CreateDirectory(Root);G=new(Path.Combine(Root,"g"));B=new(Path.Combine(Root,"b"));Development=new(Path.Combine(Root,"development"));
            string approved=Path.Combine(MaterializedPayloadSource.Repo,"artifacts/2026-10-23-product-payload/final/verification-b-source");
            foreach(var f in InstallationBootstrapPackage.RuntimeFiles)
            {
                string from=Path.Combine(approved,f.Path),to=Path.Combine(B.Root,f.Path);Assert.Equal(f.Bytes,new FileInfo(from).Length);Assert.Equal(f.Sha256,MaterializedPayloadSource.Hash(from),ignoreCase:true);Directory.CreateDirectory(Path.GetDirectoryName(to)!);File.Copy(from,to);
            }
            File.WriteAllText(Path.Combine(B.Root,"KuaiYi.Launcher.runtimeconfig.json"),InstallationBootstrapPackage.SelfContainedRuntime);
            File.Copy(Path.Combine(MaterializedPayloadSource.Repo,"tests/fixtures/payload-runtime/KuaiYi.Launcher.deps.json"),Path.Combine(B.Root,"KuaiYi.Launcher.deps.json"),true);
            var files=Directory.GetFiles(B.Root,"*",SearchOption.AllDirectories).Where(p=>Path.GetFileName(p)!="package.json").Order(StringComparer.Ordinal).Select(p=>new{path=Path.GetRelativePath(B.Root,p).Replace('\\','/'),bytes=new FileInfo(p).Length,sha256=MaterializedPayloadSource.Hash(p)}).ToArray();
            File.WriteAllBytes(Path.Combine(B.Root,"package.json"),JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,product="KuaiYi",version=B.Version,files}));
        }
        public void Dispose(){if(!Keep)Directory.Delete(Root,true);}
    }
    private sealed class Host:IHostRegistrationStore
    {
        internal HostRegistrationSlot Chrome=new(false,null),Edge=new(false,null);
        internal Action<IntegrationBrowser>? BeforeWrite;
        internal int Writes;
        public bool CleanupConfirmed=>true;
        public HostRegistrationSlot Read(IntegrationBrowser browser)=>browser==IntegrationBrowser.Chrome?Chrome:Edge;
        public void Write(IntegrationBrowser browser,HostRegistrationSlot expected,string? value)
        {Writes++;BeforeWrite?.Invoke(browser);if(Read(browser)!=expected)throw new HostRegistrationConflictException();if(browser==IntegrationBrowser.Chrome)Chrome=new(true,value);else Edge=new(true,value);}
        public void FinalizeCleanup(){}
    }
    private sealed class Startup(bool initiallyUnsettled=false):IStartupRegistrationStore
    {
        internal InstallationStartupValue Value=new(false);
        internal Action? AfterWrite;
        internal Action? BeforeRead;
        internal Exception? CloseError;
        internal int Reads,Writes,Closes;
        private bool unsettled=initiallyUnsettled;
        public bool CleanupConfirmed=>!unsettled;
        public InstallationStartupValue Read(){Reads++;BeforeRead?.Invoke();return Value;}
        public void Write(InstallationStartupValue expected,InstallationStartupValue next)
        {if(Value!=expected)throw new HostRegistrationConflictException();Value=next;Writes++;AfterWrite?.Invoke();if(CloseError is{} error){unsettled=true;throw error;}}
        public void FinalizeCleanup(){Closes++;if(CloseError is{} error){unsettled=true;throw error;}unsettled=false;}
    }
    private sealed class Rig
    {
        private static readonly List<Rig> Retained=[];
        internal readonly string Root=Path.Combine(Path.GetTempPath(),"ky-wd-"+Guid.NewGuid().ToString("N"));
        internal string App=>Path.Combine(Root,"app");internal string Data=>Path.Combine(Root,"data");
        internal readonly Host Host=new();internal readonly Startup Startup=new();
        internal readonly List<InstallationRegistrationWithdrawal> Operations=[];
        internal readonly List<InstallationWithdrawalOutcome> Outcomes=[];
        internal readonly List<InstallationMaintenanceOutcome> PreflightOutcomes=[];
        internal readonly Dictionary<string,string> Payload=[];
        internal Dictionary<string,string?> Before=[];
        internal byte[] Pins=[];
        internal InstalledVersion Selected=null!;internal string Bundle="";
        internal bool Keep;
        internal string? EvidenceOutput;
        internal InstallationStore Store=>new(App,Data,()=>true);
        internal string[] Records=>[Path.Combine(App,"current.json"),InstallationBootstrapDeployment.ActivePath(App),Path.Combine(App,"host-registration.json"),InstallationStartupRegistration.OwnerPath(App)];
        internal Dictionary<string,string?> Snapshot()=>Records.ToDictionary(p=>p,p=>File.Exists(p)?Convert.ToBase64String(File.ReadAllBytes(p)):null);
        internal static async Task<Rig> Create(Sources source,bool development=false)
        {
            var r=new Rig();
            try
            {
                Directory.CreateDirectory(r.Data);File.WriteAllText(Path.Combine(r.Data,"PUBLIC_OFF"),"PUBLIC_PRESERVED");using(var setup=r.Store.EnterRoleAdmission()){}
                r.Selected=await r.Store.ApplyAsync(source.G.Root,source.G.Digest);var b=development?source.Development:source.B;
                await new InstallationBootstrapDeployment(r.App,r.Data).DeployAsync(b.Root,b.Digest);await new HostRegistrationTransaction(r.App,r.Data,r.Host).RunAsync(true);
                var owner=InstallationBootstrapDeployment.Decode<InstallationBootstrapDeployment.StoreOwner>(File.ReadAllBytes(InstallationBootstrapDeployment.OwnerPath(r.App)));
                var active=InstallationBootstrapDeployment.Decode<InstallationBootstrapDeployment.Receipt>(File.ReadAllBytes(InstallationBootstrapDeployment.ActivePath(r.App)));r.Bundle=InstallationBootstrapDeployment.BundlePath(r.App,active.Bundle);
                string command=InstallationStartupRegistration.Command(r.Bundle);var receipt=new InstallationStartupOwnership(1,"KuaiYi",owner.Owner,r.App,r.Data,r.Selected,active.Bundle,source.G.Digest,b.Digest,command);
                File.WriteAllBytes(InstallationStartupRegistration.OwnerPath(r.App),JsonSerializer.SerializeToUtf8Bytes(receipt));r.Startup.Value=new(true,"REG_SZ",command);
                r.Pins=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,kind="KuaiYiInstallerSourcePins",availability="Pinned",generationSha256=source.G.Digest,bootstrapSha256=b.Digest});
                foreach(string directory in new[]{r.Selected.DirectoryPath,r.Bundle})foreach(string p in Directory.GetFiles(directory,"*",SearchOption.AllDirectories))r.Payload[p]=MaterializedPayloadSource.Hash(p);
                r.Before=r.Snapshot();r.Host.Writes=r.Startup.Writes=0;return r;
            }
            catch{r.Keep=true;source.Keep=true;lock(Retained)Retained.Add(r);throw;}
        }
        internal InstallationRegistrationWithdrawal New(CancellationToken cancellation=default,InstallationWithdrawalFaults? faults=null,byte[]? pins=null)
        {
            var registry=new InstallationStartupRegistrySnapshot(new InstallationDeploymentRegistrySnapshot(Host),Startup);
            var value=new InstallationRegistrationWithdrawal(pins??Pins,App,Data,registry,cancellation,faults);Operations.Add(value);return value;
        }
        internal async Task<InstallationWithdrawalOutcome> Run(InstallationRegistrationWithdrawal operation)
        {var result=await operation.Start().WaitAsync(TimeSpan.FromSeconds(90));Outcomes.Add(result);if(result.Status is InstallationWithdrawalStatus.Unconfirmed or InstallationWithdrawalStatus.ReferencesWithdrawnReceiptUncertain)Keep=true;return result;}
        internal InstallationRegistrationWithdrawal Preflight(InstallationMaintenanceIntent intent=InstallationMaintenanceIntent.Uninstall,CancellationToken cancellation=default,InstallationWithdrawalFaults? faults=null,byte[]? pins=null,IInstallationStartupRegistry? registry=null)
        {
            var value=InstallationRegistrationWithdrawal.ForMaintenancePreflight(pins??Pins,App,Data,registry??new InstallationStartupRegistrySnapshot(new InstallationDeploymentRegistrySnapshot(Host),Startup),intent,cancellation,faults);Operations.Add(value);return value;
        }
        internal async Task<InstallationMaintenanceOutcome> Inspect(InstallationRegistrationWithdrawal operation)
        {var value=await operation.StartPreflight().WaitAsync(TimeSpan.FromSeconds(90));PreflightOutcomes.Add(value);if(value.Status!=InstallationMaintenanceStatus.ObservedReadOnly)Keep=true;return value;}
        internal void PayloadUnchanged(){foreach(var p in Payload)Assert.Equal(p.Value,MaterializedPayloadSource.Hash(p.Key));Assert.Equal("PUBLIC_PRESERVED",File.ReadAllText(Path.Combine(Data,"PUBLIC_OFF")));}
        internal void Empty(){Assert.All(Snapshot(),p=>Assert.Null(p.Value));Assert.Null(Host.Chrome.Value);Assert.Null(Host.Edge.Value);Assert.Equal(new(false),Startup.Value);}
        internal void Restored(){Assert.Equal(Before.OrderBy(p=>p.Key),Snapshot().OrderBy(p=>p.Key));Assert.Equal(Before[Path.Combine(App,"host-registration.json")]==null?null:Path.Combine(Selected.DirectoryPath,InstallationPayload.HostManifestName),Host.Chrome.Value);Assert.Equal(Host.Chrome.Value,Host.Edge.Value);}
        internal void Cleanup()
        {
            List<Exception> errors=[];
            try{PayloadUnchanged();}catch(Exception error){Keep=true;errors.Add(error);}
            if(Operations.Any(o=>!o.OriginalWork.IsCompleted||!o.CleanupConfirmed))Keep=true;
            try
            {
                string? output=EvidenceOutput??Environment.GetEnvironmentVariable("KUAIYI_WITHDRAWAL_EVIDENCE");
                if(output!=null)
                {
                    Directory.CreateDirectory(output);var metadata=Directory.GetFiles(App,"*",SearchOption.TopDirectoryOnly).Where(p=>p.EndsWith(".json",StringComparison.Ordinal)).ToDictionary(p=>Path.GetFileName(p)!,p=>Convert.ToBase64String(File.ReadAllBytes(p)));
                    var outcomes=Outcomes.Select(o=>new{o.Status,o.CleanupConfirmed,Errors=o.Errors.Select(error=>error.ToString()).ToArray()}).ToArray();
                    var preflights=PreflightOutcomes.Select(o=>new{o.Operation,o.Intent,o.Status,o.CleanupConfirmed,o.Observation,o.RemainingSteps,Errors=o.Errors.Select(error=>error.ToString()).ToArray()}).ToArray();
                    File.WriteAllBytes(Path.Combine(output,Path.GetFileName(Root)+".json"),JsonSerializer.SerializeToUtf8Bytes(new{Root,Outcomes=outcomes,Preflights=preflights,metadata,Payload,Host.Chrome,Host.Edge,Startup.Value,HostWrites=Host.Writes,StartupWrites=Startup.Writes,Keep}));
                }
            }
            catch(Exception error){Keep=true;errors.Add(error);}
            try{if(Keep){lock(Retained)Retained.Add(this);}else Directory.Delete(Root,true);}
            catch(Exception error){errors.Add(error);}
            InstallationErrors.Throw(errors);
        }
    }
    private static IEnumerable<Exception> Leaves(Exception error)=>error is AggregateException a?a.Flatten().InnerExceptions:new[]{error};
    private async Task WithRig(Func<Rig,Task> body,bool development=false)
    {
        var r=await Rig.Create(sources,development);List<Exception> errors=[];
        try{await body(r);}catch(Exception error){r.Keep=true;errors.Add(error);}
        try{r.Cleanup();}catch(Exception error){errors.Add(error);}
        if(r.Keep)sources.Keep=true;
        InstallationErrors.Throw(errors);
    }
    [Fact]public async Task SevenReferencesWithdrawWithOriginalOwnerAndRepeatIsIndependentlyReadOnly()
    {
        await WithRig(async r=>
        {
            var steps=new List<int>();var operation=r.New(faults:new(){AfterEffect=(n,reverse)=>{Assert.False(reverse);steps.Add(n);Assert.True(File.Exists(r.Store.PendingPath));}});
            Assert.Same(operation.Start(),operation.Start());var result=await r.Run(operation);Assert.Equal(InstallationWithdrawalStatus.ReferencesWithdrawn,result.Status);Assert.True(result.CleanupConfirmed);Assert.Empty(result.Errors);Assert.Equal(Enumerable.Range(1,7),steps);r.Empty();Assert.False(File.Exists(r.Store.PendingPath));r.PayloadUnchanged();
            int writes=r.Host.Writes+r.Startup.Writes;var repeat=await r.Run(r.New());Assert.Equal(InstallationWithdrawalStatus.AlreadyWithdrawnReadOnly,repeat.Status);Assert.True(repeat.CleanupConfirmed);Assert.Empty(repeat.Errors);Assert.Equal(writes,r.Host.Writes+r.Startup.Writes);Assert.Single(Directory.GetFiles(r.App,"withdrawal-finished-*.json"));
        });
    }
    [Theory]
    [InlineData(1,false)][InlineData(2,false)][InlineData(3,false)][InlineData(4,false)][InlineData(5,false)][InlineData(6,false)][InlineData(7,false)]
    [InlineData(1,true)][InlineData(2,true)][InlineData(3,true)][InlineData(4,true)][InlineData(5,true)][InlineData(6,true)][InlineData(7,true)]
    public async Task EveryKnownWriteAfterEffectFailureOrCancellationRestoresOnlyInReverse(int stage,bool cancel)
    {
        await WithRig(async r=>
        {
            using var stop=new CancellationTokenSource();var expected=new IOException("PUBLIC_WITHDRAW_AFTER_"+stage);var reversed=new List<int>();
            var operation=r.New(stop.Token,new(){AfterEffect=(step,reverse)=>{if(reverse)reversed.Add(step);else if(step==stage){if(cancel)stop.Cancel();else throw expected;}}});
            var result=await r.Run(operation);Assert.Equal(InstallationWithdrawalStatus.AbortedRestored,result.Status);Assert.True(result.CleanupConfirmed);Assert.Equal(Enumerable.Range(1,stage).Reverse(),reversed);
            Assert.Contains(result.Errors.SelectMany(Leaves),e=>cancel?e is OperationCanceledException:ReferenceEquals(e,expected));r.Restored();Assert.True(r.Startup.Value.Exists);Assert.False(File.Exists(r.Store.PendingPath));Assert.Single(Directory.GetFiles(r.App,"withdrawal-aborted-*.json"));r.PayloadUnchanged();
        });
    }
    [Fact]public async Task ForeignCompareCannotWashOutOrCompensateEarlierDependenciesAfterExternalRestore()
    {
        await WithRig(async r=>
        {
            var original=r.Host.Edge;bool armed=true;r.Host.BeforeWrite=browser=>{if(browser==IntegrationBrowser.Edge&&armed){armed=false;r.Host.Edge=new(true,"PUBLIC_FOREIGN");}};
            var operation=r.New();var result=await r.Run(operation);Assert.Equal(InstallationWithdrawalStatus.Unconfirmed,result.Status);Assert.True(File.Exists(r.Store.PendingPath));Assert.Equal(1,r.Host.Writes);Assert.False(r.Startup.Value.Exists);
            Assert.True(File.Exists(Path.Combine(r.App,"current.json")));Assert.True(File.Exists(InstallationBootstrapDeployment.ActivePath(r.App)));Assert.False(File.Exists(Path.Combine(r.App,"host-registration.json")));
            r.Host.Edge=original;int writes=r.Host.Writes+r.Startup.Writes;Assert.Empty(await operation.SettleOriginalsAsync());Assert.Same(result,await operation.Start());
            var fresh=await r.Run(r.New());Assert.Equal(InstallationWithdrawalStatus.Refused,fresh.Status);Assert.Equal(writes,r.Host.Writes+r.Startup.Writes);Assert.True(File.Exists(r.Store.PendingPath));
        });
    }
    [Fact]public async Task OriginalCloseFailureKeepsPendingAndSettlementNeverReplaysPublication()
    {
        await WithRig(async r=>
        {
            var original=new IOException("PUBLIC_ORIGINAL_RUN_CLOSE");r.Startup.AfterWrite=()=>r.Startup.CloseError=original;
            var operation=r.New();var result=await r.Run(operation);Assert.Equal(InstallationWithdrawalStatus.Unconfirmed,result.Status);Assert.False(result.CleanupConfirmed);Assert.Contains(result.Errors,e=>ReferenceEquals(e,original));Assert.True(File.Exists(r.Store.PendingPath));
            int writes=r.Host.Writes+r.Startup.Writes;r.Startup.CloseError=null;r.Startup.AfterWrite=null;Assert.Empty(await operation.SettleOriginalsAsync());Assert.True(operation.CleanupConfirmed);Assert.Equal(writes,r.Host.Writes+r.Startup.Writes);Assert.True(File.Exists(r.Store.PendingPath));Assert.Same(result,await operation.Start());
        });
    }
    [Fact]public async Task InitialProbeCloseFailureRetainsOriginalAdmissionBeforeAnyPendingTicket()
    {
        await WithRig(async r=>
        {
            var failure=new IOException("PUBLIC_INITIAL_PROBE_CLOSE");r.Startup.CloseError=failure;
            var operation=r.New();var result=await r.Run(operation);
            Assert.Equal(InstallationWithdrawalStatus.Unconfirmed,result.Status);Assert.False(result.CleanupConfirmed);
            Assert.Contains(result.Errors.SelectMany(Leaves),e=>ReferenceEquals(e,failure));
            Assert.False(File.Exists(r.Store.PendingPath));Assert.Equal(0,r.Host.Writes+r.Startup.Writes);r.Restored();
            Assert.Throws<InstallationBusyException>(()=>r.Store.EnterRoleAdmission());
            r.Startup.CloseError=null;
            Assert.Empty(await operation.SettleOriginalsAsync());Assert.True(operation.CleanupConfirmed);
            using(var admission=r.Store.EnterRoleAdmission()){}
            Assert.Equal(0,r.Host.Writes+r.Startup.Writes);Assert.False(File.Exists(r.Store.PendingPath));
            Assert.Same(result,await operation.Start());
        });
    }
    [Theory]
    [InlineData(1,false)][InlineData(3,false)][InlineData(6,false)][InlineData(7,false)]
    [InlineData(1,true)][InlineData(3,true)][InlineData(6,true)][InlineData(7,true)]
    public async Task PublicationMoveFailureKeepsUnknownPendingAndNeverCompensatesOrReplays(int stage,bool moved)
    {
        await WithRig(async r=>
        {
            string canonical=stage switch{1=>InstallationStartupRegistration.OwnerPath(r.App),3=>Path.Combine(r.App,"host-registration.json"),6=>InstallationBootstrapDeployment.ActivePath(r.App),7=>Path.Combine(r.App,"current.json"),_=>throw new InvalidOperationException()};
            var failure=new IOException("PUBLIC_PUBLICATION_MOVE_"+stage);int moves=0,writesAtFailure=-1;var knownEffects=new List<int>();
            var operation=r.New(faults:new()
            {
                Move=(from,to)=>
                {
                    moves++;
                    if(from==canonical){writesAtFailure=r.Host.Writes+r.Startup.Writes;if(moved)File.Move(from,to);throw failure;}
                    File.Move(from,to);
                },
                AfterEffect=(step,reverse)=>{Assert.False(reverse);knownEffects.Add(step);}
            });
            var result=await r.Run(operation);
            Assert.Equal(InstallationWithdrawalStatus.Unconfirmed,result.Status);Assert.True(result.CleanupConfirmed);
            Assert.Contains(result.Errors.SelectMany(Leaves),e=>ReferenceEquals(e,failure));Assert.True(File.Exists(r.Store.PendingPath));
            Assert.Equal(Enumerable.Range(1,stage-1),knownEffects);
            Assert.Equal(new[]{1,3,6,7}.Count(step=>step<=stage),moves);Assert.Equal(writesAtFailure,r.Host.Writes+r.Startup.Writes);
            Assert.Equal(!moved,File.Exists(canonical));Assert.Equal(moved,File.Exists(Path.Combine(r.App,$"withdrawal-removed-{operation.Operation}-{stage}.json")));
            Assert.Empty(Directory.GetFiles(r.App,"withdrawal-aborted-*.json"));Assert.Empty(Directory.GetFiles(r.App,"withdrawal-finished-*.json"));
            int originalMoves=moves;Assert.Empty(await operation.SettleOriginalsAsync());Assert.Same(result,await operation.Start());
            Assert.Equal(originalMoves,moves);Assert.Equal(writesAtFailure,r.Host.Writes+r.Startup.Writes);Assert.True(File.Exists(r.Store.PendingPath));r.PayloadUnchanged();
        });
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ReversePublicationMoveFailureKeepsUnknownAndStopsEarlierCompensation(bool moved)
    {
        await WithRig(async r=>
        {
            string canonical=Path.Combine(r.App,"host-registration.json");int moves=0,reverseMoves=0;var reverseEffects=new List<int>();
            var knownFailure=new IOException("PUBLIC_KNOWN_FORWARD_EFFECT_3");var moveFailure=new IOException("PUBLIC_REVERSE_MOVE_3");
            var operation=r.New(faults:new()
            {
                Move=(from,to)=>
                {
                    moves++;
                    if(to==canonical){reverseMoves++;if(moved)File.Move(from,to);throw moveFailure;}
                    File.Move(from,to);
                },
                AfterEffect=(step,reverse)=>{if(reverse)reverseEffects.Add(step);else if(step==3)throw knownFailure;}
            });
            var result=await r.Run(operation);
            Assert.Equal(InstallationWithdrawalStatus.Unconfirmed,result.Status);Assert.True(result.CleanupConfirmed);
            Assert.Contains(result.Errors.SelectMany(Leaves),e=>ReferenceEquals(e,knownFailure));Assert.Contains(result.Errors.SelectMany(Leaves),e=>ReferenceEquals(e,moveFailure));
            Assert.Equal(3,moves);Assert.Equal(1,reverseMoves);Assert.Empty(reverseEffects);
            Assert.Equal(moved,File.Exists(canonical));Assert.Equal(!moved,File.Exists(Path.Combine(r.App,$"withdrawal-removed-{operation.Operation}-3.json")));
            Assert.False(r.Startup.Value.Exists);Assert.False(File.Exists(InstallationStartupRegistration.OwnerPath(r.App)));Assert.True(File.Exists(Path.Combine(r.App,$"withdrawal-removed-{operation.Operation}-1.json")));
            Assert.True(File.Exists(r.Store.PendingPath));Assert.Empty(Directory.GetFiles(r.App,"withdrawal-aborted-*.json"));Assert.Empty(Directory.GetFiles(r.App,"withdrawal-finished-*.json"));
            int writes=r.Host.Writes+r.Startup.Writes;Assert.Empty(await operation.SettleOriginalsAsync());Assert.Same(result,await operation.Start());
            Assert.Equal(3,moves);Assert.Equal(1,reverseMoves);Assert.Equal(writes,r.Host.Writes+r.Startup.Writes);Assert.True(File.Exists(r.Store.PendingPath));r.PayloadUnchanged();
        });
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task AuditFailureKeepsOriginalRootAndPreservesBodyErrorBeforeAuditError(bool bodyFails)
    {
        Rig? retained=null;var bodyFailure=new IOException("PUBLIC_BODY_BEFORE_AUDIT");
        var error=await Record.ExceptionAsync(()=>WithRig(r=>
        {
            retained=r;r.EvidenceOutput=Path.Combine(r.Root,"PUBLIC_AUDIT_OUTPUT_IS_A_FILE");File.WriteAllText(r.EvidenceOutput,"PUBLIC_RETAIN_AUDIT_FAILURE");
            if(bodyFails)throw bodyFailure;
            return Task.CompletedTask;
        }));
        Assert.NotNull(error);var errors=Leaves(error).ToArray();Assert.Equal(bodyFails?2:1,errors.Length);
        if(bodyFails)Assert.Same(bodyFailure,errors[0]);Assert.IsAssignableFrom<IOException>(errors[^1]);
        Assert.NotNull(retained);Assert.True(retained.Keep);Assert.True(Directory.Exists(retained.Root));retained.PayloadUnchanged();
    }
    [Fact]public async Task CancellationCannotReleaseOriginalLeaseOrCloseAnUnsettledWrite()
    {
        await WithRig(async r=>
        {
            var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);using var release=new ManualResetEventSlim();
            r.Startup.AfterWrite=()=>{entered.TrySetResult();if(!release.Wait(TimeSpan.FromSeconds(8)))throw new TimeoutException("PUBLIC_ORIGINAL_WRITE_PENDING");};
            var operation=r.New();Task<InstallationWithdrawalOutcome> original=operation.Start();List<Exception> errors=[];
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(8));operation.Abort();Assert.False(original.IsCompleted);Assert.False(operation.CleanupConfirmed);
                Assert.Throws<InstallationBusyException>(()=>r.Store.EnterRoleAdmission());Assert.True(File.Exists(r.Store.PendingPath));
            }
            catch(Exception error){errors.Add(error);}
            try{release.Set();}catch(Exception error){errors.Add(error);}
            try{var result=await r.Run(operation);Assert.Equal(InstallationWithdrawalStatus.AbortedRestored,result.Status);Assert.True(result.CleanupConfirmed);r.Restored();}
            catch(Exception error){errors.Add(error);}
            InstallationErrors.Throw(errors);
        });
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task FinalArchiveMoveErrorNeverReplaysAndSeparatesBeforeFromAfterEffect(bool moved)
    {
        await WithRig(async r=>
        {
            int moves=0;var original=new IOException("PUBLIC_WITHDRAW_ARCHIVE");var operation=r.New(faults:new(){Archive=(from,to)=>{moves++;if(moved)File.Move(from,to);throw original;}});
            var result=await r.Run(operation);Assert.Equal(moved?InstallationWithdrawalStatus.ReferencesWithdrawnReceiptUncertain:InstallationWithdrawalStatus.Unconfirmed,result.Status);Assert.True(result.CleanupConfirmed);Assert.Contains(result.Errors,e=>ReferenceEquals(e,original));r.Empty();Assert.Equal(!moved,File.Exists(r.Store.PendingPath));Assert.Equal(moved,Directory.GetFiles(r.App,"withdrawal-finished-*.json").Length==1);
            Assert.Same(result,await operation.Start());Assert.Equal(1,moves);Assert.Empty(await operation.SettleOriginalsAsync());Assert.Equal(1,moves);
        });
    }
    [Fact]public async Task CancellationAfterClosingDoesNotRestoreBindingsOrConvertToABorted()
    {
        await WithRig(async r=>
        {
            using var stop=new CancellationTokenSource();var result=await r.Run(r.New(stop.Token,new(){AfterClosing=()=>stop.Cancel()}));Assert.Equal(InstallationWithdrawalStatus.ReferencesWithdrawn,result.Status);Assert.True(result.CleanupConfirmed);r.Empty();
        });
    }
    [Theory][InlineData("pins")][InlineData("same-path-no-owner")][InlineData("owner-root")]
    public async Task ReceiptsAndMatchingPathsCannotMintSourceOrOwnership(string kind)
    {
        await WithRig(async r=>
        {
            byte[] pins=r.Pins;
            if(kind=="pins")pins=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,kind="KuaiYiInstallerSourcePins",availability="Pinned",generationSha256=new string('A',64),bootstrapSha256=sources.B.Digest});
            if(kind=="same-path-no-owner")File.Move(InstallationStartupRegistration.OwnerPath(r.App),Path.Combine(r.Root,"PUBLIC_OWNER_OUTSIDE_OPERATION"));
            if(kind=="owner-root")
            {string path=InstallationBootstrapDeployment.OwnerPath(r.App);var owner=InstallationBootstrapDeployment.Decode<InstallationBootstrapDeployment.StoreOwner>(File.ReadAllBytes(path));File.WriteAllBytes(path,JsonSerializer.SerializeToUtf8Bytes(owner with{Root=Path.Combine(r.Root,"PUBLIC_OTHER_ROOT")}));}
            var before=r.Snapshot();var result=await r.Run(r.New(pins:pins));Assert.Equal(InstallationWithdrawalStatus.Refused,result.Status);Assert.True(result.CleanupConfirmed);Assert.Equal(0,r.Host.Writes+r.Startup.Writes);Assert.Equal(before.OrderBy(x=>x.Key),r.Snapshot().OrderBy(x=>x.Key));Assert.False(File.Exists(r.Store.PendingPath));
        });
    }
    [Fact]public async Task FrameworkDependentBootstrapDoesNotQualifyForWithdrawalAuthority()
    {await WithRig(async r=>{var result=await r.Run(r.New());Assert.Equal(InstallationWithdrawalStatus.Refused,result.Status);Assert.True(result.CleanupConfirmed);Assert.Equal(0,r.Host.Writes+r.Startup.Writes);r.Restored();},development:true);}
    [Fact]public async Task UnavailableEmbeddedPinsRefuseBeforeCreatingRootOrCallingRegistry()
    {
        string root=Path.Combine(Path.GetTempPath(),"ky-wd-unavailable-"+Guid.NewGuid().ToString("N"));var host=new Host();var startup=new Startup();
        var registry=new InstallationStartupRegistrySnapshot(new InstallationDeploymentRegistrySnapshot(host),startup);
        var operation=new InstallationRegistrationWithdrawal(File.ReadAllBytes(Path.Combine(MaterializedPayloadSource.Repo,"packaging/installer-source-pins.json")),Path.Combine(root,"app"),Path.Combine(root,"data"),registry);
        var result=await operation.Start();Assert.Equal(InstallationWithdrawalStatus.Refused,result.Status);Assert.True(result.CleanupConfirmed);Assert.False(Directory.Exists(root));Assert.Equal(0,startup.Reads+startup.Writes+host.Writes);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task EarlyFileQualificationRefusalReleasesAdmissionWithoutAdoptingUnstartedRegistry(bool initiallyUnsettled)
    {
        await WithRig(async r=>
        {
            var host=new Host();var startup=new Startup(initiallyUnsettled);
            var registry=new InstallationStartupRegistrySnapshot(new InstallationDeploymentRegistrySnapshot(host),startup);
            string absentData=Path.Combine(r.Root,"PUBLIC_ABSENT_DATA");
            var operation=new InstallationRegistrationWithdrawal(r.Pins,r.App,absentData,registry);r.Operations.Add(operation);
            var result=await r.Run(operation);Assert.Equal(InstallationWithdrawalStatus.Refused,result.Status);Assert.True(result.CleanupConfirmed);Assert.True(operation.CleanupConfirmed);
            Assert.Equal("Original user data directory absent.",Assert.IsType<InstallationRecoveryException>(Assert.Single(result.Errors)).Message);
            Assert.False(Directory.Exists(absentData));Assert.False(File.Exists(r.Store.PendingPath));
            using(var admission=r.Store.EnterRoleAdmission()){}
            Assert.Equal(!initiallyUnsettled,registry.CleanupConfirmed);Assert.Equal(0,startup.Reads+startup.Writes+startup.Closes+host.Writes);
            Assert.Empty(await operation.SettleOriginalsAsync());Assert.Same(result,await operation.Start());Assert.True(operation.CleanupConfirmed);
            Assert.Equal(!initiallyUnsettled,registry.CleanupConfirmed);Assert.Equal(0,startup.Reads+startup.Writes+startup.Closes+host.Writes);r.Restored();
        });
    }
}

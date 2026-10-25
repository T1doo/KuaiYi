using KuaiYi.Core.Installation;
using KuaiYi.NativeHost;
using Xunit;
namespace KuaiYi.Core.Tests;

public sealed partial class InstallationRegistrationWithdrawalTests
{
    private static Dictionary<string,string> AllOriginalFiles(Rig r)=>Directory.GetFiles(r.Root,"*",SearchOption.AllDirectories)
        .ToDictionary(p=>Path.GetRelativePath(r.Root,p),MaterializedPayloadSource.Hash);
    private static void FilesUnchanged(Rig r,Dictionary<string,string> before)
    {
        Assert.Equal(before.OrderBy(p=>p.Key),AllOriginalFiles(r).OrderBy(p=>p.Key));
        Assert.Equal(0,r.Host.Writes+r.Startup.Writes);Assert.False(File.Exists(r.Store.PendingPath));
        Assert.Empty(Directory.GetFiles(r.App,"withdrawal-*"));
    }
    [Theory][InlineData(0)][InlineData(1)]
    public async Task PreflightObservesTheExistingPinnedClosureWithoutAuthorizingAnyRemainingAction(int intentValue)
    {
        var intent=(InstallationMaintenanceIntent)intentValue;
        await WithRig(async r=>
        {
            var before=AllOriginalFiles(r);var operation=r.Preflight(intent);var value=await r.Inspect(operation);
            Assert.Equal(InstallationMaintenanceStatus.ObservedReadOnly,value.Status);Assert.True(value.CleanupConfirmed);Assert.Empty(value.Errors);
            Assert.Equal(intent,value.Intent);Assert.Equal(operation.Operation,value.Operation);Assert.True(operation.OriginalWork.IsCompleted);Assert.True(operation.CleanupConfirmed);
            Assert.NotNull(value.Observation);Assert.Equal(r.Selected,value.Observation.Generation);Assert.Equal(r.Bundle,InstallationBootstrapDeployment.BundlePath(r.App,value.Observation.Bundle));
            Assert.All(value.RemainingSteps,step=>Assert.Equal(InstallationMaintenanceAuthorization.NotAuthorized,step.Authorization));
            if(intent==InstallationMaintenanceIntent.Uninstall)
            {Assert.Equal(InstallationMaintenanceAction.WithdrawSevenReferences,value.RemainingSteps[0].Action);Assert.Contains(value.RemainingSteps,step=>step.Action==InstallationMaintenanceAction.RemoveSettingsAndKeys);}
            else
            {Assert.Contains(value.RemainingSteps,step=>step.Action==InstallationMaintenanceAction.RestorePayload);Assert.DoesNotContain(value.RemainingSteps,step=>step.Action==InstallationMaintenanceAction.RemoveSettingsAndKeys);}
            Assert.Throws<InvalidOperationException>(()=>{_=operation.Start();});FilesUnchanged(r,before);r.Restored();
        });
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task PreflightCancellationBeforeAcquisitionOrAfterQualificationNeverCreatesAnIntent(bool afterQualification)
    {
        await WithRig(async r=>
        {
            var before=AllOriginalFiles(r);using var stop=new CancellationTokenSource();if(!afterQualification)stop.Cancel();
            var operation=r.Preflight(cancellation:stop.Token,faults:new(){BeforeClosing=()=>stop.Cancel()});var value=await r.Inspect(operation);
            Assert.Equal(InstallationMaintenanceStatus.Cancelled,value.Status);Assert.Null(value.Observation);Assert.True(value.CleanupConfirmed);
            Assert.Contains(value.Errors,e=>e is OperationCanceledException);Assert.Same(value,await operation.StartPreflight());
            if(!afterQualification)Assert.Equal(0,r.Startup.Reads+r.Startup.Closes);
            FilesUnchanged(r,before);r.Restored();
        });
    }
    [Fact]public async Task PreflightDuplicateRequestsShareTheOriginalTaskAndCannotBecomeWithdrawal()
    {
        await WithRig(async r=>
        {
            var before=AllOriginalFiles(r);var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);using var release=new ManualResetEventSlim();
            var operation=r.Preflight(faults:new(){BeforeClosing=()=>{entered.TrySetResult();if(!release.Wait(TimeSpan.FromSeconds(10)))throw new TimeoutException("PUBLIC_PREFLIGHT_WAIT");}});
            var first=operation.StartPreflight();
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));Assert.Same(first,operation.StartPreflight());
                Assert.Throws<InstallationBusyException>(()=>r.Store.EnterRoleAdmission());Assert.Throws<InstallationBusyException>(()=>{_=operation.SettleOriginalsAsync();});
                Assert.Throws<InvalidOperationException>(()=>{_=operation.Start();});
                var other=await r.Inspect(r.Preflight(InstallationMaintenanceIntent.Repair));Assert.Equal(InstallationMaintenanceStatus.Refused,other.Status);Assert.True(other.CleanupConfirmed);
            }
            finally{release.Set();}
            var value=await r.Inspect(operation);Assert.Equal(InstallationMaintenanceStatus.ObservedReadOnly,value.Status);Assert.Same(value,await first);
            Assert.Same(value,await operation.StartPreflight());FilesUnchanged(r,before);r.Restored();
        });
    }
    [Fact]public async Task PreflightClosedSourcesReturnOnlyTheHistoricalObservationAndAFreshOwnerMustRequalify()
    {
        await WithRig(async r=>
        {
            var operation=r.Preflight();var value=await r.Inspect(operation);Assert.Equal(InstallationMaintenanceStatus.ObservedReadOnly,value.Status);
            File.WriteAllText(Path.Combine(r.App,"current.json"),"PUBLIC_DAMAGED_CURRENT");var damaged=AllOriginalFiles(r);int reads=r.Startup.Reads;
            Assert.Same(value,await operation.StartPreflight());Assert.Equal(reads,r.Startup.Reads);Assert.Empty(await operation.SettleOriginalsAsync());
            Assert.Throws<InvalidOperationException>(()=>{_=operation.Start();});
            var fresh=await r.Inspect(r.Preflight(InstallationMaintenanceIntent.Repair));Assert.Equal(InstallationMaintenanceStatus.Refused,fresh.Status);Assert.Null(fresh.Observation);
            FilesUnchanged(r,damaged);
        });
    }
    [Fact]public async Task PreflightUnknownPendingIsPreservedWithoutAdoptingTheOtherOwnerOrReadingRegistry()
    {
        await WithRig(async r=>
        {
            byte[] pending="PUBLIC_UNKNOWN_ORIGINAL_PENDING"u8.ToArray();File.WriteAllBytes(r.Store.PendingPath,pending);var before=AllOriginalFiles(r);
            var value=await r.Inspect(r.Preflight(InstallationMaintenanceIntent.Repair));Assert.Equal(InstallationMaintenanceStatus.Refused,value.Status);Assert.Null(value.Observation);Assert.True(value.CleanupConfirmed);
            Assert.Equal(0,r.Startup.Reads+r.Startup.Closes+r.Host.Writes+r.Startup.Writes);Assert.Equal(pending,File.ReadAllBytes(r.Store.PendingPath));
            Assert.Equal(before.OrderBy(p=>p.Key),AllOriginalFiles(r).OrderBy(p=>p.Key));
        });
    }
    [Fact]public async Task PreflightRegistryCloseUnknownRetainsTheOriginalAdmissionAndSettlesWithoutReplay()
    {
        await WithRig(async r=>
        {
            var before=AllOriginalFiles(r);var failure=new IOException("PUBLIC_PREFLIGHT_ORIGINAL_REGISTRY_CLOSE");r.Startup.CloseError=failure;
            var operation=r.Preflight();var value=await r.Inspect(operation);Assert.Equal(InstallationMaintenanceStatus.Unconfirmed,value.Status);Assert.False(value.CleanupConfirmed);Assert.Null(value.Observation);
            Assert.Contains(value.Errors.SelectMany(Leaves),e=>ReferenceEquals(e,failure));Assert.Throws<InstallationBusyException>(()=>r.Store.EnterRoleAdmission());int reads=r.Startup.Reads;
            r.Startup.CloseError=null;Assert.Empty(await operation.SettleOriginalsAsync());Assert.True(operation.CleanupConfirmed);
            Assert.Same(value,await operation.StartPreflight());Assert.False(value.CleanupConfirmed);Assert.Equal(reads,r.Startup.Reads);
            using(var admission=r.Store.EnterRoleAdmission()){}FilesUnchanged(r,before);r.Restored();
        });
    }
    [Fact]public async Task PreflightIOCloseUnknownRetainsTheExactOriginalStreamAndAdmission()
    {
        await WithRig(async r=>
        {
            var before=AllOriginalFiles(r);var failure=new IOException("PUBLIC_PREFLIGHT_ORIGINAL_IO_CLOSE");IDisposable? original=null;
            var io=new InstallationPreparationFaults
            {
                Acquired=(kind,path,value)=>{if(path==Path.Combine(r.App,"current.json"))original=value;},
                BeforeClose=(kind,path)=>{if(path==Path.Combine(r.App,"current.json"))throw failure;}
            };
            var operation=r.Preflight(faults:new(){IO=io});var value=await r.Inspect(operation);
            Assert.Equal(InstallationMaintenanceStatus.Unconfirmed,value.Status);Assert.False(value.CleanupConfirmed);Assert.NotNull(original);Assert.Null(value.Observation);
            Assert.Contains(value.Errors.SelectMany(Leaves),e=>ReferenceEquals(e,failure));Assert.Throws<InstallationBusyException>(()=>r.Store.EnterRoleAdmission());
            io.BeforeClose=null;Assert.Empty(await operation.SettleOriginalsAsync());Assert.True(operation.CleanupConfirmed);Assert.Same(value,await operation.StartPreflight());
            Assert.IsType<FileStream>(original);Assert.False(((FileStream)original!).CanRead);FilesUnchanged(r,before);r.Restored();
        });
    }
    [Fact]public async Task PreflightAdmissionCloseUnknownClosesIndependentResourcesAndPreservesTheOwner()
    {
        await WithRig(async r=>
        {
            var before=AllOriginalFiles(r);var failure=new IOException("PUBLIC_PREFLIGHT_UPGRADE_CLOSE");var closed=new List<string>();
            var admission=new InstallationAdmissionFaults{ClosingLease=kind=>{closed.Add(kind);if(kind=="upgrade")throw failure;}};
            var operation=r.Preflight(faults:new(){Admission=admission});var value=await r.Inspect(operation);
            Assert.Equal(InstallationMaintenanceStatus.Unconfirmed,value.Status);Assert.False(value.CleanupConfirmed);Assert.Null(value.Observation);
            Assert.Contains("host",closed);Assert.DoesNotContain("setup",closed);Assert.Contains(value.Errors.SelectMany(Leaves),e=>ReferenceEquals(e,failure));
            Assert.Throws<InstallationBusyException>(()=>r.Store.EnterRoleAdmission());int reads=r.Startup.Reads;
            admission.ClosingLease=null;Assert.Empty(await operation.SettleOriginalsAsync());Assert.True(operation.CleanupConfirmed);Assert.Equal(reads,r.Startup.Reads);
            Assert.Same(value,await operation.StartPreflight());FilesUnchanged(r,before);r.Restored();
        });
    }
    [Fact]public async Task PreflightMatchingReceiptsAndPathsCannotReplaceThisInvocationsEmbeddedPins()
    {
        await WithRig(async r=>
        {
            var before=AllOriginalFiles(r);using var pins=System.Text.Json.JsonDocument.Parse(r.Pins);
            var foreign=System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,kind="KuaiYiInstallerSourcePins",availability="Pinned",generationSha256=new string('0',64),bootstrapSha256=pins.RootElement.GetProperty("bootstrapSha256").GetString()});
            var value=await r.Inspect(r.Preflight(InstallationMaintenanceIntent.Repair,pins:foreign));Assert.Equal(InstallationMaintenanceStatus.Refused,value.Status);Assert.True(value.CleanupConfirmed);Assert.Null(value.Observation);
            FilesUnchanged(r,before);r.Restored();
        });
    }
    [Fact]public async Task PreflightDamagedPayloadIsReportedAndRetainedWithoutRepairingItFromAReceipt()
    {
        await WithRig(async r=>
        {
            string damaged=Path.Combine(r.Selected.DirectoryPath,"KuaiYi.Core.dll");File.AppendAllText(damaged,"PUBLIC_EXTERNAL_DAMAGE");
            // Expected externally damaged input: the preflight must preserve these bytes, not the earlier valid fixture.
            r.Payload[damaged]=MaterializedPayloadSource.Hash(damaged);var before=AllOriginalFiles(r);
            var value=await r.Inspect(r.Preflight(InstallationMaintenanceIntent.Repair));Assert.Equal(InstallationMaintenanceStatus.Refused,value.Status);Assert.True(value.CleanupConfirmed);Assert.Null(value.Observation);
            FilesUnchanged(r,before);Assert.True(r.Keep);
        });
    }
    [Fact]public async Task PreflightChangedSecondObservationRefusesWithoutWritingEitherRegistryValue()
    {
        await WithRig(async r=>
        {
            var before=AllOriginalFiles(r);r.Startup.BeforeRead=()=>{if(r.Startup.Reads==2)r.Startup.Value=new(false);};
            var value=await r.Inspect(r.Preflight());Assert.Equal(InstallationMaintenanceStatus.Refused,value.Status);Assert.True(value.CleanupConfirmed);Assert.Null(value.Observation);
            Assert.Equal(2,r.Startup.Reads);Assert.False(r.Startup.Value.Exists);FilesUnchanged(r,before);
        });
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task PreflightUnavailableSourcesNeverTouchEvenANoncleanUnstartedRegistry(bool nonclean)
    {
        await WithRig(async r=>
        {
            var before=AllOriginalFiles(r);var startup=new Startup(nonclean);var registry=new InstallationStartupRegistrySnapshot(new InstallationDeploymentRegistrySnapshot(r.Host),startup);
            byte[] unavailable=File.ReadAllBytes(Path.Combine(MaterializedPayloadSource.Repo,"packaging/installer-source-pins.json"));
            var value=await r.Inspect(r.Preflight(pins:unavailable,registry:registry));Assert.Equal(InstallationMaintenanceStatus.Refused,value.Status);Assert.True(value.CleanupConfirmed);
            Assert.Equal(0,startup.Reads+startup.Writes+startup.Closes);Assert.Equal(!nonclean,startup.CleanupConfirmed);FilesUnchanged(r,before);
        });
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task PreflightClosingErrorsRemainDistinctFromCancellationAndNeverReturnAnObservation(bool afterClosing)
    {
        await WithRig(async r=>
        {
            var before=AllOriginalFiles(r);var failure=new IOException("PUBLIC_PREFLIGHT_CLOSING_ERROR");
            var faults=afterClosing?new InstallationWithdrawalFaults{AfterClosing=()=>throw failure}:new InstallationWithdrawalFaults{BeforeClosing=()=>throw failure};
            var operation=r.Preflight(faults:faults);var value=await r.Inspect(operation);
            Assert.Equal(afterClosing?InstallationMaintenanceStatus.Unconfirmed:InstallationMaintenanceStatus.Refused,value.Status);Assert.True(value.CleanupConfirmed);Assert.Null(value.Observation);
            Assert.Contains(value.Errors,e=>ReferenceEquals(e,failure));Assert.Same(value,await operation.StartPreflight());FilesUnchanged(r,before);
        });
    }
    [Fact]public async Task PreflightAuditFailurePreservesItsOriginalResultAndRoot()
    {
        Rig? retained=null;var error=await Record.ExceptionAsync(()=>WithRig(async r=>
        {
            retained=r;var value=await r.Inspect(r.Preflight());Assert.Equal(InstallationMaintenanceStatus.ObservedReadOnly,value.Status);
            r.EvidenceOutput=Path.Combine(r.Root,"PUBLIC_PREFLIGHT_AUDIT_IS_A_FILE");File.WriteAllText(r.EvidenceOutput,"PUBLIC_KEEP_PREFLIGHT_AUDIT_FAILURE");
        }));
        Assert.IsAssignableFrom<IOException>(error);Assert.NotNull(retained);Assert.True(retained.Keep);Assert.True(Directory.Exists(retained.Root));
        Assert.Single(retained.PreflightOutcomes);Assert.All(retained.Operations,o=>Assert.True(o.CleanupConfirmed));retained.PayloadUnchanged();
    }
    [Fact]public async Task WithdrawalOwnerCannotBeReinterpretedAsAPreflightEvenBeforeItStarts()
    {
        await WithRig(async r=>
        {
            var before=AllOriginalFiles(r);var operation=r.New();Assert.Throws<InvalidOperationException>(()=>{_=operation.StartPreflight();});operation.Abort();
            var value=await r.Run(operation);Assert.Equal(InstallationWithdrawalStatus.Refused,value.Status);FilesUnchanged(r,before);
            Assert.Throws<ArgumentOutOfRangeException>(()=>r.Preflight((InstallationMaintenanceIntent)99));
        });
    }
}

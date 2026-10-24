using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using Xunit;
using Xunit.Abstractions;
namespace KuaiYi.Core.Tests;
public sealed class ShellLifecycleTests(ITestOutputHelper output)
{
    private sealed class DirectoryPolicy:ISecretDirectoryPolicy{public void EnsurePrivate(string path)=>Directory.CreateDirectory(path);}
    private sealed class NoSecretReads:ISecretProtector
    {
        public int Reads;
        public byte[] Protect(ReadOnlySpan<byte> p)=>throw new InvalidOperationException("No keys in this fixture.");
        public byte[] Unprotect(ReadOnlySpan<byte> p){Reads++;throw new InvalidOperationException("Startup must not read keys.");}
    }
    private sealed class Fixture:IDisposable
    {
        public string Root{get;}=Path.Combine(Path.GetTempPath(),"kuaiyi-settings-tests-"+Guid.NewGuid().ToString("N"));
        public SettingsStore Store{get;}public SettingsCoordinator Coordinator{get;}public NoSecretReads Protector{get;}=new();
        public Fixture(){Store=new(Path.Combine(Root,"config"));Coordinator=new(Store,new SecretFileStore(Path.Combine(Root,"secrets"),Protector,new DirectoryPolicy()));}
        public async Task Seed(bool authenticated=false)
        {
            using var d=await Coordinator.OpenDraftAsync();d.Update(s=>s with{TriggerMode=TriggerMode.Drop,ActiveProfileId="public",Profiles=[new(){Id="public",Model="synthetic-model",BaseUrl="https://synthetic.invalid/v1",Authentication=authenticated?ApiAuthentication.Bearer:ApiAuthentication.None,SecretRef=authenticated?"key/"+new string('a',32):null}],Overlay=s.Overlay with{LastPosition=new(){MonitorId="public-secondary",X=.2,Y=.6}}});await Coordinator.SaveAsync(d);
        }
        public void Dispose(){if(Directory.Exists(Root))Directory.Delete(Root,true);}
    }
    private static EnableIntent Enable(ShellLifecycle shell)
    {Assert.True(shell.TryPrepareEnable(out var intent,out var missing),missing);Assert.True(shell.ConfirmEnable(intent!,true));return intent!;}
    [Fact]public void StrictProductionGateRejectsUnboundLocalIdentityEvenAfterLaterNetworkEnable()
    {
        var gate=new RuntimeGate(requireProfileBinding:true);var origin=new EndpointOrigin(new Uri("https://synthetic.invalid"));
        Assert.True(gate.TryStartConfirmedLocalRequest(gate.Snapshot.Epoch,Guid.NewGuid(),Guid.NewGuid(),out var local,out _));
        gate.ConsentTo(origin);Assert.True(gate.TryEnable(origin,true));Assert.False(gate.CanSend(local,origin));int sends=0;
        Assert.False(gate.TryBeginSend(local,origin,()=>{sends++;return Task.FromResult(1);},out _));Assert.Equal(0,sends);gate.Disable();
    }
    [Fact]public async Task SessionConsentSurvivesOffWhileNewOriginAndRestartRequireOwnConsent()
    {
        using var f=new Fixture();await f.Seed();using var shell=new ShellLifecycle(f.Coordinator);await shell.InitializeAsync();Assert.True(shell.TryPrepareEnable(out var initial,out _));Assert.False(shell.Gate.HasConsentTo(initial!.Origin));
        Enable(shell);shell.Gate.Disable();Assert.True(shell.Gate.HasConsentTo(initial.Origin));Enable(shell);
        using(var d=await f.Coordinator.OpenDraftAsync()){d.Update(s=>s with{Profiles=s.Profiles.Select(p=>p with{BaseUrl="https://different-synthetic.invalid"}).ToImmutableArray()});await f.Coordinator.SaveAsync(d);}
        Assert.True(shell.TryPrepareEnable(out var changed,out _));Assert.False(shell.Gate.HasConsentTo(changed!.Origin));
        using var restart=new ShellLifecycle(f.Coordinator);await restart.InitializeAsync();Assert.False(restart.Gate.HasConsentTo(initial.Origin));Assert.False(restart.Gate.HasConsentTo(changed.Origin));Assert.Equal(Activation.Off,restart.Gate.Snapshot.Activation);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ActualSavedStartupLoadsMetadataModePositionAndNeverRestoresOnOrDecrypts(bool authentication)
    {
        using var f=new Fixture();await f.Seed(authentication);using var shell=new ShellLifecycle(f.Coordinator);await shell.InitializeAsync();
        Assert.Equal(Activation.Off,shell.Gate.Snapshot.Activation);Assert.Equal(TriggerMode.Drop,shell.Gate.Snapshot.Mode);Assert.Equal("public-secondary",shell.Settings!.Data.Overlay.LastPosition!.MonitorId);Assert.Equal(0,f.Protector.Reads);
        Assert.False(shell.Gate.TryBeginPassiveCapture(TriggerMode.Drop,out _));Assert.False(shell.Gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out _,out _));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>shell.InitializeAsync());Assert.Equal(Activation.Off,shell.Gate.Snapshot.Activation);
    }
    [Fact]public async Task MissingConfigurationAndMissingKeyRemainOffWithoutConsentOrRequest()
    {
        using var f=new Fixture();using var shell=new ShellLifecycle(f.Coordinator);await shell.InitializeAsync();Assert.False(shell.TryPrepareEnable(out _,out var missing));Assert.Contains("API",missing);
        using var d=await f.Coordinator.OpenDraftAsync();d.Update(s=>s with{Profiles=[new(){Id="public",BaseUrl="https://synthetic.invalid",Model="public",Authentication=ApiAuthentication.Bearer}],ActiveProfileId="public"});await f.Coordinator.SaveAsync(d);
        Assert.False(shell.TryPrepareEnable(out _,out missing));Assert.Contains("Key",missing);Assert.Equal(Activation.Off,shell.Gate.Snapshot.Activation);Assert.Equal(0,f.Protector.Reads);
    }
    [Theory][InlineData("{bad")][InlineData("{\"schemaVersion\":99}")]
    public async Task DamagedOrFutureSettingsStayOffAndByteIdentical(string bytes)
    {
        using var f=new Fixture();Directory.CreateDirectory(f.Store.DirectoryPath);await File.WriteAllTextAsync(f.Store.PrimaryPath,bytes);using var shell=new ShellLifecycle(f.Coordinator);await shell.InitializeAsync();
        Assert.False(shell.TryPrepareEnable(out _,out _));Assert.Equal(bytes,await File.ReadAllTextAsync(f.Store.PrimaryPath));Assert.Equal(Activation.Off,shell.Gate.Snapshot.Activation);Assert.Equal(0,f.Protector.Reads);
        await shell.SavePositionAsync(new(){MonitorId="public",X=.5,Y=.5});Assert.Equal(bytes,await File.ReadAllTextAsync(f.Store.PrimaryPath));
    }
    [Fact]public async Task DeclinedAndStaleConsentCannotEnableAfterHideLockOrSettingsPublication()
    {
        using var f=new Fixture();await f.Seed();using var shell=new ShellLifecycle(f.Coordinator);await shell.InitializeAsync();Assert.True(shell.TryPrepareEnable(out var intent,out _));Assert.False(shell.ConfirmEnable(intent!,false));
        shell.Hide();Assert.False(shell.ConfirmEnable(intent!,true));shell.Show();Assert.True(shell.TryPrepareEnable(out intent,out _));shell.SetSessionLocked(true);Assert.False(shell.ConfirmEnable(intent!,true));shell.Show();Assert.Equal(Presentation.Hidden,shell.Gate.Snapshot.Presentation);shell.SetSessionLocked(false);
        Assert.True(shell.TryPrepareEnable(out intent,out _));using var d=await f.Coordinator.OpenDraftAsync();d.Update(s=>s with{Profiles=s.Profiles.Select(p=>p with{BaseUrl="https://other-synthetic.invalid"}).ToImmutableArray()});await f.Coordinator.SaveAsync(d);
        Assert.False(shell.ConfirmEnable(intent!,true));Assert.Equal(Activation.Off,shell.Gate.Snapshot.Activation);Enable(shell);Assert.Equal(0,f.Protector.Reads);
    }
    [Theory][InlineData("hide")][InlineData("lock")][InlineData("exit")]
    public async Task UnifiedGateLifecycleCancelsOriginalLinkedOperationAndRejectsLateSend(string action)
    {
        using var f=new Fixture();await f.Seed();using var shell=new ShellLifecycle(f.Coordinator);await shell.InitializeAsync();var intent=Enable(shell);
        Assert.False(shell.Gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out _,out _)); // Strict production dispatch cannot bypass bindings.
        Assert.True(shell.Gate.TryStartProfileRequest(intent.Binding,Guid.NewGuid(),Guid.NewGuid(),null,out var id,out var token));Assert.True(shell.Gate.TryLinkRequestCancellation(id,default,out var linked));using var operation=linked!;
        Task pending=Task.Delay(Timeout.Infinite,operation.Token);
        if(action=="hide")shell.Hide();else if(action=="lock")shell.SetSessionLocked(true);else shell.Dispose();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>await pending.WaitAsync(TimeSpan.FromSeconds(2)));Assert.True(token.IsCancellationRequested);Assert.False(shell.Gate.CanApply(id));Assert.False(shell.Gate.CanSend(id,intent.Origin));Assert.Equal(Activation.Off,shell.Gate.Snapshot.Activation);
        if(action=="lock")shell.SetSessionLocked(false);if(action!="exit")shell.Show();Assert.Equal(Activation.Off,shell.Gate.Snapshot.Activation);
        using var restart=new ShellLifecycle(f.Coordinator);await restart.InitializeAsync();Assert.Equal(Activation.Off,restart.Gate.Snapshot.Activation);
    }
    [Fact]public async Task SettingsCancelDoesNotPersistOrStopTaskAndCommitRevokesBindingWithoutOff()
    {
        using var f=new Fixture();await f.Seed();using var shell=new ShellLifecycle(f.Coordinator);await shell.InitializeAsync();var intent=Enable(shell);Assert.True(shell.Gate.TryStartProfileRequest(intent.Binding,Guid.NewGuid(),Guid.NewGuid(),null,out var id,out var token));
        string before=await File.ReadAllTextAsync(f.Store.PrimaryPath);shell.Gate.SetSuspension(Suspension.Settings,true);
        using(var d=await f.Coordinator.OpenDraftAsync())d.Update(s=>s with{Profiles=[] ,ActiveProfileId=null});shell.Gate.SetSuspension(Suspension.Settings,false);
        Assert.False(token.IsCancellationRequested);Assert.True(shell.Gate.CanApply(id));Assert.Equal(before,await File.ReadAllTextAsync(f.Store.PrimaryPath));
        using var committed=await f.Coordinator.OpenDraftAsync();committed.Update(s=>s with{Profiles=[],ActiveProfileId=null});await f.Coordinator.SaveAsync(committed);
        Assert.True(token.IsCancellationRequested);Assert.Equal(Activation.On,shell.Gate.Snapshot.Activation);Assert.False(shell.Gate.TryStartProfileRequest(intent.Binding,Guid.NewGuid(),Guid.NewGuid(),null,out _,out _));Assert.False(shell.TryPrepareEnable(out _,out _));
    }
    [Fact]public async Task PositionSavesThroughProductionTransactionAndRememberOffDoesNotRewrite()
    {
        using var f=new Fixture();await f.Seed();using var shell=new ShellLifecycle(f.Coordinator);await shell.InitializeAsync();var intent=Enable(shell);Assert.True(shell.Gate.TryStartProfileRequest(intent.Binding,Guid.NewGuid(),Guid.NewGuid(),null,out var id,out var token));
        var position=new OverlayPosition{MonitorId="public-negative-origin",X=.1,Y=.8};await shell.SavePositionAsync(position);Assert.Equal(position,(await f.Store.LoadAsync()).Data.Overlay.LastPosition);Assert.False(token.IsCancellationRequested);Assert.True(shell.Gate.CanApply(id));
        using(var d=await f.Coordinator.OpenDraftAsync()){d.Update(s=>s with{Overlay=s.Overlay with{RememberPosition=false}});await f.Coordinator.SaveAsync(d);}
        string before=await File.ReadAllTextAsync(f.Store.PrimaryPath);await shell.SavePositionAsync(position with{X=.9});Assert.Equal(before,await File.ReadAllTextAsync(f.Store.PrimaryPath));Assert.Equal(0,f.Protector.Reads);
    }
    [Fact]public async Task StartupIoFailureRemainsOffThenExplicitRealSaveRecoveryMakesShellUsable()
    {
        using var f=new Fixture();Directory.CreateDirectory(f.Store.DirectoryPath);Directory.CreateDirectory(f.Store.PrimaryPath);using var shell=new ShellLifecycle(f.Coordinator);
        await Assert.ThrowsAsync<IOException>(()=>shell.InitializeAsync());Assert.False(shell.TryPrepareEnable(out _,out _));Assert.Equal(Activation.Off,shell.Gate.Snapshot.Activation);
        Directory.Delete(f.Store.PrimaryPath);await f.Seed();Assert.True(shell.TryPrepareEnable(out _,out _));Assert.Equal(Activation.Off,shell.Gate.Snapshot.Activation);Enable(shell);Assert.Equal(0,f.Protector.Reads);
    }
    [Fact]public async Task ExplicitDefaultsRecoveryReinitializesBindingsAndModeWithoutEnabling()
    {
        using var f=new Fixture();Directory.CreateDirectory(f.Store.DirectoryPath);await File.WriteAllTextAsync(f.Store.PrimaryPath,"{corrupt-public");using var shell=new ShellLifecycle(f.Coordinator);await shell.InitializeAsync();
        using var d=await f.Coordinator.OpenDraftAsync();d.Update(s=>s with{ActiveProfileId="public",TriggerMode=TriggerMode.Drop,Profiles=[new(){Id="public",BaseUrl="https://synthetic.invalid",Model="public",Authentication=ApiAuthentication.None}]});await f.Coordinator.SaveAsync(d,SettingsRecovery.UseDefaults);
        Assert.Equal(Activation.Off,shell.Gate.Snapshot.Activation);Assert.Equal(TriggerMode.Drop,shell.Gate.Snapshot.Mode);Assert.True(shell.TryPrepareEnable(out _,out _));Assert.Equal("{corrupt-public",await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(f.Store.DirectoryPath,"settings.corrupt-*.json"))));Assert.Equal(0,f.Protector.Reads);
    }
    [Theory][InlineData(1.0)][InlineData(1.25)][InlineData(1.5)][InlineData(2.0)]
    public void PositionRestoreClampsNormalizedSavedAnchorAgainstNewWorkAreaAndRemovedMonitor(double dpi)
    {
        var primary=new MonitorGeometry("primary",new(-1600,-900,0,0),new(dpi));var second=new MonitorGeometry("secondary",new(0,0,1800,1000),new(1));var saved=new OverlayPosition{MonitorId="removed",X=1,Y=1,OffsetXDip=10000,OffsetYDip=10000};
        var restored=OverlayPositionMapper.Restore(saved,[primary,second],primary.Id);Assert.True(primary.WorkArea.Inset(primary.Dpi.CeilingPixels(12)).Contains(restored));Assert.Equal(primary.Dpi.CeilingPixels(48),restored.Width);
        var captured=OverlayPositionMapper.Capture(restored,primary);Assert.Equal(restored,OverlayPositionMapper.Restore(captured,[primary,second],primary.Id));
        var shrinking=primary with{WorkArea=new(-800,-500,0,0),Dpi=new(2)};Assert.True(shrinking.WorkArea.Contains(OverlayPositionMapper.Restore(captured,[shrinking],primary.Id)));
    }
    [Theory][InlineData(1.0,6)][InlineData(2.0,12)]
    public void ProductionPointerThresholdUsesDpiAndSystemThresholdWithoutTogglingForDrag(double scale,int threshold)
    {
        var gesture=new OverlayPointerGesture();Assert.Equal(OverlayPointerAction.Pressed,gesture.Down(new(100,100),new(90,90,160,160),true));
        Assert.Equal(OverlayPointerAction.Ignored,gesture.Move(new(100+threshold-1,100+threshold-1),out _,4,4,new(scale)));Assert.Equal(OverlayPointerAction.Clicked,gesture.Up());
        gesture.Down(new(100,100),new(90,90,160,160),true);Assert.Equal(OverlayPointerAction.Moved,gesture.Move(new(100+threshold,100),out _,4,4,new(scale)));Assert.Equal(OverlayPointerAction.Released,gesture.Up());
        gesture.Down(new(100,100),new(90,90,160,160),true);Assert.Equal(OverlayPointerAction.Ignored,gesture.Move(new(109,100),out _,10,10,new(1)));Assert.Equal(OverlayPointerAction.Clicked,gesture.Up());
    }
    private sealed class InstanceChild(string role,ProcessStartInfo info)
    {
        private static readonly TimeSpan ExitLimit=TimeSpan.FromSeconds(4);
        private static readonly List<InstanceChild> Retained=[];
        private Process? process;private Task? exit,stdout;
        private Task<string>? stderr;private readonly List<Task> writes=[];
        private readonly TaskCompletionSource<string?> first=new(TaskCreationOptions.RunContinuationsAsynchronously);
        private string? firstLine,remainingOutput,diagnostics;private int? exitCode;
        private bool actualExit,ioSettled,resourcesDisposed,killed;
        internal void Start()
        {
            process=Process.Start(info)??throw new IOException("Original instance fixture Start returned no process.");
            List<Exception> errors=[];
            try{exit=process.WaitForExitAsync();}catch(Exception error){errors.Add(error);}
            try{stderr=process.StandardError.ReadToEndAsync();}catch(Exception error){errors.Add(error);}
            try{stdout=ReadOutput();}catch(Exception error){first.TrySetException(error);errors.Add(error);}
            ThrowInstanceErrors(errors);
        }
        private async Task ReadOutput()
        {
            try
            {
                firstLine=await process!.StandardOutput.ReadLineAsync();first.TrySetResult(firstLine);
                remainingOutput=await process.StandardOutput.ReadToEndAsync();
            }
            catch(Exception error){first.TrySetException(error);throw;}
        }
        internal Task<string?> Row()=>first.Task.WaitAsync(TimeSpan.FromSeconds(5));
        internal Task Release()
        {var original=process!.StandardInput.WriteLineAsync();writes.Add(original);return original;}
        internal void Kill(){process!.Kill();killed=true;}
        internal async Task Finish(int? expected=null)
        {
            if(exit==null)throw new IOException("Original instance exit task is unavailable.");
            await exit.WaitAsync(ExitLimit);actualExit=process!.HasExited;
            if(!actualExit)throw new IOException("Original instance process has not exited.");
            exitCode=process.ExitCode;if(expected.HasValue)Assert.Equal(expected.Value,exitCode.Value);
        }
        internal async Task Cleanup(List<Exception> errors)
        {
            if(process==null)return; // Start threw before returning an owned Process; never adopt a PID.
            try{if(!process.HasExited)Kill();}catch(Exception error){AddInstanceError(errors,error);}
            try{await Finish();}catch(Exception error){AddInstanceError(errors,error);}
            foreach(var original in writes.Concat(new Task?[]{first.Task,stdout,stderr}.OfType<Task>()))
                try{await original.WaitAsync(ExitLimit);}catch(Exception error){AddInstanceError(errors,error);}
            if(stderr?.IsCompletedSuccessfully==true)diagnostics=stderr.Result;
            ioSettled=first.Task.IsCompleted&&stdout?.IsCompleted==true&&stderr?.IsCompleted==true&&writes.All(task=>task.IsCompleted);
            if(!actualExit||exit?.IsCompletedSuccessfully!=true||!ioSettled)
            {lock(Retained)Retained.Add(this);errors.Add(new IOException("Original instance process/IO remains unconfirmed; original owner retained."));return;}
            resourcesDisposed=true;
            foreach(Func<IDisposable> resource in new Func<IDisposable>[]{()=>process.StandardInput,()=>process.StandardOutput,()=>process.StandardError,()=>process})
                try{resource().Dispose();}catch(Exception error){resourcesDisposed=false;AddInstanceError(errors,error);}
        }
        internal string Evidence=>JsonSerializer.Serialize(new
        {
            Role=role,Arguments=info.ArgumentList.ToArray(),FirstLine=firstLine,RemainingStdout=remainingOutput,Stderr=diagnostics,
            ExitCode=exitCode,ActualExitConfirmed=actualExit,OriginalExit=exit?.Status.ToString(),OriginalFirstLine=first.Task.Status.ToString(),OriginalStdout=stdout?.Status.ToString(),OriginalStderr=stderr?.Status.ToString(),
            OriginalWrites=writes.Select(task=>task.Status.ToString()).ToArray(),OriginalIoSettled=ioSettled,ResourcesDisposed=resourcesDisposed,ExplicitOwnedKill=killed
        });
    }
    private static void AddInstanceError(List<Exception> errors,Exception error)
    {if(!errors.Any(original=>ReferenceEquals(original,error)))errors.Add(error);}
    private static void ThrowInstanceErrors(List<Exception> errors)
    {
        if(errors.Count==1)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if(errors.Count>1)throw new AggregateException(errors);
    }
    private async Task WithInstanceChildren(Func<Func<string,int,string,InstanceChild>,Task> body)
    {
        List<InstanceChild> children=[];List<Exception> errors=[];
        InstanceChild Start(string user,int session,string role)=>Child(user,session,role,children);
        try{await body(Start);}catch(Exception error){errors.Add(error);}
        foreach(var child in children)try{await child.Cleanup(errors);}catch(Exception error){AddInstanceError(errors,error);}
        foreach(var child in children)try{output.WriteLine("PUBLIC_INSTANCE_OBSERVATION "+child.Evidence);}catch(Exception error){AddInstanceError(errors,error);}
        ThrowInstanceErrors(errors);
    }
    private static InstanceChild Child(string user,int session,string role,List<InstanceChild> children)
    {
        string dll=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../tools/KuaiYi.SettingsStoreFixture/bin",new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name,"net10.0/KuaiYi.SettingsStoreFixture.dll"));
        var info=new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet"){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var arg in new[]{dll,"--instance",user,session.ToString(System.Globalization.CultureInfo.InvariantCulture)})info.ArgumentList.Add(arg);
        var child=new InstanceChild(role,info);children.Add(child);child.Start();return child;
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ActualChildInstanceExcludesDuplicateAndReleasesAfterExitOrCrash(bool kill)
    {
        string user="public-synthetic-"+Guid.NewGuid().ToString("N");await WithInstanceChildren(async start=>
        {
            var owner=start(user,7,"owner");Assert.Equal("OWNER",await owner.Row());
            var secondary=start(user,7,"secondary");Assert.Equal("SECONDARY",await secondary.Row());await secondary.Finish(4);
            if(kill)owner.Kill();else await owner.Release();await owner.Finish();
            var next=start(user,7,"replacement");Assert.Equal("OWNER",await next.Row());await next.Release();await next.Finish(0);
        });
    }
    [Fact]public async Task ActualDifferentUserAndDifferentSessionChildInstancesCanOwnIndependentScopes()
    {
        string user="public-synthetic-"+Guid.NewGuid().ToString("N");await WithInstanceChildren(async start=>
        {
            var children=new[]{start(user,7,"scope-a"),start(user,8,"scope-b"),start(user+"other",7,"scope-c")};
            foreach(var child in children)Assert.Equal("OWNER",await child.Row());
            foreach(var child in children){await child.Release();await child.Finish(0);}
        });
    }
}

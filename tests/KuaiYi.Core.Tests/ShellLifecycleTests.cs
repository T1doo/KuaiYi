using System.Collections.Immutable;
using System.Diagnostics;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class ShellLifecycleTests
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
    private static Process Child(string user,int session)
    {
        string dll=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../tools/KuaiYi.SettingsStoreFixture/bin",new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name,"net10.0/KuaiYi.SettingsStoreFixture.dll"));
        var info=new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet"){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var arg in new[]{dll,"--instance",user,session.ToString(System.Globalization.CultureInfo.InvariantCulture)})info.ArgumentList.Add(arg);return Process.Start(info)!;
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ActualChildInstanceExcludesDuplicateAndReleasesAfterExitOrCrash(bool kill)
    {
        string user="public-synthetic-"+Guid.NewGuid().ToString("N");using var owner=Child(user,7);
        try
        {
            Assert.Equal("OWNER",await owner.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)));using var secondary=Child(user,7);Assert.Equal("SECONDARY",await secondary.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)));await secondary.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(4));Assert.Equal(4,secondary.ExitCode);
            if(kill)owner.Kill();else await owner.StandardInput.WriteLineAsync();await owner.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(4));
            using var next=Child(user,7);try{Assert.Equal("OWNER",await next.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)));await next.StandardInput.WriteLineAsync();await next.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(4));Assert.Equal(0,next.ExitCode);}finally{if(!next.HasExited){next.Kill();await next.WaitForExitAsync();}}
        }
        finally{if(!owner.HasExited){owner.Kill();await owner.WaitForExitAsync();}}
    }
    [Fact]public async Task ActualDifferentUserAndDifferentSessionChildInstancesCanOwnIndependentScopes()
    {
        string user="public-synthetic-"+Guid.NewGuid().ToString("N");var children=new[]{Child(user,7),Child(user,8),Child(user+"other",7)};
        try{foreach(var child in children)Assert.Equal("OWNER",await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)));foreach(var child in children){await child.StandardInput.WriteLineAsync();await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(4));Assert.Equal(0,child.ExitCode);}}
        finally{foreach(var child in children){if(!child.HasExited){child.Kill();await child.WaitForExitAsync();}child.Dispose();}}
    }
}

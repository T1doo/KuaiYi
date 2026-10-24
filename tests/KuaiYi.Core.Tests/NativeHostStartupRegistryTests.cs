using System.ComponentModel;
using System.Text;
using KuaiYi.Core.Installation;
using KuaiYi.NativeHost;
using Xunit;
namespace KuaiYi.Core.Tests;

// All registry calls below are synthetic. Never invoke the production Native implementation.
public sealed class NativeHostStartupRegistryTests
{
    private const string Command="\"C:\\PUBLIC\\KuaiYi.Launcher.exe\"";
    private static readonly InstallationStartupValue Absent=new(false),Target=new(true,"REG_SZ",Command);
    private static TaskCompletionSource Signal()=>new(TaskCreationOptions.RunContinuationsAsynchronously);
    private sealed record Raw(uint Type,byte[] Bytes);
    private sealed class Native:IStartupRegistryNative
    {
        internal bool KeyExists=true;
        internal Raw? Named;
        internal readonly Dictionary<string,string> Neighbours=new(){["PUBLIC_OTHER_PRODUCT"]="PUBLIC_UNCHANGED",[""]="PUBLIC_DEFAULT_UNCHANGED"};
        internal readonly List<int> Accesses=[];
        internal readonly List<nint> Closed=[];
        internal readonly HashSet<nint> Live=[];
        internal readonly List<int> QueryBuffers=[];
        internal int Opens,Creates,Queries,Sets,Deletes,Flushes,CloseCode;
        internal Action? AfterOpen,BeforeQuery,AfterCreate,AfterSet;
        private nint next=100;
        internal void Put(string value,uint kind=1)=>Named=new(kind,Encoding.Unicode.GetBytes(value+"\0"));
        private void Own(ref nint handle){handle=++next;Assert.True(Live.Add(handle));}
        public int Open(int access,ref nint handle)
        {Opens++;Accesses.Add(access);if(!KeyExists)return 2;Own(ref handle);AfterOpen?.Invoke();return 0;}
        public int Create(int access,ref nint handle)
        {Creates++;Accesses.Add(access);KeyExists=true;Own(ref handle);AfterCreate?.Invoke();return 0;}
        public int Query(nint handle,byte[] buffer,out uint type,ref uint bytes)
        {
            Assert.Contains(handle,Live);Queries++;QueryBuffers.Add(buffer.Length);BeforeQuery?.Invoke();
            if(Named==null){type=0;bytes=uint.MaxValue;return 2;}
            type=Named.Type;bytes=(uint)Named.Bytes.Length;if(bytes>buffer.Length)return 234;
            Named.Bytes.CopyTo(buffer,0);return 0;
        }
        public int Set(nint handle,byte[] bytes)
        {Assert.Contains(handle,Live);Sets++;Named=new(1,bytes.ToArray());AfterSet?.Invoke();return 0;}
        public int Delete(nint handle){Assert.Contains(handle,Live);Deletes++;Named=null;return 0;}
        public int Flush(nint handle){Assert.Contains(handle,Live);Flushes++;return 0;}
        public int Close(nint handle)
        {Assert.Contains(handle,Live);Closed.Add(handle);if(CloseCode!=0)return CloseCode;Assert.True(Live.Remove(handle));return 0;}
    }
    private sealed class Host:IHostRegistrationStore
    {
        internal HostRegistrationSlot Chrome=new(false,null),Edge=new(false,null);
        internal int Reads,Writes,Closes;
        internal Exception? CloseError;
        private bool unsettled;
        public bool CleanupConfirmed=>!unsettled;
        public HostRegistrationSlot Read(IntegrationBrowser browser){Reads++;return browser==IntegrationBrowser.Chrome?Chrome:Edge;}
        public void Write(IntegrationBrowser browser,HostRegistrationSlot expected,string? value)
        {Writes++;Assert.Equal(expected,browser==IntegrationBrowser.Chrome?Chrome:Edge);if(browser==IntegrationBrowser.Chrome)Chrome=new(true,value);else Edge=new(true,value);}
        public void FinalizeCleanup(){Closes++;if(CloseError is{} error){unsettled=true;throw error;}unsettled=false;}
    }
    private static InstallationStartupRegistrySnapshot Snapshot(Host host,Native native)=>new(new InstallationDeploymentRegistrySnapshot(host),new WindowsStartupRegistrationStore(native));
    private static async Task Prepare(InstallationStartupRegistrySnapshot owner)
    {await owner.CaptureAsync();Assert.Equal(Absent,await owner.ReadStartupAsync());await owner.ReadForPreparationAsync();}
    private static IReadOnlyList<Exception> Leaves(Exception error)=>error is AggregateException many?many.Flatten().InnerExceptions:[error];

    [Fact]public void ProductionFactoryExposesStartupCapabilityWithoutAnyPlatformOperation()
    {
        var owner=InstallationDeploymentRegistrySnapshot.CreateWindows();
        Assert.IsType<InstallationStartupRegistrySnapshot>(owner);Assert.True(owner.CleanupConfirmed);Assert.False(owner.ConflictObserved);
        Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Run",WindowsStartupRegistrationStore.KeyPath);
        Assert.Equal("KuaiYi",WindowsStartupRegistrationStore.ValueName);
    }
    [Fact]public void NamedReadAndSetRemoveTouchNeitherSharedKeyNorNeighbours()
    {
        var native=new Native();var store=new WindowsStartupRegistrationStore(native);var neighbours=native.Neighbours.ToArray();
        Assert.Equal(Absent,store.Read());store.Write(Absent,Target);Assert.Equal(Target,store.Read());store.Write(Target,Absent);
        Assert.Null(native.Named);Assert.True(native.KeyExists);Assert.Equal(neighbours,native.Neighbours.ToArray());
        Assert.Equal(1,native.Sets);Assert.Equal(1,native.Deletes);Assert.Equal(2,native.Flushes);Assert.Equal(0,native.Creates);
        Assert.Equal(new[]{0x101,0x103,0x101,0x103},native.Accesses);Assert.All(native.QueryBuffers,n=>Assert.Equal(8194,n));Assert.Empty(native.Live);Assert.True(store.CleanupConfirmed);
    }
    [Fact]public void MissingKeyCreatesOnlyForAnExpectedAbsentValueAndRechecksNamedValue()
    {
        var native=new Native{KeyExists=false};var store=new WindowsStartupRegistrationStore(native);Assert.Equal(Absent,store.Read());store.Write(Absent,Target);
        Assert.Equal(1,native.Creates);Assert.Equal(Target,store.Read());Assert.Empty(native.Live);
        native=new(){KeyExists=false};native.AfterCreate=()=>native.Put("PUBLIC_FOREIGN");store=new(native);
        Assert.Throws<HostRegistrationConflictException>(()=>store.Write(Absent,Target));Assert.Equal(0,native.Sets);Assert.True(native.KeyExists);Assert.Empty(native.Live);
    }
    [Fact]public void AbsentToAbsentDoesNotCreateAKeyAndIdenticalValueDoesNotRewrite()
    {
        var native=new Native{KeyExists=false};var store=new WindowsStartupRegistrationStore(native);store.Write(Absent,Absent);Assert.Equal(0,native.Creates);Assert.Equal(0,native.Flushes);
        native.KeyExists=true;native.Put(Command);store.Write(Target,Target);Assert.Equal(0,native.Sets);Assert.Equal(0,native.Deletes);Assert.Equal(0,native.Flushes);Assert.Empty(native.Live);
    }
    [Theory][InlineData("expand")][InlineData("dword")][InlineData("unterminated")][InlineData("odd")][InlineData("embedded-null")][InlineData("surrogate")][InlineData("oversize")]
    public void UnsupportedStoredBytesAreObservedAsUnknownAndNeverExpandedOrRepaired(string kind)
    {
        var native=new Native();native.Named=kind switch
        {
            "expand"=>new(2,Encoding.Unicode.GetBytes("%PUBLIC%\\KuaiYi.Launcher.exe\0")),"dword"=>new(4,[1,0,0,0]),
            "unterminated"=>new(1,Encoding.Unicode.GetBytes("PUBLIC")),"odd"=>new(1,[65,0,0]),"embedded-null"=>new(1,[0,0,0,0]),
            "surrogate"=>new(1,[0,216,0,0]),"oversize"=>new(1,new byte[8196]),_=>throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var store=new WindowsStartupRegistrationStore(native);var actual=store.Read();Assert.True(actual.Exists);Assert.NotEqual("REG_SZ",actual.Kind);
        Assert.Throws<InstallationRecoveryException>(()=>InstallationStartupRegistration.Validate(actual));
        Assert.Throws<HostRegistrationConflictException>(()=>store.Write(Absent,Target));Assert.Equal(0,native.Sets);Assert.Equal(0,native.Deletes);Assert.Empty(native.Live);
    }
    [Theory][InlineData("long")][InlineData("expand")][InlineData("newline")][InlineData("null")][InlineData("surrogate")]
    public void UnsupportedNewCommandIsRejectedBeforeOpeningAnyKey(string kind)
    {
        var native=new Native();var store=new WindowsStartupRegistrationStore(native);
        InstallationStartupValue value=kind switch{"long"=>new InstallationStartupValue(true,"REG_SZ",new string('p',261)),"expand"=>new(true,"REG_EXPAND_SZ","%PUBLIC%"),"newline"=>new(true,"REG_SZ","PUBLIC\n"),"null"=>new(true,"REG_SZ","PUBLIC\0"),"surrogate"=>new(true,"REG_SZ","\ud800"),_=>throw new ArgumentOutOfRangeException(nameof(kind))};
        Assert.Throws<InstallationRecoveryException>(()=>store.Write(Absent,value));Assert.Equal(0,native.Opens);Assert.Equal(0,native.Sets);
    }
    [Fact]public void WindowsCommandBoundaryIsExactAndNotAnExpandedBudget()
    {
        var native=new Native();var store=new WindowsStartupRegistrationStore(native);var value=new InstallationStartupValue(true,"REG_SZ",new string('p',260));
        store.Write(Absent,value);Assert.Equal(value,store.Read());Assert.Throws<InstallationRecoveryException>(()=>store.Write(value,value with{Value=value.Value+"p"}));Assert.Equal(1,native.Sets);Assert.Empty(native.Live);
    }
    [Fact]public void AcquisitionAfterHandleThenFailureRetainsOriginalOwnerAndBothErrorsUntilExactClose()
    {
        var original=new IOException("PUBLIC_OPEN_AFTER_HANDLE");var native=new Native{AfterOpen=()=>throw original,CloseCode=5};var store=new WindowsStartupRegistrationStore(native);
        var error=Assert.Throws<AggregateException>(()=>store.Read());Assert.Same(original,error.InnerExceptions[0]);Assert.Equal(5,Assert.IsType<Win32Exception>(error.InnerExceptions[1]).NativeErrorCode);
        Assert.False(store.CleanupConfirmed);nint held=Assert.Single(native.Live);Assert.Throws<IOException>(()=>store.Read());Assert.Equal(1,native.Opens);
        native.CloseCode=0;native.AfterOpen=null;store.FinalizeCleanup();Assert.True(store.CleanupConfirmed);Assert.Empty(native.Live);Assert.Equal(new[]{held,held},native.Closed);Assert.Equal(1,native.Opens);
    }
    [Fact]public async Task ForeignNamedCompareAndCloseFailureStayStickyAfterExternalRestoration()
    {
        var native=new Native();var host=new Host();var owner=Snapshot(host,native);await Prepare(owner);
        native.Put("PUBLIC_FOREIGN");native.CloseCode=5;
        var error=await Assert.ThrowsAsync<AggregateException>(()=>owner.WriteStartupAsync(Absent,Target));
        Assert.IsType<HostRegistrationConflictException>(error.InnerExceptions[0]);Assert.Equal(5,Assert.IsType<Win32Exception>(error.InnerExceptions[1]).NativeErrorCode);
        Assert.True(owner.ConflictObserved);Assert.False(owner.CleanupConfirmed);nint original=Assert.Single(native.Live);int queries=native.Queries;
        native.Named=null;native.CloseCode=0;await owner.CloseOriginalsAsync();Assert.True(owner.CleanupConfirmed);Assert.True(owner.ConflictObserved);Assert.Empty(native.Live);Assert.Equal(original,native.Closed[^1]);
        Assert.Throws<InvalidOperationException>(()=>{_ =owner.ReadStartupAsync();});Assert.Throws<InvalidOperationException>(()=>{_ =owner.WriteDefaultAsync(true,new(false,null),"PUBLIC_NO_WRITE");});Assert.Equal(0,host.Writes);Assert.Equal(queries,native.Queries);
    }
    [Fact]public async Task OrdinaryWriteAfterEffectFailureCanBeObservedAndReversedWithoutForeignReclassification()
    {
        var original=new IOException("PUBLIC_SET_AFTER_EFFECT");var native=new Native();var host=new Host();var owner=Snapshot(host,native);await Prepare(owner);
        native.AfterSet=()=>throw original;Exception? error=await Record.ExceptionAsync(()=>owner.WriteStartupAsync(Absent,Target));Assert.Same(original,error);
        Assert.False(owner.ConflictObserved);Assert.True(owner.CleanupConfirmed);Assert.Equal(Target,await owner.ReadStartupAsync());
        native.AfterSet=null;await owner.WriteStartupAsync(Target,Absent);Assert.Equal(Absent,await owner.ReadStartupAsync());await owner.CloseOriginalsAsync();Assert.True(owner.CleanupConfirmed);Assert.Equal(1,native.Sets);Assert.Equal(1,native.Deletes);
    }
    [Fact]public async Task OriginalNativeWorkIsRegisteredBeforeInvocationAndBlocksHostWorkAndPrematureClose()
    {
        var entered=Signal();using var release=new ManualResetEventSlim();var native=new Native();var host=new Host();var owner=Snapshot(host,native);await Prepare(owner);int reads=host.Reads;
        native.BeforeQuery=()=>{Assert.False(owner.CleanupConfirmed);entered.TrySetResult();if(!release.Wait(TimeSpan.FromSeconds(8)))throw new TimeoutException("PUBLIC_ORIGINAL_QUERY_HELD");};
        Task<InstallationStartupValue> original=owner.ReadStartupAsync();
        List<Exception> errors=[];
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(8));Assert.False(original.IsCompleted);Assert.False(owner.CleanupConfirmed);
            Assert.Throws<InvalidOperationException>(()=>{_ =owner.ReadPublishedAsync();});Assert.Throws<InvalidOperationException>(()=>{_ =owner.CloseOriginalsAsync();});Assert.Equal(reads,host.Reads);
        }
        catch(Exception error){errors.Add(error);}
        try{release.Set();}catch(Exception error){errors.Add(error);}
        try{await original;}catch(Exception error){errors.Add(error);}
        if(errors.Count==1)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if(errors.Count>1)throw new AggregateException(errors);
        native.BeforeQuery=null;await owner.CloseOriginalsAsync();Assert.True(owner.CleanupConfirmed);Assert.Empty(native.Live);
    }
    [Fact]public async Task ClosingIndependentlyAttemptsBothOriginalOwnersAndPreservesBothFailures()
    {
        var hostFailure=new IOException("PUBLIC_HOST_CLOSE");var native=new Native();var host=new Host();var owner=Snapshot(host,native);await Prepare(owner);
        native.CloseCode=5;await Assert.ThrowsAsync<Win32Exception>(()=>owner.ReadStartupAsync());nint original=Assert.Single(native.Live);host.CloseError=hostFailure;
        Exception? error=await Record.ExceptionAsync(()=>owner.CloseOriginalsAsync());Assert.NotNull(error);var errors=Leaves(error!);Assert.Equal(2,errors.Count);Assert.Same(hostFailure,errors[0]);Assert.Equal(5,Assert.IsType<Win32Exception>(errors[1]).NativeErrorCode);
        Assert.Equal(original,native.Closed[^1]);Assert.False(owner.CleanupConfirmed);host.CloseError=null;native.CloseCode=0;await owner.CloseOriginalsAsync();Assert.True(owner.CleanupConfirmed);Assert.Empty(native.Live);
    }
    [Fact]public async Task MissingPreparationOrVerificationCannotAuthorizeNamedWrites()
    {
        var native=new Native();var host=new Host();var owner=Snapshot(host,native);
        Assert.Throws<InvalidOperationException>(()=>{_ =owner.ReadStartupAsync();});Assert.Throws<InvalidOperationException>(()=>{_ =owner.WriteStartupAsync(Absent,Target);});
        await owner.CaptureAsync();Assert.Throws<InvalidOperationException>(()=>{_ =owner.WriteStartupAsync(Absent,Target);});await owner.VerifyAsync();
        Assert.Throws<InvalidOperationException>(()=>{_ =owner.WriteStartupAsync(Absent,Target);});Assert.Equal(0,native.Sets);await owner.CloseOriginalsAsync();
    }
}

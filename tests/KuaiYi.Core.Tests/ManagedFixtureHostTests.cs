using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KuaiYi.Core.Settings;
using Xunit;
using Xunit.Abstractions;

namespace KuaiYi.Core.Tests;

// Test fixtures need the selected SDK host; a native testhost/apphost cannot dispatch another DLL.
internal static class ManagedFixtureHost
{
    internal static string Executable=>Select(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH"),Environment.ProcessPath);
    internal static string Select(string? declared,string? currentProcess)
    {
        string? selected=declared??currentProcess;
        if(selected==null||!Path.IsPathFullyQualified(selected)||
           !(Path.GetFileName(selected).Equals("dotnet",StringComparison.OrdinalIgnoreCase)||Path.GetFileName(selected).Equals("dotnet.exe",StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("An explicit SDK dotnet host is required for managed test fixtures.");
        return selected;
    }
}

public sealed class ManagedFixtureHostTests(ITestOutputHelper output)
{
    [Theory][InlineData("testhost.exe")][InlineData("public-apphost.exe")]
    public void DeclaredSdkHostWinsOverTheCurrentApphost(string leaf)
    {
        string sdk=Path.Combine(Path.GetTempPath(),"sdk","dotnet.exe");
        Assert.Equal(sdk,ManagedFixtureHost.Select(sdk,Path.Combine(Path.GetTempPath(),leaf)));
    }
    [Theory][InlineData("testhost.exe")][InlineData("public-apphost.exe")]
    public void AnApphostCannotBecomeAnImplicitDotnetHost(string leaf)
        =>Assert.Throws<InvalidOperationException>(()=>ManagedFixtureHost.Select(null,Path.Combine(Path.GetTempPath(),leaf)));
    [Theory][InlineData("dotnet")][InlineData("dotnet.exe")]
    public void TheCurrentProcessIsOnlyAFallbackWhenItIsActuallyDotnet(string leaf)
    {
        string sdk=Path.Combine(Path.GetTempPath(),leaf);Assert.Equal(sdk,ManagedFixtureHost.Select(null,sdk));
    }
    [Theory][InlineData("")][InlineData("dotnet")][InlineData("relative/dotnet.exe")]
    public void AnInvalidDeclaredHostCannotSilentlyFallBack(string declared)
        =>Assert.Throws<InvalidOperationException>(()=>ManagedFixtureHost.Select(declared,Path.Combine(Path.GetTempPath(),"dotnet")));

    [Fact]
    public async Task RealSdkAndRealApphostHaveDifferentDllDispatchAndEmitOwnedProcessReceipts()
    {
        string fixture=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../tools/KuaiYi.SettingsStoreFixture/bin/Release/net10.0/KuaiYi.SettingsStoreFixture.dll"));
        string apphost=Path.ChangeExtension(fixture,OperatingSystem.IsWindows()?".exe":null);
        Assert.True(File.Exists(apphost));
        var sdk=await Probe(ManagedFixtureHost.Executable,fixture);
        var ownApphost=await Probe(apphost,fixture);
        Assert.Equal(0,sdk.Exit);Assert.Equal(2,ownApphost.Exit);
        Assert.NotNull(sdk.Stdout);Assert.NotNull(sdk.Stderr);Assert.NotNull(ownApphost.Stdout);Assert.NotNull(ownApphost.Stderr);
        using var expected=JsonDocument.Parse(SettingsJson.Write(new()));using var actual=JsonDocument.Parse(sdk.Stdout.Prefix);
        Assert.True(JsonElement.DeepEquals(expected.RootElement,actual.RootElement));
        Assert.False(sdk.Stdout.Truncated);
        Assert.Equal(0,sdk.Stderr.Bytes);Assert.Equal(0,ownApphost.Stdout.Bytes);Assert.Equal(0,ownApphost.Stderr.Bytes);
    }
    private sealed record Drain(long Bytes,string Sha256,string Prefix,bool Truncated);
    private sealed record Receipt(string Executable,string ExecutableSha256,string CurrentProcess,string? DeclaredHost,int? Exit,bool ExitConfirmed,string Cleanup,string? OriginalExitWait,string? StdoutStatus,string? StderrStatus,Drain? Stdout,Drain? Stderr,string[] Failures);
    // Retain the exact process and original tasks on a failed diagnostic cleanup. Never release an unconfirmed owner.
    private sealed record RetainedProbe(Process Child,Task? Exit,Task<Drain>? Stdout,Task<Drain>? Stderr);
    private static readonly List<RetainedProbe> retained=[];
    private async Task<Receipt> Probe(string executable,string fixture)
    {
        var info=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        info.ArgumentList.Add(fixture);info.ArgumentList.Add("--defaults"); // Fixed public synthetic input only.
        string executableHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(executable)));
        Process? child=null;Task? exit=null;Task<Drain>? stdout=null,stderr=null;List<Exception> errors=[];
        bool exited=false,settled=false;int? exitCode=null;Receipt? receipt=null;
        try
        {
            child=Process.Start(info)??throw new IOException("Public fixture did not start.");
            exit=child.WaitForExitAsync();stdout=Read(child.StandardOutput.BaseStream);stderr=Read(child.StandardError.BaseStream);
            child.StandardInput.Close();await exit.WaitAsync(TimeSpan.FromSeconds(3));
            await Task.WhenAll(stdout,stderr).WaitAsync(TimeSpan.FromSeconds(3));
        }
        catch(Exception error){errors.Add(error);}
        finally
        {
            if(child!=null)
            {
                try{if(!child.HasExited)child.Kill();}catch(Exception error){errors.Add(error);}
                if(exit!=null)try{await exit.WaitAsync(TimeSpan.FromSeconds(3));}catch(Exception error){errors.Add(error);}
                if(stdout!=null&&stderr!=null)try{await Task.WhenAll(stdout,stderr).WaitAsync(TimeSpan.FromSeconds(3));}catch(Exception error){errors.Add(error);}
                try{exited=child.HasExited;if(exited)exitCode=child.ExitCode;}catch(Exception error){errors.Add(error);}
                settled=exited&&exit is {IsCompleted:true}&&stdout is {IsCompleted:true}&&stderr is {IsCompleted:true};
                if(settled)child.Dispose();
                else lock(retained)retained.Add(new(child,exit,stdout,stderr));
            }
            receipt=new(executable,executableHash,Environment.ProcessPath??"Unavailable",Environment.GetEnvironmentVariable("DOTNET_HOST_PATH"),exitCode,exited,
                child==null?"NoChildStarted":settled?"OriginalExitAndIoSettled":"Unconfirmed_OwnerAndOriginalTasksRetained",
                exit?.Status.ToString(),stdout?.Status.ToString(),stderr?.Status.ToString(),
                stdout is {IsCompletedSuccessfully:true}?stdout.Result:null,stderr is {IsCompletedSuccessfully:true}?stderr.Result:null,
                errors.Select(error=>$"{error.GetType().Name}:{error.HResult:X8}").ToArray());
            output.WriteLine("PUBLIC_MANAGED_CHILD "+JsonSerializer.Serialize(receipt));
        }
        if(errors.Count!=0||!settled)throw new AggregateException("Public managed fixture diagnostic failed; receipt preserves original task/exit state.",errors);
        return receipt;
    }
    private static async Task<Drain> Read(Stream input)
    {
        byte[] buffer=new byte[1024];using var prefix=new MemoryStream();using var digest=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);long total=0;int count;
        while((count=await input.ReadAsync(buffer))!=0)
        {
            total+=count;digest.AppendData(buffer.AsSpan(0,count));
            int retained=Math.Min(count,4096-(int)prefix.Length);if(retained>0)prefix.Write(buffer,0,retained);
            CryptographicOperations.ZeroMemory(buffer);
        }
        return new(total,Convert.ToHexString(digest.GetHashAndReset()),Encoding.UTF8.GetString(prefix.ToArray()),total>prefix.Length);
    }
}

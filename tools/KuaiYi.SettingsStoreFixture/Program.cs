using System.Security.Cryptography;
using System.Collections.Immutable;
using KuaiYi.Core.Settings;
// Public synthetic portable crash driver; never referenced by production App.
if(args.SequenceEqual(new[]{"--schema"})){Console.WriteLine(SettingsJson.Schema().ToJsonString(SettingsJson.Options));return 0;}
if(args.SequenceEqual(new[]{"--defaults"})){Console.WriteLine(System.Text.Encoding.UTF8.GetString(SettingsJson.Write(new())));return 0;}
if(args.Length is not (2 or 3)||(args.Length==3&&args[2]!="remove-shared")||!Path.GetFileName(args[0]).StartsWith("kuaiyi-settings-tests-",StringComparison.Ordinal)||!Enum.TryParse<SettingsWriteStage>(args[1],out var stopAt))return 2;
var store=new SettingsStore(Path.Combine(args[0],"config")){WriteStageForTest=stage=>{if(stage==stopAt){Console.WriteLine("PUBLIC_STAGE="+stage);Console.Out.Flush();Console.ReadLine();}}};
var coordinator=new SettingsCoordinator(store,new SecretFileStore(Path.Combine(args[0],"secrets"),new FixtureProtector(),new FixtureDirectory()));
using var draft=await coordinator.OpenDraftAsync();
if(args.Length==3)draft.Update(s=>s with{Profiles=s.Profiles.Where(p=>p.Id!="a").ToImmutableArray(),ActiveProfileId="b"});
else{draft.Update(s=>s with{Overlay=s.Overlay with{FontSizeDip=18}});draft.ClearKey("public");}
await coordinator.SaveAsync(draft);return 0;
sealed class FixtureDirectory:ISecretDirectoryPolicy{public void EnsurePrivate(string path)=>Directory.CreateDirectory(path);}
sealed class FixtureProtector:ISecretProtector
{
    private static readonly byte[] Key=Enumerable.Repeat((byte)19,32).ToArray();
    public byte[] Protect(ReadOnlySpan<byte> input){byte[] result=new byte[input.Length+28];RandomNumberGenerator.Fill(result.AsSpan(0,12));using var cipher=new AesGcm(Key,16);cipher.Encrypt(result.AsSpan(0,12),input,result.AsSpan(28),result.AsSpan(12,16));return result;}
    public byte[] Unprotect(ReadOnlySpan<byte> input){byte[] result=new byte[input.Length-28];using var cipher=new AesGcm(Key,16);cipher.Decrypt(input[..12],input[28..],input.Slice(12,16),result);return result;}
}

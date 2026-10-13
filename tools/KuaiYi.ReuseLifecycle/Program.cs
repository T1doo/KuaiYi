using KuaiYi.ReuseLifecycle;
if(args is ["--offline-tests"])return await LifecycleOffline.Run();
if(!OperatingSystem.IsWindows()){Console.WriteLine("KUAIYI_LIFECYCLE_BLOCKED WindowsNotRun");return 2;}
try {
    if(args is ["--parent",var worker,var kind,var token]&&byte.TryParse(kind,out byte mode)&&mode is >=1 and <=4&&Guid.TryParseExact(token,"N",out var nonce)&&nonce!=Guid.Empty)return await LifecycleParent.Run(worker,(LifecycleCase)mode,nonce);
    if(args is ["--supervise",var native]&&Path.GetFileName(native)=="KuaiYi.ReuseLifecycleNative.exe"&&File.Exists(native))return await LifecycleSupervisor.Run(Path.GetFullPath(native));
}catch{if(args.Length>0&&args[0]=="--parent")return 3;Console.WriteLine("KUAIYI_LIFECYCLE_FAIL EntryFailure");return 3;}
return 2;

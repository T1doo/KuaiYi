Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
function Assert-AlphaPe([string]$Path) {
    $bytes=[IO.File]::ReadAllBytes($Path)
    if($bytes.Length -lt 128 -or [BitConverter]::ToUInt16($bytes,0) -ne 0x5a4d){throw "Invalid PE: $Path"}
    $offset=[BitConverter]::ToInt32($bytes,0x3c)
    if($offset -lt 64 -or $offset -gt $bytes.Length-26 -or [BitConverter]::ToUInt32($bytes,$offset) -ne 0x4550 -or [BitConverter]::ToUInt16($bytes,$offset+4) -ne 0x8664 -or [BitConverter]::ToUInt16($bytes,$offset+24) -ne 0x20b){throw "Expected PE32+ x64: $Path"}
}
function Assert-AlphaBundle([string]$BundleRoot) {
    $root=[IO.Path]::GetFullPath($BundleRoot)
    if((Get-Item -LiteralPath $root).LinkType){throw 'Bundle root must not be a symbolic link.'}
    $manifest=Get-Content -LiteralPath (Join-Path $root 'text-alpha-manifest.json') -Raw | ConvertFrom-Json
    if($manifest.SchemaVersion -ne 1 -or $manifest.Kind -cne 'KuaiYiManagedTextAlpha' -or $manifest.Rid -cne 'win-x64' -or $manifest.SelfContained -ne $true -or $manifest.Installer -ne $false -or $manifest.ProductNativeDlls -cne 'NotIncluded'){throw 'Not a supported managed text Alpha bundle.'}
    $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($entry in $manifest.Files) {
        $relative=[string]$entry.Path
        if([IO.Path]::IsPathRooted($relative) -or $relative.Contains('\') -or $relative.Contains(':') -or $relative.Split('/') -contains '..' -or $relative.Split('/') -contains '.' -or $relative.Split('/') -contains '' -or -not $seen.Add($relative)){throw 'Unsafe or duplicate manifest path.'}
        $parent=Get-Item -LiteralPath (Split-Path (Join-Path $root $relative) -Parent)
        while(-not [StringComparer]::OrdinalIgnoreCase.Equals($parent.FullName,$root)){if($parent.LinkType){throw 'Bundle file parent is a symbolic link.'};$parent=Get-Item -LiteralPath (Split-Path $parent.FullName -Parent)}
        $file=Get-Item -LiteralPath (Join-Path $root $relative)
        if($file.PSIsContainer -or $file.LinkType -or $file.Length -ne $entry.Bytes -or (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant() -cne $entry.Sha256){throw "Bundle bytes differ: $relative"}
    }
    $actual=@(Get-ChildItem -LiteralPath $root -File -Recurse | ForEach-Object{[IO.Path]::GetRelativePath($root,$_.FullName).Replace('\','/')} | Where-Object{$_ -cne 'text-alpha-manifest.json'})
    if($actual.Count -ne $seen.Count -or @($actual | Where-Object{-not $seen.Contains($_)}).Count){throw 'Unlisted bundle files.'}
    foreach($name in 'run-text-alpha.ps1','text-alpha-common.ps1','selection-public.txt','app/Assets/KuaiYi.ico'){if(-not $seen.Contains($name)){throw "Required Alpha resource missing: $name"}}
    foreach($role in $manifest.Roles) {
        $allowed=@{'KuaiYi.App'='app';'KuaiYi.CaptureBroker'='app';'KuaiYi.ClipboardHost'='app';'KuaiYi.CompatibilityProbe'='diagnostics';'KuaiYi.GrowthHarness'='diagnostics';'KuaiYi.MockProvider'='test-provider'}
        if(-not $allowed.ContainsKey([string]$role.Name) -or $allowed[$role.Name] -cne $role.Folder){throw 'Unknown role or unsafe role folder.'}
        $folder=Join-Path $root $role.Folder
        Assert-AlphaPe (Join-Path $folder "$($role.Name).exe")
        $options=(Get-Content -LiteralPath (Join-Path $folder "$($role.Name).runtimeconfig.json") -Raw | ConvertFrom-Json).runtimeOptions
        if($options.PSObject.Properties.Name -contains 'framework' -or $options.PSObject.Properties.Name -contains 'frameworks' -or -not($options.PSObject.Properties.Name -contains 'includedFrameworks')){throw "Role is not self-contained: $($role.Name)"}
        foreach($framework in $options.includedFrameworks){if($framework.version -cne $manifest.Runtime){throw 'Runtime version differs from the frozen pack version.'}}
        foreach($name in 'coreclr.dll','hostfxr.dll','hostpolicy.dll','System.Private.CoreLib.dll'){if(-not(Test-Path -LiteralPath (Join-Path $folder $name))){throw "Missing app-local runtime: $name"}}
        $deps=Get-Content -LiteralPath (Join-Path $folder "$($role.Name).deps.json") -Raw | ConvertFrom-Json
        if($role.Folder -cne 'test-provider' -and @($deps.libraries.PSObject.Properties.Name | Where-Object{$_ -match 'MockProvider|\.Tests/'}).Count){throw 'A production/diagnostic role references the test provider.'}
    }
    if($manifest.Roles.Count -ne 6 -or @($manifest.Roles.Name | Sort-Object -Unique).Count -ne 6){throw 'Missing/duplicate Alpha role.'}
    foreach($name in 'PresentationFramework.dll','PresentationCore.dll','WindowsBase.dll','wpfgfx_cor3.dll'){if(-not(Test-Path -LiteralPath (Join-Path $root "app/$name"))){throw "Missing app-local Windows Desktop runtime: $name"}}
    return $manifest
}
function Assert-AlphaDesktop {
    if(-not $IsWindows -or [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString() -cne 'X64' -or [Environment]::OSVersion.Version.Build -lt 22000){throw 'Blocked: Windows 11 x64 is required. No desktop process started.'}
    if(-not [Environment]::UserInteractive -or [Diagnostics.Process]::GetCurrentProcess().SessionId -eq 0){throw 'Blocked: interactive user desktop required. No desktop process started.'}
    $principal=[Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
    if($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Blocked: run the Alpha test in an ordinary user session, not elevated.'}
    if(-not ('KuaiYiAlphaDesktopGuard' -as [type])) {
        Add-Type -TypeDefinition @'
using System; using System.Text; using System.Runtime.InteropServices;
public static class KuaiYiAlphaDesktopGuard {
 [DllImport("user32.dll",SetLastError=true)] public static extern IntPtr OpenInputDesktop(uint flags,bool inherit,uint access);
 [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] public static extern bool GetUserObjectInformation(IntPtr handle,int index,StringBuilder value,uint length,out uint needed);
 [DllImport("user32.dll")] public static extern bool CloseDesktop(IntPtr handle);
}
'@
    }
    $desktop=[KuaiYiAlphaDesktopGuard]::OpenInputDesktop(0,$false,1)
    if($desktop -eq [IntPtr]::Zero){throw 'Blocked: input desktop unavailable or locked.'}
    try{$name=[Text.StringBuilder]::new(256);[uint32]$needed=0;if(-not[KuaiYiAlphaDesktopGuard]::GetUserObjectInformation($desktop,2,$name,512,[ref]$needed) -or $name.ToString() -cne 'Default'){throw 'Blocked: unlocked Default input desktop required.'}}finally{[void][KuaiYiAlphaDesktopGuard]::CloseDesktop($desktop)}
    foreach($process in @(Get-Process -Name 'KuaiYi.App' -ErrorAction SilentlyContinue)){
        if($process.SessionId -eq [Diagnostics.Process]::GetCurrentProcess().SessionId){throw 'Blocked: an existing KuaiYi instance is running; it will not be closed or reused.'}
    }
}

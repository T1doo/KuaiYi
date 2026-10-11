param([string]$InstallRoot, [switch]$SourceOnly)
. "$PSScriptRoot/common.ps1"
Assert-SpecIntegrity
[xml]$manifest = Get-Content (Join-Path $script:RepositoryRoot 'packaging/identity/AppxManifest.xml') -Raw
$ns = [Xml.XmlNamespaceManager]::new($manifest.NameTable)
foreach ($entry in @{
    f='http://schemas.microsoft.com/appx/manifest/foundation/windows10'
    uap10='http://schemas.microsoft.com/appx/manifest/uap/windows10/10'
    com='http://schemas.microsoft.com/appx/manifest/com/windows10'
    desktop5='http://schemas.microsoft.com/appx/manifest/desktop/windows10/5'
}.GetEnumerator()) { $ns.AddNamespace($entry.Key,$entry.Value) }
$identity = $manifest.SelectSingleNode('/f:Package/f:Identity',$ns)
if ($identity.Name -cne 'KuaiYi.Development' -or $identity.Publisher -cne 'CN=KuaiYi Development' -or $identity.ProcessorArchitecture -ne 'x64') { throw 'Development identity mismatch.' }
if ($manifest.SelectSingleNode('/f:Package/f:Properties/uap10:AllowExternalContent',$ns).InnerText -ne 'true') { throw 'External content must be enabled.' }
$expected = @{ KuaiYi='KuaiYi.App.exe'; ImageWorker='KuaiYi.ImageWorker.exe' }
$apps = @($manifest.SelectNodes('/f:Package/f:Applications/f:Application',$ns))
if ($apps.Count -ne 2) { throw 'Both EXEs must have independent declarations.' }
foreach ($app in $apps) {
    if (-not $expected.ContainsKey($app.Id) -or $expected[$app.Id] -cne $app.Executable -or $app.GetAttribute('TrustLevel',$ns.LookupNamespace('uap10')) -ne 'mediumIL' -or $app.GetAttribute('RuntimeBehavior',$ns.LookupNamespace('uap10')) -ne 'win32App') { throw 'Application declaration mismatch.' }
    $project = if ($app.Id -eq 'KuaiYi') { 'KuaiYi.App' } else { 'KuaiYi.ImageWorker' }
    [xml]$sxs = Get-Content (Join-Path $script:RepositoryRoot "src/$project/app.manifest") -Raw
    $msix = $sxs.GetElementsByTagName('msix','urn:schemas-microsoft-com:msix.v1')
    if ($msix.Count -ne 1 -or $msix[0].packageName -cne $identity.Name -or $msix[0].publisher -cne $identity.Publisher -or $msix[0].applicationId -cne $app.Id) { throw "Independent EXE identity mismatch: $project" }
}
$classes=@($manifest.SelectNodes('//com:Class',$ns))
$types=@($manifest.SelectNodes('//desktop5:ItemType',$ns))
$extensions=@('.png','.jpg','.jpeg','.webp','.bmp','.gif')
if ($classes.Count -ne 1 -or $classes[0].Id -cne '86F7AB57-0F30-47A6-B284-3C7BC82DF8E2' -or $classes[0].Path -cne 'KuaiYi.ShellExtension.dll' -or $classes[0].ThreadingModel -cne 'STA' -or $types.Count -ne 6) { throw 'Native COM registration mismatch.' }
foreach ($type in $types) {
    if ($type.Type -notin $extensions) { throw 'Unsupported/wildcard menu scope.' }
    $verbs=@($type.SelectNodes('desktop5:Verb',$ns)); if ($verbs.Count -ne 1 -or $verbs[0].Clsid -cne $classes[0].Id) { throw 'Verb CLSID mismatch.' }
}
if (@($types | ForEach-Object Type | Sort-Object -Unique).Count -ne 6) { throw 'Duplicate file scope.' }
if (-not $SourceOnly) {
    if (-not $InstallRoot -or -not [IO.Path]::IsPathFullyQualified($InstallRoot)) { throw 'Absolute installed root required.' }
    $root = (Resolve-Path -LiteralPath $InstallRoot).Path
    foreach ($file in @('KuaiYi.App.exe','KuaiYi.ImageWorker.exe','KuaiYi.OcrNative.dll','KuaiYi.ShellExtension.dll')) {
        $path=Join-Path $root $file
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing install file: $file" }
        $bytes=[IO.File]::ReadAllBytes($path)
        if ($bytes.Length -lt 128 -or $bytes[0] -ne 77 -or $bytes[1] -ne 90) { throw "Not PE: $file" }
        $offset=[BitConverter]::ToInt32($bytes,60)
        if ($offset -lt 64 -or $offset + 26 -gt $bytes.Length -or [BitConverter]::ToUInt32($bytes,$offset) -ne 0x4550 -or [BitConverter]::ToUInt16($bytes,$offset+4) -ne 0x8664 -or [BitConverter]::ToUInt16($bytes,$offset+24) -ne 0x20b) { throw "Not PE32+ x64: $file" }
        if ($file.EndsWith('.exe')) {
            $text=[Text.Encoding]::UTF8.GetString($bytes)
            if (-not $text.Contains('urn:schemas-microsoft-com:msix.v1') -or -not $text.Contains('KuaiYi.Development') -or -not $text.Contains('CN=KuaiYi Development')) { throw "Embedded identity missing: $file" }
            $aid=if($file -eq 'KuaiYi.App.exe'){'KuaiYi'}else{'ImageWorker'}
            if (-not $text.Contains(('applicationId="' + $aid + '"'))) { throw "Embedded Application Id mismatch: $file" }
        }
    }
}
[pscustomobject]@{ Check='DevelopmentManifestConsistency'; Result='Pass'; SourceOnly=[bool]$SourceOnly; WindowsSchemaValidation='NotRun'; RuntimeIdentity='NotRun'; ModernMenu='NotRun' } | ConvertTo-Json

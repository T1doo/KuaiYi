param([ValidateSet('Validate','Register')][string]$Mode='Validate', [Parameter(Mandatory)][string]$InstallRoot, [Parameter(Mandatory)][string]$PackagePath)
. "$PSScriptRoot/common.ps1"
Assert-WindowsDesktop
& "$PSScriptRoot/validate-dev-identity.ps1" -InstallRoot $InstallRoot
$root=(Resolve-Path -LiteralPath $InstallRoot).Path
$package=(Resolve-Path -LiteralPath $PackagePath).Path
$tool=Get-Command SignTool.exe -ErrorAction Stop
if ((Get-Item $tool.Source).VersionInfo.CompanyName -notmatch 'Microsoft') { throw 'Microsoft Windows SDK SignTool is required.' }
Invoke-Checked $tool.Source @('verify','/pa',$package)
$signature=Get-AuthenticodeSignature -LiteralPath $package
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -cne 'CN=KuaiYi Development') { throw 'Only trusted development identity signature is accepted.' }
# Inspect package XML rather than trust filename/signing subject alone.
Add-Type -AssemblyName System.IO.Compression
$zip=[IO.Compression.ZipFile]::OpenRead($package)
try {
    $entry=$zip.GetEntry('AppxManifest.xml'); if (-not $entry) { throw 'Missing packaged manifest.' }
    $reader=[IO.StreamReader]::new($entry.Open())
    try { [xml]$xml=$reader.ReadToEnd() } finally { $reader.Dispose() }
    $expected=[IO.File]::ReadAllText((Join-Path $script:RepositoryRoot 'packaging/identity/AppxManifest.xml'))
    [xml]$expectedXml=$expected
    if ($xml.OuterXml -cne $expectedXml.OuterXml) { throw 'Packaged manifest differs from reviewed development manifest.' }
} finally { $zip.Dispose() }
$existing=@(Get-AppxPackage -Name 'KuaiYi.Development' | Where-Object { $_.Name -ceq 'KuaiYi.Development' })
if ($existing.Count -gt 0) { throw 'Own development identity already exists; inspect it explicitly. This script never removes or replaces registered packages.' }
if ($Mode -eq 'Register') {
    # Explicit future user invocation only. No cert import, elevation, security-policy bypass or Explorer restart.
    Add-AppxPackage -Path $package -ExternalLocation $root
    Write-Output 'Development registration requested; run test-installed-identity.ps1 for each EXE. Registration alone is not runtime/OCR/menu proof.'
} else { Write-Output 'Signature/manifest/install path checks passed; Validate mode made no registration or certificate changes.' }

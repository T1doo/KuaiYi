param([ValidateSet('Development','Release')][string]$Mode='Development', [Parameter(Mandatory)][string]$InstallRoot,
      [string]$OutputPath, [string]$CertificateThumbprint)
. "$PSScriptRoot/common.ps1"
if ($Mode -eq 'Release') { throw 'Blocked: release signing and installer implementation are outside this P0 development slice; no unsigned release fallback.' }
Assert-WindowsDesktop
& "$PSScriptRoot/validate-dev-identity.ps1" -InstallRoot $InstallRoot
function Find-WindowsSdkTool([string]$Name) {
    $tool=Get-Command $Name -ErrorAction Stop
    $company=(Get-Item $tool.Source).VersionInfo.CompanyName
    if ($company -notmatch 'Microsoft') { throw "Not Microsoft Windows SDK tool: $Name" }
    return $tool.Source
}
$makeappx=Find-WindowsSdkTool 'MakeAppx.exe'
if (-not $OutputPath) { $OutputPath=Join-Path $script:RepositoryRoot 'artifacts/KuaiYi.Development.msix' }
$OutputPath=[IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Force (Split-Path $OutputPath -Parent) | Out-Null
# Sparse manifest intentionally references external EXEs/DLL. /nv bypasses their absence inside the identity package.
# Source consistency/installed PE checks run separately; Windows schema/runtime remain separately recorded.
Invoke-Checked $makeappx @('pack','/o','/d',(Join-Path $script:RepositoryRoot 'packaging/identity'),'/nv','/p',$OutputPath)
if ($CertificateThumbprint) {
    $cert=Get-Item -LiteralPath "Cert:\CurrentUser\My\$CertificateThumbprint"
    if ($cert.Subject -cne 'CN=KuaiYi Development' -or -not $cert.HasPrivateKey -or $cert.NotAfter -lt (Get-Date)) { throw 'Development signing certificate mismatch/expired/no private key.' }
    $sign=Find-WindowsSdkTool 'SignTool.exe'
    Invoke-Checked $sign @('sign','/fd','SHA256','/sha1',$CertificateThumbprint,$OutputPath)
    Invoke-Checked $sign @('verify','/pa',$OutputPath)
    Write-Output 'Signed development identity package. No registration or certificate trust was performed.'
} else { Write-Output 'Unsigned development identity package created; cannot register until explicitly signed/trusted. No registration performed.' }

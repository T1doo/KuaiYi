Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. "$PSScriptRoot/../../scripts/sdk-compile-evidence.ps1"
$checks=0
function Check([bool]$Condition){if(-not $Condition){throw ('Compiler evidence contract failed at check '+$script:checks)};$script:checks++}
$secret='PRIVATE_SENTINEL_sk_SYNTHETIC'
$raw=@"
C:\$secret\repo\tools\KuaiYi.ComCaptureProbe\ComCaptureProbe.cpp(248,17): error C2440: $secret raw pointer 0x12345678 [C:\$secret\native.vcxproj]
C:\$secret\repo\tools\KuaiYi.ComCaptureProbe\ComCaptureProbe.cpp(248,17): error C2440: duplicate $secret
C:\$secret\repo\tools\KuaiYi.ComCaptureProbe\SelectionPolicy.h(12): warning C4267: $secret command line token
C:\$secret\SDK\arbitrary_private.h(997): error C2065: $secret environment
private.obj : error LNK2019: unresolved $secret
mt.exe : general error c1010001: private manifest $secret
C:\$secret\unexpected.cpp(19): error C9999: $secret
$secret stdout actual translation
"@
$e=@(Get-SdkCompilerEvidence $raw)
Check ($e.Count -eq 6)
Check ($e[0].Code -ceq 'C2440' -and $e[0].Source -ceq 'Probe' -and $e[0].Line -eq 248)
Check ($e[1].Code -ceq 'C4267' -and $e[1].Source -ceq 'Policy' -and $e[1].Line -eq 12)
Check ($e[2].Code -ceq 'C2065' -and $e[2].Source -ceq 'Unknown' -and $null -eq $e[2].Line)
Check ($e[3].Code -ceq 'LNK2019' -and $e[3].Source -ceq 'Unknown' -and $null -eq $e[3].Line)
Check ($e[4].Code -ceq 'C1010001' -and $e[4].Source -ceq 'Unknown')
Check ($e[5].Code -ceq 'Unknown' -and $e[5].Source -ceq 'Unknown')
$json=$e | ConvertTo-Json -Depth 5
Check ($json -notmatch 'PRIVATE_SENTINEL|SYNTHETIC|0x12345678|\\repo|\.h|\.cpp|\.obj|\.vcxproj|command|environment|pointer|manifest')
foreach($row in $e){Check ((@($row.PSObject.Properties.Name) -join ',') -ceq 'Code,Source,Line')}
foreach($position in @('0','100001','999999','2147483648','-1','1.2','PRIVATE_SENTINEL')){
    $r=@(Get-SdkCompilerEvidence "C:\secret\ComCaptureProbe.cpp($position): error C2440: $secret")
    Check ($r.Count -eq 1 -and $r[0].Source -ceq 'Unknown' -and $null -eq $r[0].Line)
}
Check (@(Get-SdkCompilerEvidence 'ComCaptureProbe.cpp(100000): fatal error C1083: hidden').Count -eq 1)
Check (@(Get-SdkCompilerEvidence 'source.obj : error LNK2019: ComCaptureProbe.cpp(44): error C2440: PRIVATE_SENTINEL').Count -eq 1)
Check (@(Get-SdkCompilerEvidence 'C:\secret\NotComCaptureProbe.cpp(8): error C2440: private')[0].Source -ceq 'Unknown')
Check (@(Get-SdkCompilerEvidence 'C:\secret\ComCaptureProbe.cpp.backup(8): error C2440: private')[0].Source -ceq 'Unknown')
Check (@(Get-SdkCompilerEvidence ('PRIVATE_SENTINEL '+('x'*8200)+': error C2440: hidden')).Count -eq 0)
Check (@(Get-SdkCompilerEvidence ('x'*4194305)).Count -eq 0)
Check (@(Get-SdkCompilerEvidence "PRIVATE_SENTINEL\nNo compiler diagnostic").Count -eq 0)
$many=1..50 | ForEach-Object {"ComCaptureProbe.cpp($_): error C2440: PRIVATE_SENTINEL"}
Check (@(Get-SdkCompilerEvidence ($many -join "`n")).Count -eq 16)
Check (@(Get-SdkCompilerEvidence '').Count -eq 0)
Check (@(Get-SdkCompilerEvidence '1>ComCaptureProbe.cpp(25): error C2440: PRIVATE_SENTINEL')[0].Source -ceq 'Probe')
Check (@(Get-SdkCompilerEvidence '  2>SelectionPolicy.h(14) : warning C4267: PRIVATE_SENTINEL')[0].Line -eq 14)
Check (@(Get-SdkCompilerEvidence 'cl : command line warning D9002: PRIVATE_SENTINEL')[0].Code -ceq 'D9002')
Check (@(Get-SdkCompilerEvidence 'cl : warning D9002: PRIVATE_SENTINEL')[0].Code -ceq 'D9002')
# Exercise the runner's environment boundary with secrets on every non-success stream.
function Get-SyntheticCiEnvironment {
    Write-Error $secret -ErrorAction Continue
    Write-Warning $secret
    Write-Verbose $secret -Verbose
    Write-Debug $secret -Debug
    Write-Information $secret -InformationAction Continue
    Write-Host $secret
    [pscustomobject]@{NativeBlockers=@();SyntheticSuccess=$true}
}
$visible=@(& {Get-SyntheticCiEnvironment 2>$null 3>$null 4>$null 5>$null 6>$null} *>&1)
Check ($visible.Count -eq 1)
Check ($visible[0].SyntheticSuccess -eq $true -and @($visible[0].NativeBlockers).Count -eq 0)
Check (($visible | ConvertTo-Json -Depth 5) -notmatch 'PRIVATE_SENTINEL_sk_SYNTHETIC')
Write-Host "SDK_COMPILE_EVIDENCE_CONTRACTS passed=$checks failed=0"

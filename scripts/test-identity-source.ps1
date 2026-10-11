. "$PSScriptRoot/common.ps1"
Assert-SpecIntegrity
$folder=Join-Path $script:RepositoryRoot 'artifacts/test-results/identity-source'
New-Item -ItemType Directory -Force $folder | Out-Null
$pwsh=(Get-Command pwsh).Source
function Run-SourceCheck([string]$Script,[string]$ExpectedError) {
    $info=[Diagnostics.ProcessStartInfo]::new($pwsh);$info.UseShellExecute=$false;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    foreach($argument in @('-NoProfile','-File',$Script,'-SourceOnly')){$info.ArgumentList.Add($argument)}
    $process=[Diagnostics.Process]::Start($info)
    try {
        $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
        if(-not $process.WaitForExit(10000)){ $process.Kill();if(-not $process.WaitForExit(2000)){throw 'Source check exit unconfirmed'};throw 'Source check timeout' }
        $output=$stdout.GetAwaiter().GetResult();$errorText=$stderr.GetAwaiter().GetResult()
        if($ExpectedError){if($process.ExitCode -ne 1 -or -not $errorText.Contains($ExpectedError)){throw 'Wrong rejection/crash; not a valid test result'}}
        elseif($process.ExitCode -ne 0){throw "Baseline source validator failed: $errorText"}
        return $process.ExitCode
    } finally { if($process.HasExited){$process.Dispose()} }
}
$checks=@([pscustomobject]@{Case='Baseline';Result='Pass';ExitCode=(Run-SourceCheck (Join-Path $PSScriptRoot 'validate-dev-identity.ps1') '')})
$cases=@{WrongWorkerId='Independent EXE identity mismatch';WrongPublisher='Development identity mismatch';MissingWorker='Both EXEs must have independent declarations';WildcardMenu='Unsupported/wildcard menu scope';WrongClsid='Verb CLSID mismatch'}
foreach($case in $cases.GetEnumerator()){
    $copy=Join-Path $folder ([Guid]::NewGuid().ToString('N'));New-Item -ItemType Directory $copy|Out-Null
    try {
        foreach($relative in @('SPEC.md','快译_产品与技术开发规格_v1.0.md','docs/traceability.json','scripts/common.ps1','scripts/validate-dev-identity.ps1','packaging/identity/AppxManifest.xml','src/KuaiYi.App/app.manifest','src/KuaiYi.ImageWorker/app.manifest')){
            $destination=Join-Path $copy $relative;New-Item -ItemType Directory -Force (Split-Path $destination -Parent)|Out-Null;Copy-Item -LiteralPath (Join-Path $script:RepositoryRoot $relative) -Destination $destination
        }
        $manifest=Join-Path $copy 'packaging/identity/AppxManifest.xml';$text=[IO.File]::ReadAllText($manifest)
        switch($case.Key){
            WrongWorkerId {$sxs=Join-Path $copy 'src/KuaiYi.ImageWorker/app.manifest';[IO.File]::WriteAllText($sxs,[IO.File]::ReadAllText($sxs).Replace('applicationId="ImageWorker"','applicationId="KuaiYi"'))}
            WrongPublisher {$text=$text.Replace('Publisher="CN=KuaiYi Development"','Publisher="CN=Different"')}
            MissingWorker {[xml]$xml=$text;$node=$xml.SelectSingleNode('//*[local-name()="Application" and @Id="ImageWorker"]');$null=$node.ParentNode.RemoveChild($node);$text=$xml.OuterXml}
            WildcardMenu {$text=$text.Replace('Type=".png"','Type="*"')}
            WrongClsid {$text=$text.Replace('Clsid="86F7AB57-0F30-47A6-B284-3C7BC82DF8E2"','Clsid="00000000-0000-0000-0000-000000000000"')}
        }
        [IO.File]::WriteAllText($manifest,$text)
        $exitCode=Run-SourceCheck (Join-Path $copy 'scripts/validate-dev-identity.ps1') $case.Value
        $checks+=[pscustomobject]@{Case=$case.Key;Result='Pass';ExitCode=$exitCode;ExpectedDiagnostic=$case.Value}
    } finally {Remove-Item -LiteralPath $copy -Recurse -Force}
}
$checks|ConvertTo-Json -Depth 4|Tee-Object -FilePath (Join-Path $folder 'results.json')

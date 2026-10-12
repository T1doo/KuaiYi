param([Parameter(Mandatory)][string]$BundleRoot,
      [ValidateSet('Preflight','TextAlpha','SelectionProbe','Overlay')][string]$Mode='Preflight',
      [switch]$Execute,[ValidateRange(10,900)][int]$DurationSeconds=120,
      [ValidateSet('fragmented','non-streaming','slow','unauthorized','disconnect','stream-error')][string]$Scenario='fragmented')
. "$PSScriptRoot/text-alpha-common.ps1"
$BundleRoot=[IO.Path]::GetFullPath($BundleRoot)
$manifest=Assert-AlphaBundle $BundleRoot
if(-not $Execute -or $Mode -eq 'Preflight') {
    @{Kind='TextAlphaPreflight';BundleValidated=$true;SourceCommit=$manifest.SourceCommit;SourceDirty=$manifest.SourceDirty;Runtime=$manifest.Runtime;SelfContained=$true;Installer=$false;WindowsExecution='NotRun';ProcessesStarted=0;Requirement='Authorized Windows11 x64 unlocked ordinary interactive desktop and controlled public fixture apps';Modes=@('TextAlpha','SelectionProbe','Overlay');RealModels='NotRequested'} | ConvertTo-Json
    return
}
Assert-AlphaDesktop # Before directories, provider or desktop child processes.
$run=[Guid]::NewGuid().ToString('N')
$evidence=Join-Path ([IO.Path]::GetTempPath()) "KuaiYi-Alpha-Evidence-$run"
New-Item -ItemType Directory -Path $evidence | Out-Null
$provider=$null;$child=$null;$receipt=$null;$sourceCheck='NotRun';$wrong=0;$selected=0;$fixed=0
$childExitCode=$null;$timedOut=$false
function Start-AlphaChild([string]$Path,[string[]]$Arguments,[bool]$ReadOutput=$true,[bool]$Hidden=$false) {
    $info=[Diagnostics.ProcessStartInfo]::new($Path);$info.UseShellExecute=$false;$info.CreateNoWindow=$Hidden
    $info.WorkingDirectory=Split-Path $Path -Parent;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    foreach($argument in $Arguments){[void]$info.ArgumentList.Add($argument)}
    $process=[Diagnostics.Process]::Start($info)
    if($null -eq $process){throw 'Owned child failed to start.'}
    return [pscustomobject]@{Process=$process;Output=$(if($ReadOutput){$process.StandardOutput.ReadToEndAsync()}else{$null});Error=$process.StandardError.ReadToEndAsync()}
}
function Stop-AlphaChild($Owned) {
    if($null -eq $Owned){return}
    try {
        if(-not $Owned.Process.HasExited){[void]$Owned.Process.CloseMainWindow();if(-not $Owned.Process.WaitForExit(3000)){$Owned.Process.Kill($true);if(-not $Owned.Process.WaitForExit(5000)){throw 'Owned child exit unconfirmed.'}}}
    }finally{$Owned.Process.Dispose()}
}
try {
    if($Mode -eq 'TextAlpha') {
        $provider=Start-AlphaChild (Join-Path $BundleRoot 'test-provider/KuaiYi.MockProvider.exe') @() $false $true
        $line=$provider.Process.StandardOutput.ReadLineAsync()
        if(-not $line.Wait(10000) -or $provider.Process.HasExited){throw 'Local fixture readiness failed.'}
        $value=$line.GetAwaiter().GetResult()
        if($null -eq $value -or -not $value.StartsWith('KUAIYI_MOCK_URL=',[StringComparison]::Ordinal)){throw 'Unexpected fixture readiness message.'}
        $base=[Uri]::new($value.Substring('KUAIYI_MOCK_URL='.Length));$ip=$null
        if($base.Scheme -cne 'http' -or -not[Net.IPAddress]::TryParse($base.Host,[ref]$ip) -or -not[Net.IPAddress]::IsLoopback($ip) -or $base.UserInfo -or $base.Query -or $base.Fragment -or $base.AbsolutePath -cne '/'){throw 'Fixture URI is not a literal loopback origin.'}
        $prefix=[Uri]::new($base,"runs/$run/$Scenario/")
        $child=Start-AlphaChild (Join-Path $BundleRoot 'app/KuaiYi.App.exe') @('--translation-test-provider',$prefix.AbsoluteUri)
        Write-Output 'Public local text Alpha is Off initially. Open selection-public.txt in a controlled app. Enable explicitly, select the public full text, then exercise collapse/reopen/regenerate. No real service or key is used.'
    } elseif($Mode -eq 'SelectionProbe') {
        $child=Start-AlphaChild (Join-Path $BundleRoot 'diagnostics/KuaiYi.CompatibilityProbe.exe') @((Join-Path $BundleRoot 'app/KuaiYi.CaptureBroker.exe'),(Join-Path $BundleRoot 'selection-public.txt'))
        Write-Output 'Select only selection-public.txt in the controlled target within five seconds. This runs the actual UIA probe, without a model.'
    } else {
        $child=Start-AlphaChild (Join-Path $BundleRoot 'diagnostics/KuaiYi.GrowthHarness.exe') @('--actual-overlay',(Join-Path $evidence 'actual-overlay.csv'),'240')
        Write-Output 'Public production-overlay probe only. Geometry CSV is not an FPS or full translation acceptance result.'
    }
    if(-not $child.Process.WaitForExit($DurationSeconds*1000)){$timedOut=$true;Write-Output 'Bounded session ended; only this launcher owned processes will close.'}else{$childExitCode=$child.Process.ExitCode}
    if($Mode -eq 'TextAlpha' -and -not $provider.Process.HasExited) {
        $handler=[Net.Http.SocketsHttpHandler]::new();$handler.UseProxy=$false;$handler.UseCookies=$false;$handler.AllowAutoRedirect=$false
        $http=[Net.Http.HttpClient]::new($handler);$http.Timeout=[TimeSpan]::FromSeconds(3)
        try {
            $json=$http.GetStringAsync([Uri]::new($base,"requests/$run")).GetAwaiter().GetResult()
            $requests=@($json | ConvertFrom-Json);$public=[IO.File]::ReadAllText((Join-Path $BundleRoot 'selection-public.txt')).Replace("`r`n","`n").Trim()
            foreach($request in $requests) {
                $body=$request.Body | ConvertFrom-Json
                $message=@($body.messages)[0]
                # Protocol envelopes are explicit; normal text is user message index1 for Chat.
                $source=if($request.Path.EndsWith('/messages',[StringComparison]::Ordinal)){$message.content[0].text}else{@($body.messages)[1].content}
                if($source -ceq 'KuaiYi connection test. Please reply: 快译连接测试。'){$fixed++}
                elseif($source -is [string] -and $source.Replace("`r`n","`n").Trim() -ceq $public){$selected++}
                else{$wrong++} # Unknown private source is never persisted or printed.
            }
            $sourceCheck=if($wrong -gt 0){'Fail'}elseif($selected -gt 0){'Pass'}else{'NotRun'}
            $receipt=@{Kind='PublicSourceHttpComparisonOnly';Status=$sourceCheck;SelectedFixtureRequests=$selected;FixedSampleRequests=$fixed;WrongSourceRequests=$wrong;RequestBodiesPersisted=$false;ModelCalls='None'}
        }finally{$http.Dispose()}
    } elseif($Mode -eq 'SelectionProbe' -and $child.Process.HasExited) {
        $output=$child.Output.GetAwaiter().GetResult();$row=@($output.Split("`n") | Where-Object{$_.Trim().StartsWith('{')} | Select-Object -Last 1)
        if($row.Count -eq 1){$receipt=$row[0] | ConvertFrom-Json}
        # Probe comparison and exit code are evidence; core compatibility rate still needs the required20 actions/views.
    }
} finally {
    try{Stop-AlphaChild $child}finally{Stop-AlphaChild $provider}
    @{Kind='BoundedWindowsTextAlphaSession';Mode=$Mode;SourceCommit=$manifest.SourceCommit;SourceDirty=$manifest.SourceDirty;OS=[Runtime.InteropServices.RuntimeInformation]::OSDescription;DurationSeconds=$DurationSeconds;ChildExitCode=$childExitCode;BoundedTimeout=$timedOut;Receipt=$receipt;WholeAcceptance='NotRun';WindowsCompatibilityMatrix='NotRun';NativeProductDlls='NotIncluded';Installer=$false;RealModelCalls='None';Registration='None';Credentials='None';ManualPending=@('Off/On privacy and focus','production take-words/source association','actual rendered growth and collapse/reopen','both protocol settings','mixed-DPI boundaries','full core compatibility matrix')} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $evidence 'session.json') -Encoding utf8NoBOM
    Write-Output "Evidence directory: $evidence; whole acceptance remains NotRun."
}

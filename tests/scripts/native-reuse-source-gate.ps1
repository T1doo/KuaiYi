Set-StrictMode -Version Latest
function Get-NativeReuseSourceCommit([string]$Repository,[string]$Expected) {
    $head=@(& git -C $Repository rev-parse HEAD 2>$null)
    $headExit=$LASTEXITCODE
    if($headExit -ne 0 -or $head.Count -ne 1){throw 'Invalid source'}
    $commit=([string]$head[0]).Trim()
    if($commit -cnotmatch '^[0-9a-f]{40}$' -or $Expected -cne $commit){throw 'Invalid source'}
    $status=@(& git -C $Repository status --porcelain --untracked-files=no 2>$null)
    $statusExit=$LASTEXITCODE
    if($statusExit -ne 0 -or $status.Count -ne 0){throw 'Invalid source'}
    return $commit
}

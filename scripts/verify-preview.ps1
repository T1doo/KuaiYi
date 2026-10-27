param([Parameter(Mandatory=$true)][string]$Root)
$ErrorActionPreference='Stop'
# Read-only verification. This does not launch, install, register or grant trust.
try {
    $folder=[System.IO.Path]::GetFullPath($Root)
    $ancestor=[System.IO.DirectoryInfo]::new($folder)
    while($null -ne $ancestor){
        if(($ancestor.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Linked package path refused'}
        $ancestor=$ancestor.Parent
    }
    function Assert-PlainFile([string]$relative) {
        $parts=$relative.Split('/')
        $cursor=$folder
        for($i=0;$i -lt $parts.Length;$i++){
            $cursor=Join-Path $cursor $parts[$i]
            $item=Get-Item -LiteralPath $cursor -Force
            if(($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Linked package component refused before read'}
            if($i -lt $parts.Length-1 -and -not $item.PSIsContainer){throw 'Non-directory package component'}
            if($i -eq $parts.Length-1 -and $item.PSIsContainer){throw 'Expected regular package file'}
        }
        return $item
    }
    # Enumerate without -Recurse: inspect each directory before descending into it.
    $stack=[System.Collections.Generic.Stack[string]]::new()
    $stack.Push($folder)
    $inventory=[System.Collections.Generic.List[System.IO.FileInfo]]::new()
    while($stack.Count -gt 0){
        $directory=$stack.Pop()
        $current=Get-Item -LiteralPath $directory -Force
        if(($current.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Linked inventory directory refused'}
        foreach($entry in Get-ChildItem -LiteralPath $directory -Force){
            if(($entry.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Linked package entry refused before read'}
            if($entry.PSIsContainer){$stack.Push($entry.FullName)}else{$inventory.Add($entry)}
            if($inventory.Count+$stack.Count -gt 10000){throw 'Inventory exceeds bound'}
        }
    }
    $manifest=Join-Path $folder 'manifest.json'
    if((Assert-PlainFile 'manifest.json').Length -gt 8MB){throw 'Manifest exceeds bound'}
    $m=Get-Content -LiteralPath $manifest -Raw -Encoding utf8 | ConvertFrom-Json
    if($m.schemaVersion -ne 1 -or $m.kind -cne 'UnsealedManagedPreview_NotInstaller' -or $m.installer -ne $false -or $m.productionPins -cne 'Unavailable'){throw 'Unexpected package contract'}
    $seen=[System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $seen.Add('manifest.json') | Out-Null
    foreach($row in $m.files){
        $rel=[string]$row.path
        if($rel -cnotmatch '^[A-Za-z0-9_. /-]+$' -or $rel.StartsWith('/') -or $rel.Split('/') -contains '..' -or $rel.Split('/') -contains '.' -or $rel.Contains('//') -or -not $seen.Add($rel)){throw 'Invalid or duplicate package path'}
        $file=Join-Path $folder $rel
        $entry=Assert-PlainFile $rel
        if($entry.PSIsContainer -or ($entry.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 -or $entry.Length -ne $row.bytes){throw 'Package file identity differs'}
        if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() -cne $row.sha256){throw 'Package file hash differs'}
    }
    foreach($entry in $inventory){
        $prefix=$folder.TrimEnd([System.IO.Path]::DirectorySeparatorChar)+[System.IO.Path]::DirectorySeparatorChar
        if(-not $entry.FullName.StartsWith($prefix,[System.StringComparison]::OrdinalIgnoreCase)){throw 'Package path escaped root'}
        $rel=$entry.FullName.Substring($prefix.Length).Replace('\','/')
        if(-not $seen.Remove($rel)){throw 'Unexpected package file'}
    }
    if($seen.Count -ne 0){throw 'Package files missing'}
    Write-Output 'Package bytes verified. Unsealed build inputs only; no installation or Windows acceptance is granted.'
    exit 0
} catch {
    Write-Error ('Preview integrity refused: '+$_.Exception.GetType().Name)
    exit 2
}

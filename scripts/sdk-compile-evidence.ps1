Set-StrictMode -Version Latest
# The only public compiler detail is a finite code, fixed repository category and bounded line.
function Get-SdkCompilerEvidence([string]$Output) {
    if ($null -eq $Output -or $Output.Length -gt 4194304) { return }
    $allowed=@('C1001','C1083','C2001','C2011','C2039','C2061','C2065','C2079','C2143','C2146','C2182','C2220','C2440','C2504','C2511','C2660','C2664','C2672','C2678','C2993','C3646','C3861','C4099','C4127','C4172','C4189','C4244','C4245','C4267','C4389','C4456','C4458','C4800','C4996','D8003','D8004','D8021','D9002','D9024','D9025','LNK1104','LNK1112','LNK1120','LNK2001','LNK2019','MSB8003','MSB8020','MSB8036','MSB8066','C1010001')
    $seen=@{};$count=0
    foreach($line in ($Output -split '\r?\n')) {
        if ($line.Length -gt 8192) { continue }
        $fault=[regex]::Match($line,':\s*(?:command\s+line\s+)?(?:(?:(?:fatal|general)\s+)?error|warning)\s+(?<Code>[CD][0-9]{4}|LNK[0-9]{4}|MSB[0-9]{4}|[cC][0-9a-fA-F]{7})\s*:',[Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if(-not $fault.Success){continue}
        $code=$fault.Groups['Code'].Value.ToUpperInvariant()
        if($code -cnotin $allowed){$code='Unknown'}
        # Restrict source matching to the location BEFORE the diagnostic delimiter; messages are never parsed.
        $location=$line.Substring(0,$fault.Index)
        $source='Unknown';$number=$null
        $known=[regex]::Match($location,'(?:^\s*(?:[0-9]+>)?|[\\/])(?<File>ComCaptureProbe\.cpp|SelectionPolicy\.h)\((?<Line>[0-9]{1,6})(?:,[0-9]{1,6})?\)\s*$',[Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if($known.Success) {
            $candidate=[int]$known.Groups['Line'].Value
            if($candidate -ge 1 -and $candidate -le 100000){$source=if($known.Groups['File'].Value -ieq 'ComCaptureProbe.cpp'){'Probe'}else{'Policy'};$number=$candidate}
        }
        $key=$code+'/'+$source+'/'+$number
        if($seen.ContainsKey($key)){continue};$seen[$key]=$true
        [pscustomobject][ordered]@{Code=$code;Source=$source;Line=$number}
        if(++$count -ge 16){break}
    }
}

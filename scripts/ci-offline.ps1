param([ValidateSet('Debug','Release')][string]$Configuration='Release')
. "$PSScriptRoot/common.ps1"
Push-Location $script:RepositoryRoot
try {
    & "$PSScriptRoot/build.ps1" -Scope Managed -Configuration $Configuration
    & "$PSScriptRoot/test.ps1" -Suite Offline -Configuration $Configuration
    $root=Join-Path $script:RepositoryRoot 'artifacts/test-results/offline'
    $counts=@{}
    foreach($name in 'KuaiYi.Core.Tests','KuaiYi.Provider.Tests') {
        $settings=[System.Xml.XmlReaderSettings]::new();$settings.DtdProcessing=[System.Xml.DtdProcessing]::Prohibit;$settings.XmlResolver=$null
        $reader=[System.Xml.XmlReader]::Create((Join-Path $root "$name.trx"),$settings)
        try{$document=[System.Xml.XmlDocument]::new();$document.Load($reader)}finally{$reader.Dispose()}
        $counter=$document.GetElementsByTagName('Counters')[0]
        if(-not $counter -or [int]$counter.total -le 0 -or [int]$counter.failed -ne 0 -or [int]$counter.notExecuted -ne 0 -or [int]$counter.passed -ne [int]$counter.total){throw "Incomplete or failed actual test results: $name"}
        $counts[$name]=@{Total=[int]$counter.total;Passed=[int]$counter.passed;Failed=[int]$counter.failed;NotExecuted=[int]$counter.notExecuted}
    }
    $reader=[System.Xml.XmlReader]::Create((Join-Path $root 'browser-tests.xml'),$settings)
    try{$browser=[System.Xml.XmlDocument]::new();$browser.Load($reader)}finally{$reader.Dispose()}
    $cases=$browser.GetElementsByTagName('testcase').Count
    if($cases -le 0 -or $browser.GetElementsByTagName('failure').Count -gt 0 -or $browser.GetElementsByTagName('error').Count -gt 0 -or $browser.GetElementsByTagName('skipped').Count -gt 0){throw 'Incomplete or failed browser test results.'}
    $counts['Browser']=@{Total=$cases;Passed=$cases;Failed=0;NotExecuted=0}
    @{Kind='ExecutedLocalOfflineCi';Configuration=$Configuration;Sdk=((& dotnet --version).Trim());Counts=$counts;WindowsDesktop='NotRun';RemoteGitHubCi='NotRun';Installer='NotIncluded';ModelCalls='None'} |
        ConvertTo-Json -Depth 6 | Set-Content (Join-Path $root 'ci-offline-summary.json') -Encoding utf8NoBOM
    Write-Output 'Actual local offline CI completed. Windows desktop and remote CI have not been exercised.'
} finally {Pop-Location}

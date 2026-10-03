param([switch]$ProbeOnly)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath('E:\personalProject\Demo3\demo6')
$taskLog = Join-Path $taskRoot 'ResourceCleanupLogs\2026-10-03'
Add-Type -Path (Join-Path $taskLog 'RecycleOnly.cs')
$taskShell = New-Object -ComObject Shell.Application
if ($null -eq $taskShell.Namespace(10)) { throw 'Recycle Bin unavailable' }

if ($ProbeOnly) {
    $taskProbe = Join-Path $taskLog 'recycle-restore-probe.txt'
    if ((Test-Path -LiteralPath $taskProbe) -and [IO.File]::ReadAllText($taskProbe) -ne 'Demo6 cleanup recovery probe 2026-10-03') { throw 'Unexpected existing probe' }
    [IO.File]::WriteAllText($taskProbe,'Demo6 cleanup recovery probe 2026-10-03')
    $taskHash = (Get-FileHash -LiteralPath $taskProbe -Algorithm SHA256).Hash
    $taskResult = [Demo6Cleanup.Recycler]::Send($taskProbe)
    $taskMatch = @($taskShell.Namespace(10).Items()) | Where-Object { $_.Path -eq $taskResult.Destination }
    if ($null -eq $taskMatch) { throw 'Recycled probe is absent from Explorer Recycle Bin' }
    $taskMatch.InvokeVerb('undelete')
    for ($taskWait=0; $taskWait -lt 20 -and -not (Test-Path -LiteralPath $taskProbe); $taskWait++) { Start-Sleep -Milliseconds 250 }
    if (-not (Test-Path -LiteralPath $taskProbe)) { throw 'Probe restore failed' }
    if ((Get-FileHash -LiteralPath $taskProbe -Algorithm SHA256).Hash -ne $taskHash) { throw 'Restored hash differs' }
    [pscustomobject]@{ProbeRestored=$true;Sha256=$taskHash;RecyclePath=$taskResult.Destination;DeleteFlags=$taskResult.DeleteFlags} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskLog 'recovery-probe.json') -Encoding UTF8
    Get-Content -LiteralPath (Join-Path $taskLog 'recovery-probe.json')
    exit
}

$taskProbeLog = Get-Content -LiteralPath (Join-Path $taskLog 'recovery-probe.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $taskProbeLog.ProbeRestored) { throw 'Recovery not proven' }
$taskBefore = Get-Content -LiteralPath (Join-Path $taskLog 'before.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$taskResultPath = Join-Path $taskLog 'recycle-results.json'
if (Test-Path -LiteralPath $taskResultPath) { throw 'Refusing to repeat recycling' }
$taskResults = [System.Collections.Generic.List[object]]::new()
foreach ($taskCandidate in $taskBefore.candidates) {
    $taskTarget = (Resolve-Path -LiteralPath (Join-Path $taskRoot $taskCandidate.path)).Path
    if (-not $taskTarget.StartsWith(($taskRoot+'\아트\'),[StringComparison]::OrdinalIgnoreCase)) { throw 'Target outside art directory' }
    if ($taskTarget -notmatch '\\검사-남향-모션-v[12]\\review-(grid|sequence)$') { throw 'Unexpected target' }
    $taskContents = @(Get-ChildItem -LiteralPath $taskTarget -Recurse -Force)
    if (@($taskContents | Where-Object {($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0}).Count -ne 0) { throw 'Reparse point under target' }
    $taskFiles = @($taskContents | Where-Object {-not $_.PSIsContainer})
    if ($taskFiles.Count -ne $taskCandidate.files.Count) { throw 'Candidate count changed' }
    foreach ($taskExpected in $taskCandidate.files) {
        $taskPath = Join-Path $taskRoot $taskExpected.path
        $taskNow = Get-Item -LiteralPath $taskPath
        if ($taskNow.Length -ne $taskExpected.bytes -or (Get-FileHash -LiteralPath $taskPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $taskExpected.sha256) { throw "Candidate changed: $taskPath" }
        # Node's JSON timestamp is millisecond precision; Windows preserves 100 ns ticks.
        if ([Math]::Abs(($taskNow.LastWriteTimeUtc-([datetime]$taskExpected.mtimeUtc).ToUniversalTime()).TotalMilliseconds) -gt 1) { throw "Candidate recently saved: $taskPath" }
    }
    if (Test-Path -LiteralPath ($taskTarget+'.meta')) { throw 'Unexpected Unity meta; reevaluate pair first' }
    $taskRecycled = [Demo6Cleanup.Recycler]::Send($taskTarget)
    if (Test-Path -LiteralPath $taskTarget) { throw 'Original still exists after recycle' }
    $taskBinItem = @($taskShell.Namespace(10).Items()) | Where-Object {$_.Path -eq $taskRecycled.Destination}
    if ($null -eq $taskBinItem) { throw 'No Explorer recovery entry' }
    foreach ($taskExpected in $taskCandidate.files) {
        $taskRelative = $taskExpected.path.Substring($taskCandidate.path.Length+1)
        $taskTrashFile = Join-Path $taskRecycled.Destination $taskRelative
        if ((Get-FileHash -LiteralPath $taskTrashFile -Algorithm SHA256).Hash.ToLowerInvariant() -ne $taskExpected.sha256) { throw 'Recycle contents differ' }
    }
    $taskResults.Add([pscustomobject]@{Original=$taskTarget;RecycledTo=$taskRecycled.Destination;Files=$taskCandidate.files.Count;Bytes=($taskCandidate.files | Measure-Object bytes -Sum).Sum;RecycledUtc=[DateTime]::UtcNow.ToString('o');ContentsHashVerified=$true;ExplorerRecoveryEntry=$true;DeleteFlags=$taskRecycled.DeleteFlags})
    ConvertTo-Json -InputObject @($taskResults.ToArray()) -Depth 5 | Set-Content -LiteralPath $taskResultPath -Encoding UTF8
    $taskResults[$taskResults.Count-1] | ConvertTo-Json -Compress
}

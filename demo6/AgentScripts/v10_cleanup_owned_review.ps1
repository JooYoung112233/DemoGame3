$v10Expected = 'E:\personalProject\Demo3\topdown-v10-review-isolated'
$v10Resolved = (Resolve-Path -LiteralPath $v10Expected).Path
if ($v10Resolved -ne $v10Expected) { throw 'Unexpected resolved review path' }
if ((Get-Item -LiteralPath $v10Resolved).Attributes -band [System.IO.FileAttributes]::ReparsePoint) { throw 'Review root is a reparse point' }
if ((Get-Content -LiteralPath (Join-Path $v10Resolved '.v10-owned-review') -Raw).Trim() -ne 'Demo6 v10 texture review') { throw 'Review ownership marker mismatch' }
$v10Processes = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($v10Expected) })
foreach ($v10Process in $v10Processes) {
    if ($v10Process.CommandLine -notlike '*-projectPath*') { throw 'Unrecognized process; preserving review folder' }
    Stop-Process -Id $v10Process.ProcessId -Force
}
Remove-Item -LiteralPath $v10Resolved -Recurse -Force
[pscustomobject]@{ Removed = $v10Resolved; Processes = $v10Processes.ProcessId; Time = (Get-Date).ToUniversalTime().ToString('o') } | ConvertTo-Json | Set-Content -LiteralPath 'E:\personalProject\Demo3\demo6\검증\정수리-질감수정-v10\review-cleanup.json' -Encoding utf8

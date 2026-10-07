$ErrorActionPreference='Stop'
$taskRoot='E:\personalProject\Demo3\demo6'
$taskWorkspace='E:\personalProject\Demo3'
$taskTargets=@(
 'E:\personalProject\Demo3\topdown-neutral-light-review',
 'E:\personalProject\Demo3\topdown-rat-v21-review',
 'E:\personalProject\Demo3\topdown-v11-harmony-review',
 'E:\personalProject\Demo3\topdown-walk-v22-review',
 'E:\personalProject\Demo3\demo6\art-isolated\ogre-slam-v1\unity',
 'E:\personalProject\Demo3\demo6\art-isolated\ogre-slam-v1\revision2\unity',
 'E:\personalProject\Demo3\demo6\art-isolated\ogre-slam-v1\revision3\unity'
)
foreach($taskTarget in $taskTargets){
 $taskResolved=(Resolve-Path -LiteralPath $taskTarget).Path
 if($taskResolved -cne $taskTarget -or -not $taskResolved.StartsWith($taskWorkspace+'\',[StringComparison]::OrdinalIgnoreCase) -or $taskResolved -eq $taskRoot){throw 'Target path guard failed'}
 if(-not (Test-Path -LiteralPath (Join-Path $taskResolved 'ProjectSettings\ProjectVersion.txt'))){throw 'Unity project marker absent'}
}
$taskProcesses=Get-CimInstance Win32_Process -Filter "name = 'Unity.exe'"
foreach($taskProcess in $taskProcesses){foreach($taskTarget in $taskTargets){if($taskProcess.CommandLine -and $taskProcess.CommandLine.IndexOf($taskTarget,[StringComparison]::OrdinalIgnoreCase) -ge 0){throw ('Target project still running: '+$taskTarget)}}}
$taskOgre=Get-Content -LiteralPath (Join-Path $taskRoot '보존\별도검증작업-20261004\ogre-preservation-recheck.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if($taskOgre.files -ne 2369 -or $taskOgre.mismatch.Count -ne 0){throw 'Ogre preservation check failed'}
foreach($taskName in @('topdown-neutral-light-review','topdown-rat-v21-review','topdown-v11-harmony-review','topdown-walk-v22-review')){
 $taskManifest=Get-Content -LiteralPath (Join-Path $taskRoot ('보존\별도검증작업-20261004\'+$taskName+'\preservation-manifest.json')) -Raw -Encoding UTF8 | ConvertFrom-Json
 foreach($taskFile in $taskManifest.files){if(-not (Test-Path -LiteralPath $taskFile.preservedAt)){throw ('Preserved file missing: '+$taskFile.preservedAt)}}
}
Add-Type -Path (Join-Path $taskRoot 'AgentScripts\RecycleVerifiedProjects.cs')
$taskResults=@()
foreach($taskTarget in $taskTargets){
 $taskRecycled=[ReviewCleanup.Recycle]::Only($taskTarget)
 if(Test-Path -LiteralPath $taskTarget){throw ('Target remains: '+$taskTarget)}
 $taskResults += [PSCustomObject]@{source=$taskTarget;recyclePaths=$taskRecycled;sourceRemoved=$true}
 $taskResults | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $taskRoot '보존\별도검증작업-20261004\recycled-projects.json') -Encoding UTF8
}
$taskResults | ConvertTo-Json -Depth 6

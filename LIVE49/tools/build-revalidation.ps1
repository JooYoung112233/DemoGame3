$revalidationRoot = Split-Path -Parent $PSScriptRoot
$revalidationNames = @('VerifyFirstWeek','VerifyRegionTravel','VerifyCampLife','VerifyTutorialStructure','VerifySearchRuntime','VerifyKitchenPresentation','VerifyBanditEncounter','RevalidateFirstPass')
$revalidationUsing = [System.Collections.Generic.HashSet[string]]::new()
$revalidationBodies = foreach ($revalidationName in $revalidationNames) {
    $revalidationSource = [System.IO.File]::ReadAllText((Join-Path $revalidationRoot "Demo3/AgentScripts/$revalidationName.cs"))
    foreach ($revalidationMatch in [regex]::Matches($revalidationSource, '(?m)^using [^\r\n]+;\r?$')) { [void]$revalidationUsing.Add($revalidationMatch.Value.Trim()) }
    $revalidationBody = [regex]::Replace($revalidationSource, '(?m)^using [^\r\n]+;\r?\n', '')
    if ($revalidationName -ne 'RevalidateFirstPass') { $revalidationBody = $revalidationBody.Replace('../Screenshots/', '../Screenshots/Revalidation-2026-09-13/') }
    $revalidationBody
}
$revalidationOutput = Join-Path $revalidationRoot 'Demo3/Screenshots/Revalidation-2026-09-13'
[void][System.IO.Directory]::CreateDirectory($revalidationOutput)
[System.IO.File]::WriteAllText((Join-Path $revalidationOutput 'VerificationBundle.cs'), (($revalidationUsing | Sort-Object) -join "`n") + "`n" + ($revalidationBodies -join "`n"))
Write-Output 'Created isolated verification bundle from current AgentScripts.'

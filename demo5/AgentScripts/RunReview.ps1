param([string[]]$Entries)
$cli='C:/Users/admin/AppData/Local/Unity/bin/unity.exe'
foreach($entry in $Entries){
 $class=$entry.Split('.')[0]
 $raw=& $cli command run_script --project-path D:/Demo3/demo5 --format json --timeout 120 --file "AgentScripts/$class.cs" --entry $entry
 $result=$raw|ConvertFrom-Json
 $record=[ordered]@{entry=$entry;time=(Get-Date).ToString('o');response=$result}
 $record|ConvertTo-Json -Depth 20 -Compress|Add-Content -Encoding utf8 D:/Demo3/demo5/AgentScripts/review-results.jsonl
 if(!$result.success -or !$result.data.result.success){$raw;throw "Review failed: $entry"}
 Write-Output "$entry : $($result.data.result.result)"
}

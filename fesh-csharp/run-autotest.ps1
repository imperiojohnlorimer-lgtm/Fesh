# Runs the Debug autotest with its window minimized, so it can run while you work.
#   .\run-autotest.ps1                      the full play-through
#   .\run-autotest.ps1 -Test FESH_MATCH_TEST=1   only one script (any FESH_*_TEST=value)
# Output goes to test-output\<name> (screenshots + autotest.log) and a throwaway save in the temp folder.
param(
    [string]$Test = "",
    [string]$Name = (Get-Date -Format "yyyyMMdd-HHmmss"),
    [int]$TimeoutMinutes = 60
)
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $here "bin\Debug\net8.0\Fesh.exe"
if (-not (Test-Path $exe)) { Write-Error "Build Debug first: dotnet build"; exit 2 }
$out = Join-Path $here "test-output\$Name"
New-Item -ItemType Directory -Force $out | Out-Null
$save = Join-Path ([IO.Path]::GetTempPath()) "fesh-autotest-save\x.json"
New-Item -ItemType Directory -Force (Split-Path $save) | Out-Null
$env:FESH_AUTOTEST = $out
$env:FESH_SAVE = $save
Get-ChildItem Env: | Where-Object { $_.Name -like "FESH_*_TEST" } | ForEach-Object { Remove-Item "Env:$($_.Name)" }   # a leftover from an earlier call would pick the wrong script
if ($Test) { $k, $v = $Test -split "=", 2; Set-Item "Env:$k" $v }
$p = Start-Process $exe -WindowStyle Minimized -PassThru
$sw = [Diagnostics.Stopwatch]::StartNew()
while (-not $p.HasExited -and $sw.Elapsed.TotalMinutes -lt $TimeoutMinutes) { Start-Sleep 5 }
if (-not $p.HasExited) { $p.Kill(); Write-Host "Timed out after $TimeoutMinutes minutes" }
$log = Join-Path $out "autotest.log"
if (-not (Test-Path $log)) { Write-Host "No autotest.log: the run ended early (window closed?)"; exit 1 }
$lines = Get-Content $log
$fail = $lines | Select-String "^FAIL"
Write-Host ("PASS {0}  FAIL {1}  ({2:N1} min)  {3}" -f ($lines | Select-String "^PASS").Count, $fail.Count, $sw.Elapsed.TotalMinutes, $out)
$fail | ForEach-Object { Write-Host $_.Line }
exit ([int]($fail.Count -gt 0))

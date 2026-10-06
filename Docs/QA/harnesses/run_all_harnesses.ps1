param([string[]]$Only)
$runs = @(
    @{ flag = "-qaharness"; tag = "\[QA\]"; name = "qa"; timeout = 900 },
    @{ flag = "-uiharness"; tag = "\[Harness\]"; name = "ui"; timeout = 300 },
    @{ flag = "-repharness"; tag = "\[RepTest\]|\[Rep"; name = "rep"; timeout = 300 },
    @{ flag = "-climbharness"; tag = "\[ClimbTest\]|\[Climb"; name = "climb"; timeout = 400 },
    @{ flag = "-aiharness"; tag = "\[AiTest\]|\[AI"; name = "ai"; timeout = 400 },
    @{ flag = "-skyharness"; tag = "\[SkyTest\]|\[Sky"; name = "sky"; timeout = 400 },
    @{ flag = "-nightharness"; tag = "\[NightTest\]"; name = "night"; timeout = 400 },
    @{ flag = "-menuharness"; tag = "\[MenuTest\]"; name = "menu"; timeout = 400 },
    @{ flag = "-settingsharness"; tag = "\[SettingsTest\]"; name = "settings"; timeout = 400 }
)
foreach ($r in $runs) {
    if ($Only -and ($Only -notcontains $r.name)) { continue }
    $shots = "Q:\qashots\$($r.name)"
    New-Item -ItemType Directory -Force $shots | Out-Null
    $log = "Q:\qa_$($r.name)_log.txt"
    $p = Start-Process -FilePath "Q:\harnessbuild\BeastHarness.exe" -ArgumentList @($r.flag, "-shots", $shots, "-screen-width", "1600", "-screen-height", "900", "-screen-fullscreen", "0", "-logFile", $log) -PassThru
    $status = if (-not $p.WaitForExit($r.timeout * 1000)) { $p.Kill(); "TIMEOUT" } else { "exit $($p.ExitCode)" }
    $lines = Get-Content $log
    $pass = ($lines | Select-String -Pattern "PASS" | Where-Object { $_.Line -match $r.tag }).Count
    $fail = ($lines | Select-String -Pattern "FAIL" | Where-Object { $_.Line -match $r.tag }).Count
    $exc = ($lines | Select-String -Pattern "Exception" | Where-Object { $_.Line -notmatch "^\[QA\]" }).Count
    $err = ($lines | Select-String -Pattern "^Error|^\[Error\]|error:" ).Count
    "== $($r.name): $status, PASS $pass, FAIL $fail, exception lines $exc"
}

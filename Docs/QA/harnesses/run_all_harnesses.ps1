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
    @{ flag = "-settingsharness"; tag = "\[SettingsTest\]"; name = "settings"; timeout = 400 },
    @{ flag = "-lookharness"; tag = "\[LookTest\]"; name = "look"; timeout = 400 },
    @{ flag = "-styleharness"; tag = "\[StyleTest\]"; name = "style"; timeout = 300 },
    @{ flag = "-craftharness"; tag = "\[CraftTest\]"; name = "craft"; timeout = 300 },
    @{ flag = "-townharness"; tag = "\[TownTest\]"; name = "town"; timeout = 300 },
    @{ flag = "-padharness"; tag = "\[PadTest\]"; name = "pad"; timeout = 300 },
    @{ flag = "-cinemaharness"; tag = "\[CinemaTest\]"; name = "cinema"; timeout = 300 },
    @{ flag = "-foeharness"; tag = "\[FoeTest\]"; name = "foe"; timeout = 300 },
    @{ flag = "-folkharness"; tag = "\[FolkTest\]"; name = "folk"; timeout = 300 },
    @{ flag = "-gearharness"; tag = "\[GearTest\]"; name = "gear"; timeout = 300 },
    @{ flag = "-penharness"; tag = "\[PenTest\]"; name = "pen"; timeout = 300 },
    @{ flag = "-seasonharness"; tag = "\[SeasonTest\]"; name = "season"; timeout = 400 },
    @{ flag = "-wildharness"; tag = "\[WildTest\]"; name = "wild"; timeout = 300 },
    @{ flag = "-boardharness"; tag = "\[BoardTest\]"; name = "board"; timeout = 300 },
    @{ flag = "-upgradeharness"; tag = "\[UpgradeTest\]"; name = "upgrade"; timeout = 300 },
    @{ flag = "-keysharness"; tag = "\[KeysTest\]"; name = "keys"; timeout = 300 },
    @{ flag = "-hudharness"; tag = "\[HudTest\]"; name = "hud"; timeout = 300 },
    @{ flag = "-talkharness"; tag = "\[TalkTest\]"; name = "talk"; timeout = 300 },
    @{ flag = "-tutorharness"; tag = "\[TutorTest\]"; name = "tutor"; timeout = 300 }
)
foreach ($r in $runs) {
    if ($Only -and ($Only -notcontains $r.name)) { continue }
    $shots = "Q:\qashots\$($r.name)"
    New-Item -ItemType Directory -Force $shots | Out-Null
    $log = "Q:\qa_$($r.name)_log.txt"
    $p = Start-Process -FilePath "Q:\harnessbuild\BeastHarness.exe" -ArgumentList @($r.flag, "-shots", $shots, "-screen-width", "1600", "-screen-height", "900", "-screen-fullscreen", "0", "-logFile", $log) -PassThru
    $status = if (-not $p.WaitForExit($r.timeout * 1000)) { $p.Kill(); "TIMEOUT" } else { "exit $($p.ExitCode)" }
    $lines = Get-Content $log
    $pass = ($lines | Select-String -Pattern "PASS" -CaseSensitive | Where-Object { $_.Line -match $r.tag }).Count
    $fail = ($lines | Select-String -Pattern "FAIL" -CaseSensitive | Where-Object { $_.Line -match $r.tag }).Count
    $exc = ($lines | Select-String -Pattern "Exception" | Where-Object { $_.Line -notmatch "^\[QA\]" }).Count
    $err = ($lines | Select-String -Pattern "^Error|^\[Error\]|error:" ).Count
    $watch = ($lines | Select-String -Pattern "\[ErrorWatch\] errors=(\d+)" | Select-Object -Last 1)
    $errors = if ($watch) { $watch.Matches[0].Groups[1].Value } else { "?" }
    "== $($r.name): $status, PASS $pass, FAIL $fail, exception lines $exc, logged errors $errors"
    if ($watch -and $errors -ne "0") { $lines | Select-String -Pattern "\[ErrorWatch\] #" | ForEach-Object { "     " + $_.Line } }
}

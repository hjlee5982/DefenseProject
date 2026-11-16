param(
    [string]$Params,
    [string]$Mode = "report",          # report | detail | probe
    [string]$Stages = "30,40,45",
    [bool]$Title = $false,
    [string]$ProbeOut = "",
    [double]$Eta = 1.0,
    [int]$Seeds = 9
)
$s = "C:\Users\zxca0\AppData\Local\Temp\claude\2a18fab108f6e620\9241c106-5810-4670-b162-06392c5689da\scratchpad"
Add-Type -TypeDefinition (Get-Content "$s\BalanceSim.cs" -Raw -Encoding UTF8) -IgnoreWarnings
$p = Get-Content $Params -Raw -Encoding UTF8 | ConvertFrom-Json
[BalanceSim]::MinLineY = 4.5; [BalanceSim]::PlayerY = -2.895
[BalanceSim]::CountA = $p.countA; [BalanceSim]::CountB = $p.countB; [BalanceSim]::CountC = $p.countC; [BalanceSim]::MinCount = $p.minCount
[BalanceSim]::HpBase = $p.hpBase; [BalanceSim]::HpLin = $p.hpLin; [BalanceSim]::HpPow = $p.hpPow
if ($p.PSObject.Properties.Name -contains "hpExp") { [BalanceSim]::HpExp = $p.hpExp } else { [BalanceSim]::HpExp = 0.0 }
[BalanceSim]::AtkBase = $p.atkBase; [BalanceSim]::AtkPerStage = $p.atkPerStage; [BalanceSim]::ExpPerKill = $p.exp
for ($i=0; $i -lt 7; $i++) {
    [BalanceSim]::Speed[$i] = $p.speed[$i]; [BalanceSim]::HpMul[$i] = $p.hpMul[$i]; [BalanceSim]::AtkMul[$i] = $p.atkMul[$i]
    [BalanceSim]::Weight[$i] = $p.weight[$i]; [BalanceSim]::UnlockStage[$i] = $p.unlock[$i]
}
[BalanceSim]::Eta = $Eta

function Median($title,$n){ $f=@(); for($i=0;$i -lt $n;$i++){ $f += [BalanceSim]::RunGame($title,(New-Object Random (1000+$i)),$null) }; $f=$f|Sort-Object; return $f[[int]($f.Count/2)] }

if ($Mode -eq "report") {
    "NONE median fail stage: " + (Median $false $Seeds) + "   FULL median fail stage: " + (Median $true $Seeds)
}
elseif ($Mode -eq "detail") {
    [BalanceSim]::Detail($Title, 1000)
}
elseif ($Mode -eq "probe") {
    # levels the model player has at each requested stage
    [BalanceSim]::BarrierOverride = 1e9
    $log = New-Object 'System.Collections.Generic.List[BalanceSim+StageLog]'
    $fail = [BalanceSim]::RunGame($Title, (New-Object Random 1000), $log)
    $scen = @()
    foreach ($st in ($Stages -split ",")) {
        $stage = [int]$st
        $rec = $log | Where-Object { $_.stage -eq $stage } | Select-Object -First 1
        if ($null -eq $rec) { "stage $stage not reached (run failed at $fail)"; continue }
        $line = [BalanceSim]::StageTest($stage, $Title, [int]$rec.level, 5)
        $line
        $tokens = ([BalanceSim]::LastTokens -split ",")
        $scen += [ordered]@{
            name = ($(if ($Title) {"FULL"} else {"NONE"}) + "_s" + $stage)
            stage = $stage - 1
            items = $tokens
            enhance = @(("AttackUp=" + [math]::Round([BalanceSim]::LastAttackPct,2).ToString([Globalization.CultureInfo]::InvariantCulture)), ("AttackSpeedUp=" + [math]::Round([BalanceSim]::LastSpeedPct,2).ToString([Globalization.CultureInfo]::InvariantCulture)))
            timeScale = 5
            maxSeconds = 400
        }
    }
    if ($ProbeOut -ne "") {
        $json = @{ scenarios = $scen } | ConvertTo-Json -Depth 6
        Set-Content -Path $ProbeOut -Value $json -Encoding UTF8
        "wrote $ProbeOut"
    }
}

# RunScenario.ps1
# Usage: .\RunScenario.ps1 -Scenario Base -Agent fsm
#        .\RunScenario.ps1 -Scenario EconomicCrisis -Agent qlearn
#        .\RunScenario.ps1 -Scenario DynastyAtRisk
#
# This script:
#   1. Starts the JominiServer with the chosen scenario's CSV set
#   2. Waits for it to be ready
#   3. Starts the JominiAIClient agent
#   4. Saves evaluation results to EvaluationLogs/
#
# NOTE: The JominiServer's networking is currently commented out.
#       Once networking is enabled in JominiEngineReWrite/JominiServer/Server.cs,
#       step 1-3 will work end-to-end. Until then, use --evaluate for dry-run tests.

param(
    [ValidateSet("Base","EconomicCrisis","DynastyAtRisk")]
    [string]$Scenario = "Base",

    [ValidateSet("fsm","qlearn")]
    [string]$Agent = "fsm",

    [switch]$DryRun   # Pass -DryRun to run offline scenario suite instead of live
)

$ServerDir = "$PSScriptRoot\JominiEngineReWrite\JominiServer"
$ClientDir = "$PSScriptRoot\JominiAIClient\JominiAIClient"
$LogDir    = "$PSScriptRoot\JominiAIClient\EvaluationLogs"
$Timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
$LogFile   = "$LogDir\${Agent}_${Scenario}_${Timestamp}.json"

Write-Host ""
Write-Host "  ═══ Jomini AI Evaluation ═══" -ForegroundColor Cyan
Write-Host "  Scenario : $Scenario" -ForegroundColor Cyan
Write-Host "  Agent    : $Agent"    -ForegroundColor Cyan
Write-Host "  Log      : $LogFile"  -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "  [DryRun] Running offline FSM scenario suite..." -ForegroundColor Yellow
    Push-Location $ClientDir
    dotnet run -- --evaluate
    Pop-Location
    exit
}

# ── Step 1: Start the server with the chosen scenario ─────────────────────
Write-Host "  [1/2] Starting server with scenario '$Scenario'..." -ForegroundColor Green
$serverProc = Start-Process -FilePath "dotnet" `
    -ArgumentList "run -- --scenario $Scenario" `
    -WorkingDirectory $ServerDir `
    -PassThru -NoNewWindow

Start-Sleep -Seconds 3   # Give the server time to load CSVs

if ($serverProc.HasExited) {
    Write-Host "  [ERROR] Server failed to start. Check server output." -ForegroundColor Red
    exit 1
}

# ── Step 2: Run the agent ──────────────────────────────────────────────────
Write-Host "  [2/2] Running $Agent agent against scenario '$Scenario'..." -ForegroundColor Green

$agentArgs = if ($Agent -eq "qlearn") { "-- --agent qlearn --save `"$LogFile`"" } else { "" }

Push-Location $ClientDir
try {
    dotnet run $agentArgs
}
finally {
    Pop-Location
    # Stop server
    if (-not $serverProc.HasExited) {
        $serverProc.Kill()
        Write-Host "  Server stopped." -ForegroundColor DarkGray
    }
}

Write-Host ""
Write-Host "  Done. Results saved to: $LogFile" -ForegroundColor Cyan

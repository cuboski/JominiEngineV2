# Runs the baseline evaluation: FSM, then Q-learning training, then Q-learning
# exploit. Logs land in EvaluationLogs/. Run from JominiAIClient/JominiAIClient.
# The server must be running first.

param(
    [string]$Scenario = "Base",
    [int]$FsmSeasons = 5,
    [int]$QTrainSeasons = 10,
    [int]$QExploitSeasons = 5
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force EvaluationLogs | Out-Null

Write-Host "Building client..."
dotnet build -nologo | Out-Null

Write-Host "FSM, $FsmSeasons seasons on $Scenario..."
dotnet run --no-build -- --seasons $FsmSeasons --scenario $Scenario `
    --eval-log "EvaluationLogs/fsm_$Scenario.json"

Write-Host "Q-learning training, $QTrainSeasons seasons..."
dotnet run --no-build -- --agent qlearn --seasons $QTrainSeasons `
    --save qtable.json --scenario $Scenario `
    --eval-log "EvaluationLogs/q_train_$Scenario.json"

Write-Host "Q-learning exploit, $QExploitSeasons seasons..."
dotnet run --no-build -- --agent qlearn --load qtable.json --exploit `
    --seasons $QExploitSeasons --scenario $Scenario `
    --eval-log "EvaluationLogs/q_exploit_$Scenario.json"

Write-Host "Done. Logs in EvaluationLogs/."

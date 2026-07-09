# Evaluation runbook

How to run the validation and performance tests for both agents and produce the
logs used for analysis. Run every command from `JominiAIClient/JominiAIClient/`.

## Before you start

- The game server must be running and reachable (client target is set in
  `Player.cs` / `TestTextClient.cs`).
- `--scenario <name>` on the client is only a label written into the log. The
  actual scenario data is whatever the server was started with. Start the server
  on the matching scenario before each run, and restart it between runs so the
  world starts fresh (treasury, fiefs and family are not reset by reconnecting).
- Logs are written under `EvaluationLogs/`. The folder is created if missing.

## What each log now contains

Every `--eval-log` file has three parts:

- `Seasons[]`  one row per season: treasury, fiefs, bailiffs, troops, threat,
  heir/spouse/alive flags and the composite score. This is the performance data.
- `Actions[]`  one row per action taken: the season and index, the agent state
  at decision time, the action and its arguments, whether the server accepted it
  (`Success`), and the days/treasury before and after. This is the validation
  data.
- `Summary`    totals computed at save time: seasons run, average and final
  score, action acceptance rate, action-type distribution, and
  `MoneyCreationFlags` (count of zero-day non-transfer actions that added more
  than 50k total treasury; used to check no gold is being created out of thin air).

## Phase 1 - validation (are the actions correct?)

### 1a. Offline action-correctness (oracle)

Runs the fixed scenario suite and checks each chosen action against the expected
action. No game progression. FSM should pass all or nearly all. Q-learning has no
dynasty actions in its 10-action space, so it is expected to fail the dynasty
scenarios - that gap is a measured result, not a bug.

    # FSM
    dotnet run -- --evaluate --eval-log EvaluationLogs/validation_fsm.json

    # Q-learning - evaluate the greedy policy of a trained table
    dotnet run -- --agent qlearn --load qtable_crisis.json --exploit --evaluate --eval-log EvaluationLogs/validation_qlearn.json

(The harness forces epsilon 0 for the Q agent during `--evaluate`, so the result
is deterministic regardless of flags. Still pass `--load` so you are validating a
trained table, not an empty one.)

### 1b. Live legality

The `Actions[]` rows from any live run below double as legality validation:
`Success = false` means the agent chose an action the server rejected in a real
game state. A valid agent should have very few or zero rejected actions.

## Phase 2 - performance (are they good agents?)

Restart the server on the target scenario before each command.

### FSM (rule-based, no training)

    dotnet run -- --seasons 5 --scenario Base           --eval-log EvaluationLogs/fsm_base.json
    dotnet run -- --seasons 5 --scenario EconomicCrisis --eval-log EvaluationLogs/fsm_crisis.json
    dotnet run -- --seasons 5 --scenario DynastyAtRisk  --eval-log EvaluationLogs/fsm_dynasty.json

### Q-learning (train, then evaluate)

Train with exploration on (saves the table), then evaluate the learned policy
with exploration off. Restart the server before the training run and again before
the evaluation run.

    # train on EconomicCrisis
    dotnet run -- --agent qlearn --seasons 20 --scenario EconomicCrisis --save qtable_crisis.json --eval-log EvaluationLogs/qlearn_crisis_train.json

    # evaluate the trained policy
    dotnet run -- --agent qlearn --load qtable_crisis.json --exploit --seasons 5 --scenario EconomicCrisis --eval-log EvaluationLogs/qlearn_crisis_eval.json

Repeat the train/evaluate pair per scenario if you want per-scenario Q results
(use a separate `--save` file name per scenario).

## What to hand back for analysis

The JSON files under `EvaluationLogs/`:

- `validation_fsm.json`, `validation_qlearn.json`  (Phase 1a)
- `fsm_base.json`, `fsm_crisis.json`, `fsm_dynasty.json`  (FSM performance)
- `qlearn_*_train.json`, `qlearn_*_eval.json`  (Q-learning performance)

The console output is also useful, but the JSON is the exploitable artifact.

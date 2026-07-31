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

Each `Actions[]` row also carries:

- `Reason`      the server's refusal reason, or null when it accepted. Read this
  first when an action fails; it is what tells you whether a leader was refused
  for standing in the wrong fief or for already leading the army.
- `Negotiation` true when the refusal was a haggling round (a hire bid the NPC
  has not accepted yet) rather than a decision the server judged invalid.
- `ElapsedMs`   wall-clock time to choose and execute the action, server
  round-trip included.

and `Summary` correspondingly gains `FailureReasons` (refusals grouped by action
and reason, commonest first), `NegotiationActions` and `DecisionSuccessRate`
(acceptance with haggling rounds excluded - the figure that reflects decision
quality), and `MeanActionMs` / `MedianActionMs` / `P95ActionMs` for what a single
decision costs.

`Seasons[]` rows carry `SeasonSeconds`, and the summary `MeanSeasonSeconds`, for
the comparison against a human player's pace. Note these are much larger than the
action timings summed: the state refresh after each action, the pause between
passes and the wait on the season update all sit outside the decision itself.
`MeanDecisionSecondsPerSeason` is the share the agent itself accounts for.

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

Long runs are what surface the interesting behaviour (the bailiff ceiling, the
large treasuries, the heir lapse), so run the FSM for as many seasons as you can
spare. The paper's figures come from a 200-season Base run and 60-season crisis
and dynasty runs; restart the server between each.

    dotnet run -- --seasons 200 --scenario Base           --eval-log EvaluationLogs/fsm_base_v2.json
    dotnet run -- --seasons 60  --scenario EconomicCrisis --eval-log EvaluationLogs/fsm_crisis_v2.json
    dotnet run -- --seasons 60  --scenario DynastyAtRisk  --eval-log EvaluationLogs/fsm_dynasty_v2.json

### Q-learning (train, then evaluate)

Train with exploration on (saves the table), then evaluate the learned policy
with exploration off. Restart the server before the training run and again before
the evaluation run.

    # train on EconomicCrisis
    dotnet run -- --agent qlearn --seasons 60 --scenario EconomicCrisis --save qtable_crisis.json --eval-log EvaluationLogs/qlearn_crisis_train.json

    # evaluate the trained policy over a long run (confirms the closed transfer exploit)
    dotnet run -- --agent qlearn --load qtable_crisis.json --exploit --seasons 137 --scenario EconomicCrisis --eval-log EvaluationLogs/qlearn_crisis_eval.json

Repeat the train/evaluate pair per scenario if you want per-scenario Q results
(use a separate `--save` file name per scenario). Read `MoneyCreationFlags` in the
eval `Summary`: it must be 0. A non-zero count means the transfer exploit (or a
new one) has reopened.

### Multi-seed (turning indicative numbers into established ones)

Each command above is one run. For the statistically-established figures the paper
flags as future work, run each scenario several times with a fresh server each
time and a distinct log name, then average. Nothing in the client fixes a seed, so
repeated runs already differ; keep them apart by file name:

    for i in 1 2 3 4 5; do dotnet run -- --seasons 60 --scenario Base --eval-log EvaluationLogs/fsm_base_seed$i.json; done
    # (restart the server between iterations so each starts from a fresh world)

## Phase 3 - war (does the military path complete?)

`--aggressive` makes the FSM raise a war party, lead it, march on a neighbour and
lay siege, rather than waiting to be attacked. A solo agent in a peaceful world is
never attacked, so without this the war state is never entered at all.

    dotnet run -- --aggressive --seasons 60 --scenario War --eval-log EvaluationLogs/fsm_war_v2.json

Read the run by its `FailureReasons`, not just its score. Up to July 20 2026 every
`ArmyAppointLeader` in every war run was refused (the state builder compared the
army's leader *name* to the player's char id, so it never saw that the PC already
led, and re-appointed him every season); that is fixed, and a working run now shows
a non-zero `ThreatLevel` in `Seasons[]` and the `ArmyRecruit -> ArmyAppointLeader
-> NPCmoveDirection -> SiegeBesiegeCurrentFief -> SiegeReduction` sequence firing in
order in `Actions[]`. To press a siege through to the fief actually changing hands,
give the war party more days: it ran out mid-siege in the 60-season run.

## Phase 4 - scalability with two agents (the "versus" run)

Two clients against one server, playing at the same time on different accounts.
This is what backs the multi-agent scalability claim, and with `--aggressive` on
both it is also the only way to get a war between two agents rather than a
manufactured siege on a passive neighbour.

Start the server once, then launch both within a few seconds of each other, in
two separate shells:

    # shell 1
    dotnet run -- --aggressive --seasons 150 --user acc1 --pass <pw> --eval-log EvaluationLogs/versus_A.json

    # shell 2
    dotnet run -- --aggressive --seasons 150 --user acc2 --pass <pw> --eval-log EvaluationLogs/versus_B.json

Compare the two logs on seasons survived, fief growth, composite score and
`MeanSeasonSeconds` - the timing figure is the one that says whether two agents on
one server slow each other down. Note the accounts do not start symmetrically (acc2
begins with far fewer fiefs), so read growth as a rate rather than comparing
final fief counts directly.

## What to hand back for analysis

The JSON files under `EvaluationLogs/`:

- `validation_fsm.json`, `validation_qlearn.json`  (Phase 1a)
- `fsm_base.json`, `fsm_crisis.json`, `fsm_dynasty.json`  (FSM performance)
- `qlearn_*_train.json`, `qlearn_*_eval.json`  (Q-learning performance)

The console output is also useful, but the JSON is the exploitable artifact.

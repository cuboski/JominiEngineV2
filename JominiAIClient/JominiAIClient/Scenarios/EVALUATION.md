# Agent Evaluation

How to run the FSM and Q-learning agents, what we measure, and the caveats to
keep in mind when reading the numbers.

## Prerequisites

1. The game server must be running (it listens on 10.130.61.5:8080).
2. Build the client once:

   ```
   cd JominiAIClient/JominiAIClient
   dotnet build
   ```

3. Account `acc1` / `pass1` (King Richard, Char_47) must exist in the server's
   database. A fresh database starts Char_47 in the Base scenario.

## Commands

All commands run from `JominiAIClient/JominiAIClient`.

| Goal | Command |
|------|---------|
| FSM decision check (no game state changes) | `dotnet run -- --evaluate` |
| FSM, one season, logged | `dotnet run -- --seasons 1 --eval-log EvaluationLogs/fsm_s1.json --scenario Base` |
| FSM, five seasons, logged | `dotnet run -- --seasons 5 --eval-log EvaluationLogs/fsm_s5.json --scenario Base` |
| Q-learning, train 10 seasons | `dotnet run -- --agent qlearn --seasons 10 --save qtable.json --eval-log EvaluationLogs/q_train.json` |
| Q-learning, keep training (load + continue) | `dotnet run -- --agent qlearn --load qtable.json --seasons 10 --save qtable.json` |
| Q-learning, exploit (no exploration) | `dotnet run -- --agent qlearn --load qtable.json --exploit --seasons 5 --eval-log EvaluationLogs/q_exploit.json` |

`run_evaluation.ps1` (same folder) chains the FSM run, the Q-learning training,
and the Q-learning exploit run, then leaves the JSON logs in `EvaluationLogs/`.

## What we measure

Each season end is scored by `ScoreComputer.Compute` (see EvaluationRunner.cs).
The composite score is a weighted sum in [roughly -0.1, 1.0]:

| Component | Weight | 0.0 means | 1.0 means |
|-----------|--------|-----------|-----------|
| Economic health | 0.30 | home treasury <= 0 | home treasury >= 100k |
| Bailiff coverage | 0.15 | no fiefs have a bailiff | every fief has one |
| Military strength | 0.15 | no troops | 500+ troops |
| Dynasty | 0.30 | no heir, dead, unmarried | heir + alive + spouse |
| Threat penalty | -0.10 | at war | at peace |

Per-season the log also stores: treasury, fiefs in deficit, fief count, bailiff
count, army troops, threat level, heir status, alive, spouse, actions executed,
actions failed.

Headline numbers per run (printed by `PrintSummary` and stored in the JSON):
average score across seasons, final-season score, dynasty intact, heir ever
appointed.

## Comparison procedure

1. Run the FSM for 5 seasons on each scenario, save the logs.
2. Train Q-learning for 10+ seasons on the same scenario, saving the table.
3. Re-run Q-learning with `--exploit` for 5 seasons and save that log.
4. Compare FSM vs exploit-Q on average and final composite score, per scenario.

Report scores per scenario (Base, EconomicCrisis, DynastyAtRisk), not pooled,
because the scenarios stress different sub-scores.

## Role-fit candidate selection

Both agents now pick role-appropriate characters instead of the first available
one. The game scores roles by different stats, so the wrong person in a role is
wasteful:

- **Bailiff**: ranked by `(stature + management) / 2`, matching the engine's fief
  income and loyalty modifiers (`Fief.CalcBaseFiefLoyMod`, `CalcBlfIncMod`).
- **Army leader**: ranked by `combat`, matching battle and siege resolution. This
  replaced the old behaviour of grabbing the first male NPC at the army's fief.
- **Heir**: still ranked by `management` (the heir runs the demesne).

`stature` was previously not sent to the client; it is now copied through
`ReducedProtoMessage` and `RedDeepCopier`. The chosen candidate and its fit score
are printed in the per-turn facts line (`bailiff cand: ... (fit X.X)`,
`leader cand: ... (combat X.X)`) so the choice is auditable in a run. Traits
(`BATTLE`, `SIEGE`, `FIEFLOY`) and `virility` are still not exposed; wiring those
would refine the scores further but needs an array-mapping pass.

## Caveats (read before trusting the numbers)

- **Expenditure-adjust clears the deficit instantly.** In a live run,
  `FiefAutoAdjustExpenditure` on the deeply-negative home fief moves the
  displayed treasury from a large negative to 0 in a single zero-day action
  (observed: -392000 to 0). This is the server zeroing planned over-spend, not
  the agent earning gold, but it inflates the economic sub-score. The client now
  prints a `[CHECK]` line whenever total treasury rises by more than 50k on a
  zero-day, non-transfer action so this is visible in the trace. Treat economic
  recovery within a single season with suspicion; multi-season trends are more
  meaningful.

- **Q-learning has no dynasty actions.** Its 10-action space omits heir
  appointment, marriage, and proposals, so its dynasty sub-score (0.30 weight)
  will usually trail the FSM. This is intentional and should be stated when
  comparing totals.

- **The season-end crash needs a server rebuild.** A null-heir death during
  `SeasonUpdate` crashed the server with "Collection was modified" in
  `Character.TransferPropertyToKing`. The fix (iterate a copy of the fief list)
  is in `server/JominiEngine/Character.cs`, but the running server uses the old
  binary. Rebuild and restart the server before doing multi-season runs, or the
  crash can recur at the first season boundary where a character dies.

- **State refresh is slow.** Each action re-fetches state across all 52 fiefs,
  so a full season takes a few minutes. Budget time accordingly.

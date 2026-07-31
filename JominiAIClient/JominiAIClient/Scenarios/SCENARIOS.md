# Evaluation Scenarios

This directory contains fixed-seed CSV copies for reproducible agent evaluation.

> **Server restart required (June 2026):** The server binary was rebuilt with a critical fix
> for the `NPCcamp` action (it was always rejected with `ErrorGenericMessageInvalid`).
> Stop the running server process and restart it before evaluating so the new binary is active.
> Without camp working, days never advance and treasury never recovers.
> See [Camp fix notes](#known-issues--fixes) below.

---

## How to Evaluate

### Dry-run (offline, no server needed)
Tests the FSM's decision logic against 15 hand-crafted cases without executing any server calls.
```
dotnet run -- --evaluate
```
Each case is PASS/FAIL based on whether the FSM chose the expected action.

### Live evaluation (requires server)
Run either agent against a live scenario and collect per-season metrics automatically.
Pass `--eval-log <path>` to activate logging; results are written as JSON.
Pass `--scenario <name>` to label the results (e.g. `Base`, `EconomicCrisis`, `DynastyAtRisk`).
```
# FSM, Base scenario with logging
dotnet run -- --eval-log EvaluationLogs/fsm_base.json --scenario Base

# FSM, EconomicCrisis scenario (restart server with EconomicCrisis CSVs first)
dotnet run -- --eval-log EvaluationLogs/fsm_crisis.json --scenario EconomicCrisis

# Q-learning, training mode with logging
dotnet run -- --agent qlearn --eval-log EvaluationLogs/qlearn_base.json --scenario Base

# Q-learning, exploit mode (load trained table)
dotnet run -- --agent qlearn --exploit --load qtable.json --eval-log EvaluationLogs/qlearn_exploit.json
```

Display also shows **Home treasury** and **Total treasury** delta after each action so
you can see which fiefs are being affected, not just the home fief.

---

## Scenario Directory Structure

```
Scenarios/
  Base/               fief.csv  pc.csv  npc.csv  - default game state
  EconomicCrisis/     fief.csv  pc.csv  npc.csv  - ALL 52 Char_47 fiefs treasury = -50 000
  DynastyAtRisk/      fief.csv  pc.csv  npc.csv  - Char_47 (Richard) unmarried, Char_6000 (John) as heir candidate
```

---

## Scenario Descriptions

### Base (Scenario A)
**Intent:** Stable peacetime. Baseline for both agents.
**Changes:** None - exact copies of the default server CSVs.
**Expected FSM behaviour:**
- Tries for a child each eligible season with his spouse Berenguela (Char_589), who
  starts co-located at ELN01. This is the ONLY path to an heir here: Richard has no
  living son or brother, so `NPCappointHeir` has no candidate and does not fire.
  (Roger/Char_363 is NOT Richard's family - his familyID is Char_1, a different dynasty.)
- Hires available NPCs (up to 3 employees)
- Auto-adjusts expenditures if any fief goes into deficit
- Maintains and recruits army troops

**Note on the heir:** An heir is not appointed in season 1 - the dynasty continues only
once a son is *born and survives infancy*. Conception is reliable, but ~50% of births
are daughters and ~34% of newborn sons are stillborn, with a ~3-season gestation, so a
surviving son appears slowly (roughly one per ~9 seasons on average). Short runs often
end heirless; long runs usually produce a surviving son unless the lord dies young first.

**Evaluation metrics to watch:**
- `HeirAppointed` typically stays `false` (heirs come from surviving sons via
  primogeniture, not from `NPCappointHeir`); watch for a surviving son over a long run
- `HomeTreasury` stays above 0
- `FiefsWithBailiff` equals `FiefCount`

---

### EconomicCrisis (Scenario B)
**Intent:** Force the agent into `EconomicCrisis` state immediately across the entire realm.
**Changes (fief.csv):**
- ALL 52 Char_47 fiefs -> `Fief Treasury = -50000`
  (including ELN01/London home fief; no fief has a positive balance)

**Why all 52?** Setting only 1-2 fiefs negative was insufficient - the home fief can still be in
surplus, so the FSM never enters `EconomicCrisis`. Setting all fiefs forces a true realm-wide crisis.

**Expected FSM behaviour:**
- Enters `EconomicCrisis` FSM state on first tick (home treasury = -50 000, critical)
- Calls `FiefAutoAdjustExpenditure` on each deficit fief (up to 16 per season before camping)
- Once expenditures are cut, camps to advance time -> treasury recovers from fief income
- Does NOT spend on new hires or bailiffs while in crisis
- Dynasty stays priority 1 even in crisis, but as in Base there is no male relative to
  appoint, so `NPCappointHeir` does not fire; an heir depends on a surviving son

**Evaluation metrics to watch:**
- `FiefsInDeficit` drops from 52 to 0 after first FiefAutoAdjust pass
- `HomeTreasury` recovers above 1 000 after 2-3 seasons (exits critical)
- `FiefsWithBailiff` stays 0 during crisis (no bailiff hiring while critical)
- Composite score recovers across seasons

---

### DynastyAtRisk (Scenario C)
**Intent:** Force the agent to fix the dynasty: Richard (Char_47) is unmarried, John is eligible heir.
**Changes (pc.csv):**
- `Char_47` (King Richard I) -> Spouse column cleared (no wife)

**Changes (npc.csv):**
- `Char_589` (Berenguela) -> Spouse column cleared (not married to Richard)
- `Char_6000` (John Plantaganet, born 1167) -> NEW male family NPC added at ELN01 as heir candidate

**Initial state:**
- Richard (Char_47, born 1157): unmarried, no heir appointed
- John (Char_6000, born 1167): male, familyID=Char_47, at ELN01, eligible heir
- Berenguela (Char_589): female, familyID=Char_47, unmarried, at ELN01

**Note on Char_1 (Henry):** The original scenario entry also clears Henry's spouse, which remains
unchanged. Henry (with account `ai_fsm`) can still be used for the same scenario if you switch accounts.

**Expected FSM behaviour for Char_47:**
- Season 1: appoints John (Char_6000) as heir immediately
- Season 1: tries to propose marriage (limited by NPC stub visibility - only works if marriage
  candidates have been fetched with full details previously)
- If marriage candidate is found: `PCHasSpouse` becomes true

**Evaluation metrics to watch:**
- `HeirAppointed` becomes true within season 1
- `PCHasSpouse` becomes true (if a marriage candidate is available)

---

## Evaluation Scoring

Each season is scored on a 0-1 scale:

| Metric            | Weight | Notes                                  |
|-------------------|--------|----------------------------------------|
| Economic health   | 30 %   | Home treasury / 100 000, capped at 1   |
| Bailiff coverage  | 15 %   | Fiefs with bailiff / total fiefs       |
| Military strength | 15 %   | Army troops / 500, capped at 1         |
| Dynasty score     | 30 %   | Heir (40%) + alive (40%) + spouse (20%)|
| Threat penalty    | -10 %  | Threat level / 3                       |

**Composite = Σ(metric × weight)**

---

## Q-Learning Agent - What to Expect in the Log

The Q-learning agent prints verbose `[Q]` lines at every step. This is intentional and lets you
see learning happening in real time. Here is what each line means:

```
  [Q] reward=+0.123  Q[FiefAutoAdjustExpenditure]: +0.000 -> +0.012  session=+0.456
```
The action taken last step earned reward +0.123; the Q-value for that action increased.

```
  [Q] state=000000110001  obs=[0.00,0.31,...]  states=4  ε=0.200
```
The current discrete state key, raw obs vector, number of unique states seen so far, and
   current exploration rate. `states` growing each episode means the agent is exploring new
   situations. After many episodes `states` should plateau.

```
  [Q] Q-vals: NPCcamp=+0.00  FiefAutoAdjustExpenditure=+0.01  ...  [EXPLORE: ArmyRecruit]
```
Q-values for every action in the current state; `EXPLORE` = random choice (ε-greedy),
  `EXPLOIT` = best known action. Early runs will mostly `EXPLORE`; later runs (after loading
  a saved table with `--exploit`) should mostly `EXPLOIT`.

```
  [Q] Season 1 complete | reward=+1.234 | ε=0.199 | states=12
```
End of season summary. `reward` is the total cumulative reward this season.
  A positive trend across seasons indicates the agent is improving.

### Before vs after training

1. **Fresh run** (`dotnet run -- --agent qlearn`): `states` starts at 0, all Q-values are 0.00,
   `EXPLORE` appears on almost every step. Session reward will be near 0 or slightly negative.
2. **Trained run** (`dotnet run -- --agent qlearn --exploit --load qtable.json`): Q-values are
   non-zero, `EXPLOIT` appears on most steps, the agent consistently picks known good actions.
   Session reward should be noticeably higher than the fresh run.

Save path: `qtable.json` (in the `JominiAIClient/JominiAIClient/` working directory by default).
Use `--save <path>` to override.

### Training summary (printed at the end of every Q-learning run)
```
  [Q] Training summary: episodes=5 | steps=237 | states=28 | ε=0.180
```
`episodes` = seasons completed this run; `steps` = total actions taken; `states` = Q-table size.
After multiple runs with the same save file, `episodes` and `steps` will accumulate, showing
total training progress across all runs.

---

## Comparing FSM vs Q-Learning

Run both agents on the same scenario seed and compare:

| Metric             | FSM  | Q-Learning (fresh) | Q-Learning (trained) |
|--------------------|------|--------------------|----------------------|
| Avg composite score| ?    | ?                  | ?                    |
| Heir appointed     | ?    | N/A (not in Q-space)| N/A                 |
| Treasury season 5  | ?    | ?                  | ?                    |
| Sessions survived  | ?    | ?                  | ?                    |

Fill in this table after live runs to compare agent quality on identical starting conditions.
Note: Q-learning does not handle dynasty (heir/marriage) - factor this into the dynasty score
column when evaluating fairness of the comparison.

---

## Known Issues & Fixes

### NPCcamp always rejected (fixed June 2026)
**Symptom:** Camp never advances days; the log shows endless `NPCcamp` fail lines and
days stay at 90 throughout the entire 50-action run. Treasury stays frozen after
`FiefAutoAdjustExpenditure` completes because time never passes.

**Root cause (server):** ProtoBuf-net calls the `ProtoMessage()` constructor during
deserialization, which pre-fills `MessageFields = ["None"]`. It then appends the wire
value `"5"`, giving `["None", "5"]`. The old camp handler did `MessageFields[0]` =
`"None"` -> `Convert.ToByte("None")` throws -> `ErrorGenericMessageInvalid`.
**Fix:** Server's camp handler now iterates MessageFields, skips `"None"`, and parses
the first valid byte. Rebuilt June 8, 2026. **User must restart server.**

**Root cause (client infinite loop):** `FSMAgent.Camp()` fell back to `NPCexitKeep`
when camp failed. `EnterExitKeep` is a toggle - calling it when NOT in keep enters the
keep, which cleared `FailedActions`, causing an infinite enter/exit/camp loop (visible
as 30+ identical steps in the log).
**Fix:** When `Failed(NPCcamp)` is true and not in keep, `Camp()` returns `None`
immediately (season ends cleanly). Committed June 2026. No server restart needed.

**After the fix:** Camp should succeed, days will decrease from 90 each time camp
runs, and treasury will update from income/expenses. The INITIAL STATE treasury
showing `-50 000` and FINAL STATE treasury showing a positive value confirms recovery.

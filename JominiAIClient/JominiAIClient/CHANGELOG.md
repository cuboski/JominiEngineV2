# Changelog

Running notes on the AI client and the engine changes it needed. Newest first.
Server edits are marked SERVER, those need an MSBuild rebuild (VS2022, .NET
Framework 4.8) and a restart of the server process; `dotnet build` won't touch them.

## July 22 2026, the long-run confirmation pass

With the faults above closed, a full set of long runs was captured on fresh server worlds
to confirm the fixes hold over hundreds of seasons rather than a handful, and these are the
numbers the write-up now uses (logs under `EvaluationLogs/`, the `_v2` files):

- FSM, Base, 200 seasons: heir named in season 11, realm grows 52 -> 188 fiefs, average
  score 0.632, `MoneyCreationFlags` 0. The length is what surfaced the bailiff ceiling below.
- FSM, EconomicCrisis and DynastyAtRisk, 60 seasons each: full bailiff coverage held, heir
  secured, 96-98% acceptance.
- FSM, War (aggressive), 60 seasons: the recruit -> appoint-leader -> move -> besiege ->
  reduce sequence fires in order for the first time, 100% acceptance. The siege runs out of
  days before the fief changes hands; that needs more days in one season, not another fix.
- Q-learning, Base eval 150 seasons (avg 0.709) and Crisis eval 137 seasons (avg 0.628):
  both `MoneyCreationFlags` 0, coverage held near full, heir secured.
- Two FSM agents on one server world at once (`versus_A`/`versus_B`): no recurrence of the
  framing or dead-character faults, both ran to completion, timings matched the single-agent
  figures. First evidence the client and engine hold up with concurrent agents.

The reward-hack closure is the headline. The three legs are now all in place and confirmed:
the reward scores *total* realm wealth (not home), the transfer action is gated to fire only
when the home fief is short of gold, and each internal transfer carries a small fixed penalty
(`QLearningAgent.cs` ComputeReward/ResolveArguments, ~L198) so a self-transfer is strictly
reward-negative unless it does real work. The early crisis run that chose the free transfer
200 times in 250 actions and drove home treasury past 74M is down to 18 transfers in 137
seasons on the same world, and 4 in 150 on Base, with no money-creation events either time.

The one open item these runs newly expose is the bailiff-appointment ceiling: from ~season 40
the server refuses `AppointBailiff` with an authorisation error (1,094 times over the 200-season
Base run) so coverage decays to ~57% as the realm keeps growing, while the Q run over a similar
span did not hit it. That points at a per-fief eligibility condition rather than a numeric cap;
the refusal reason is already logged per action and is the next thing to read.

## July 21 2026, SERVER: recruitment could kill the listen thread

A Q-learning training run took the server down with a NullReferenceException in
`PlayerCharacter.RecruitTroops`. Recruiting costs a random 1-5 days, so a character with days
still in hand can be short of what the attempt needs. That check set `proceed = false` and
built an error message, but never returned it, the pre-checks above it do return, and this
one was added later and missed it. So the call fell straight through: the recruitment block
was skipped, the days were charged anyway, and the success reply at the bottom was built from
`thisArmy`, which is null when the request was to raise a NEW army. The dereference
threw on the listen thread, which is not one request failing but the whole server stopping.

Returns the error now. Also added a null guard at the reply site, since this is the second
NPE found in this one method and the cost of being wrong there is the entire server.

Client side, both agents now require 5 days in hand before asking to recruit (`RecruitMaxDays`
in `AgentState`), rather than 1. Anything less was a gamble on the server's die roll that
wasted the action when it lost. The FSM mostly got away with it; the Q agent, which recruits
more opportunistically, is what found the crash.

## July 20 2026 (later still), season timing was measuring the wrong thing

`MeanSeasonSeconds` was derived by summing the per-action timings, which gave 1.3 s for a
season that actually takes about a minute. The per-action stopwatch wraps `playOneAction`
only, so everything after it - the full state refresh, the pause between passes, the wait on
the season-update barrier - fell outside every measurement. Summing them was never going to
reconstruct the season.

Seasons are now timed directly: `SeasonSnapshot.SeasonSeconds` holds the measured wall clock,
`MeanSeasonSeconds` averages it, and the old derived figure is kept alongside as
`MeanDecisionSecondsPerSeason`, which is a fair description of what it always was - the share
of the season the agent spends deciding. Both are worth reporting: the first says whether the
agent keeps pace with a human, the second says how much of that is its own thinking rather
than waiting on the server.

Worth remembering when reading a run: a 200-season run is roughly three hours, not four
minutes.

## July 20 2026 (later), SERVER: the actual army-leader blocker

The diagnostic run (`fsm_war_diag.json`) did its job: 20 seasons, 95.5% acceptance, and the
only failures left were 19 leader appointments, now carrying a reason, `ErrorGenericMessageInvalid`, which is not a "you may not do that" at all but an argument
that failed validation before the appointment was even considered.

The arguments logged fine (`Army_4,Char_47`), so the mismatch is in how the server reads
them. `AppointBailiff` reads the character id from `MessageFields[1]` and is accepted on
every call, 104 for 104 in the same run, while `AppointLeader` was changed in July to read
`[0]`, on the reasoning that "every client puts the id at [0]". The two are sent by the
identical `SendRequestToServer(action, message, new[]{ id })` call, so the bailiff case is
the ground truth: the id lands at `[1]`, `[0]` is not a charID, `ValidateCharacterID` rejects
it, and out comes ErrorGenericMessageInvalid. Reverted `AppointLeader` and `Attack` to `[1]`.

Also tightened all three guards from `Length < 1` to `Length < 2`, since all three index `[1]`, a one-field message would have thrown IndexOutOfRange and taken the listen thread down.

SERVER change: needs an MSBuild rebuild (VS2022, .NET Framework 4.8) and a server restart
before it takes effect. `dotnet build` does not touch it.

## July 20 2026, five refusal loops closed, and the war path opened

The 200-season runs made a set of faults visible that the short runs never could: four
actions were being refused at rates between 74% and 100%, and between them they accounted
for most of what the agent did all run. All four turned out to be client-side, and three of
them were the same mistake, comparing or indexing the wrong field and never being told why
the server said no.

**The army leader was refused 100% of the time, in every war run ever recorded** (120/120,
62/62, 38/38). `RedProtoArmy` only carried `leaderName`, which the server fills with
`"firstName familyName"`, and `AgentState` compared that display name to a charID:

    pcLeadsMainArmy = main.leaderName == gs.myPC.charID;   // never true

So the aggressive FSM never believed its lord was leading, re-appointed him every season,
and the server refused ("already leading this army"). The war party never marched, no siege
or battle was ever fought, and threat stayed at zero in every run, the entire military
dimension of the project was dark because of this one comparison. `RedProtoArmy` now carries
`leaderID` (the server has always sent it) and every leadership test uses it.

`tryGetArmyFiefID` had a matching deadlock: it located an army through its leader, so a
leaderless army had no known location and could never be given one. It now falls back to the
PC's fief, a leaderless army cannot march, so it stands where it was raised, and returns
false instead of throwing when the leader can't be resolved.

**A lord kept trying to accept his own marriage proposals** (185 attempts, 185 refusals in
the two-agent run). Received proposals were detected by fixed positions in the journal
entry's `personae` array, testing "the groom is not me". Marry a son off and the groom is the
son, so the lord read his own proposal as one addressed to him, tried to accept it, and was
refused, only the bride's head of family may reply. The entry stays unread until answered
properly, so he tried again every pass. Now matched by role name (`headOfFamilyBride`),
which is also robust to the server dropping personae it can't resolve and shifting every
index, and entries already `replied` to are skipped.

**Hiring was refused 19 times in 20** (436/444, 473/479, 529/546). The salary estimate
mirrored only the server's trait-based figure and ignored the NPC's current wage, but the
server prices a man at the *higher* of the two, so anyone already in service was
under-priced and the opening bid landed below the floor at which offers are dismissed out of
hand. Added the current-salary term, and moved the opening bid from 1.1× to 1.15× of the
estimate: the engine accepts anything more than 10% above its own valuation, so 1.1 sat
exactly on the line and went to a coin toss. Haggling refusals are now also flagged
`Negotiation` in the log and reported separately, because a rejected bid is how the engine
bargains, not a decision the agent got wrong.

**Bailiff appointments were refused 2,656 times in the dynasty run** (82% of all its
actions). A few fiefs refuse permanently, the client lists them as ours while the server
disagrees about ownership, and the per-season block was cleared every season, so those same
fiefs were retried for 200 consecutive seasons. A fief that refuses three times running is
now left alone and re-tried once every 25 seasons, since ownership can legitimately change.

**Every refusal now records its reason.** `ActionRecord` gained `Reason`, and the summary a
`FailureReasons` breakdown, so a run says *why* something was refused instead of only that it
was. This is what the army-leader fault needed and never had.

**Per-action timing.** `ActionRecord.ElapsedMs` times the full decide-and-execute cycle
(server round-trip included, since that cost is real to a player), and the summary reports
mean/median/p95 and seconds per season. This is the evidence for whether an agent keeps pace
with a human, which had no data behind it at all before.

## July 18 2026, succession clarity, and all three scenarios runnable

**Made "days did not refresh" say what it means.** A long base run ended with that message
while the last snapshot showed the lord alive, which read like a day-allocation bug. It
wasn't: the lord (maxHealth 9) lived to ~76 and outlived his lower-health sons, then died
heirless in the final season-update, a legitimate extinction. Traced the succession path to
be sure: when a lord dies with an heir, the server promotes the heir and rebinds the account
to him (`ProcessDeath`), and the client already refreshes who it controls after every
request, so `GetPlayers` reports the new lord. That path is sound but had never actually
fired in a completed run, because the first lord always outlived his heirs.

The end-of-run check now resyncs the controlled character across the season boundary and
reports the cause: `[SUCCESSION] X died; Y succeeds` when the heir takes over,
`Bloodline extinct` when a lord dies with no heir, and a red `[ANOMALY]` if a lord is
genuinely alive but has no days, the last being the only case that would point at a real
day-allocation fault. Added `Player.CurrentPCId` and `ResyncCurrentPC()` for this.

**SERVER, all three scenarios load without swapping files.** The server hard-coded
`gameObjects - Copy.csv`; only the base game ever ran. It now reads the `JOMINI_SCENARIO`
environment variable and loads `gameObjects - <name>.csv` when set, falling back to base
otherwise, and prints which it loaded. Generated the two scenario data files beside the base
one, in the monolithic format the server actually reads (the `Scenarios/*/` folders were in a
split format it can't load):
- `gameObjects - EconomicCrisis.csv`, all 52 of Char_47's fiefs set to −50000 treasury,
  everyone else untouched.
- `gameObjects - DynastyAtRisk.csv`, Richard unmarried, Berenguela no longer his wife, and a
  new male family member (Char_9999 "John", born 1180, at ELN01) as a ready heir to appoint.

To run a scenario: set the env var, restart the server, run the client as usual, e.g.
`$env:JOMINI_SCENARIO="EconomicCrisis"` then start the server. Unset it (or set it empty) to
go back to base. No file copying, nothing to restore.

## July 17 2026 (late), an heir and a spare

A long Q-learning run kept an heir for most of it, then went heirless at season 62 with the
PC still alive and married. Diagnosis: the heir was a son (appointed season 21) who died at
season 62 after ~40 seasons, natural mortality, not corruption. `HasHeir` reads the server's
`GetHeir()`, and nothing clears an heir but appointing a new one or the heir leaving the
family list, which only happens on death. Over 40 seasons even a low per-season child death
rate compounds to about a coin-flip, so losing an only heir eventually is likely.

The real gap was that the dynasty kept just one heir at a time, so a single death ended the
succession. Fixed by breeding until there's an heir **and** a spare: the FSM now fathers
children while it has fewer than two living sons, in both Stable and Consolidating. It can't
spam, a successful attempt leaves the wife pregnant, which blocks further attempts for the
~3 seasons of gestation, so it costs about one day every few seasons, cheap insurance for
the run's top priority. (The Q agent breeds on exploration, so it won't build a spare as
reliably; that's a known Q limitation, not addressed here.)

Also fixed a misleading log field: `FamilyNPCCount` was declared in the season snapshot but
never assigned, so it always read 0 even with a living heir. The snapshot now records
`FamilyNPCCount`, `LivingSonCount` and the heir's `HeirId`, so a future run shows plainly
when an heir changes, a new son appointed versus the heir dying, instead of a bare
true/false. The console `[DYNASTY]` line already showed the heir id and living-son count.

## July 17 2026 (night), why dynasties kept dying out: the death rate

A fresh 6-season run ended heirless once more, and the season snapshots in the eval JSON
made it plain: the PC had a spouse in season 1 and none from season 2 on. The wife died in
the very first season-update, so the FSM was a childless widower for the rest of the run, `CanTryForChild` was correctly false (no wife), and the camping it did was the only legal
move left, not a mistake. The "days did not refresh" ending was genuine bloodline
extinction, not a season-advance bug.

**SERVER, natural death was rolling four times too often.** `CheckForDeath` runs every
season, i.e. four times a year, but the rate `(10 − health) × 2.8` (2.5 for women) reads
like an annual figure. So a healthy thirty-year-old faced ~4% a season and about a third
were dead within a year; the lords' wives rarely lived long enough to bear an heir, and the
vassals died so fast their fiefs escheated to the crown in bulk (fief count 52→97 in a year
and a half). Nobody intends a healthy young noble to average a 2.5-year lifespan.

Kept the per-season cadence (that's how the game is structured) and rescaled the rate to
what a noble who reached adulthood actually faced: increment 2.8/2.5 → 0.35/0.30. Compounded
over the four seasonal rolls that gives a prime adult ~1.5, 2% a year, an older lord 6, 10%,
the elderly past 10%, and a mother about 2% at each birth. A maxHealth-6 wife now has an
expected ~21 further years instead of ~2.5, so she lives to have children, and the vassal
die-off drops to a trickle. This, together with the earlier stillbirth, pregnancy-reset and
heir-serialisation fixes, is what should finally let an heir survive to be appointed. The
FSM was never the problem here; its priorities were already right.

## July 17 2026 (evening), two failed actions were ending the whole season

Symptom: a season was one hire that never landed, one bailiff that never took, and then
nothing. Three causes, one of them a design trap worth remembering.

**FailedActions is action-wide and only clears on a success.** Every failure did
`FailedActions.Add(nextAction)`, so one fief refusing a bailiff barred bailiff appointments
on all hundred-odd other fiefs, and one man refusing a wage barred hiring outright. Once
both were barred there was nothing left that could succeed, so the set could never clear
and the agent camped out the rest of the season. Two argument-specific refusals were enough
to end it. Those actions now record only the args-aware season block and never the
action-wide one, safe, because each already has a per-target guard that guarantees fresh
arguments on the next pass. The rule to remember: only bar the whole action when the
refusal is about the action, not about who or what it was aimed at.

**The hire loop.** `CharacterOfferLow` and `CharacterOfferAlmost` were being counted as
successes, but both mean the man is still not hired. So a rejected lowball cleared the
failure memory, the FSM recomputed the identical offer, it's a pure function of the salary
estimate, and sent it again, forever. This only started showing up now because the stub bug
meant no hirable NPC was ever visible before.

The offer was too low because our estimate feeds raw management/leadership into the same
formula the server uses, while the server prices a man on his traits, his stature and what
he already earns. Rather than try to replicate `CalcSalary`, the agent now haggles the way
the engine expects: each rejected bid goes up 25%, capped at four times our estimate or
what the home fief can actually pay, whichever is lower. A man bid past that is skipped so
the next candidate gets a turn, picking the first name on the list and then giving up on
him left everyone behind him unhired. Uncommented `lastOfferID`/`lastOfferAmount` on
RedProtoNPC: the server only fills those in for someone with no family and no employer, so
a non-empty lastOfferID doubles as a reliable "this one really is hirable". The foreign
character cache has to be dropped after each attempt or it serves back the old offer and
the bid never rises.

Worth knowing for later: the server checks "too low" before it checks "you must beat your
last offer", so a repeated lowball comes back as Low forever and never as Haggle.

## July 17 2026 (later still), multiple agents, and the CHECK warning was wrong

**The money-creation warning was measuring the wrong thing.** `ProtoMessage.cs` fills the
fief's `treasury` field with `GetAvailableTreasury(true)`, which is not the fief's gold, it's gold minus family expenses, minus overlord taxes, minus next season's planned
spending. Spending headroom, in other words. `AdjustExpenditure` never touches the
treasury; all it does is cut the plan to fit the purse, which raises headroom without a
coin moving. So every `[CHECK]` line on a FiefAutoAdjustExpenditure was a false alarm by
construction, and the old "-392000 to 0 in one action" was the deficit in the *plan* being
cut, not money appearing.

Real gold turns out to be recoverable from what the server already sends, so no proto
change was needed: `keyStatsNext[10]` is fief expenses plus family expenses and
`keyStatsNext[11]` is overlord taxes, so gold = treasury + keyStatsNext[10] +
keyStatsNext[11]. Added `actualTreasury` to RedProtoFief and `TotalGoldBefore/After` to the
action rows; `[CHECK]` and `MoneyCreationFlags` now weigh gold. Expect the flag to read 0
from here on, and treat any hit as real.

Left alone on purpose: the agent still budgets against headroom, which is the right number
for deciding what it can afford, and the economic sub-score still uses it, so scores stay
comparable with the earlier runs. Only the audit changed. The paper's caveat about
AutoAdjust inflating the economic score needs rewording though, headroom rising after a
spending cut is correct behaviour, not inflation.

**Running several agents at once.** `--user` / `--pass` pick the account and `--server`
picks the host, so each process can be its own player. Each account must map to its own
PlayerCharacter; two agents on one PC will fight over its days.

**SERVER, the season is now a barrier.** The clock is global and any client could roll it,
so with two agents the first one to run out of days dragged the other into the next season
mid-turn. `SeasonUpdate` now marks the caller ready and only advances once every connected
client is ready, then clears the flags. Clients pass back the last rollover count they saw
(`Globals_Game.seasonEpoch`), which is how one that's waiting notices the season it asked
for has happened, without it, whoever waits never learns it can carry on. Only clients
still holding a connection are counted, since `Server.Disconnect` nulls `conn` but leaves
the Client in the dictionary, and a player who quit must not stall the table. With one
agent connected this behaves exactly as it always did. The client waits up to five minutes
and says who it's waiting for.

## July 17 2026 (later), the heir could never have worked

**SERVER, `includeAll` never called `onIncludeAll`.** This is the real reason no run
ever appointed an heir, and it made every other heir fix moot. `ProtoMessage.cs`
`includeAll(Character c)` populates the character's own fields and stops;
`includeSpy` right below it ends with `onIncludeSpy(c)`, and `includeAll` was missing
the matching `onIncludeAll(c)`. That override is the only place a PlayerCharacter's
`myHeir`, `myNPCs`, `playerID` and `ownedFiefs` are filled in (the constructor only
does homeFief, ancestralHomeFief, outlawed and purse), and the only place an NPC's
`salary`, `employer`, `inEntourage` and `isHeir` are filled in (its constructor does
nothing at all). So all of those reached the client empty no matter what was actually
true in the game.

The client reads `myHeirId` off `myHeir`, so it was always null, `HasHeir` was always
false, and the FSM dutifully re-appointed the same son every season forever. The
2026-07-17 run appointed Char_7583, a living son, confirmed in birthdiag.log, at
seasons 5, 6 and 7, each returning Success, and `HeirEverAppointed` still came out
false. It also explains the old note about the server's `inEntourage` refresh being
unreliable: it was never sent. Same for NPC salary, which is why hire offers were
computed off a salary of 0.

Fixed by adding `onIncludeAll(c)` at the end of `includeAll`, and removing the trailing
`includeAll(character)` from `ProtoNPC.onIncludeAll`, that line would have turned
includeAll → onIncludeAll → includeAll into a stack overflow the moment the first fix
landed. `onIncludeSpy` doesn't call back into `includeSpy`, which is what gives the game
away that it was a mistake.

Checked the wire contracts before shipping, since these fields had never actually been
sent: server and client agree exactly on `ProtoCharacter` and `ProtoNPC`, and
`ProtoPlayerCharacter` differs only by a client-side `purse` that sorts last
alphabetically. Tags under `ProtoContract(ImplicitFields = ImplicitFields.AllPublic)`
are assigned in alphabetical order, so myHeir is tag 3 on both sides and nothing shifts.
Worth remembering: adding a public member to one side only renumbers every tag after it.

**One fief was wedging every bailiff appointment.** 30 of 255 appointments failed and
coverage froze at 109 of 162 fiefs. It was always the same fief, ENF01, refused five
times a season with whatever NPC was offered, and those same NPCs were accepted on
other fiefs in the same season, so it is the fief being refused, not the candidate.
`OtherFiefNeedingBailiff` always handed back the first uncovered fief, so ENF01 sat at
the head of the queue and everything behind it went uncovered for the rest of the run.
Refused fiefs now go into `bailiffBlockedFiefIDs` for the season and are skipped, and
there's a `[BAILIFF]` line logging the server's actual refusal reason.

Why ENF01 refuses is still open, `AppointBailiff` can only fail on the candidate check
(ruled out), fief ownership (`fief.owner == pc` while `pc.ownedFiefs` contains it, i.e.
inconsistent state), or male/age. Worth knowing that Char_47 is the king, so dead
vassals' fiefs escheat to him (`Character.cs` ~1802) and the realm grows from 52 to 162
fiefs over 14 seasons, and unlike `Fief.TransferOwnership`, that escheat path never
clears the dead owner's bailiff. Prime suspect. The next run's `[BAILIFF]` line will say.

## July 17 2026, marriage, succession, and why runs took hours

Long Base runs were slow, ended heirless, and never married anyone. Three separate
causes, all bugs rather than tuning.

**Foreign characters were ID-only stubs.** `ObtainCurrentGameState` was building
`notMyNPCs` as `new RedProtoNPC { charID = ... }` to save round-trips. Every
marriage and hiring rule in `AgentState.Build` tests `isMale`, `familyID`,
`birthYear`, `spouse`, all of which sat at their defaults on a stub, so the
filters matched nobody and `NPCmarryPC` / `NPCmarryChild` / `NPChire` could never
fire. That is the whole reason a widowed lord never remarried, never fathered
another child, and died with no heir. Details are fetched properly again, cached
per season in `foreignCharCache`. (This was written up as a known limitation back
in June; it was a bug.)

**ViewCaptives was the runtime.** The same method called `ViewCaptives` once per
owned fief, and the client refreshes state after every action. 196 fiefs x 1254
actions is about 245,000 round trips for one run. Captives only change on
kidnap/ransom/execute/release, so they're cached per season now and invalidated on
those four actions.

**One NPC was bailiff of nearly the whole realm.** `FindBailiffCandidate` ranked on
role fit, so once every employee held a post it kept returning the same best man, Char_4031 was appointed 194 times out of 196 fiefs. `ProcessDeath` strips a dead
character of all his posts at once, so his death wiped coverage everywhere and the
FSM then spent about 50 actions a season for five seasons putting it back. That's
the score dip and the troop drop at seasons 9-13 and 24-28 in `fsm_base.json`.
Candidates are now ordered by how many fiefs they already hold, fit only breaking
ties, so a death costs one man's share instead of the map.

**SERVER, unmanaged houses can answer proposals.** `ProposeMarriage` only files a
journal entry, and `AddPastEvent` delivers it only to a head of family with a client
attached. Every other house left proposals unanswered forever, which put marriage
(and remarriage, and the succession that depends on it) out of reach without a full
table of human players. Houses with nobody at the wheel now answer for themselves in
`Game.AutoAnswerIfUnmanaged`: consent is weighed on the standing of the suitor's
house against the bride's, 60% for an even match, +/-10 points per point of stature,
clamped to 10-90. Worth flagging in the write-up as an engine change made for
single-player evaluation.

**SERVER, activeProposals was never cleared.** Nothing removed the entry, not even
a successful marriage, so `ChecksBeforeProposal` turned down every later match with
`CharacterProposalAlready` and a character could propose exactly once per run.
Cleared in `ReplyToProposal` once the proposal is answered.

Smaller things from the same pass:

- `Player.AcceptRejectProposal` sent the PC's charID where the server does
  `Convert.ToUInt32` on the journal entry ID, and one message field where the server
  reads `MessageFields[1]`. Every reply failed to parse. Now sends the jEntryID,
  plumbed through the new `GameState.receivedProposalJEntryID`.
- `myCharsWhoProposedThisSeason` was copied forward from the previous GameState
  unconditionally, so "this season" really meant "this run". Now seeded from an
  agent-side set that `ResetSeasonState` clears.
- The marry handlers recorded the bride as having proposed instead of the groom, and
  used `Dictionary.Add`, which throws once the key exists.
- New `[DYNASTY]` line at each season end: wife, whether she can conceive and why not
  if she can't, and living male kin. The summary alone couldn't tell you which
  precondition failed.

`Agents/FSAMActionExecutor.cs` is dead code, nothing references it, the live path is
`Agent.ExecuteAction`. Left in place but don't bother fixing bugs in it.

## July 15-16 2026, war path, stillbirths, sticky failures

- SERVER: `Game.cs` dispatch read `MessageFields[1]` for `AppointLeader` and `Attack`,
  but every client puts the id at `[0]`. IndexOutOfRange, server down. Both now `[0]`.
  These only ever fired once the agent actually had an army.
- SERVER: guarded two null derefs in `Character.RecruitTroops` (`thisArmy?.armyID`)
  that crash when recruiting creates a new army.
- SERVER: `GiveBirth` built `string[5]` but only filled 0-3; the null at index 4 made
  protobuf-net throw inside `SeasonUpdate` and killed the server thread. Set to "".
  Added a scrub in `Server.cs SendViaProto` that replaces null elements right before
  serialising, since that's the single choke point for outgoing messages.
- SERVER: `ProcessScheduledEvents` iterated `jentries` live while births and deaths
  scheduled more, invalidating the enumerator. Iterates a snapshot now.
- SERVER: stillbirths were eating every son. `CheckForDeath` uses age-adjusted health,
  which for a newborn applies a x0.25 infant modifier, then birth risk multiplies by
  x1.5 on top, about 34% per son. Base the check on the baby's own maxHealth instead:
  `(10 - maxHealth) * (2.8 male / 2.5 female) * 1.5`, roughly 10%.
- SERVER: `GetSpousePregnant` set `isPregnant = true` before confirming the birth was
  actually scheduled, so a failed schedule left the wife pregnant forever. Moved inside
  the success block.
- SERVER: `GiveBirth` never reset the mother's `isPregnant`. Only the NPC constructor
  and `AbortPregnancy` did, so after one birth she stayed pregnant and every later
  TryForChild failed. A lord could father exactly one child.
- Army creation: recruiting with a null armyID makes the server create one. New
  `MilitaryContext.CanBuildArmy`; `Player.RecruitTroops` normalises "" to null. Before
  this the agents never had an army at all and the military sub-score was always 0.
  The 250k treasury gate meant a war party never formed inside a run; dropped to 50k.
- `--aggressive` flag on the FSM so war is reachable solo: build army, recruit to 50,
  make the PC the leader so the army travels with him, march on the cheapest reachable
  non-owned fief, besiege. Off by default.
- Sticky per-season failure block. `FailedActions` was wiped on every non-camp success,
  so an action that always fails with fixed args got re-issued after each unrelated
  success and burned the season. Added `seasonBlockedSignatures` (args-aware, cleared
  per season) and the `Blocked(a, args)` helper.
- Moved remarriage and child marriage above the war block in `HandleStable`. A widowed
  warlord marched every season and never remarried.
- Known from the war run: AppointLeader was refused 95/95 times. Added a diagnostic
  that logs the server's actual reason on failure.

## July 2026, evaluation logging, Q-learning consolidation, heir rules

- `EvaluationResult` carries per-action rows (season, index, state label, action, args,
  success, days and treasury before/after, threat, scenario) plus a summary block with
  acceptance rate, action distribution and `MoneyCreationFlags`.
- `ScenarioHarness.RunDryRunScenarios` takes `Agent` rather than `FSMAgent` and runs for
  both agents; forces epsilon 0 and no training so the oracle check is deterministic.
- Q-learning was camping everything: unseen states have all-zero Q, argmax returns index
  0, and camp is always applicable. Selection is action-masked to whatever
  `ResolveArguments` can fill, ties broken by a fixed priority (dynasty > economy >
  military > camp). Action space 10 -> 15 with the dynasty and leader actions.
  `Load` pads or trims saved rows so old tables don't crash, but retrain from scratch.
- Q reward scores total treasury rather than home, and `FiefTransferFunds` is masked
  unless home is under 5000. The "-600k to a billion" was reward hacking: transfers are
  zero-day and pump home treasury, which drove EconomicHealth.
- Heir candidate filter was requiring age >= 14, stricter than the server's
  `ChecksForHeir`, so a young son was skipped. Relaxed to male + same family, oldest
  first, matching `GetHeir`.
- SERVER: `ProtoJournalEntry` ran `.Where` on a null `MessageFields`, throwing on
  ViewJournalEntries for death and rebellion entries. Guarded.

## June 2026, FSM pipeline, AgentState, scenarios

- Network flooding: travel costs for every known fief were re-queried on every refresh.
  Cached on the PC's location.
- `FailedActions` was private, so the FSM never consulted it. Made protected; camping no
  longer clears failure memory.
- Stall detector overrode with NPCcamp while camp was also the chosen action, giving an
  infinite two-step cycle. Added the forced-camp escalation counter, returns None after 3.
- Camp: the server rejects it inside a keep, so exit first. And the ExitKeep fallback on
  camp failure had to go, since EnterExitKeep is a toggle and calling it outside the keep
  enters it, clears FailedActions, and loops forever.
- SERVER: protobuf-net calls the ProtoMessage constructor during deserialisation, which
  sets `MessageFields = ["None"]`, then appends the wire value, giving `["None", "5"]`.
  The camp handler read `[0]` and choked. Iterates past "None" now.
- Seasons: the server has `UseAutoUpdate = false`, so the update timer never fires and
  the client has to send `SeasonUpdate` itself. Camp only burns local days; income lands
  on the season update. `Agent.AdvanceSeason()` drives it. Agents camp the whole
  remaining season in one action rather than tripping the anti-stall.
- `WorldFacts` deleted in favour of `AgentState` with its four sub-contexts and
  `DerivedMetrics.ToObservationVector()` (fixed float[12]). `GameState` is the raw server
  layer, `AgentState` the semantic one; decision logic never touches GameState fields.
- `RoleFit`: bailiffs ranked on (stature + management)/2, army leaders on combat. The
  leader pick was the real gap, it was first male NPC at the army's fief. Required
  plumbing `stature` through `ReducedProtoMessage` and `RedDeepCopier`.
- Bailiffs have no co-location requirement server-side; the old location filter was
  starving most fiefs of candidates as the realm grew. Household cap raised 3 -> 12.
- Bailiff appointment removed from EconomicCrisis, it contradicts cost cutting.
- `--evaluate` dry-run suite (15 scenarios), `--simulate`, `--eval-log`, `--scenario`.
- Anti-stall compares action and arguments, not just action type, which was a false
  positive on legitimate multi-fief work.
- `[CHECK]` line when total treasury rises over 50k on a zero-day non-transfer action.
  `FiefAutoAdjustExpenditure` moves a deep negative straight to 0 because the server is
  zeroing planned overspend, not minting coin, but it flatters the economic sub-score.
  Treat single-season recovery with suspicion and read multi-season trends.

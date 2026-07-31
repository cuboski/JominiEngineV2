using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace JominiAI
{
    // Metrics recorded at the end of a season.
    public class SeasonSnapshot
    {
        public int Season { get; set; }
        public int Year { get; set; }
        public int HomeTreasury { get; set; }
        public int FiefsInDeficit { get; set; }
        public int FiefCount { get; set; }
        public int FiefsWithBailiff { get; set; }
        public uint ArmyTroops { get; set; }
        public int ThreatLevel { get; set; }
        public bool HeirAppointed { get; set; }
        public string HeirId { get; set; }         // who the heir is this season; a change from one season to the next means the heir was replaced (new son) or lost (heir died)
        // Wall-clock length of the season, end to end. This is the figure to compare against
        // how long a human takes over a turn; the per-action timings are only the thinking.
        public double SeasonSeconds { get; set; }
        public bool PCIsAlive { get; set; }
        public bool PCHasSpouse { get; set; }
        public int FamilyNPCCount { get; set; }
        public int LivingSonCount { get; set; }    // depth of succession: 0 here while HeirAppointed is true means the heir is the last of the line, so his death ends it
        public int ActionsExecuted { get; set; }
        public int ActionsFailed { get; set; }
        public float CompositeScore { get; set; }
    }

    // A single action taken during a run - what the agent decided, whether the server
    // accepted it, and the treasury/day movement it caused, so choices can be audited.
    // This is the row the validation run is graded on.
    public class ActionRecord
    {
        public int Season { get; set; }
        public int Index { get; set; }
        public string AgentState { get; set; }   // FSM state or Q descriptor at decision time
        public string Action { get; set; }
        public string Args { get; set; }
        public bool Success { get; set; }         // live: server accepted; dry-run: matched oracle
        // The server's DisplayMessages reason when it refused, or null. Without this a failing
        // run could only say THAT an action was refused, never why, which is what left the
        // army-leader fault undiagnosed for so long.
        public string Reason { get; set; }
        // A refusal that is a step in a negotiation rather than a rejected decision: the hire
        // haggle returns "too low"/"almost" until the bid is right. These are counted apart
        // from real failures so the acceptance rate measures decision quality, not bargaining.
        public bool Negotiation { get; set; }
        // Wall-clock time to choose AND execute this action, in milliseconds. This is the
        // per-action cost that decides whether an agent can keep up with a human player.
        public double ElapsedMs { get; set; }
        public double DaysBefore { get; set; }
        public double DaysAfter { get; set; }
        public int HomeTreasuryBefore { get; set; }
        public int HomeTreasuryAfter { get; set; }
        // Spending headroom summed across fiefs (the server's GetAvailableTreasury), which is
        // what the treasury field carries and what the agent budgets against.
        public double TotalTreasuryBefore { get; set; }
        public double TotalTreasuryAfter { get; set; }
        // Actual gold summed across fiefs. Only this can answer "was money created?" - headroom
        // rises whenever a spending plan is cut, with no coin involved.
        public double TotalGoldBefore { get; set; }
        public double TotalGoldAfter { get; set; }
        public int ThreatLevel { get; set; }
        public string Scenario { get; set; }      // dry-run rows only
    }

    // Aggregates computed once at save time, so the report doesn't have to re-derive
    // anything from the per-action rows.
    public class RunSummary
    {
        public int SeasonsRun { get; set; }
        public float AverageScore { get; set; }
        public float FinalScore { get; set; }
        public bool DynastyIntact { get; set; }
        public bool HeirEverAppointed { get; set; }
        public int TotalActions { get; set; }
        public int SuccessfulActions { get; set; }
        public float SuccessRate { get; set; }
        // Refusals that were negotiation steps (see ActionRecord.Negotiation), and the
        // acceptance rate with those excluded - the figure that reflects decision quality.
        public int NegotiationActions { get; set; }
        public float DecisionSuccessRate { get; set; }
        public int MoneyCreationFlags { get; set; }   // zero-day non-transfer actions that added >50k total
        // What it costs the agent to decide and issue one action, server round-trip included.
        public double MeanActionMs { get; set; }
        public double MedianActionMs { get; set; }
        public double P95ActionMs { get; set; }
        // Measured wall clock for a whole season, which is the figure to set against a human
        // player's turn. It is far larger than the action timings summed, because the state
        // refresh after each action, the pause between passes and the wait on the season
        // update all sit outside the decision itself.
        public double MeanSeasonSeconds { get; set; }
        // The decision-making share of that season: what the agent itself accounts for.
        public double MeanDecisionSecondsPerSeason { get; set; }
        // Why actions were refused, most common first. Turns a bare failure count into a
        // diagnosis without anyone having to read the raw rows.
        public Dictionary<string, int> FailureReasons { get; set; } = new();
        public Dictionary<string, int> ActionCounts { get; set; } = new();
    }

    // The full result of one evaluation run (all seasons).
    public class EvaluationResult
    {
        public string AgentType { get; set; }
        public string ScenarioName { get; set; }
        public DateTime RunTime { get; set; }
        public List<SeasonSnapshot> Seasons { get; set; } = new();
        public List<ActionRecord> Actions { get; set; } = new();
        public RunSummary Summary { get; set; }

        public float FinalScore => Seasons.Count > 0 ? Seasons[^1].CompositeScore : 0f;
        public float AverageScore => Seasons.Count > 0 ? Seasons.Average(s => s.CompositeScore) : 0f;
        public bool DynastyIntact => Seasons.Count > 0 && Seasons[^1].PCIsAlive;
        public bool HeirEverAppointed => Seasons.Any(s => s.HeirAppointed);
    }

    // Collapses a season snapshot into a single composite score.
    public static class ScoreComputer
    {
        public static float Compute(SeasonSnapshot snap)
        {
            // Economic health: 0 = bankrupt, 1 = 100k or more.
            float econScore = Math.Max(0f, Math.Min(1f, snap.HomeTreasury / 100_000f));

            float bailiffScore = snap.FiefCount > 0
                ? (float)snap.FiefsWithBailiff / snap.FiefCount : 0f;

            // Military: 0 = no troops, 1 = 500+ troops.
            float milScore = Math.Min(1f, snap.ArmyTroops / 500f);

            float threatPenalty = snap.ThreatLevel / 3f;

            float dynastyScore =
                (snap.HeirAppointed ? 0.4f : 0f)
                + (snap.PCIsAlive ? 0.4f : 0f)
                + (snap.PCHasSpouse ? 0.2f : 0f);

            return econScore * 0.30f
                 + bailiffScore * 0.15f
                 + milScore * 0.15f
                 + dynastyScore * 0.30f
                 - threatPenalty * 0.10f;
        }

        public static SeasonSnapshot FromAgentState(
            AgentState state, int season, int actionsExecuted, int actionsFailed)
        {
            var snap = new SeasonSnapshot
            {
                Season = season,
                Year = state.Year,
                HomeTreasury = state.Economy.HomeTreasury,
                FiefsInDeficit = state.Economy.FiefsInDeficit,
                FiefCount = state.Economy.FiefCount,
                FiefsWithBailiff = state.Economy.FiefsWithBailiff,
                ArmyTroops = state.Military.MainArmyTroops,
                ThreatLevel = (int)state.Military.Threat,
                HeirAppointed = state.Character.HasHeir,
                HeirId = state.Character.HeirId,
                PCIsAlive = state.Character.IsAlive,
                PCHasSpouse = state.Character.HasSpouse,
                FamilyNPCCount = state.Diplomacy.FamilyMemberCount,
                LivingSonCount = state.Diplomacy.LivingSonCount,
                ActionsExecuted = actionsExecuted,
                ActionsFailed = actionsFailed,
            };
            snap.CompositeScore = ScoreComputer.Compute(snap);
            return snap;
        }
    }

    // Records actions and seasons during a run and writes the JSON log. Call
    // RecordSeasonEnd once per completed season, then SaveResults.
    public class EvaluationRunner
    {
        private readonly EvaluationResult _result;
        private int _season = 0;
        private int _actionsThisSeason = 0;
        private int _failsThisSeason = 0;
        private readonly List<double> _seasonSeconds = new();

        public EvaluationRunner(string agentType, string scenarioName)
        {
            _result = new EvaluationResult
            {
                AgentType = agentType,
                ScenarioName = scenarioName,
                RunTime = DateTime.UtcNow,
            };
        }

        // 1-based season the next recorded action belongs to. A season is only counted
        // once RecordSeasonEnd runs, so during play this is _season + 1.
        public int CurrentSeason => _season + 1;

        public void RecordAction(ActionRecord rec)
        {
            _result.Actions.Add(rec);
            _actionsThisSeason++;
            if (!rec.Success) _failsThisSeason++;
        }

        public void RecordSeasonEnd(AgentState finalState, double seasonSeconds = 0)
        {
            _season++;
            var snap = ScoreComputer.FromAgentState(
                finalState, _season, _actionsThisSeason, _failsThisSeason);
            snap.SeasonSeconds = seasonSeconds;

            _seasonSeconds.Add(seasonSeconds);
            _result.Seasons.Add(snap);
            _actionsThisSeason = 0;
            _failsThisSeason = 0;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(
                $"  [EVAL] Season {_season} | score={snap.CompositeScore:F3} | " +
                $"treasury={snap.HomeTreasury} | heir={snap.HeirAppointed} | " +
                $"troops={snap.ArmyTroops} | threat={snap.ThreatLevel} | " +
                $"ok={snap.ActionsExecuted - snap.ActionsFailed}/{snap.ActionsExecuted}");
            Console.ResetColor();
        }

        private RunSummary BuildSummary()
        {
            int total = _result.Actions.Count;
            int ok = _result.Actions.Count(a => a.Success);

            int moneyFlags = _result.Actions.Count(a =>
                a.DaysBefore == a.DaysAfter
                && a.Action != nameof(MainActions.FiefTransferFunds)
                && (a.TotalGoldAfter - a.TotalGoldBefore) > 50_000);

            var counts = _result.Actions
                .GroupBy(a => a.Action)
                .OrderByDescending(g => g.Count())
                .ToDictionary(g => g.Key, g => g.Count());

            // Haggling rounds are refusals but not misjudgements, so they are held apart:
            // TotalActions/SuccessRate stay as they were for continuity, and the decision
            // rate below is the one that says whether the agent chose well.
            int negotiation = _result.Actions.Count(a => !a.Success && a.Negotiation);
            int decisions = total - negotiation;

            var reasons = _result.Actions
                .Where(a => !a.Success && !string.IsNullOrWhiteSpace(a.Reason))
                .GroupBy(a => a.Action + ": " + a.Reason)
                .OrderByDescending(g => g.Count())
                .ToDictionary(g => g.Key, g => g.Count());

            // Timing is only recorded on live runs, so ignore rows that carry no measurement.
            var times = _result.Actions.Where(a => a.ElapsedMs > 0)
                                       .Select(a => a.ElapsedMs)
                                       .OrderBy(ms => ms)
                                       .ToList();
            double Percentile(double p) =>
                times.Count == 0 ? 0
                : times[Math.Min(times.Count - 1, Math.Max(0, (int)Math.Ceiling(p * times.Count) - 1))];

            var measured = _seasonSeconds.Where(s => s > 0).ToList();

            return new RunSummary
            {
                SeasonsRun = _result.Seasons.Count,
                AverageScore = _result.AverageScore,
                FinalScore = _result.FinalScore,
                DynastyIntact = _result.DynastyIntact,
                HeirEverAppointed = _result.HeirEverAppointed,
                TotalActions = total,
                SuccessfulActions = ok,
                SuccessRate = total > 0 ? (float)ok / total : 0f,
                NegotiationActions = negotiation,
                DecisionSuccessRate = decisions > 0 ? (float)ok / decisions : 0f,
                MoneyCreationFlags = moneyFlags,
                MeanActionMs = times.Count > 0 ? times.Average() : 0,
                MedianActionMs = Percentile(0.50),
                P95ActionMs = Percentile(0.95),
                MeanSeasonSeconds = measured.Count > 0 ? measured.Average() : 0,
                MeanDecisionSecondsPerSeason = _result.Seasons.Count > 0
                    ? times.Sum() / 1000.0 / _result.Seasons.Count : 0,
                FailureReasons = reasons,
                ActionCounts = counts,
            };
        }

        public void PrintSummary()
        {
            var sum = BuildSummary();
            Console.WriteLine();
            Console.WriteLine($"  EVALUATION SUMMARY [{_result.AgentType} | {_result.ScenarioName}]");
            Console.WriteLine($"  Seasons run   : {sum.SeasonsRun}");
            Console.WriteLine($"  Avg score     : {sum.AverageScore:F3}");
            Console.WriteLine($"  Final score   : {sum.FinalScore:F3}");
            Console.WriteLine($"  Dynasty intact: {sum.DynastyIntact}");
            Console.WriteLine($"  Heir appointed: {sum.HeirEverAppointed}");
            Console.WriteLine($"  Actions        : {sum.SuccessfulActions}/{sum.TotalActions} accepted ({sum.SuccessRate:P0})");
            if (sum.NegotiationActions > 0)
                Console.WriteLine($"  Of which haggling: {sum.NegotiationActions} (decision rate {sum.DecisionSuccessRate:P0})");
            Console.WriteLine($"  Money-creation flags: {sum.MoneyCreationFlags}");
            if (sum.MeanActionMs > 0)
                Console.WriteLine($"  Action time   : mean {sum.MeanActionMs:F0} ms | median {sum.MedianActionMs:F0} ms | p95 {sum.P95ActionMs:F0} ms");
            if (sum.MeanSeasonSeconds > 0)
                Console.WriteLine($"  Season time   : {sum.MeanSeasonSeconds:F1} s wall clock, of which {sum.MeanDecisionSecondsPerSeason:F1} s deciding");

            if (sum.FailureReasons.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("  Why actions were refused:");
                foreach (var kv in sum.FailureReasons.Take(10))
                    Console.WriteLine($"    {kv.Key,-52} {kv.Value}");
            }

            if (sum.ActionCounts.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("  Action distribution:");
                foreach (var kv in sum.ActionCounts)
                    Console.WriteLine($"    {kv.Key,-28} {kv.Value}");
            }

            if (_result.Seasons.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("  Season | Score | Treasury   | Troops | Threat | Heir");
                Console.WriteLine("  -------|-------|------------|--------|--------|-----");
                foreach (var s in _result.Seasons)
                    Console.WriteLine(
                        $"  {s.Season,6} | {s.CompositeScore:F3} | {s.HomeTreasury,10} | {s.ArmyTroops,6} | {s.ThreatLevel,6} | {s.HeirAppointed}");
            }
            Console.WriteLine();
        }

        public void SaveResults(string path)
        {
            _result.Summary = BuildSummary();
            var json = JsonSerializer.Serialize(_result,
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
            Console.WriteLine($"  [EVAL] Results saved to '{path}'");
        }
    }

    // Offline dry-run harness: checks each agent picks the expected action per scenario,
    // no server needed.
    public static class ScenarioHarness
    {
        // Runs the fixed scenario suite against an agent, checks each chosen action against
        // the oracle, and optionally writes a JSON log. The oracle answer is fixed up front,
        // so this is the "did it take the correct action" validation both agents are graded on.
        public static void RunDryRunScenarios(Agent agent, string logPath = null)
        {
            bool isFsm = agent is FSMAgent;
            string label = isFsm ? "FSM" : "QLearn";

            Console.WriteLine();
            Console.WriteLine($"  {label} DRY-RUN SCENARIO EVALUATION (action-correctness)");
            Console.WriteLine();

            var scenarios = BuildScenarios();
            var runner = new EvaluationRunner(label, "DryRun");
            int idx = 0;

            // Validation judges the greedy policy, not a random exploration roll, so force
            // epsilon=0 / no training on the Q agent to keep the run deterministic.
            float savedEpsilon = 0f; bool savedTraining = false;
            if (agent is QLearningAgent q)
            {
                savedEpsilon = q.Epsilon; savedTraining = q.TrainingMode;
                q.Epsilon = 0f; q.TrainingMode = false;
            }

            foreach (var (name, state) in scenarios)
            {
                MainActions action;
                string[] args;
                string agentState;

                if (agent is FSMAgent fsm)
                {
                    action = fsm.DryRun(state, out args, out FSMState predicted);
                    agentState = predicted.ToString();
                }
                else
                {
                    action = agent.findNextAction(state, out args);
                    agentState = "Q";
                }

                string argStr = args?.Length > 0 ? $"({string.Join(", ", args)})" : "";
                bool correct = IsCorrectAction(name, action);

                runner.RecordAction(new ActionRecord
                {
                    Season = 0,
                    Index = ++idx,
                    Scenario = name,
                    AgentState = agentState,
                    Action = action.ToString(),
                    Args = string.Join(",", args ?? Array.Empty<string>()),
                    Success = correct,
                    ThreatLevel = (int)state.Military.Threat,
                });

                Console.WriteLine($"  [{(correct ? "PASS" : "FAIL")}] {name}");
                Console.WriteLine($"         state={agentState} | Action: {action}{argStr}");
                Console.WriteLine();
            }

            if (agent is QLearningAgent qr)
            {
                qr.Epsilon = savedEpsilon; qr.TrainingMode = savedTraining;
            }

            runner.PrintSummary();

            if (!string.IsNullOrEmpty(logPath))
            {
                string dir = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                runner.SaveResults(logPath);
            }
        }

        private static List<(string Name, AgentState State)> BuildScenarios()
        {
            return new List<(string, AgentState)>
            {
                // Stable / economy
                ("SCN-01 Stable, healthy treasury",
                 Scn(treasury: 50_000)),

                ("SCN-02 Stable, heir needed, should appoint heir",
                 Scn(treasury: 50_000, hasUnappointedHeir: true, heirCandID: "NPC_heir")),

                ("SCN-03 Stable, marriage opportunity, should propose",
                 Scn(treasury: 50_000, hasMarriageOpp: true, marriageCandID: "NPC_bride")),

                ("SCN-04 Stable, child marriage opportunity, should propose for child",
                 Scn(treasury: 50_000, hasChildMarriageOpp: true,
                     childID: "NPC_son", childCandID: "NPC_daughter")),

                ("SCN-05 Stable, received proposal, should accept",
                 Scn(treasury: 50_000, hasReceivedProposal: true)),

                ("SCN-06 Stable, army needs leader, should appoint",
                 Scn(treasury: 50_000, hasArmy: true, armyID: "A1",
                     armyTroops: 100, armyNeedsLeader: true, leaderCandID: "NPC_leader")),

                // Economic crisis
                ("SCN-07 Economic crisis, deficit fief, adjust expenditure",
                 Scn(treasury: -500, isCritical: true)),

                ("SCN-08 Economic crisis with surplus fief, transfer funds",
                 Scn(treasury: -500, isCritical: true, hasSurplus: true,
                     surplusID: "fief_rich", surplusAmt: 8_000)),

                ("SCN-09 Economic crisis, heir still needed, heir first",
                 Scn(treasury: -500, isCritical: true,
                     hasUnappointedHeir: true, heirCandID: "NPC_heir")),

                // Military / war
                ("SCN-10 Under threat, army needs maint, maintain army",
                 Scn(treasury: 15_000, threat: ThreatLevel.EnemyNearby,
                     hasArmy: true, armyID: "A1", armyTroops: 100, armyNeedsMaint: true)),

                ("SCN-11 Under threat, heir needed, heir before army ops",
                 Scn(treasury: 15_000, threat: ThreatLevel.EnemyPresent,
                     hasArmy: true, armyID: "A1", armyTroops: 100,
                     hasUnappointedHeir: true, heirCandID: "NPC_heir")),

                ("SCN-12 At war, active siege (low keep), storm",
                 Scn(treasury: 20_000, threat: ThreatLevel.ActiveCombat,
                     hasArmy: true, armyID: "A1", armyTroops: 200,
                     activeSiegeID: "Siege_1", keepLevel: 1.5f)),

                ("SCN-13 At war, active siege (high keep), reduction",
                 Scn(treasury: 20_000, threat: ThreatLevel.ActiveCombat,
                     hasArmy: true, armyID: "A1", armyTroops: 200,
                     activeSiegeID: "Siege_1", keepLevel: 4f)),

                ("SCN-14 At war, enemy army here, attack",
                 Scn(treasury: 20_000, threat: ThreatLevel.ActiveCombat,
                     hasArmy: true, armyID: "A1", armyTroops: 150,
                     enemyArmyID: "EnemyArmy_1")),

                ("SCN-15 At war, heir needed, appoint heir first",
                 Scn(treasury: 20_000, threat: ThreatLevel.ActiveCombat,
                     hasArmy: true, armyID: "A1", armyTroops: 150,
                     hasUnappointedHeir: true, heirCandID: "NPC_heir",
                     activeSiegeID: "Siege_2", keepLevel: 2f)),
            };
        }

        // Expected action per scenario. More specific patterns have to come before the
        // shorter substrings they contain (e.g. "heir first" before "propose").
        private static bool IsCorrectAction(string name, MainActions action) => name switch
        {
            var n when n.Contains("appoint heir") => action == MainActions.NPCappointHeir,
            var n when n.Contains("heir first") => action == MainActions.NPCappointHeir,
            var n when n.Contains("propose for child") => action == MainActions.NPCmarryChild,
            var n when n.Contains("propose") => action == MainActions.NPCmarryPC,
            var n when n.Contains("accept") => action == MainActions.NPCacceptRejectProposal,
            var n when n.Contains("appoint") && n.Contains("leader") => action == MainActions.ArmyAppointLeader,
            var n when n.Contains("transfer funds") => action == MainActions.FiefTransferFunds,
            var n when n.Contains("adjust expenditure") => action == MainActions.FiefAutoAdjustExpenditure,
            var n when n.Contains("maintain army") => action == MainActions.ArmyMaintain,
            var n when n.Contains("storm") => action == MainActions.SiegeStorm,
            var n when n.Contains("reduction") => action == MainActions.SiegeReduction,
            var n when n.Contains("attack") => action == MainActions.ArmyAttack,
            _ => true   // no oracle for this scenario, always pass
        };

        // Builds a synthetic AgentState for one dry-run scenario.
        private static AgentState Scn(
            int treasury = 50_000,
            bool isCritical = false,
            bool hasSurplus = false,
            string surplusID = null,
            int surplusAmt = 0,
            ThreatLevel threat = ThreatLevel.None,
            bool hasArmy = false,
            string armyID = null,
            uint armyTroops = 0,
            bool armyNeedsMaint = false,
            bool armyCanRecruit = false,
            bool armyNeedsLeader = false,
            string leaderCandID = null,
            string activeSiegeID = null,
            float keepLevel = 0f,
            string enemyArmyID = null,
            bool hasUnappointedHeir = false,
            string heirCandID = null,
            bool hasMarriageOpp = false,
            string marriageCandID = null,
            bool hasChildMarriageOpp = false,
            string childID = null,
            string childCandID = null,
            bool hasReceivedProposal = false,
            bool canTryForChild = false,
            int employeeCount = 0)
        {
            bool critical = isCritical || treasury < 1_000;
            bool recovered = treasury >= 10_000;

            var chr = new CharacterContext
            {
                CharID = "Sim_PC",
                CurrentFiefID = "sim_fief",
                HomeFiefID = "sim_home",
                DaysRemaining = 90,
                InKeep = false,
                IsAlive = true,
                HasSpouse = true,
                HasHeir = !hasUnappointedHeir,
                FamilyID = "Sim_Family",
            };
            var econ = new EconomicContext
            {
                HomeTreasury = treasury,
                TotalTreasury = treasury,
                FiefCount = 1,
                FiefsInDeficit = treasury < 0 ? 1 : 0,
                FiefsWithBailiff = 1,
                HomeIsInDeficit = treasury < 0,
                IsCritical = critical,
                HasRecovered = recovered,
                FiefsNeedingExpAdjustment = treasury < 0
                    ? new[] { "sim_home" } : Array.Empty<string>(),
                HasSurplusFief = hasSurplus,
                SurplusFiefID = surplusID,
                SurplusTransferAmount = surplusAmt,
            };
            var mil = new MilitaryContext
            {
                Threat = threat,
                HasArmy = hasArmy,
                MainArmyID = armyID,
                MainArmyTroops = armyTroops,
                ArmyNeedsMaint = armyNeedsMaint,
                ArmyCanRecruit = armyCanRecruit,
                ArmyNeedsLeader = armyNeedsLeader,
                ArmyLeaderCandidateID = leaderCandID,
                IsUnderSiege = threat == ThreatLevel.EnemyPresent,
                HasActiveSiege = activeSiegeID != null,
                ActiveSiegeID = activeSiegeID,
                BesiegedFiefID = activeSiegeID != null ? "sim_target" : null,
                BesiegedKeepLevel = keepLevel,
                EnemyArmyIDInFief = enemyArmyID,
                CanBesiegeCurrent = false,
                CanPillageCurrent = false,
            };
            var dip = new DiplomaticContext
            {
                HasUnappointedHeir = hasUnappointedHeir,
                HeirCandidateID = heirCandID,
                CanTryForChild = canTryForChild,
                HasMarriageOpportunity = hasMarriageOpp,
                MarriageCandidateID = marriageCandID,
                HasChildMarriageOpportunity = hasChildMarriageOpp,
                ChildForMarriageID = childID,
                ChildMarriageCandidateID = childCandID,
                HasReceivedProposal = hasReceivedProposal,
                EmployeeCount = employeeCount,
            };
            return new AgentState(chr, econ, mil, dip, 1194);
        }
    }
}

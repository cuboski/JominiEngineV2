using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace JominiAI
{
    // Tabular Q-learning agent.
    //
    // Each tick: build the AgentState (same as the FSM), discretise its observation
    // vector into a state key, pick an action epsilon-greedily from the Q-table, and
    // resolve the concrete arguments with the same deterministic helpers the FSM uses
    // - so the Q-agent only decides WHAT to do, not which fief or NPC to use. The
    // reward for the previous transition is computed on the following tick and fed
    // into a Bellman update.
    //
    // Run with StepByStep=false and TrainingMode=true to collect experience, and use
    // Save()/Load() to persist the table between runs.
    public class QLearningAgent : Agent
    {
        public float Alpha = 0.10f;         // learning rate
        public float Gamma = 0.95f;         // discount factor
        public float Epsilon = 0.20f;       // initial exploration rate
        public float EpsilonDecay = 0.995f; // multiplicative decay per episode
        public float EpsilonMin = 0.01f;    // floor on exploration

        // When true the agent trains (updates Q-values and decays epsilon). Set false
        // for a pure exploitation run.
        public bool TrainingMode = true;

        // The action types the agent chooses between. Arguments are resolved separately
        // from the AgentState, so the agent only learns which action type to use. The
        // dynasty and army-leader actions are here so it can score on the dynasty part
        // of the composite metric; without them the space had no heir/marriage action.
        public static readonly MainActions[] ActionSpace = new[]
        {
            MainActions.NPCcamp,                   // 0  advance time / burn days
            MainActions.FiefAutoAdjustExpenditure, // 1  fix a deficit fief
            MainActions.FiefTransferFunds,         // 2  pull gold from a surplus fief
            MainActions.ArmyMaintain,              // 3  pay army upkeep
            MainActions.ArmyRecruit,               // 4  recruit troops
            MainActions.NPCappointBaillif,         // 5  assign a bailiff
            MainActions.NPCfire,                   // 6  cut salary costs
            MainActions.NPChire,                   // 7  hire an NPC
            MainActions.SiegeStorm,                // 8  siege round (storm weak keeps, reduce strong)
            MainActions.ArmyAttack,                // 9  attack an enemy army
            MainActions.NPCappointHeir,            // 10 secure the succession
            MainActions.NPCacceptRejectProposal,   // 11 accept a marriage proposal
            MainActions.NPCmarryPC,                // 12 propose marriage for the PC
            MainActions.NPCmarryChild,             // 13 propose marriage for a family NPC
            MainActions.ArmyAppointLeader,         // 14 give an army a leader
            MainActions.NPCtryForChild,            // 15 father a child (a future heir)
        };
        public const int ActionCount = 16;

        // Tie-break prior over actions, used only when Q-values are equal (e.g. an
        // unvisited state where everything is still 0). Lower means preferred. Ordered
        // dynasty > economy > military > camp to mirror the FSM, so an untrained agent
        // still does something sensible rather than camping.
        private static int ActionPriority(MainActions a) => a switch
        {
            MainActions.NPCappointHeir => 0,
            MainActions.NPCacceptRejectProposal => 1,
            MainActions.NPCmarryPC => 2,
            MainActions.NPCmarryChild => 3,
            MainActions.NPCtryForChild => 4,
            MainActions.FiefTransferFunds => 5,
            MainActions.FiefAutoAdjustExpenditure => 6,
            MainActions.NPCfire => 7,
            MainActions.NPCappointBaillif => 8,
            MainActions.NPChire => 9,
            MainActions.ArmyAppointLeader => 10,
            MainActions.SiegeStorm => 11,
            MainActions.ArmyAttack => 12,
            MainActions.ArmyMaintain => 13,
            MainActions.ArmyRecruit => 14,
            MainActions.NPCcamp => 15,
            _ => 99,
        };

        // Key = discretised state string, value = Q-values (one float per action).
        private readonly Dictionary<string, float[]> _qTable = new();

        private string _prevKey = null;
        private int _prevActionIdx = -1;
        private AgentState _prevState = null;
        private bool _prevSuccess = true;

        public int EpisodeCount = 0;
        public int TotalSteps = 0;
        public float SessionReward = 0f;
        public int TableSize => _qTable.Count;

        private readonly Random _rng;

        public QLearningAgent(string username, string password, int? seed = null)
            : base(username, password)
        {
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public override MainActions findNextAction(AgentState state, out string[] arguments)
        {
            arguments = Array.Empty<string>();

            if (!state.Character.IsAlive || state.Character.IsCaptive) return MainActions.None;
            if (state.Character.DaysRemaining <= 0) return MainActions.None;

            float[] obs = state.Metrics.ToObservationVector();
            string key = Discretise(obs);

            // Update Q from the previous transition and log the reward.
            if (_prevKey != null && TrainingMode)
            {
                float reward = ComputeReward(_prevState, state, _prevSuccess);
                SessionReward += reward;
                float[] qBefore = GetQValues(_prevKey);
                float oldQ = qBefore[_prevActionIdx];
                BellmanUpdate(_prevKey, _prevActionIdx, reward, key);
                float newQ = GetQValues(_prevKey)[_prevActionIdx];

                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine(
                    $"  [Q] reward={reward:+0.000;-0.000}  " +
                    $"Q[{ActionSpace[_prevActionIdx]}]: {oldQ:+0.000;-0.000} -> {newQ:+0.000;-0.000}  " +
                    $"session={SessionReward:+0.000;-0.000}");
                Console.ResetColor();
            }

            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine(
                $"  [Q] state={key}  obs=[{string.Join(",", obs.Select(v => $"{v:F2}"))}]  " +
                $"states={_qTable.Count}  ε={Epsilon:F3}");
            Console.ResetColor();

            // Only actions with a valid target right now are eligible. Without this the
            // agent falls back to camp on unvisited states just because camp is index 0.
            float[] qVals = GetQValues(key);
            var applicable = new List<int>();
            for (int i = 0; i < ActionCount; i++)
                if (ResolveArguments(ActionSpace[i], state) != null)
                    applicable.Add(i);

            if (applicable.Count == 0)
            {
                _prevKey = null;
                return MainActions.None;
            }

            // Epsilon-greedy over the applicable set; the fixed prior breaks greedy ties
            // (including the all-zero unvisited state).
            bool exploring = _rng.NextDouble() < Epsilon && TrainingMode;
            int actionIdx = exploring
                ? applicable[_rng.Next(applicable.Count)]
                : applicable.OrderByDescending(i => qVals[i])
                            .ThenBy(i => ActionPriority(ActionSpace[i]))
                            .First();

            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write($"  [Q] Q-vals: ");
            for (int i = 0; i < ActionCount; i++)
                Console.Write($"{ActionSpace[i]}={qVals[i]:+0.00;-0.00}  ");
            Console.WriteLine($"  [{(exploring ? "EXPLORE" : "EXPLOIT")}: {ActionSpace[actionIdx]}]");
            Console.ResetColor();

            MainActions action = ActionSpace[actionIdx];
            arguments = ResolveArguments(action, state);

            // The single siege slot becomes storm or reduction depending on keep level,
            // matching the engine (storm weak keeps, reduce strong ones).
            if (action == MainActions.SiegeStorm && state.Military.BesiegedKeepLevel >= 3f)
                action = MainActions.SiegeReduction;

            _prevKey = key;
            _prevActionIdx = actionIdx;
            _prevState = state;
            _prevSuccess = true; // updated by OnActionResult if wired up
            TotalSteps++;

            return action;
        }

        // Reward for the previous transition. All sub-rewards are bounded so nothing runs away.
        private float ComputeReward(AgentState prev, AgentState curr, bool prevSuccess)
        {
            float r = 0f;

            // Reward genuine change in realm-wide wealth, not internal reshuffling.
            // FiefTransferFunds only moves gold between the player's own fiefs, so it
            // leaves total treasury unchanged and earns nothing here; scoring the home
            // treasury alone previously let the agent farm reward with that zero-day
            // action. Home solvency is still driven by the terminal reward.
            float econDelta = (curr.Economy.TotalTreasury - prev.Economy.TotalTreasury) / 100_000f;
            r += Clamp(econDelta, -1f, 1f);

            float bailiffDelta = curr.Metrics.BailiffCoverage - prev.Metrics.BailiffCoverage;
            r += Clamp(bailiffDelta * 5f, -0.5f, 0.5f);

            float threatDelta = curr.Metrics.ThreatScore - prev.Metrics.ThreatScore;
            r -= Clamp(threatDelta, 0f, 1f);

            float milDelta = curr.Metrics.MilitaryStrength - prev.Metrics.MilitaryStrength;
            r += Clamp(milDelta * 0.5f, -0.5f, 0.5f);

            // Heir and spouse feed the dynasty sub-score, so reward gaining them.
            if (curr.Character.HasHeir && !prev.Character.HasHeir) r += 0.5f;
            if (curr.Character.HasSpouse && !prev.Character.HasSpouse) r += 0.3f;

            if (!prevSuccess) r -= 0.15f; // failed action = wasted server round-trip

            return r;
        }

        // Terminal reward at the end of a season.
        public void OnSeasonEnd(AgentState finalState)
        {
            if (!TrainingMode || _prevKey == null) return;

            float terminalReward = (finalState.Economy.HasRecovered ? 1.0f
                : finalState.Economy.IsCritical ? -0.5f
                : 0f)
                + (finalState.Character.HasHeir ? 0.5f : 0f); // surviving with a secured succession

            // No next state at the terminal, so bootstrap with the current Q-value.
            BellmanUpdate(_prevKey, _prevActionIdx, terminalReward, null);
            SessionReward += terminalReward;

            Epsilon = Math.Max(EpsilonMin, Epsilon * EpsilonDecay);
            EpisodeCount++;
            Console.WriteLine($"  [Q] Season {EpisodeCount} complete | reward={SessionReward:+0.00;-0.00} | ε={Epsilon:F3} | states={_qTable.Count}");
            SessionReward = 0f;
            _prevKey = null;
        }

        // Turns an abstract action into concrete arguments from the AgentState, or null
        // when the action has no valid target right now.
        private string[] ResolveArguments(MainActions action, AgentState s)
        {
            var econ = s.Economy;
            var mil = s.Military;
            var dip = s.Diplomacy;
            var chr = s.Character;

            switch (action)
            {
                case MainActions.NPCcamp:
                    if (s.Character.InKeep) return null; // must exit the keep first
                    // Camp = pass the remaining time. Burn the whole season so the episode
                    // ends cleanly (nothing changes mid-season; the server won't auto-advance).
                    byte days = (byte)Math.Max(1, Math.Min((int)chr.DaysRemaining, 90));
                    return new[] { chr.CharID, days.ToString() };

                case MainActions.FiefAutoAdjustExpenditure:
                    if (econ.FiefsNeedingExpAdjustment.Count == 0) return null;
                    return new[] { econ.FiefsNeedingExpAdjustment[0] };

                case MainActions.FiefTransferFunds:
                    // Only pull surplus when the home fief is actually short of gold.
                    // The transfer conserves total treasury, so without this gate the
                    // agent can drain the whole realm into home one fief at a time and
                    // farm the (home-treasury based) economic reward with a zero-day action.
                    if (!econ.HasSurplusFief || econ.HomeTreasury >= 5_000) return null;
                    return new[] { econ.SurplusFiefID, chr.HomeFiefID, econ.SurplusTransferAmount.ToString() };

                case MainActions.ArmyMaintain:
                    if (!mil.ArmyNeedsMaint) return null;
                    return new[] { mil.MainArmyID };

                case MainActions.ArmyRecruit:
                    if (!mil.ArmyCanRecruit) return null;
                    return new[] { mil.MainArmyID, "25" };

                case MainActions.NPCappointBaillif:
                    if (econ.HomeNeedsBailiff && econ.HomeBailiffCandidate != null)
                        return new[] { chr.HomeFiefID, econ.HomeBailiffCandidate };
                    if (econ.OtherFiefNeedingBailiff != null)
                        return new[] { econ.OtherFiefNeedingBailiff, econ.OtherFiefBailiffCandidate };
                    return null;

                case MainActions.NPCfire:
                    if (!dip.HasFirableEmployee) return null;
                    return new[] { dip.FireableEmployeeID };

                case MainActions.NPChire:
                    if (!dip.HasHirableNPC || dip.EmployeeCount >= 3) return null;
                    return new[] { dip.HirableNPCID, dip.HireOfferAmount.ToString() };

                case MainActions.SiegeStorm:
                    // Storm/reduction/negotiation, chosen by keep level upstream.
                    if (mil.ActiveSiegeID == null) return null;
                    return new[] { mil.ActiveSiegeID };

                case MainActions.ArmyAttack:
                    if (mil.EnemyArmyIDInFief == null || !mil.HasArmy) return null;
                    return new[] { mil.MainArmyID, mil.EnemyArmyIDInFief };

                case MainActions.NPCappointHeir:
                    if (!dip.HasUnappointedHeir || dip.HeirCandidateID == null) return null;
                    return new[] { dip.HeirCandidateID };

                case MainActions.NPCacceptRejectProposal:
                    if (!dip.HasReceivedProposal) return null;
                    return new[] { "true" };

                case MainActions.NPCmarryPC:
                    if (!dip.HasMarriageOpportunity || dip.MarriageCandidateID == null) return null;
                    return new[] { dip.MarriageCandidateID };

                case MainActions.NPCmarryChild:
                    if (!dip.HasChildMarriageOpportunity
                        || dip.ChildForMarriageID == null || dip.ChildMarriageCandidateID == null) return null;
                    return new[] { dip.ChildForMarriageID, dip.ChildMarriageCandidateID };

                case MainActions.ArmyAppointLeader:
                    if (!mil.ArmyNeedsLeader || mil.ArmyLeaderCandidateID == null) return null;
                    return new[] { mil.MainArmyID, mil.ArmyLeaderCandidateID };

                case MainActions.NPCtryForChild:
                    // Only worth it while heirless; produces a son who becomes the heir.
                    if (!dip.CanTryForChild || chr.HasHeir || dip.HasUnappointedHeir) return null;
                    return Array.Empty<string>();

                default:
                    return null;
            }
        }

        private float[] GetQValues(string key)
        {
            if (!_qTable.TryGetValue(key, out float[] q))
            {
                q = new float[ActionCount];
                _qTable[key] = q;
            }
            return q;
        }

        private void BellmanUpdate(string stateKey, int actionIdx, float reward, string nextKey)
        {
            float[] q = GetQValues(stateKey);
            float maxNext = nextKey != null ? GetQValues(nextKey).Max() : 0f;
            q[actionIdx] += Alpha * (reward + Gamma * maxNext - q[actionIdx]);
        }

        // Packs the float[12] observation into a compact string key. The first six
        // (continuous) features go into three buckets each - 0=[0,0.33), 1=[0.33,0.67),
        // 2=[0.67,1] - and the last six (binary) are kept as-is, giving 3^6 * 2^6 states.
        private static string Discretise(float[] obs)
        {
            var sb = new System.Text.StringBuilder(12);
            for (int i = 0; i < obs.Length; i++)
            {
                if (i < 6)
                    sb.Append(obs[i] < 0.33f ? '0' : obs[i] < 0.67f ? '1' : '2');
                else
                    sb.Append(obs[i] > 0.5f ? '1' : '0');
            }
            return sb.ToString();
        }

        private static float Clamp(float v, float lo, float hi)
            => v < lo ? lo : v > hi ? hi : v;

        // Persist the Q-table and hyperparameters. Call after each season/run.
        public void Save(string path)
        {
            var data = new SaveData
            {
                Epsilon = Epsilon,
                EpisodeCount = EpisodeCount,
                TotalSteps = TotalSteps,
                QTable = _qTable.ToDictionary(kv => kv.Key, kv => kv.Value),
            };
            File.WriteAllText(path, JsonSerializer.Serialize(data,
                new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"  [Q] Saved {_qTable.Count} states to '{path}'");
        }

        // Load a saved Q-table. Alpha/gamma are left alone so they can be tuned between runs.
        public void Load(string path)
        {
            if (!File.Exists(path)) { Console.WriteLine($"  [Q] No save file at '{path}', starting fresh."); return; }
            var data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(path));
            Epsilon = data.Epsilon;
            EpisodeCount = data.EpisodeCount;
            TotalSteps = data.TotalSteps;
            _qTable.Clear();
            foreach (var kv in data.QTable)
            {
                // Tables saved before the action space grew have shorter rows; pad or trim
                // to the current ActionCount so index access stays in bounds.
                float[] row = kv.Value;
                if (row.Length != ActionCount)
                {
                    var resized = new float[ActionCount];
                    Array.Copy(row, resized, Math.Min(row.Length, ActionCount));
                    row = resized;
                }
                _qTable[kv.Key] = row;
            }
            Console.WriteLine($"  [Q] Loaded {_qTable.Count} states from '{path}' (episode {EpisodeCount})");
        }

        private class SaveData
        {
            public float Epsilon { get; set; }
            public int EpisodeCount { get; set; }
            public int TotalSteps { get; set; }
            public Dictionary<string, float[]> QTable { get; set; }
        }
    }
}

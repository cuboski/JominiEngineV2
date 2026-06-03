using ProtoMessageClient;
using System;
using System.Linq;
using System.Threading;

namespace JominiAI
{
    /// <summary>
    /// Entry point and action loop.
    ///
    /// Usage:
    ///   dotnet run                                   FSM agent (default)
    ///   dotnet run -- --agent qlearn                 Q-learning, training mode
    ///   dotnet run -- --agent qlearn --exploit       Q-learning, no exploration
    ///   dotnet run -- --agent qlearn --load q.json   Q-learning, load saved table
    ///   dotnet run -- --simulate                     FSM dry-run (no server writes)
    /// </summary>
    class Program
    {
        // StepByStep = true  → pause after every action (debugging / step-through)
        // StepByStep = false → autonomous, visible in Unity client
        private const bool StepByStep = false;
        private const int MaxActions = 50;
        private const int PauseMs = 1500; // 1.5 s between actions
        private const string DefaultQSavePath = "qtable.json";

        static void Main(string[] args)
        {
            bool simulateMode = args.Contains("--simulate");
            bool useQLearning = ArgValue(args, "--agent")?.ToLower() == "qlearn";
            bool exploitMode = args.Contains("--exploit");
            string loadPath = ArgValue(args, "--load");
            string savePath = ArgValue(args, "--save") ?? DefaultQSavePath;

            Banner(useQLearning ? "JominiAI - Q-Learning Agent" : "JominiAI - FSM Agent");

            Agent agent;
            try
            {
                if (useQLearning)
                {
                    var q = new QLearningAgent("acc1", "pass1");
                    if (loadPath != null) q.Load(loadPath);
                    if (exploitMode) { q.Epsilon = 0f; q.TrainingMode = false; }
                    agent = q;
                    Ok($"Connected. Q-Learning agent | ε={q.Epsilon:F2} | training={q.TrainingMode}");
                }
                else
                {
                    var fsm = new FSMAgent("acc1", "pass1");
                    agent = fsm;
                    Ok($"Connected. FSM agent | initial state: {fsm.CurrentState}");
                }
            }
            catch (Exception ex) { Error("Connect failed", ex); return; }

            GameState gs;
            try { gs = agent.ObtainCurrentGSandUpdateAgentLists(); }
            catch (Exception ex) { Error("Initial state fetch failed", ex); return; }

            AgentState initial = agent.BuildState(gs);
            PrintState("INITIAL STATE", initial, agent);
            Pause(StepByStep && !simulateMode);

            if (simulateMode)
            {
                if (agent is FSMAgent fsmSim) RunSimulationMode(fsmSim);
                else Console.WriteLine("  [INFO] Simulation mode is only available for the FSM agent.");
                return;
            }

            Section($"Running up to {MaxActions} action passes  [{AgentLabel(agent)}]");

            MainActions lastAction = MainActions.None;
            string[] lastArgs = Array.Empty<string>();
            int sameCnt = 0;
            bool seasonEnded = false;

            for (int i = 1; i <= MaxActions; i++)
            {
                if (gs.myPC == null || gs.myPC.days <= 0 || !gs.myPC.isAlive)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"\n  [INFO] Turn cycle complete. Days: {gs.myPC?.days ?? 0}, Year: {gs.currentYear}");
                    Console.ResetColor();
                    seasonEnded = true;
                    break;
                }

                AgentState state = agent.BuildState(gs);
                PrintFacts(state, AgentLabel(agent));

                double daysBefore = gs.myPC.days;
                double treasuryBefore = gs.getHomeFief()?.treasury ?? 0;

                GameState gsAfter;
                MainActions executed;
                string[] executedArgs;
                bool success, endOfSeason;

                try
                {
                    gsAfter = agent.playOneAction(
                        gs, out executed, out executedArgs, out success, out endOfSeason);
                }
                catch (Exception ex) { Error("Execution error", ex); break; }

                string ok = success ? "ok" : "fail";
                string eos = endOfSeason ? "  [end of season]" : "";
                string aStr = executedArgs?.Length > 0 ? $"({string.Join(", ", executedArgs)})" : "";

                Console.WriteLine($"  [{i:D2}] {ok} {AgentLabel(agent),-18} {executed}{aStr}{eos}");

                // Anti-stall: identical action+args 4× in a row
                if (executed == lastAction && ArgsEqual(executedArgs, lastArgs))
                {
                    if (++sameCnt >= 4)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.WriteLine($"  [ANTI-STALL] '{executed}' repeated 4× with identical args.");
                        Console.ResetColor();
                        seasonEnded = true;
                        break;
                    }
                }
                else
                {
                    sameCnt = 0;
                    lastAction = executed;
                    lastArgs = executedArgs ?? Array.Empty<string>();
                }

                if (endOfSeason) { seasonEnded = true; break; }

                Thread.Sleep(PauseMs);

                try { gsAfter = agent.ObtainCurrentGSandUpdateAgentLists(gsAfter); }
                catch (Exception ex) { Error("State refresh failed", ex); break; }

                PrintDelta(daysBefore, treasuryBefore, gsAfter);

                gs = gsAfter;
                Pause(StepByStep);
            }

            if (agent is QLearningAgent qEnd)
            {
                qEnd.OnSeasonEnd(agent.BuildState(gs));
                qEnd.Save(savePath);
            }

            Console.WriteLine();
            PrintState("FINAL STATE", agent.BuildState(gs), agent);
            Console.WriteLine();
        }

        private static void RunSimulationMode(FSMAgent agent)
        {
            Banner("FSM SCENARIO SIMULATION (dry-run, no server execution)");

            var scenarios = new (string Label, AgentState State)[]
            {
                ("1.  Stable, healthy economy, no threats",
                 Scenario(treasury: 50_000)),
                ("2.  Stable, healthy, bailiff MISSING",
                 Scenario(treasury: 50_000, homeNeedsBailiff: true)),
                ("3.  Economic Crisis, critical treasury",
                 Scenario(treasury: -500, isCritical: true)),
                ("4.  Economic Crisis, surplus fief available",
                 Scenario(treasury: -500, isCritical: true, hasSurplus: true,
                          surplusID: "sim_rich", surplusAmt: 8_000)),
                ("5.  Under Threat, enemy nearby",
                 Scenario(treasury: 15_000, threat: ThreatLevel.EnemyNearby)),
                ("6.  Under Threat, enemy present + I have army",
                 Scenario(treasury: 15_000, threat: ThreatLevel.EnemyPresent,
                          hasArmy: true, armyTroops: 100, armyID: "A1")),
                ("7.  At War, active siege (keep level 4 → reduction)",
                 Scenario(treasury: 20_000, threat: ThreatLevel.ActiveCombat,
                          hasArmy: true, armyTroops: 200, armyID: "A1",
                          activeSiegeID: "Siege_1", keepLevel: 4f)),
                ("8.  At War, enemy army here → attack",
                 Scenario(treasury: 20_000, threat: ThreatLevel.ActiveCombat,
                          hasArmy: true, armyTroops: 150, armyID: "A1",
                          enemyArmyID: "EnemyArmy_1")),
                ("9.  Army needs maintenance",
                 Scenario(treasury: 30_000, hasArmy: true, armyTroops: 100,
                          armyID: "A1", armyNeedsMaint: true)),
                ("10. Stable, heir needed",
                 Scenario(treasury: 50_000, hasUnappointedHeir: true, heirCandID: "NPC_Heir")),
            };

            foreach (var (label, state) in scenarios)
            {
                MainActions action = agent.DryRun(state, out string[] chosenArgs, out FSMState predicted);
                string argStr = chosenArgs?.Length > 0 ? $"({string.Join(", ", chosenArgs)})" : "";

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"  {label}");
                Console.ResetColor();
                Console.WriteLine($"    FSM State : {predicted}");
                Console.WriteLine($"    Action    : {action}{argStr}");
                Console.WriteLine($"    Obs vector: [{string.Join(", ", state.Metrics.ToObservationVector().Select(v => $"{v:F2}"))}]");
                Console.WriteLine();
            }
            Banner("SIMULATION COMPLETE");
        }

        private static AgentState Scenario(
            int treasury = 50_000,
            bool isCritical = false,
            bool hasRecovered = false,
            bool homeNeedsBailiff = false,
            bool hasSurplus = false,
            string surplusID = null,
            int surplusAmt = 0,
            ThreatLevel threat = ThreatLevel.None,
            bool hasArmy = false,
            string armyID = null,
            uint armyTroops = 0,
            bool armyNeedsMaint = false,
            bool armyCanRecruit = false,
            string activeSiegeID = null,
            float keepLevel = 0f,
            string enemyArmyID = null,
            bool hasUnappointedHeir = false,
            string heirCandID = null,
            bool canTryForChild = false,
            int employeeCount = 0)
        {
            bool critical = isCritical || treasury < 1_000;
            bool recovered = hasRecovered || treasury >= 10_000;

            var chr = new CharacterContext
            {
                CharID = "Char_Sim", CurrentFiefID = "sim_fief", HomeFiefID = "sim_home",
                DaysRemaining = 90, InKeep = false, IsAlive = true,
            };
            var econ = new EconomicContext
            {
                HomeTreasury = treasury,
                TotalTreasury = treasury,
                FiefCount = 1,
                FiefsInDeficit = treasury < 0 ? 1 : 0,
                FiefsWithBailiff = homeNeedsBailiff ? 0 : 1,
                HomeIsInDeficit = treasury < 0,
                IsCritical = critical,
                HasRecovered = recovered,
                FiefsNeedingExpAdjustment = treasury < 0 ? new[] { "sim_home" } : Array.Empty<string>(),
                HomeNeedsBailiff = homeNeedsBailiff,
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
                IsUnderSiege = threat == ThreatLevel.EnemyPresent,
                HasActiveSiege = activeSiegeID != null,
                ActiveSiegeID = activeSiegeID,
                BesiegedFiefID = activeSiegeID != null ? "sim_target" : null,
                BesiegedKeepLevel = keepLevel,
                EnemyArmyIDInFief = enemyArmyID,
            };
            var dip = new DiplomaticContext
            {
                HasUnappointedHeir = hasUnappointedHeir,
                HeirCandidateID = heirCandID,
                CanTryForChild = canTryForChild,
                EmployeeCount = employeeCount,
            };
            return new AgentState(chr, econ, mil, dip, 1194);
        }

        private static string AgentLabel(Agent agent)
        {
            if (agent is FSMAgent fsm) return fsm.CurrentState.ToString();
            if (agent is QLearningAgent ql) return $"Q[ε={ql.Epsilon:F3} ep={ql.EpisodeCount}]";
            return "Agent";
        }

        private static void PrintFacts(AgentState s, string label)
        {
            var econ = s.Economy; var mil = s.Military; var dip = s.Diplomacy; var chr = s.Character;

            string army = mil.HasArmy ? $"{mil.MainArmyTroops} troops" : "none";
            string bailiff = econ.HomeNeedsBailiff ? "MISSING" : "assigned";
            string threat = mil.Threat switch
            {
                ThreatLevel.ActiveCombat => "AT WAR",
                ThreatLevel.EnemyPresent => "ENEMY PRESENT",
                ThreatLevel.EnemyNearby => "ENEMY NEARBY",
                _ => "clear"
            };
            string econStr = econ.IsCritical ? "CRITICAL" : econ.HomeIsInDeficit ? "NEGATIVE" : "stable";
            string keep = chr.InKeep ? " | IN KEEP" : "";
            string surplus = econ.HasSurplusFief
                ? $" | surplus: {econ.SurplusFiefID} ({econ.SurplusTransferAmount}g)" : "";
            string heir = dip.HasUnappointedHeir ? " | HEIR NEEDED" : "";
            string cover = $"{econ.FiefsWithBailiff}/{econ.FiefCount} bailiffs";

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n  [{label}] Treasury: {econ.HomeTreasury} ({econStr}) | Army: {army} | Bailiff: {bailiff} ({cover}) | Threat: {threat}{keep}{surplus}{heir}");
            Console.ResetColor();
        }

        private static void PrintState(string label, AgentState s, Agent agent)
        {
            Banner(label);
            Console.WriteLine($"  Character : {s.Character.CharID} | Days: {s.Character.DaysRemaining:0.#} | Year: {s.Year}");
            Console.WriteLine($"  Agent     : {AgentLabel(agent)}");
            Console.WriteLine($"  Economy   : home={s.Economy.HomeTreasury} | total={s.Economy.TotalTreasury} | deficit={s.Economy.FiefsInDeficit}/{s.Economy.FiefCount} fiefs | bailiff={s.Economy.FiefsWithBailiff}/{s.Economy.FiefCount}");
            Console.WriteLine($"  Military  : {s.Military.Threat} | army={s.Military.MainArmyTroops} troops");
            Console.WriteLine($"  Obs vec   : [{string.Join(", ", s.Metrics.ToObservationVector().Select(v => $"{v:F2}"))}]");
        }

        private static void PrintDelta(double daysBefore, double treasuryBefore, GameState gsAfter)
        {
            if (gsAfter.myPC == null) return;
            double daysUsed = daysBefore - gsAfter.myPC.days;
            double treasuryAfter = gsAfter.getHomeFief()?.treasury ?? 0;
            double treasuryDelta = treasuryAfter - treasuryBefore;

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(daysUsed != 0
                ? $"  Days used: {daysUsed:0.#} (now {gsAfter.myPC.days:0.#})"
                : $"  Days: unchanged ({gsAfter.myPC.days:0.#})");
            Console.Write(treasuryDelta != 0
                ? $"  | Treasury delta: {treasuryDelta:+0;-0;0} (now {treasuryAfter:0})"
                : $"  | Treasury: unchanged ({treasuryAfter:0})");
            Console.WriteLine($"  | Year: {gsAfter.currentYear}");
            Console.ResetColor();
        }

        private static string ArgValue(string[] args, string flag)
        {
            int idx = Array.IndexOf(args, flag);
            return (idx >= 0 && idx + 1 < args.Length) ? args[idx + 1] : null;
        }

        private static void Banner(string text)
        {
            Console.WriteLine();
            Console.WriteLine($"  {text}");
            Console.WriteLine();
        }

        private static void Section(string text) => Console.WriteLine($"\n  {text}");

        private static void Ok(string msg)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  {msg}");
            Console.ResetColor();
        }

        private static void Error(string msg, Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  [ERR] {msg}: {ex.Message}");
            Console.ResetColor();
        }

        private static void Pause(bool active)
        {
            if (!active) return;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("  press any key...");
            Console.ResetColor();
            Console.ReadKey(intercept: true);
            Console.WriteLine();
        }

        private static bool ArgsEqual(string[] a, string[] b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        public static ProtoMessage GetActionReply(Actions action, TextTestClient client)
        {
            bool gotAction = false, gotUpdate = (action == Actions.LogIn);
            ProtoMessage reply = null;
            do
            {
                ProtoMessage m = client.CheckForProtobufMessage();
                if (m == null) { client.LogOut(); return null; }
                if (m.ActionType == action)
                { reply = m; gotAction = gotUpdate = true; }
                else if (m.ActionType == Actions.Update)
                {
                    if (m.ResponseType == DisplayMessages.ErrorGenericMessageInvalid)
                    { reply = m; gotAction = true; }
                    else if (m.ResponseType == DisplayMessages.Success)
                        gotUpdate = true;
                }
            }
            while (!gotAction || !gotUpdate);
            return reply;
        }
    }
}

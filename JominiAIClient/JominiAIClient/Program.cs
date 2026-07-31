using ProtoMessageClient;
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace JominiAI
{
    // Entry point and action loop. Flags:
    //   dotnet run                                 -> FSM agent (default)
    //   dotnet run -- --agent qlearn               -> Q-learning, training mode
    //   dotnet run -- --agent qlearn --exploit     -> Q-learning, no exploration
    //   dotnet run -- --agent qlearn --load q.json -> Q-learning, load saved table
    //   dotnet run -- --seasons 20                 -> run 20 seasons (default 5)
    //   dotnet run -- --simulate                   -> FSM dry-run (no server writes)
    //   dotnet run -- --aggressive                 -> FSM raises an army and starts a war
    class Program
    {
        // StepByStep = true  pauses after every action (debugging / step-through)
        // StepByStep = false runs autonomously, visible in Unity client
        private const bool StepByStep = false;
        private const int MaxActions = 50;
        private const int DefaultMaxSeasons = 5;
        private const int PauseMs = 100; // delay between actions (was 1500; lowered for faster data runs)
        private const string DefaultQSavePath = "qtable.json";

        static void Main(string[] args)
        {
            bool simulateMode = args.Contains("--simulate");
            bool evaluateMode = args.Contains("--evaluate");  // offline dry-run scenario suite
            bool useQLearning = ArgValue(args, "--agent")?.ToLower() == "qlearn";
            bool exploitMode = args.Contains("--exploit");
            bool aggressive = args.Contains("--aggressive");  // FSM marches on a neighbour to exercise war
            string loadPath = ArgValue(args, "--load");
            string savePath = ArgValue(args, "--save") ?? DefaultQSavePath;
            string evalLogPath = ArgValue(args, "--eval-log");
            string scenarioLabel = ArgValue(args, "--scenario") ?? "Base";
            int maxSeasons = int.TryParse(ArgValue(args, "--seasons"), out int s) && s > 0
                ? s : DefaultMaxSeasons;

            // Account to play. Each process must use its own, and each account must map to its
            // own PlayerCharacter - point two agents at the same PC and they fight over its days.
            string user = ArgValue(args, "--user") ?? "acc1";
            string pass = ArgValue(args, "--pass") ?? "pass1";
            string server = ArgValue(args, "--server");
            if (server != null) Player.ServerAddress = server;

            Banner(useQLearning ? "JominiAI - Q-Learning Agent" : "JominiAI - FSM Agent");

            Agent agent;
            try
            {
                if (useQLearning)
                {
                    var q = new QLearningAgent(user, pass);
                    if (loadPath != null) q.Load(loadPath);
                    if (exploitMode) { q.Epsilon = 0f; q.TrainingMode = false; }
                    agent = q;
                    Ok($"Connected. Q-Learning agent | ε={q.Epsilon:F2} | training={q.TrainingMode}");
                }
                else
                {
                    var fsm = new FSMAgent(user, pass) { Aggressive = aggressive };
                    agent = fsm;
                    Ok($"Connected. FSM agent | initial state: {fsm.CurrentState}{(aggressive ? " | AGGRESSIVE" : "")}");
                }
            }
            catch (Exception ex) { Error("Connect failed", ex); return; }

            GameState gs;
            try { gs = agent.ObtainCurrentGSandUpdateAgentLists(); }
            catch (Exception ex) { Error("Initial state fetch failed", ex); return; }

            AgentState initial = agent.BuildState(gs);
            PrintState("INITIAL STATE", initial, agent);

            // A freshly started server world begins at year 1194. If it is already past
            // that, the server has been advanced by earlier runs (reconnecting does NOT
            // reset the world) - the dynasty may be old or extinct, which makes a run end
            // early with "Days did not refresh". Warn so the cause is obvious.
            if (gs.myPC == null || gs.currentYear > 1194)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  [WARN] Server world is not fresh (year {gs.currentYear}"
                    + (gs.myPC == null ? ", no living PC" : "") + "). Results may be truncated; "
                    + "restart the server for a clean run.");
                Console.ResetColor();
            }

            // Evaluation runner, active when --eval-log is passed
            EvaluationRunner evalRunner = evalLogPath != null
                ? new EvaluationRunner(useQLearning ? "QLearn" : "FSM", scenarioLabel)
                : null;

            Pause(StepByStep && !simulateMode);

            if (simulateMode)
            {
                if (agent is FSMAgent fsmSim) RunSimulationMode(fsmSim);
                else Console.WriteLine("  [INFO] Simulation mode is only available for the FSM agent.");
                return;
            }

            if (evaluateMode)
            {
                // Offline action-correctness check. Works for both agents; the log
                // path (if given) receives the per-scenario decisions for validation.
                ScenarioHarness.RunDryRunScenarios(agent, evalLogPath);
                return;
            }

            AgentState finalState = null;

            for (int season = 1; season <= maxSeasons; season++)
            {
                Section($"Season {season}/{maxSeasons}: running up to {MaxActions} action passes  [{AgentLabel(agent)}]");

                // Wall clock for the whole season. The per-action timer below covers only the
                // decision and its request; the state refresh after it, the pause between
                // passes and the wait on the season update all sit outside that, and together
                // they dominate. Summing action times therefore understates a season by a wide
                // margin, so the season is measured directly.
                var seasonTimer = System.Diagnostics.Stopwatch.StartNew();

                MainActions lastAction = MainActions.None;
                string[] lastArgs = Array.Empty<string>();
                int sameCnt = 0;

                // Total times each (action, args) signature has run this season. Catches
                // two zero-cost actions oscillating (A,B,A,B), which the adjacent-repeat
                // check below never trips. NPCcamp is exempt (legitimate time-passing).
                var seasonSignatureCounts = new System.Collections.Generic.Dictionary<string, int>();
                const int MaxSameSignaturePerSeason = 5;

                for (int i = 1; i <= MaxActions; i++)
                {
                    // < 1: a PC with less than a full day cannot take any day-costed action.
                    // Remaining 0-cost actions loop forever (e.g. the entourage toggle), so treat
                    // a sub-1-day PC as end-of-season instead of churning until MaxActions.
                    if (gs.myPC == null || gs.myPC.days < 1 || !gs.myPC.isAlive)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"\n  [INFO] Turn cycle complete. Days: {gs.myPC?.days ?? 0:0.#}, Year: {gs.currentYear}");
                        Console.ResetColor();
                        break;
                    }

                    AgentState state = agent.BuildState(gs);
                    PrintFacts(state, AgentLabel(agent));

                    double daysBefore = gs.myPC.days;
                    double treasuryBefore = gs.getHomeFief()?.treasury ?? 0;
                    double totalTreasuryBefore = gs.myFiefs?.Sum(f => (double)f.treasury) ?? 0;
                    // Real gold, for the money-creation audit only. The treasury figures above
                    // are spending headroom and legitimately move when spending plans change.
                    double totalGoldBefore = gs.myFiefs?.Sum(f => (double)f.actualTreasury) ?? 0;

                    GameState gsAfter;
                    MainActions executed;
                    string[] executedArgs;
                    bool success, endOfSeason;

                    // Times the whole decide-and-execute cycle, which is what has to fit inside
                    // the pace a human player sets. It includes the server round-trip, since
                    // that cost is real for anyone playing the game.
                    var actionTimer = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        gsAfter = agent.playOneAction(
                            gs, out executed, out executedArgs, out success, out endOfSeason);
                    }
                    catch (Exception ex) { Error("Execution error", ex); break; }
                    actionTimer.Stop();

                    string ok = success ? "ok" : "fail";
                    string eos = endOfSeason ? "  [end of season]" : "";
                    string aStr = executedArgs?.Length > 0 ? $"({string.Join(", ", executedArgs)})" : "";

                    Console.WriteLine($"  [{i:D2}] {ok} {AgentLabel(agent),-18} {executed}{aStr}{eos}");

                    // Build the validation row now (before-values known). After-values are
                    // filled once the state refreshes below; on end-of-season there is no
                    // refresh, so after == before.
                    ActionRecord rec = evalRunner != null ? new ActionRecord
                    {
                        Season = season,
                        Index = i,
                        AgentState = AgentLabel(agent),
                        Action = executed.ToString(),
                        Args = string.Join(",", executedArgs ?? Array.Empty<string>()),
                        Success = success,
                        Reason = success ? null : agent.LastActionReason,
                        Negotiation = !success && agent.LastActionWasNegotiation,
                        ElapsedMs = actionTimer.Elapsed.TotalMilliseconds,
                        DaysBefore = daysBefore,
                        DaysAfter = daysBefore,
                        HomeTreasuryBefore = (int)treasuryBefore,
                        HomeTreasuryAfter = (int)treasuryBefore,
                        TotalTreasuryBefore = totalTreasuryBefore,
                        TotalTreasuryAfter = totalTreasuryBefore,
                        TotalGoldBefore = totalGoldBefore,
                        TotalGoldAfter = totalGoldBefore,
                        ThreatLevel = (int)state.Military.Threat,
                    } : null;

                    if (executed != MainActions.NPCcamp && executed != MainActions.None)
                    {
                        string sig = executed + ":" + string.Join(",", executedArgs ?? Array.Empty<string>());
                        seasonSignatureCounts.TryGetValue(sig, out int cnt);
                        seasonSignatureCounts[sig] = ++cnt;
                        if (cnt >= MaxSameSignaturePerSeason)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            Console.WriteLine($"  [ANTI-STALL] '{executed}' ran {cnt}x this season with the same args; ending season.");
                            Console.ResetColor();
                            if (rec != null) evalRunner.RecordAction(rec);
                            break;
                        }
                    }

                    // Anti-stall: identical action+args 4× in a row.
                    // NPCcamp is exempt: repeated camping is legitimate time-passing and is
                    // bounded by days reaching 0 (the loop guard above), not by a repeat count.
                    if (executed != MainActions.NPCcamp && executed == lastAction && ArgsEqual(executedArgs, lastArgs))
                    {
                        if (++sameCnt >= 4)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            Console.WriteLine($"  [ANTI-STALL] '{executed}' repeated 4× with identical args.");
                            Console.ResetColor();
                            if (rec != null) evalRunner.RecordAction(rec);
                            break;
                        }
                    }
                    else
                    {
                        sameCnt = 0;
                        lastAction = executed;
                        lastArgs = executedArgs ?? Array.Empty<string>();
                    }

                    if (endOfSeason) { if (rec != null) evalRunner.RecordAction(rec); break; }

                    Thread.Sleep(PauseMs);

                    try { gsAfter = agent.ObtainCurrentGSandUpdateAgentLists(gsAfter); }
                    catch (Exception ex) { if (rec != null) evalRunner.RecordAction(rec); Error("State refresh failed", ex); break; }

                    PrintDelta(daysBefore, treasuryBefore, totalTreasuryBefore, gsAfter);

                    // Money sanity check: time did not advance and this was not a transfer, yet
                    // the realm's gold jumped. Transfers move gold between fiefs (net zero), so
                    // a large positive net jump here would mean gold was minted.
                    //
                    // This has to weigh real gold, not the treasury field. The server puts
                    // GetAvailableTreasury(true) in that field - gold minus family expenses,
                    // overlord taxes and next season's planned spend - so cutting the spending
                    // plan raises it without a coin moving. That is exactly what
                    // FiefAutoAdjustExpenditure does, which is why every one of these warnings
                    // so far has been a false alarm.
                    double daysUsedNow = daysBefore - (gsAfter.myPC?.days ?? daysBefore);
                    double totalAfterNow = gsAfter.myFiefs?.Sum(f => (double)f.treasury) ?? totalTreasuryBefore;
                    double totalGoldAfter = gsAfter.myFiefs?.Sum(f => (double)f.actualTreasury) ?? totalGoldBefore;
                    double netNow = totalGoldAfter - totalGoldBefore;
                    if (daysUsedNow == 0 && executed != MainActions.FiefTransferFunds && netNow > 50_000)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"  [CHECK] Realm gold rose {netNow:+0} on a zero-day '{executed}'. Verify no gold was created.");
                        Console.ResetColor();
                    }

                    if (rec != null)
                    {
                        rec.DaysAfter          = gsAfter.myPC?.days ?? daysBefore;
                        rec.HomeTreasuryAfter  = (int)(gsAfter.getHomeFief()?.treasury ?? treasuryBefore);
                        rec.TotalTreasuryAfter = totalAfterNow;
                        rec.TotalGoldAfter     = totalGoldAfter;
                        evalRunner.RecordAction(rec);
                    }

                    gs = gsAfter;
                    Pause(StepByStep);
                }

                finalState = agent.BuildState(gs);

                if (agent is QLearningAgent qSeason)
                    qSeason.OnSeasonEnd(finalState);

                seasonTimer.Stop();
                evalRunner?.RecordSeasonEnd(finalState, seasonTimer.Elapsed.TotalSeconds);
                PrintState($"END OF SEASON {season}", finalState, agent);
                PrintDynasty(gs);

                if (season == maxSeasons) break;

                // Reset per-season tracking, then ask the server to advance the season.
                // The server does not auto-advance (UseAutoUpdate=false), so without this
                // call fief income/expenses never apply and days never refresh.
                agent.ResetSeasonState();

                if (!agent.AdvanceSeason())
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("  Server did not acknowledge SeasonUpdate, stopping.");
                    Console.ResetColor();
                    break;
                }

                // The lord may have died in the season update just applied. If he had an heir,
                // the server has already promoted the heir and rebound this account to him, so
                // resync who we control before fetching state - otherwise we would keep asking
                // about the dead lord and mistake a normal succession for a dead end.
                string lordBefore = agent.CurrentPCId;
                agent.ResyncCurrentPC();
                string lordAfter = agent.CurrentPCId;
                if (lordAfter != null && lordAfter != lordBefore)
                {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine($"  [SUCCESSION] {lordBefore} died; {lordAfter} succeeds and the dynasty continues.");
                    Console.ResetColor();
                }

                try { gs = agent.ObtainCurrentGSandUpdateAgentLists(gs); }
                catch (Exception ex) { Error("State refresh failed after season end", ex); break; }

                if (gs.myPC == null)
                {
                    // No living character for this account, and the resync above found no heir:
                    // the bloodline has died out. A legitimate game over, not a fault.
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  Bloodline extinct: {lordBefore} died with no heir to succeed. Ending run.");
                    Console.ResetColor();
                    break;
                }
                if (gs.myPC.days < 1)
                {
                    // We DO have a living lord but the season update left him with no days. That
                    // is not an expected outcome - flag it as a real anomaly rather than a quiet
                    // "no further seasons", since it points at a day-allowance bug, not death.
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  [ANOMALY] Lord {gs.myPC.charID} is alive but has {gs.myPC.days:0.##} days after SeasonUpdate. "
                        + "Expected a full allowance - this needs investigating (day allocation).");
                    Console.ResetColor();
                    break;
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n  [INFO] Season {season + 1} starting. Days: {gs.myPC.days:0.#}, Year: {gs.currentYear}");
                Console.ResetColor();
                PrintState($"START OF SEASON {season + 1}", agent.BuildState(gs), agent);
            }

            if (agent is QLearningAgent qEnd)
            {
                qEnd.Save(savePath);
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"  [Q] Training summary: episodes={qEnd.EpisodeCount} | steps={qEnd.TotalSteps} | states={qEnd.TableSize} | ε={qEnd.Epsilon:F3}");
                Console.ResetColor();
            }

            if (evalRunner != null)
            {
                evalRunner.PrintSummary();
                string logDir = Path.GetDirectoryName(evalLogPath);
                if (!string.IsNullOrEmpty(logDir)) Directory.CreateDirectory(logDir);
                evalRunner.SaveResults(evalLogPath);
            }

            Console.WriteLine();
            PrintState("FINAL STATE", finalState ?? agent.BuildState(gs), agent);
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
                ("7.  At War, active siege (keep level 4, reduction)",
                 Scenario(treasury: 20_000, threat: ThreatLevel.ActiveCombat,
                          hasArmy: true, armyTroops: 200, armyID: "A1",
                          activeSiegeID: "Siege_1", keepLevel: 4f)),
                ("8.  At War, enemy army here, attack",
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
            string bailiffCand = econ.HomeNeedsBailiff && econ.HomeBailiffCandidate != null
                ? $" | bailiff cand: {econ.HomeBailiffCandidate} (fit {econ.HomeBailiffCandidateFit:F1})" : "";
            string leaderCand = mil.ArmyNeedsLeader && mil.ArmyLeaderCandidateID != null
                ? $" | leader cand: {mil.ArmyLeaderCandidateID} (combat {mil.ArmyLeaderCandidateFit:F1})" : "";

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n  [{label}] Treasury: {econ.HomeTreasury} ({econStr}) | Army: {army} | Bailiff: {bailiff} ({cover}) | Threat: {threat}{keep}{surplus}{heir}{bailiffCand}{leaderCand}");
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

        /// <summary>
        ///     Reports the succession picture: whether the lord still has a wife, whether she
        ///     can conceive, and who is actually in line. Every one of these is a precondition
        ///     for an heir, and when a run ends heirless the summary alone cannot say which one
        ///     failed - so print them rather than infer them from the action distribution.
        /// </summary>
        private static void PrintDynasty(GameState gs)
        {
            if (gs.myPC == null) return;

            var sons = gs.myFamilyNPCs.Where(n => n.isMale && n.isAlive).ToList();
            string line;

            if (string.IsNullOrWhiteSpace(gs.myPC.spouse))
            {
                line = "no wife (widowed or unmarried) : cannot father a child until remarried";
            }
            else if (!gs.tryGetCharacter(gs.myPC.spouse, out RedProtoCharacter w))
            {
                line = $"wife {gs.myPC.spouse} not in any known list : likely dead but still set on the PC";
            }
            else
            {
                string blocker =
                      !w.isAlive                       ? "she is dead"
                    : w.isPregnant                     ? "already pregnant"
                    : w.location != gs.myPC.location   ? $"she is at {w.location}, lord at {gs.myPC.location}"
                    : w.days <= 1                      ? $"she has {w.days:0.#} days left"
                    : "can try for a child";
                line = $"wife {w.charID} : {blocker}";
            }

            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"  Dynasty   : {line}");
            Console.WriteLine($"  Heir      : {(string.IsNullOrWhiteSpace(gs.myPC.myHeirId) ? "none appointed" : gs.myPC.myHeirId)}" +
                              $" | living sons/male kin: {sons.Count}" +
                              (sons.Count > 0 ? $" ({string.Join(", ", sons.Take(5).Select(n => $"{n.charID} b.{n.birthYear}"))})" : ""));
            Console.ResetColor();
        }

        private static void PrintDelta(double daysBefore, double treasuryBefore, double totalTreasuryBefore, GameState gsAfter)
        {
            if (gsAfter.myPC == null) return;
            double daysUsed = daysBefore - gsAfter.myPC.days;
            // Use treasuryBefore as fallback so a null home-fief never generates a false delta
            double treasuryAfter = gsAfter.getHomeFief()?.treasury ?? treasuryBefore;
            double treasuryDelta = treasuryAfter - treasuryBefore;
            double totalAfter = gsAfter.myFiefs?.Sum(f => (double)f.treasury) ?? totalTreasuryBefore;
            double totalDelta = totalAfter - totalTreasuryBefore;

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(daysUsed != 0
                ? $"  Days used: {daysUsed:0.#} (now {gsAfter.myPC.days:0.#})"
                : $"  Days: unchanged ({gsAfter.myPC.days:0.#})");
            Console.Write(treasuryDelta != 0
                ? $"  | Home: {treasuryDelta:+0;-0;0} (now {treasuryAfter:0})"
                : $"  | Home: unchanged ({treasuryAfter:0})");
            // Always show total so per-fief changes (not home-fief) are visible
            Console.Write(totalDelta != 0
                ? $"  | Total: {totalDelta:+0;-0;0} (now {totalAfter:0})"
                : $"  | Total: unchanged ({totalAfter:0})");
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

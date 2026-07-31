using System;
using System.Linq;
using System.Text;

namespace JominiAI
{
    public enum FSMState
    {
        Stable,
        EconomicCrisis,
        UnderThreat,
        AtWar,
        Consolidating
    }

    internal static class Thresholds
    {
        public const int RecruitAmount = 50;
        public const int ConsolidateRecruit = 25; // reduced recruit while consolidating
        // Household size cap. Employees are the pool bailiff posts are filled from, so a
        // cap of 3 starved bailiff coverage once the realm grew past a handful of fiefs.
        public const int MaxEmployees = 12;
        // Minimum troops before the aggressive FSM marches on a neighbour.
        public const int WarPartyMinTroops = 50;
    }

    public class FSMAgent : Agent
    {
        public FSMState CurrentState { get; private set; } = FSMState.Stable;

        // When set (via --aggressive) the agent raises an army and marches on a neighbour
        // in peacetime, so the war/siege path can actually be exercised in a solo run.
        public bool Aggressive { get; set; } = false;

        private int _consolidatingSeasonsLeft = 0;
        private const int ConsolidatingSeasons = 2;

        private string _lastFingerprint = "";
        private MainActions _lastAction = MainActions.None;
        private string[] _lastArgs = Array.Empty<string>();
        private int _consecutiveForcedCamps = 0;
        private const int MaxForcedCamps = 3;

        public FSMAgent(string username, string password) : base(username, password) { }

        public override MainActions findNextAction(AgentState state, out string[] arguments)
        {
            arguments = Array.Empty<string>();

            if (!state.Character.IsAlive || state.Character.IsCaptive) return MainActions.None;
            if (state.Character.DaysRemaining <= 0) return MainActions.None;

            if (_consecutiveForcedCamps >= MaxForcedCamps)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"  [FSM] Camp-escalation limit ({_consecutiveForcedCamps})");
                Console.ResetColor();
                return MainActions.None;
            }

            string fingerprint = Fingerprint(state);
            Transition(state);

            MainActions action = CurrentState switch
            {
                FSMState.EconomicCrisis => HandleEconomicCrisis(state, out arguments),
                FSMState.UnderThreat => HandleUnderThreat(state, out arguments),
                FSMState.AtWar => HandleAtWar(state, out arguments),
                FSMState.Consolidating => HandleConsolidating(state, out arguments),
                _ => HandleStable(state, out arguments)
            };

            // Release allied captives as a secondary pass in any non-war state.
            if ((action == MainActions.None || action == MainActions.NPCcamp)
                && state.Diplomacy.HasAlliedCaptives
                && CurrentState != FSMState.AtWar
                && !FailedActions.Contains(MainActions.KidnapReleaseCaptive))
            {
                arguments = new[] { state.Diplomacy.AlliedCaptiveID };
                action = MainActions.KidnapReleaseCaptive;
            }

            // Same fingerprint + same action + same args means we're stuck; force a camp.
            bool stall = fingerprint == _lastFingerprint
                      && action == _lastAction
                      && ArgsEqual(arguments, _lastArgs);

            if (stall)
            {
                _consecutiveForcedCamps++;
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"  [FSM] Stall on '{action}', forcing camp ({_consecutiveForcedCamps}/{MaxForcedCamps}).");
                Console.ResetColor();
                action = Camp(state, out arguments);
            }
            else if (action != MainActions.NPCcamp && action != MainActions.NPCexitKeep)
            {
                _consecutiveForcedCamps = 0;
            }

            _lastFingerprint = fingerprint;
            _lastAction = action;
            _lastArgs = arguments;
            return action;
        }

        private void Transition(AgentState s)
        {
            FSMState next = CurrentState;
            var mil = s.Military;
            var econ = s.Economy;

            if (mil.Threat == ThreatLevel.ActiveCombat)
            {
                next = FSMState.AtWar;
                _consolidatingSeasonsLeft = ConsolidatingSeasons;
            }
            else if (mil.Threat >= ThreatLevel.EnemyNearby)
            {
                // An enemy army sitting in one of my fiefs is a real threat; react
                // defensively rather than treating it as Stable.
                next = FSMState.UnderThreat;
                _consolidatingSeasonsLeft = ConsolidatingSeasons;
            }
            else if (econ.IsCritical && CurrentState != FSMState.EconomicCrisis)
            {
                next = FSMState.EconomicCrisis;
            }
            else if (CurrentState == FSMState.EconomicCrisis && econ.HasRecovered)
            {
                next = FSMState.Consolidating;
                _consolidatingSeasonsLeft = ConsolidatingSeasons;
            }
            else if ((CurrentState == FSMState.AtWar || CurrentState == FSMState.UnderThreat)
                     && mil.Threat == ThreatLevel.None)
            {
                next = FSMState.Consolidating;
                _consolidatingSeasonsLeft = ConsolidatingSeasons;
            }
            else if (CurrentState == FSMState.Consolidating)
            {
                if (_consolidatingSeasonsLeft > 0) _consolidatingSeasonsLeft--;
                if (_consolidatingSeasonsLeft == 0) next = FSMState.Stable;
            }

            CurrentState = next;
        }

        private MainActions HandleStable(AgentState s, out string[] args)
        {
            args = Array.Empty<string>();
            var econ = s.Economy;
            var mil = s.Military;
            var dip = s.Diplomacy;

            if (dip.HasReceivedProposal && !Failed(MainActions.NPCacceptRejectProposal))
            { args = new[] { "true" }; return MainActions.NPCacceptRejectProposal; }

            if (dip.HasUnappointedHeir && !Failed(MainActions.NPCappointHeir))
            { args = new[] { dip.HeirCandidateID }; return MainActions.NPCappointHeir; }

            // Keep fathering children until the line has an heir AND a spare. A single heir is
            // brittle: a son who dies in childhood (as one did around season 62 of a long run,
            // leaving the lord suddenly heirless despite being alive and married) ends the
            // succession outright. Two living sons means one death is survivable. This can't
            // spam - a successful attempt leaves the wife pregnant, which blocks CanTryForChild
            // for the ~3 seasons of gestation - so it fires at most once every few seasons and
            // costs a single day, cheap insurance for the run's top priority.
            if (dip.LivingSonCount < 2
                && dip.CanTryForChild && !Failed(MainActions.NPCtryForChild))
            { args = Array.Empty<string>(); return MainActions.NPCtryForChild; }

            // Secure the line before anything else optional: a widowed lord remarries so he
            // can father an heir, and marriageable children are wedded. This has to sit
            // above the war block, otherwise an aggressive lord marches every season and
            // never remarries, which is why the war runs ended heirless.
            if (dip.HasMarriageOpportunity && !Failed(MainActions.NPCmarryPC))
            { args = new[] { dip.MarriageCandidateID }; return MainActions.NPCmarryPC; }

            if (dip.HasChildMarriageOpportunity && !Failed(MainActions.NPCmarryChild))
            { args = new[] { dip.ChildForMarriageID, dip.ChildMarriageCandidateID }; return MainActions.NPCmarryChild; }

            // Aggressive mode: raise a war party, march on a neighbour and lay siege. Once
            // the siege starts the FSM flips to AtWar and the siege rounds take over. Behind
            // a flag so ordinary economic runs are unchanged; dynasty security still wins above.
            if (Aggressive)
            {
                if (!mil.HasArmy)
                {
                    if (mil.CanBuildArmy && !Failed(MainActions.ArmyRecruit))
                    { args = new[] { "", Thresholds.RecruitAmount.ToString() }; return MainActions.ArmyRecruit; }
                }
                else
                {
                    // Standing on a besiegeable fief with the army: lay siege.
                    if (mil.CanBesiegeCurrent && !Failed(MainActions.SiegeBesiegeCurrentFief))
                    { args = new[] { mil.MainArmyID }; return MainActions.SiegeBesiegeCurrentFief; }

                    if (mil.ArmyNeedsMaint && !Failed(MainActions.ArmyMaintain))
                    { args = new[] { mil.MainArmyID }; return MainActions.ArmyMaintain; }

                    // Not a big enough war party yet: keep recruiting.
                    if (mil.MainArmyTroops < Thresholds.WarPartyMinTroops && mil.ArmyCanRecruit
                        && !Failed(MainActions.ArmyRecruit))
                    { args = new[] { mil.MainArmyID, Thresholds.RecruitAmount.ToString() }; return MainActions.ArmyRecruit; }

                    // Big enough: make the PC lead (so the army marches with him), then march.
                    if (mil.MainArmyTroops >= Thresholds.WarPartyMinTroops)
                    {
                        var leadPC = new[] { mil.MainArmyID, s.Character.CharID };
                        if (!mil.PCLeadsMainArmy && !Blocked(MainActions.ArmyAppointLeader, leadPC))
                        { args = leadPC; return MainActions.ArmyAppointLeader; }

                        if (mil.PCLeadsMainArmy)
                        {
                            // Must be outside the keep to march the army out of the fief.
                            if (s.Character.InKeep && !Failed(MainActions.NPCexitKeep))
                            { args = new[] { s.Character.CharID }; return MainActions.NPCexitKeep; }

                            // A known enemy fief in reach: march straight to it.
                            if (mil.WarTargetFiefID != null && s.Character.CurrentFiefID != mil.WarTargetFiefID
                                && !Failed(MainActions.NPCmoveToFief))
                            { args = new[] { mil.WarTargetFiefID }; return MainActions.NPCmoveToFief; }

                            // No enemy fief known yet: march one hex outward to find a border to
                            // besiege. Arrival on any non-owned fief trips CanBesiegeCurrent above.
                            if (mil.WarTargetFiefID == null && mil.MarchDirection != null)
                            { args = new[] { mil.MarchDirection }; return MainActions.NPCmoveDirection; }
                        }
                    }
                }
            }

            // Bailiffs: home fief first, then any other fief that needs one.
            if (econ.HomeNeedsBailiff && econ.HomeBailiffCandidate != null && !Failed(MainActions.NPCappointBaillif))
            { args = new[] { s.Character.HomeFiefID, econ.HomeBailiffCandidate }; return MainActions.NPCappointBaillif; }

            if (econ.OtherFiefNeedingBailiff != null && !Failed(MainActions.NPCappointBaillif))
            { args = new[] { econ.OtherFiefNeedingBailiff, econ.OtherFiefBailiffCandidate }; return MainActions.NPCappointBaillif; }

            if (dip.HasEntourageCandidate && !Failed(MainActions.NPCaddToEntourage))
            { args = new[] { dip.EntourageCandidateID }; return MainActions.NPCaddToEntourage; }

            var leadCand = new[] { mil.MainArmyID, mil.ArmyLeaderCandidateID };
            if (mil.ArmyNeedsLeader && !Blocked(MainActions.ArmyAppointLeader, leadCand))
            { args = leadCand; return MainActions.ArmyAppointLeader; }

            if (dip.HasHirableNPC && dip.EmployeeCount < Thresholds.MaxEmployees && !Failed(MainActions.NPChire))
            { args = new[] { dip.HirableNPCID, dip.HireOfferAmount.ToString() }; return MainActions.NPChire; }

            if (!Failed(MainActions.FiefAutoAdjustExpenditure))
                foreach (string fiefID in econ.FiefsNeedingExpAdjustment)
                { args = new[] { fiefID }; return MainActions.FiefAutoAdjustExpenditure; }

            if (mil.ArmyNeedsMaint && !Failed(MainActions.ArmyMaintain))
            { args = new[] { mil.MainArmyID }; return MainActions.ArmyMaintain; }

            // Raise a standing army if we have none (military strength, and something to
            // defend with or press a siege later). Empty army id => the server creates one.
            if (mil.CanBuildArmy && !Failed(MainActions.ArmyRecruit))
            { args = new[] { "", Thresholds.RecruitAmount.ToString() }; return MainActions.ArmyRecruit; }

            if (mil.ArmyCanRecruit && !Failed(MainActions.ArmyRecruit))
            { args = new[] { mil.MainArmyID, Thresholds.RecruitAmount.ToString() }; return MainActions.ArmyRecruit; }

            if (dip.CanTryForChild && !Failed(MainActions.NPCtryForChild))
            { args = Array.Empty<string>(); return MainActions.NPCtryForChild; }

            // Only pull surplus if home is short; no point draining a healthy fief.
            if (econ.HasSurplusFief && econ.HomeTreasury < 5_000 && !Failed(MainActions.FiefTransferFunds))
            { args = new[] { econ.SurplusFiefID, s.Character.HomeFiefID, econ.SurplusTransferAmount.ToString() }; return MainActions.FiefTransferFunds; }

            return Camp(s, out args);
        }

        private MainActions HandleEconomicCrisis(AgentState s, out string[] args)
        {
            args = Array.Empty<string>();
            var econ = s.Economy;
            var mil = s.Military;
            var dip = s.Diplomacy;

            // Dynasty continuity comes first, whatever the finances look like.
            if (dip.HasUnappointedHeir && !Failed(MainActions.NPCappointHeir))
            { args = new[] { dip.HeirCandidateID }; return MainActions.NPCappointHeir; }

            if (dip.HasReceivedProposal && !Failed(MainActions.NPCacceptRejectProposal))
            { args = new[] { "true" }; return MainActions.NPCacceptRejectProposal; }

            // An immediate gold injection from a surplus fief beats waiting on spending cuts.
            if (econ.HasSurplusFief && !Failed(MainActions.FiefTransferFunds))
            { args = new[] { econ.SurplusFiefID, s.Character.HomeFiefID, econ.SurplusTransferAmount.ToString() }; return MainActions.FiefTransferFunds; }

            // Stop the bleeding on deficit fiefs.
            if (!Failed(MainActions.FiefAutoAdjustExpenditure))
                foreach (string fiefID in econ.FiefsNeedingExpAdjustment)
                { args = new[] { fiefID }; return MainActions.FiefAutoAdjustExpenditure; }

            if (dip.HasFirableEmployee && !Failed(MainActions.NPCfire))
            { args = new[] { dip.FireableEmployeeID }; return MainActions.NPCfire; }

            if (mil.ArmyNeedsMaint && !Failed(MainActions.ArmyMaintain))
            { args = new[] { mil.MainArmyID }; return MainActions.ArmyMaintain; }

            // No bailiff appointments here: we're cutting salary costs, not adding them.
            return Camp(s, out args);
        }

        private MainActions HandleUnderThreat(AgentState s, out string[] args)
        {
            args = Array.Empty<string>();
            var econ = s.Economy;
            var mil = s.Military;
            var dip = s.Diplomacy;

            // The dynasty has to survive the threat.
            if (dip.HasUnappointedHeir && !Failed(MainActions.NPCappointHeir))
            { args = new[] { dip.HeirCandidateID }; return MainActions.NPCappointHeir; }

            if (dip.HasReceivedProposal && !Failed(MainActions.NPCacceptRejectProposal))
            { args = new[] { "true" }; return MainActions.NPCacceptRejectProposal; }

            if (mil.ArmyNeedsMaint && !Failed(MainActions.ArmyMaintain))
            { args = new[] { mil.MainArmyID }; return MainActions.ArmyMaintain; }

            var leadCand = new[] { mil.MainArmyID, mil.ArmyLeaderCandidateID };
            if (mil.ArmyNeedsLeader && !Blocked(MainActions.ArmyAppointLeader, leadCand))
            { args = leadCand; return MainActions.ArmyAppointLeader; }

            if (mil.ArmyCanRecruit && !Failed(MainActions.ArmyRecruit))
            { args = new[] { mil.MainArmyID, Thresholds.RecruitAmount.ToString() }; return MainActions.ArmyRecruit; }

            if (econ.HomeNeedsBailiff && econ.HomeBailiffCandidate != null && !Failed(MainActions.NPCappointBaillif))
            { args = new[] { s.Character.HomeFiefID, econ.HomeBailiffCandidate }; return MainActions.NPCappointBaillif; }

            if (econ.OtherFiefNeedingBailiff != null && !Failed(MainActions.NPCappointBaillif))
            { args = new[] { econ.OtherFiefNeedingBailiff, econ.OtherFiefBailiffCandidate }; return MainActions.NPCappointBaillif; }

            if (!Failed(MainActions.FiefAutoAdjustExpenditure))
                foreach (string fiefID in econ.FiefsNeedingExpAdjustment)
                { args = new[] { fiefID }; return MainActions.FiefAutoAdjustExpenditure; }

            return Camp(s, out args);
        }

        private MainActions HandleAtWar(AgentState s, out string[] args)
        {
            args = Array.Empty<string>();
            var mil = s.Military;
            var econ = s.Economy;
            var dip = s.Diplomacy;

            // Secure the dynasty before battle: appointing an heir is instant and critical.
            if (dip.HasUnappointedHeir && !Failed(MainActions.NPCappointHeir))
            { args = new[] { dip.HeirCandidateID }; return MainActions.NPCappointHeir; }

            if (dip.HasReceivedProposal && !Failed(MainActions.NPCacceptRejectProposal))
            { args = new[] { "true" }; return MainActions.NPCacceptRejectProposal; }

            var leadCand = new[] { mil.MainArmyID, mil.ArmyLeaderCandidateID };
            if (mil.ArmyNeedsLeader && !Blocked(MainActions.ArmyAppointLeader, leadCand))
            { args = leadCand; return MainActions.ArmyAppointLeader; }

            if (mil.ActiveSiegeID != null)
            {
                MainActions siegeOp = mil.BesiegedKeepLevel < 3f
                    ? MainActions.SiegeStorm
                    : MainActions.SiegeReduction;
                if (!Failed(siegeOp))
                { args = new[] { mil.ActiveSiegeID }; return siegeOp; }

                if (!Failed(MainActions.SiegeNegotiation))
                { args = new[] { mil.ActiveSiegeID }; return MainActions.SiegeNegotiation; }
            }

            if (mil.EnemyArmyIDInFief != null && mil.HasArmy && !Failed(MainActions.ArmyAttack))
            { args = new[] { mil.MainArmyID, mil.EnemyArmyIDInFief }; return MainActions.ArmyAttack; }

            if (mil.CanBesiegeCurrent && !Failed(MainActions.SiegeBesiegeCurrentFief))
            { args = new[] { mil.MainArmyID }; return MainActions.SiegeBesiegeCurrentFief; }

            if (mil.CanPillageCurrent && !Failed(MainActions.ArmyPillageCurrentFief))
            { args = new[] { mil.MainArmyID }; return MainActions.ArmyPillageCurrentFief; }

            if (mil.ArmyNeedsMaint && !Failed(MainActions.ArmyMaintain))
            { args = new[] { mil.MainArmyID }; return MainActions.ArmyMaintain; }

            if (econ.IsCritical && econ.HasSurplusFief && !Failed(MainActions.FiefTransferFunds))
            { args = new[] { econ.SurplusFiefID, s.Character.HomeFiefID, econ.SurplusTransferAmount.ToString() }; return MainActions.FiefTransferFunds; }

            return Camp(s, out args);
        }

        private MainActions HandleConsolidating(AgentState s, out string[] args)
        {
            args = Array.Empty<string>();
            var econ = s.Economy;
            var mil = s.Military;
            var dip = s.Diplomacy;

            // Rebuild the dynasty after crisis or war.
            if (dip.HasReceivedProposal && !Failed(MainActions.NPCacceptRejectProposal))
            { args = new[] { "true" }; return MainActions.NPCacceptRejectProposal; }

            if (dip.HasUnappointedHeir && !Failed(MainActions.NPCappointHeir))
            { args = new[] { dip.HeirCandidateID }; return MainActions.NPCappointHeir; }

            // As in Stable: keep going until there's an heir and a spare, not just one son.
            if (dip.LivingSonCount < 2
                && dip.CanTryForChild && !Failed(MainActions.NPCtryForChild))
            { args = Array.Empty<string>(); return MainActions.NPCtryForChild; }

            if (dip.HasMarriageOpportunity && !Failed(MainActions.NPCmarryPC))
            { args = new[] { dip.MarriageCandidateID }; return MainActions.NPCmarryPC; }

            if (dip.HasChildMarriageOpportunity && !Failed(MainActions.NPCmarryChild))
            { args = new[] { dip.ChildForMarriageID, dip.ChildMarriageCandidateID }; return MainActions.NPCmarryChild; }

            if (!Failed(MainActions.FiefAutoAdjustExpenditure))
                foreach (string fiefID in econ.FiefsNeedingExpAdjustment)
                { args = new[] { fiefID }; return MainActions.FiefAutoAdjustExpenditure; }

            if (econ.HomeNeedsBailiff && econ.HomeBailiffCandidate != null && !Failed(MainActions.NPCappointBaillif))
            { args = new[] { s.Character.HomeFiefID, econ.HomeBailiffCandidate }; return MainActions.NPCappointBaillif; }

            if (econ.OtherFiefNeedingBailiff != null && !Failed(MainActions.NPCappointBaillif))
            { args = new[] { econ.OtherFiefNeedingBailiff, econ.OtherFiefBailiffCandidate }; return MainActions.NPCappointBaillif; }

            if (dip.HasEntourageCandidate && !Failed(MainActions.NPCaddToEntourage))
            { args = new[] { dip.EntourageCandidateID }; return MainActions.NPCaddToEntourage; }

            var leadCand = new[] { mil.MainArmyID, mil.ArmyLeaderCandidateID };
            if (mil.ArmyNeedsLeader && !Blocked(MainActions.ArmyAppointLeader, leadCand))
            { args = leadCand; return MainActions.ArmyAppointLeader; }

            if (mil.ArmyNeedsMaint && !Failed(MainActions.ArmyMaintain))
            { args = new[] { mil.MainArmyID }; return MainActions.ArmyMaintain; }

            if (mil.CanBuildArmy && econ.HasRecovered && !Failed(MainActions.ArmyRecruit))
            { args = new[] { "", Thresholds.ConsolidateRecruit.ToString() }; return MainActions.ArmyRecruit; }

            if (mil.ArmyCanRecruit && econ.HasRecovered && !Failed(MainActions.ArmyRecruit))
            { args = new[] { mil.MainArmyID, Thresholds.ConsolidateRecruit.ToString() }; return MainActions.ArmyRecruit; }

            if (dip.HasAlliedCaptives && !Failed(MainActions.KidnapReleaseCaptive))
            { args = new[] { dip.AlliedCaptiveID }; return MainActions.KidnapReleaseCaptive; }

            return Camp(s, out args);
        }

        private MainActions Camp(AgentState s, out string[] args)
        {
            if (s.Character.InKeep && !Failed(MainActions.NPCexitKeep))
            {
                args = new[] { s.Character.CharID };
                return MainActions.NPCexitKeep;
            }
            if (Failed(MainActions.NPCcamp))
            {
                // Camp was rejected: end the season rather than calling ExitKeep. EnterExitKeep
                // is a toggle, so calling it while NOT in keep would enter the keep, clear
                // FailedActions, and spin an infinite enter/exit/camp loop.
                args = Array.Empty<string>();
                return MainActions.None;
            }
            // Camp out the whole remaining season in one action. The server doesn't advance
            // time between requests (UseAutoUpdate=false), so nothing changes mid-season -
            // camping 5 days at a time only burned round-trips and tripped the anti-stall
            // before days could reach 0. Burning the remainder lets the season end cleanly.
            byte days = (byte)Math.Max(1, Math.Min((int)s.Character.DaysRemaining, 90));
            args = new[] { s.Character.CharID, days.ToString() };
            return MainActions.NPCcamp;
        }

        public override void ResetSeasonState()
        {
            base.ResetSeasonState();
            _consecutiveForcedCamps = 0;
            _lastFingerprint = "";
            _lastAction = MainActions.None;
            _lastArgs = Array.Empty<string>();
            // CurrentState and _consolidatingSeasonsLeft carry forward: the strategic
            // situation doesn't reset at a season boundary.
        }

        public MainActions DryRun(AgentState state, out string[] args, out FSMState predictedState)
        {
            FSMState savedState = CurrentState;
            int savedLeft = _consolidatingSeasonsLeft;

            Transition(state);
            predictedState = CurrentState;

            MainActions action = CurrentState switch
            {
                FSMState.EconomicCrisis => HandleEconomicCrisis(state, out args),
                FSMState.UnderThreat => HandleUnderThreat(state, out args),
                FSMState.AtWar => HandleAtWar(state, out args),
                FSMState.Consolidating => HandleConsolidating(state, out args),
                _ => HandleStable(state, out args)
            };

            CurrentState = savedState;
            _consolidatingSeasonsLeft = savedLeft;
            return action;
        }

        private bool Failed(MainActions a) => FailedActions.Contains(a);

        // Both a transient (this-tick) failure and a sticky (whole-season) one block an
        // action. The sticky check is args-aware, so a leader appointment that keeps being
        // refused for the same army/candidate is dropped for the season instead of being
        // re-issued after every unrelated success.
        private bool Blocked(MainActions a, string[] args) => Failed(a) || IsSignatureBlocked(a, args);

        private string Fingerprint(AgentState s)
        {
            var sb = new StringBuilder();
            sb.Append(s.Character.DaysRemaining).Append('|');
            sb.Append(s.Character.InKeep).Append('|');
            sb.Append(s.Year).Append('|');
            sb.Append(s.Economy.FiefsNeedingExpAdjustment.Count).Append('|');
            sb.Append(s.Economy.HomeTreasury).Append('|');
            sb.Append(s.Economy.FiefsWithBailiff).Append('|');
            sb.Append(s.Diplomacy.EmployeeCount).Append('|');
            sb.Append(s.Diplomacy.HasUnappointedHeir).Append('|');
            sb.Append(s.Diplomacy.HasMarriageOpportunity).Append('|');
            sb.Append(s.Diplomacy.HasChildMarriageOpportunity).Append('|');
            sb.Append(s.Diplomacy.HasReceivedProposal).Append('|');
            sb.Append(s.Military.HasArmy).Append('|');
            sb.Append(s.Military.ArmyNeedsMaint).Append('|');
            sb.Append(s.Military.ArmyNeedsLeader).Append('|');
            sb.Append(s.Military.Threat).Append('|');
            return sb.ToString();
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
    }
}

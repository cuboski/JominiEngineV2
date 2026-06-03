using System;
using System.Collections.Generic;
using System.Linq;

namespace JominiAI
{
    // ── Character context ──────────────────────────────────────────────────────
    // Everything about the player character's personal situation.

    public class CharacterContext
    {
        public string CharID          { get; init; }
        public string CurrentFiefID   { get; init; }
        public string HomeFiefID      { get; init; }
        public double DaysRemaining   { get; init; }
        public bool   InKeep          { get; init; }
        public bool   IsAlive         { get; init; }
        public bool   IsCaptive       { get; init; }
        public bool   HasSpouse       { get; init; }
        public bool   HasHeir         { get; init; }
        public string FamilyID        { get; init; }
    }

    // ── Economic context ───────────────────────────────────────────────────────
    // Treasury state, fief management, and fund redistribution opportunities.

    public class EconomicContext
    {
        public int   HomeTreasury     { get; init; }
        public int   TotalTreasury    { get; init; }   // sum across all owned fiefs
        public int   FiefCount        { get; init; }
        public int   FiefsInDeficit   { get; init; }   // treasury < 0
        public int   FiefsWithBailiff { get; init; }

        public bool  HomeIsInDeficit  { get; init; }
        public bool  IsCritical       { get; init; }   // home treasury < 1 000
        public bool  HasRecovered     { get; init; }   // home treasury >= 10 000

        // Ordered list of fief IDs that still need expenditure adjustment this season
        // (treasury < 0 AND not yet adjusted). Each handler iterates this directly.
        public IReadOnlyList<string> FiefsNeedingExpAdjustment { get; init; }

        // Bailiff placement — home fief
        public bool   HomeNeedsBailiff         { get; init; }
        public string HomeBailiffCandidate     { get; init; }   // NPC charID or null

        // Bailiff placement — first other fief that needs one and has an eligible NPC there
        public string OtherFiefNeedingBailiff   { get; init; }
        public string OtherFiefBailiffCandidate { get; init; }

        // Fund redistribution: richest non-home fief above the surplus threshold
        public bool   HasSurplusFief       { get; init; }
        public string SurplusFiefID        { get; init; }
        public int    SurplusTransferAmount { get; init; }   // half of surplus fief treasury
    }

    // ── Military context ───────────────────────────────────────────────────────
    // Armies, threats, and siege management.

    public enum ThreatLevel
    {
        None         = 0,  // no known threats
        EnemyNearby  = 1,  // enemy army in one of my fiefs (not current)
        EnemyPresent = 2,  // enemy besieging my fief OR enemy army in current fief
        ActiveCombat = 3   // I am conducting a siege, OR enemy army here + I have army
    }

    public class MilitaryContext
    {
        public ThreatLevel Threat             { get; init; }
        public bool        HasArmy            { get; init; }
        public string      MainArmyID         { get; init; }
        public uint        MainArmyTroops     { get; init; }
        public bool        ArmyNeedsMaint     { get; init; }
        public bool        ArmyCanRecruit     { get; init; }
        public bool        IsUnderSiege       { get; init; }
        public bool        HasActiveSiege     { get; init; }   // player is besieging someone
        // Details of the siege in the player's current fief (null if player isn't there)
        public string      ActiveSiegeID      { get; init; }
        public string      BesiegedFiefID     { get; init; }
        public float       BesiegedKeepLevel  { get; init; }
        // Enemy army in the current fief (for attack action)
        public string      EnemyArmyIDInFief  { get; init; }
        // Pre-computed booleans for offensive actions in current fief
        public bool        CanBesiegeCurrent  { get; init; }
        public bool        CanPillageCurrent  { get; init; }
    }

    // ── Diplomatic context ─────────────────────────────────────────────────────
    // Family, NPCs, captives, and dynastic opportunities.

    public class DiplomaticContext
    {
        public bool   CanTryForChild     { get; init; }
        public bool   HasUnappointedHeir { get; init; }
        public string HeirCandidateID    { get; init; }

        public bool   HasHirableNPC      { get; init; }
        public string HirableNPCID       { get; init; }
        public uint   HireOfferAmount    { get; init; }  // salary * 1.1, pre-computed
        public int    EmployeeCount      { get; init; }

        public bool   HasFirableEmployee { get; init; }
        public string FireableEmployeeID { get; init; }

        public bool   HasAlliedCaptives  { get; init; }
        public string AlliedCaptiveID    { get; init; }
    }

    // ── Derived metrics ────────────────────────────────────────────────────────
    // Pre-computed normalized scores. Used for display and for the Q-learning
    // observation vector (fixed-size float array, never changes length).

    public class DerivedMetrics
    {
        public const int ObservationSize = 12;

        public float EconomicHealth   { get; }   // 0 = bankrupt, 1 = wealthy
        public float MilitaryStrength { get; }   // 0 = no army, 1 = large army
        public float BailiffCoverage  { get; }   // fraction of fiefs with bailiff
        public float ThreatScore      { get; }   // 0 = peaceful, 1 = at war
        public float DaysFraction     { get; }   // remaining days / 90

        private readonly float[] _vec;

        internal DerivedMetrics(CharacterContext chr, EconomicContext econ,
                                MilitaryContext mil, DiplomaticContext dip)
        {
            EconomicHealth   = Math.Max(0f, Math.Min(1f, econ.HomeTreasury / 100_000f));
            MilitaryStrength = Math.Min(1f, mil.MainArmyTroops / 500f);
            BailiffCoverage  = econ.FiefCount > 0
                             ? (float)econ.FiefsWithBailiff / econ.FiefCount : 0f;
            ThreatScore      = (float)mil.Threat / 3f;
            DaysFraction     = (float)(chr.DaysRemaining / 90.0);

            float deficitFrac = econ.FiefCount > 0
                              ? (float)econ.FiefsInDeficit / econ.FiefCount : 0f;

            _vec = new float[ObservationSize]
            {
                EconomicHealth,                      // 0  home treasury (normalised)
                deficitFrac,                         // 1  fraction of fiefs in deficit
                BailiffCoverage,                     // 2  fraction of fiefs with bailiff
                MilitaryStrength,                    // 3  army size (normalised)
                ThreatScore,                         // 4  threat level (normalised)
                DaysFraction,                        // 5  days remaining (normalised)
                econ.IsCritical        ? 1f : 0f,   // 6  treasury critical flag
                econ.HasRecovered      ? 1f : 0f,   // 7  treasury recovered flag
                mil.HasArmy            ? 1f : 0f,   // 8  has army
                chr.InKeep             ? 1f : 0f,   // 9  character in keep
                dip.CanTryForChild     ? 1f : 0f,   // 10 spouse available for child
                dip.HasUnappointedHeir ? 1f : 0f,   // 11 heir needed
            };
        }

        /// <summary>
        /// Returns a fixed-length float[ObservationSize] suitable for a Q-learning input.
        /// Each call returns a fresh copy so the caller can mutate it safely.
        /// </summary>
        public float[] ToObservationVector() => (float[])_vec.Clone();
    }

    // ── Unified agent state ────────────────────────────────────────────────────

    public class AgentState
    {
        public CharacterContext  Character { get; }
        public EconomicContext   Economy   { get; }
        public MilitaryContext   Military  { get; }
        public DiplomaticContext Diplomacy { get; }
        public DerivedMetrics    Metrics   { get; }
        public int               Year      { get; }

        public AgentState(CharacterContext character, EconomicContext economy,
                          MilitaryContext military, DiplomaticContext diplomacy,
                          int year)
        {
            Character = character ?? throw new ArgumentNullException(nameof(character));
            Economy   = economy   ?? throw new ArgumentNullException(nameof(economy));
            Military  = military  ?? throw new ArgumentNullException(nameof(military));
            Diplomacy = diplomacy ?? throw new ArgumentNullException(nameof(diplomacy));
            Year      = year;
            Metrics   = new DerivedMetrics(character, economy, military, diplomacy);
        }

        // ── Factory: build from server-fetched GameState ──────────────────────
        // adjustedFiefIDs: fiefs whose expenditure has already been adjusted this season.
        // estimateSalary:  delegate pointing at Agent.estimateNPCacceptableSalary — passed
        //                  in so AgentState stays decoupled from the Agent class.

        public static AgentState Build(
            GameState gs,
            HashSet<string> adjustedFiefIDs,
            Func<RedProtoNPC, double> estimateSalary)
        {
            if (gs.myPC == null)
                throw new InvalidOperationException("GameState has no player character.");

            RedProtoFief home    = gs.getHomeFief();
            RedProtoFief current = gs.getCurrentFief();

            // ── CharacterContext ──────────────────────────────────────────────
            var chr = new CharacterContext
            {
                CharID        = gs.myPC.charID,
                CurrentFiefID = current.fiefID,
                HomeFiefID    = home.fiefID,
                DaysRemaining = gs.myPC.days,
                InKeep        = gs.myPC.inKeep,
                IsAlive       = gs.myPC.isAlive,
                IsCaptive     = !string.IsNullOrWhiteSpace(gs.myPC.captor),
                HasSpouse     = !string.IsNullOrWhiteSpace(gs.myPC.spouse),
                HasHeir       = !string.IsNullOrWhiteSpace(gs.myPC.myHeirId),
                FamilyID      = gs.myPC.familyID,
            };

            // ── EconomicContext ───────────────────────────────────────────────
            int totalTreasury    = (int)gs.myFiefs.Sum(f => f.treasury);
            int fiefsInDeficit   = gs.myFiefs.Count(f => f.treasury < 0);
            int fiefsWithBailiff = gs.myFiefs.Count(f => !string.IsNullOrWhiteSpace(f.bailiffID));

            var needAdj = gs.myFiefs
                .Where(f => f.treasury < 0 && !adjustedFiefIDs.Contains(f.fiefID))
                .Select(f => f.fiefID)
                .ToList();

            string homeCand = string.IsNullOrWhiteSpace(home.bailiffID)
                            ? FindBailiffCandidate(gs, home.fiefID) : null;

            string otherFief = null, otherCand = null;
            foreach (var fief in gs.myFiefs)
            {
                if (!string.IsNullOrWhiteSpace(fief.bailiffID) || fief.fiefID == home.fiefID)
                    continue;
                string c = FindBailiffCandidate(gs, fief.fiefID);
                if (c != null) { otherFief = fief.fiefID; otherCand = c; break; }
            }

            var surplusFief = gs.myFiefs
                .Where(f => f.fiefID != home.fiefID && f.treasury > 5_000)
                .OrderByDescending(f => f.treasury)
                .FirstOrDefault();

            var econ = new EconomicContext
            {
                HomeTreasury             = (int)home.treasury,
                TotalTreasury            = totalTreasury,
                FiefCount                = gs.myFiefs.Count,
                FiefsInDeficit           = fiefsInDeficit,
                FiefsWithBailiff         = fiefsWithBailiff,
                HomeIsInDeficit          = home.treasury < 0,
                IsCritical               = home.treasury < 1_000,
                HasRecovered             = home.treasury >= 10_000,
                FiefsNeedingExpAdjustment = needAdj,
                HomeNeedsBailiff          = string.IsNullOrWhiteSpace(home.bailiffID),
                HomeBailiffCandidate      = homeCand,
                OtherFiefNeedingBailiff   = otherFief,
                OtherFiefBailiffCandidate = otherCand,
                HasSurplusFief            = surplusFief != null,
                SurplusFiefID             = surplusFief?.fiefID,
                SurplusTransferAmount     = surplusFief != null ? (int)(surplusFief.treasury / 2.0) : 0,
            };

            // ── MilitaryContext ───────────────────────────────────────────────
            string enemyArmyIDInFief = null;
            if (current.armyIDs != null)
                foreach (string id in current.armyIDs)
                    if (gs.tryGetArmy(id, out RedProtoArmy a) && gs.enemyPCids.Contains(a.ownerName))
                    { enemyArmyIDInFief = id; break; }

            bool underSiege  = gs.siegeOnMyFiefs.Count > 0;
            bool activeSiege = gs.mySieges.Count > 0;

            ThreatLevel threat;
            if (activeSiege || (enemyArmyIDInFief != null))
                threat = ThreatLevel.ActiveCombat;
            else if (underSiege)
                threat = ThreatLevel.EnemyPresent;
            else
            {
                bool enemyInMyFief = false;
                foreach (var fief in gs.myFiefs)
                {
                    if (fief.armyIDs == null) continue;
                    foreach (string id in fief.armyIDs)
                        if (gs.tryGetArmy(id, out RedProtoArmy a) && gs.enemyPCids.Contains(a.ownerName))
                        { enemyInMyFief = true; break; }
                    if (enemyInMyFief) break;
                }
                threat = enemyInMyFief ? ThreatLevel.EnemyNearby : ThreatLevel.None;
            }

            // Active siege in player's current fief
            var siege = gs.mySieges.FirstOrDefault(s => s.besiegedFiefID == current.fiefID);

            bool hasArmy = false; string mainArmyID = null; uint mainArmyTroops = 0;
            bool armyNeedsMaint = false, armyCanRecruit = false;
            if (gs.tryGetMainArmy(out RedProtoArmy main))
            {
                hasArmy        = true;
                mainArmyID     = main.armyID;
                mainArmyTroops = Tools.CalculSumUintArray(main.troops);
                armyNeedsMaint = !main.isMaintained && home.treasury >= (int)main.maintCost;
                armyCanRecruit = mainArmyTroops < 50
                              && !current.hasRecruited
                              && current.ownerID == gs.myPC.charID
                              && gs.tryGetArmyFiefID(main.armyID, out string armyFief)
                              && armyFief == current.fiefID;
            }

            // Pre-compute whether offensive actions are valid in the current fief
            bool armyHere = hasArmy
                         && gs.tryGetArmyFiefID(mainArmyID ?? "", out string af)
                         && af == current.fiefID;
            bool canBesiege  = !current.ownerID.Equals(gs.myPC.charID)
                            && string.IsNullOrWhiteSpace(current.siege)
                            && armyHere;
            bool canPillage  = !current.ownerID.Equals(gs.myPC.charID)
                            && !current.isPillaged
                            && armyHere;

            var mil = new MilitaryContext
            {
                Threat            = threat,
                HasArmy           = hasArmy,
                MainArmyID        = mainArmyID,
                MainArmyTroops    = mainArmyTroops,
                ArmyNeedsMaint    = armyNeedsMaint,
                ArmyCanRecruit    = armyCanRecruit,
                IsUnderSiege      = underSiege,
                HasActiveSiege    = activeSiege,
                ActiveSiegeID     = siege?.siegeID,
                BesiegedFiefID    = siege?.besiegedFiefID,
                BesiegedKeepLevel = siege != null ? (float)siege.keepLevel : 0f,
                EnemyArmyIDInFief = enemyArmyIDInFief,
                CanBesiegeCurrent = canBesiege,
                CanPillageCurrent = canPillage,
            };

            // ── DiplomaticContext ─────────────────────────────────────────────
            bool canTryForChild = false;
            if (!string.IsNullOrWhiteSpace(gs.myPC.spouse)
                && gs.tryGetCharacter(gs.myPC.spouse, out RedProtoCharacter spouse))
            {
                canTryForChild = !spouse.isPregnant
                    && spouse.location == current.fiefID
                    && spouse.days > 1
                    && (string.IsNullOrWhiteSpace(current.siege)
                        || gs.myPC.inKeep == spouse.inKeep);
            }

            // Hirable NPC (male, unattached, adult, in current fief)
            var hirableNPC = gs.notMyNPCs.FirstOrDefault(n =>
                n.isMale && string.IsNullOrWhiteSpace(n.captor)
                && string.IsNullOrWhiteSpace(n.familyID)
                && n.birthYear > 0 && (gs.currentYear - n.birthYear) >= 14);
            uint hireOffer = 0;
            if (hirableNPC != null && estimateSalary != null)
                hireOffer = (uint)Math.Ceiling(estimateSalary(hirableNPC) * 1.1);

            // Firable employee (cheapest employee whose salary is below the threshold)
            RedProtoNPC fireable = null;
            if (estimateSalary != null)
                fireable = gs.myEmployeeNPCs
                    .Where(n => n.days > 0 && estimateSalary(n) <= 1_500)
                    .OrderBy(n => estimateSalary(n))
                    .FirstOrDefault();

            // Heir candidate (eldest eligible male family member)
            string heirCandID = null;
            bool hasUnappointedHeir = string.IsNullOrWhiteSpace(gs.myPC.myHeirId);
            if (hasUnappointedHeir)
            {
                heirCandID = gs.myFamilyNPCs
                    .Where(n => n.isMale && string.IsNullOrWhiteSpace(n.captor)
                             && !string.IsNullOrWhiteSpace(n.familyID)
                             && n.familyID == gs.myPC.familyID
                             && (gs.currentYear - n.birthYear) >= 14)
                    .OrderByDescending(n => n.management)
                    .Select(n => n.charID)
                    .FirstOrDefault();
                hasUnappointedHeir = heirCandID != null;
            }

            // Allied captives we are holding
            string alliedCaptiveID = gs.myCaptiveIDs
                .FirstOrDefault(c => gs.allyPCids.Contains(c));

            var dip = new DiplomaticContext
            {
                CanTryForChild     = canTryForChild,
                HasUnappointedHeir = hasUnappointedHeir,
                HeirCandidateID    = heirCandID,
                HasHirableNPC      = hirableNPC != null,
                HirableNPCID       = hirableNPC?.charID,
                HireOfferAmount    = hireOffer,
                EmployeeCount      = gs.myEmployeeNPCs.Count,
                HasFirableEmployee = fireable != null,
                FireableEmployeeID = fireable?.charID,
                HasAlliedCaptives  = alliedCaptiveID != null,
                AlliedCaptiveID    = alliedCaptiveID,
            };

            return new AgentState(chr, econ, mil, dip, gs.currentYear);
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static string FindBailiffCandidate(GameState gs, string fiefID)
        {
            return gs.myEmployeeNPCs
                .Where(n => n.isMale && string.IsNullOrWhiteSpace(n.captor)
                         && (gs.currentYear - n.birthYear) >= 14
                         && n.location == fiefID)
                .OrderByDescending(n => n.management)
                .Select(n => n.charID)
                .FirstOrDefault();
        }
    }
}

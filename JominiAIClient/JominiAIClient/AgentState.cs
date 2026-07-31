using System;
using System.Collections.Generic;
using System.Linq;

namespace JominiAI
{
    // The player character's personal situation.
    public class CharacterContext
    {
        public string CharID { get; init; }
        public string CurrentFiefID { get; init; }
        public string HomeFiefID { get; init; }
        public double DaysRemaining { get; init; }
        public bool InKeep { get; init; }
        public bool IsAlive { get; init; }
        public bool IsCaptive { get; init; }
        public bool HasSpouse { get; init; }
        public bool HasHeir { get; init; }
        public string HeirId { get; init; }       // current heir's charID, or null - lets a run show WHEN the heir changes (a new son appointed vs. the heir dying) rather than just a true/false flag
        public string FamilyID { get; init; }
    }

    // Treasury state, fief management and fund redistribution opportunities.
    public class EconomicContext
    {
        public int HomeTreasury { get; init; }
        public int TotalTreasury { get; init; }        // sum across all owned fiefs
        public int FiefCount { get; init; }
        public int FiefsInDeficit { get; init; }       // treasury < 0
        public int FiefsWithBailiff { get; init; }

        public bool HomeIsInDeficit { get; init; }
        public bool IsCritical { get; init; }          // home treasury < 1000
        public bool HasRecovered { get; init; }        // home treasury >= 10000

        // Fiefs still needing expenditure adjustment this season (deficit, not yet adjusted).
        public IReadOnlyList<string> FiefsNeedingExpAdjustment { get; init; }

        public bool HomeNeedsBailiff { get; init; }
        public string HomeBailiffCandidate { get; init; }
        public double HomeBailiffCandidateFit { get; init; }   // (stature + management) / 2

        // First other fief that needs a bailiff and has an eligible NPC on the spot.
        public string OtherFiefNeedingBailiff { get; init; }
        public string OtherFiefBailiffCandidate { get; init; }

        // Richest non-home fief above the surplus threshold, and half its treasury.
        public bool HasSurplusFief { get; init; }
        public string SurplusFiefID { get; init; }
        public int SurplusTransferAmount { get; init; }
    }

    public enum ThreatLevel
    {
        None = 0,          // no known threats
        EnemyNearby = 1,   // enemy army in one of my fiefs (not the current one)
        EnemyPresent = 2,  // enemy besieging my fief, or enemy army in the current fief
        ActiveCombat = 3   // I'm conducting a siege, or enemy army here and I have an army
    }

    // Armies, threats and siege management.
    public class MilitaryContext
    {
        public ThreatLevel Threat { get; init; }
        public bool HasArmy { get; init; }
        public string MainArmyID { get; init; }
        public uint MainArmyTroops { get; init; }
        public bool ArmyNeedsMaint { get; init; }
        public bool ArmyCanRecruit { get; init; }
        public bool IsUnderSiege { get; init; }
        public bool HasActiveSiege { get; init; }   // the player is besieging someone

        // The siege in the player's current fief (null when the player isn't there).
        public string ActiveSiegeID { get; init; }
        public string BesiegedFiefID { get; init; }
        public float BesiegedKeepLevel { get; init; }

        public string EnemyArmyIDInFief { get; init; }
        public bool CanBesiegeCurrent { get; init; }
        public bool CanPillageCurrent { get; init; }

        public bool ArmyNeedsLeader { get; init; }
        public string ArmyLeaderCandidateID { get; init; }
        public double ArmyLeaderCandidateFit { get; init; }   // the candidate's combat rating

        public bool CanBuildArmy { get; init; }        // no army yet, but the PC can raise one here
        public bool PCLeadsMainArmy { get; init; }     // PC leads the main army (so it marches with him)
        public string WarTargetFiefID { get; init; }   // cheapest reachable non-owned fief to besiege
        public string MarchDirection { get; init; }    // an affordable adjacent direction to march when no enemy fief is known yet
    }

    // Family, NPCs, captives and dynastic opportunities.
    public class DiplomaticContext
    {
        public bool CanTryForChild { get; init; }
        public bool HasUnappointedHeir { get; init; }
        public string HeirCandidateID { get; init; }

        public bool HasHirableNPC { get; init; }
        public string HirableNPCID { get; init; }
        public uint HireOfferAmount { get; init; }   // salary * 1.1
        public int EmployeeCount { get; init; }
        public int FamilyMemberCount { get; init; }   // living family NPCs (sons, brothers, etc.); the dynasty's depth of succession, so a run shows whether a lost heir had a spare behind him
        public int LivingSonCount { get; init; }       // male family members of the PC's own family - the pool a future heir can come from

        public bool HasFirableEmployee { get; init; }
        public string FireableEmployeeID { get; init; }

        public bool HasAlliedCaptives { get; init; }
        public string AlliedCaptiveID { get; init; }

        public bool HasMarriageOpportunity { get; init; }        // PC can propose to an eligible NPC
        public string MarriageCandidateID { get; init; }
        public bool HasChildMarriageOpportunity { get; init; }   // a male family NPC can propose
        public string ChildForMarriageID { get; init; }          // the family NPC who should propose
        public string ChildMarriageCandidateID { get; init; }    // the bride candidate

        public bool HasReceivedProposal { get; init; }

        public bool HasEntourageCandidate { get; init; }
        public string EntourageCandidateID { get; init; }
    }

    // Normalised scores used for display and as the Q-learning observation vector.
    // The vector is a fixed-size float array and never changes length.
    public class DerivedMetrics
    {
        public const int ObservationSize = 12;

        public float EconomicHealth { get; }    // 0 = bankrupt, 1 = wealthy
        public float MilitaryStrength { get; }  // 0 = no army, 1 = large army
        public float BailiffCoverage { get; }   // fraction of fiefs with a bailiff
        public float ThreatScore { get; }       // 0 = peaceful, 1 = at war
        public float DaysFraction { get; }       // remaining days / 90

        private readonly float[] _vec;

        internal DerivedMetrics(CharacterContext chr, EconomicContext econ,
                                MilitaryContext mil, DiplomaticContext dip)
        {
            EconomicHealth = Math.Max(0f, Math.Min(1f, econ.HomeTreasury / 100_000f));
            MilitaryStrength = Math.Min(1f, mil.MainArmyTroops / 500f);
            BailiffCoverage = econ.FiefCount > 0 ? (float)econ.FiefsWithBailiff / econ.FiefCount : 0f;
            ThreatScore = (float)mil.Threat / 3f;
            DaysFraction = (float)(chr.DaysRemaining / 90.0);

            float deficitFrac = econ.FiefCount > 0 ? (float)econ.FiefsInDeficit / econ.FiefCount : 0f;

            _vec = new float[ObservationSize]
            {
                EconomicHealth,                     // 0  home treasury (normalised)
                deficitFrac,                        // 1  fraction of fiefs in deficit
                BailiffCoverage,                    // 2  fraction of fiefs with bailiff
                MilitaryStrength,                   // 3  army size (normalised)
                ThreatScore,                        // 4  threat level (normalised)
                DaysFraction,                       // 5  days remaining (normalised)
                econ.IsCritical ? 1f : 0f,          // 6  treasury critical flag
                econ.HasRecovered ? 1f : 0f,        // 7  treasury recovered flag
                mil.HasArmy ? 1f : 0f,              // 8  has army
                chr.InKeep ? 1f : 0f,               // 9  character in keep
                dip.CanTryForChild ? 1f : 0f,       // 10 spouse available for a child
                dip.HasUnappointedHeir ? 1f : 0f,   // 11 heir needed
            };
        }

        // Fresh copy each call so callers can mutate the result safely.
        public float[] ToObservationVector() => (float[])_vec.Clone();
    }

    public class AgentState
    {
        // Each rejected wage offer is raised by this much, up to this multiple of our own
        // salary estimate. The engine only accepts an offer within 10% of what the man is
        // really worth, and it will not look at one that fails to beat our previous bid.
        private const double HireRaise = 1.25;
        private const double MaxHireMultiple = 4.0;

        // The server charges a random 1-5 days for a recruitment attempt and refuses outright
        // if the character cannot cover the roll, so anything less than 5 days in hand is a
        // gamble that wastes the action when it loses. Ask only when the whole cost is covered.
        private const int RecruitMaxDays = 5;

        public CharacterContext Character { get; }
        public EconomicContext Economy { get; }
        public MilitaryContext Military { get; }
        public DiplomaticContext Diplomacy { get; }
        public DerivedMetrics Metrics { get; }
        public int Year { get; }

        public AgentState(CharacterContext character, EconomicContext economy,
                          MilitaryContext military, DiplomaticContext diplomacy,
                          int year)
        {
            Character = character ?? throw new ArgumentNullException(nameof(character));
            Economy = economy ?? throw new ArgumentNullException(nameof(economy));
            Military = military ?? throw new ArgumentNullException(nameof(military));
            Diplomacy = diplomacy ?? throw new ArgumentNullException(nameof(diplomacy));
            Year = year;
            Metrics = new DerivedMetrics(character, economy, military, diplomacy);
        }

        // Builds the state from a server-fetched GameState.
        // adjustedFiefIDs are fiefs whose expenditure was already adjusted this season.
        // estimateSalary points at Agent.estimateNPCacceptableSalary and is passed in so
        // this class doesn't have to depend on Agent directly.
        public static AgentState Build(
            GameState gs,
            HashSet<string> adjustedFiefIDs,
            HashSet<string> entouragedIDs,
            Func<RedProtoNPC, double> estimateSalary,
            Dictionary<string, double> travelCosts = null,
            Dictionary<MoveDirections, double> adjacentCosts = null,
            HashSet<string> bailiffBlockedFiefIDs = null)
        {
            if (gs.myPC == null)
                throw new InvalidOperationException("GameState has no player character.");

            RedProtoFief home = gs.getHomeFief();
            RedProtoFief current = gs.getCurrentFief();

            var chr = new CharacterContext
            {
                CharID = gs.myPC.charID,
                CurrentFiefID = current.fiefID,
                HomeFiefID = home.fiefID,
                DaysRemaining = gs.myPC.days,
                InKeep = gs.myPC.inKeep,
                IsAlive = gs.myPC.isAlive,
                IsCaptive = !string.IsNullOrWhiteSpace(gs.myPC.captor),
                HasSpouse = !string.IsNullOrWhiteSpace(gs.myPC.spouse),
                HasHeir = !string.IsNullOrWhiteSpace(gs.myPC.myHeirId),
                HeirId = gs.myPC.myHeirId,
                FamilyID = gs.myPC.familyID,
            };

            // Accumulate in long: int Sum() is checked and threw an OverflowException once
            // the (runaway, see FiefTransferFunds money-creation issue) total passed int.MaxValue.
            // Clamp back into the int field so state-building never crashes on an inflated total.
            long treasurySum = gs.myFiefs.Sum(f => (long)f.treasury);
            int totalTreasury = (int)Math.Clamp(treasurySum, int.MinValue, int.MaxValue);
            int fiefsInDeficit = gs.myFiefs.Count(f => f.treasury < 0);
            int fiefsWithBailiff = gs.myFiefs.Count(f => !string.IsNullOrWhiteSpace(f.bailiffID));

            var needAdj = gs.myFiefs
                .Where(f => f.treasury < 0 && !adjustedFiefIDs.Contains(f.fiefID))
                .Select(f => f.fiefID)
                .ToList();

            // A bailiff only has to be a male employee of age - the server imposes no
            // co-location requirement - so any eligible employee can cover any fief, and one
            // NPC may hold any number of them.
            //
            // Spread the posts evenly instead of always handing them to the best-fitting NPC.
            // Ranking on fit alone made one man bailiff of nearly every fief in the realm, and
            // because the server strips a dead character of all his posts at once, his death
            // wiped out coverage everywhere and the agent then spent whole seasons re-appointing.
            // Balancing on load means a death costs only that man's share.
            var bailiffLoad = new Dictionary<string, int>();
            foreach (var f in gs.myFiefs)
                if (!string.IsNullOrWhiteSpace(f.bailiffID))
                    bailiffLoad[f.bailiffID] = bailiffLoad.TryGetValue(f.bailiffID, out int n) ? n + 1 : 1;

            bool BailiffBlocked(string fiefID) =>
                bailiffBlockedFiefIDs != null && bailiffBlockedFiefIDs.Contains(fiefID);

            RedProtoNPC homeCandNpc = string.IsNullOrWhiteSpace(home.bailiffID) && !BailiffBlocked(home.fiefID)
                ? FindBailiffCandidate(gs, bailiffLoad) : null;
            string homeCand = homeCandNpc?.charID;
            double homeCandFit = homeCandNpc != null ? RoleFit.Bailiff(homeCandNpc) : 0;

            // Count the home post against the candidate so the next fief this tick doesn't
            // simply pick the same man again.
            var otherLoad = new Dictionary<string, int>(bailiffLoad);
            if (homeCand != null)
                otherLoad[homeCand] = otherLoad.TryGetValue(homeCand, out int h) ? h + 1 : 1;
            RedProtoNPC otherCandNpc = FindBailiffCandidate(gs, otherLoad);

            string otherFief = null, otherCand = null;
            if (otherCandNpc != null)
                foreach (var fief in gs.myFiefs)
                {
                    if (!string.IsNullOrWhiteSpace(fief.bailiffID) || fief.fiefID == home.fiefID
                        || BailiffBlocked(fief.fiefID))
                        continue;
                    otherFief = fief.fiefID; otherCand = otherCandNpc.charID; break;
                }

            var surplusFief = gs.myFiefs
                .Where(f => f.fiefID != home.fiefID && f.treasury > 5_000)
                .OrderByDescending(f => f.treasury)
                .FirstOrDefault();

            var econ = new EconomicContext
            {
                HomeTreasury = (int)home.treasury,
                TotalTreasury = totalTreasury,
                FiefCount = gs.myFiefs.Count,
                FiefsInDeficit = fiefsInDeficit,
                FiefsWithBailiff = fiefsWithBailiff,
                HomeIsInDeficit = home.treasury < 0,
                IsCritical = home.treasury < 1_000,
                HasRecovered = home.treasury >= 10_000,
                FiefsNeedingExpAdjustment = needAdj,
                HomeNeedsBailiff = string.IsNullOrWhiteSpace(home.bailiffID),
                HomeBailiffCandidate = homeCand,
                HomeBailiffCandidateFit = homeCandFit,
                OtherFiefNeedingBailiff = otherFief,
                OtherFiefBailiffCandidate = otherCand,
                HasSurplusFief = surplusFief != null,
                SurplusFiefID = surplusFief?.fiefID,
                SurplusTransferAmount = surplusFief != null ? (int)(surplusFief.treasury / 2.0) : 0,
            };

            string enemyArmyIDInFief = null;
            if (current.armyIDs != null)
                foreach (string id in current.armyIDs)
                    if (gs.tryGetArmy(id, out RedProtoArmy a) && gs.enemyPCids.Contains(a.ownerName))
                    { enemyArmyIDInFief = id; break; }

            bool underSiege = gs.siegeOnMyFiefs.Count > 0;
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

            var siege = gs.mySieges.FirstOrDefault(s => s.besiegedFiefID == current.fiefID);

            bool hasArmy = false; string mainArmyID = null; uint mainArmyTroops = 0;
            bool armyNeedsMaint = false, armyCanRecruit = false, pcLeadsMainArmy = false;
            if (gs.tryGetMainArmy(out RedProtoArmy main))
            {
                hasArmy = true;
                mainArmyID = main.armyID;
                mainArmyTroops = Tools.CalculSumUintArray(main.troops);
                // leaderID, not leaderName: the name field is "firstName familyName" and never
                // equals a charID, so this test was false even when the PC was already leading.
                // The aggressive path keyed off it, so the agent re-appointed him every season,
                // the server refused ("already leading this army"), and the war party never
                // marched - every appointment in every war run failed on this one comparison.
                pcLeadsMainArmy = !string.IsNullOrWhiteSpace(main.leaderID)
                               && main.leaderID == gs.myPC.charID;
                armyNeedsMaint = !main.isMaintained && home.treasury >= (int)main.maintCost;
                armyCanRecruit = mainArmyTroops < 50
                              && !current.hasRecruited
                              && current.ownerID == gs.myPC.charID
                              && gs.myPC.days >= RecruitMaxDays
                              && gs.tryGetArmyFiefID(main.armyID, out string armyFief)
                              && armyFief == current.fiefID;
            }

            // Raise a first army: no army yet, standing in an owned fief that can still
            // recruit this season, not under siege, with plenty of gold in the home
            // treasury so recruitment stays clear of the server's insufficient-funds path.
            bool canBuildArmy = !hasArmy
                             && current.ownerID == gs.myPC.charID
                             && !current.hasRecruited
                             && string.IsNullOrWhiteSpace(current.siege)
                             && gs.myPC.days >= RecruitMaxDays
                             && home.treasury > 50_000;   // enough to cover a first recruit; the old 250k gate meant a war party never formed within a run

            // Cheapest-to-reach fief we know but don't own, within the PC's day budget.
            // Only used by the aggressive FSM mode to pick something to march on.
            string warTargetFiefID = null;
            if (travelCosts != null && gs.notMyFiefs != null)
            {
                double bestCost = double.MaxValue;
                foreach (var f in gs.notMyFiefs)
                {
                    if (f == null || string.IsNullOrWhiteSpace(f.fiefID)) continue;
                    if (!travelCosts.TryGetValue(f.fiefID, out double cost)) continue;
                    if (cost <= gs.myPC.days && cost < bestCost)
                    { bestCost = cost; warTargetFiefID = f.fiefID; }
                }
            }

            bool armyHere = hasArmy
                         && gs.tryGetArmyFiefID(mainArmyID ?? "", out string af)
                         && af == current.fiefID;
            bool canBesiege = !current.ownerID.Equals(gs.myPC.charID)
                            && string.IsNullOrWhiteSpace(current.siege)
                            && armyHere;
            bool canPillage = !current.ownerID.Equals(gs.myPC.charID)
                            && !current.isPillaged
                            && armyHere;

            // Army leader: main army has no leader and there's a male PC/employee at its fief.
            // Pick the best fighter there rather than the first available male, since battle
            // resolution scales with the leader's combat rating.
            bool armyNeedsLeader = false;
            string armyLeaderCandID = null;
            double armyLeaderCandFit = 0;
            if (hasArmy && string.IsNullOrWhiteSpace(main?.leaderID)
                && gs.tryGetArmyFiefID(mainArmyID ?? "", out string armyLeaderFief))
            {
                RedProtoCharacter leader = gs.myEmployeeNPCs.Concat<RedProtoCharacter>(new[] { gs.myPC })
                    .Where(cand => cand.isMale && cand.location == armyLeaderFief)
                    .OrderByDescending(cand => RoleFit.Leader(cand))
                    .FirstOrDefault();
                if (leader != null)
                {
                    armyNeedsLeader = true;
                    armyLeaderCandID = leader.charID;
                    armyLeaderCandFit = RoleFit.Leader(leader);
                }
            }

            // When no enemy fief is known yet, pick a single affordable adjacent direction to
            // march. Directions are tried in enum order (E first), so the war party heads
            // consistently one way towards a border rather than oscillating on cost.
            string marchDir = null;
            if (adjacentCosts != null && adjacentCosts.Count > 0)
                marchDir = adjacentCosts
                    .Where(kv => kv.Value <= gs.myPC.days)
                    .OrderBy(kv => (int)kv.Key)
                    .Select(kv => kv.Key.ToString())
                    .FirstOrDefault();

            var mil = new MilitaryContext
            {
                Threat = threat,
                HasArmy = hasArmy,
                MainArmyID = mainArmyID,
                MainArmyTroops = mainArmyTroops,
                ArmyNeedsMaint = armyNeedsMaint,
                ArmyCanRecruit = armyCanRecruit,
                IsUnderSiege = underSiege,
                HasActiveSiege = activeSiege,
                ActiveSiegeID = siege?.siegeID,
                BesiegedFiefID = siege?.besiegedFiefID,
                BesiegedKeepLevel = siege != null ? (float)siege.keepLevel : 0f,
                EnemyArmyIDInFief = enemyArmyIDInFief,
                CanBesiegeCurrent = canBesiege,
                CanPillageCurrent = canPillage,
                ArmyNeedsLeader = armyNeedsLeader,
                ArmyLeaderCandidateID = armyLeaderCandID,
                ArmyLeaderCandidateFit = armyLeaderCandFit,
                CanBuildArmy = canBuildArmy,
                PCLeadsMainArmy = pcLeadsMainArmy,
                WarTargetFiefID = warTargetFiefID,
                MarchDirection = marchDir,
            };

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

            // Who we could hire, and what to offer him.
            //
            // Haggle rather than repeat one number. Our salary estimate is only a guess - the
            // server prices a man on his traits, his stature and what he already earns, none of
            // which we can see - so a first offer is usually turned down as too low. The engine
            // expects a negotiation and will not look at an offer that fails to beat our last
            // one, so the bid goes up each time. Without this the agent re-sent the same
            // rejected number every pass and spent the whole season on one man it never hired.
            //
            // Someone we have already bid up past what he looks worth, or past what we can pay,
            // is skipped so the next candidate gets a turn - picking the first man on the list
            // and then giving up on him left everyone behind him unhired.
            RedProtoNPC hirableNPC = null;
            uint hireOffer = 0;
            if (estimateSalary != null)
                foreach (RedProtoNPC n in gs.notMyNPCs)
                {
                    // lastOfferID is only filled in for a man with no family and no employer,
                    // so it stands in for "the server will entertain an offer for this one".
                    bool eligible = n.isMale
                        && string.IsNullOrWhiteSpace(n.captor)
                        && string.IsNullOrWhiteSpace(n.familyID)
                        && !string.IsNullOrWhiteSpace(n.lastOfferID)
                        && n.birthYear > 0 && (gs.currentYear - n.birthYear) >= 14;
                    if (!eligible) continue;

                    double estimate = estimateSalary(n);
                    // The engine accepts any offer more than 10% above what it prices the man
                    // at, and only rolls the dice in the band below that. Opening at 1.1 sat
                    // exactly on that line, so a first offer went to a coin toss and usually
                    // lost; opening just clear of it buys the man outright in one action when
                    // we can afford him, instead of over several seasons of haggling.
                    double next = n.lastOfferAmount > 0
                        ? n.lastOfferAmount * HireRaise
                        : estimate * 1.15;
                    double ceiling = Math.Min(estimate * MaxHireMultiple, Math.Max(0, home.treasury));
                    if (next > ceiling) continue;

                    hirableNPC = n;
                    hireOffer = (uint)Math.Ceiling(next);
                    break;
                }

            // Firable employee: cheapest one whose salary is below the threshold.
            RedProtoNPC fireable = null;
            if (estimateSalary != null)
                fireable = gs.myEmployeeNPCs
                    .Where(n => n.days > 0 && estimateSalary(n) <= 1_500)
                    .OrderBy(n => estimateSalary(n))
                    .FirstOrDefault();

            // Heir candidate. The server (ChecksForHeir) only wants a male family member of the
            // same family, with no age gate, and GetHeir promotes a son/brother of any age at
            // death. Match that here so a young son still counts instead of leaving the PC
            // heirless, preferring the oldest like GetHeir does.
            string heirCandID = null;
            bool hasUnappointedHeir = string.IsNullOrWhiteSpace(gs.myPC.myHeirId);
            if (hasUnappointedHeir)
            {
                heirCandID = gs.myFamilyNPCs
                    .Where(n => n.isMale && string.IsNullOrWhiteSpace(n.captor)
                             && !string.IsNullOrWhiteSpace(n.familyID)
                             && n.familyID == gs.myPC.familyID
                             && n.birthYear > 0)
                    .OrderByDescending(n => gs.currentYear - n.birthYear)
                    .Select(n => n.charID)
                    .FirstOrDefault();
                hasUnappointedHeir = heirCandID != null;
            }

            string alliedCaptiveID = gs.myCaptiveIDs
                .FirstOrDefault(c => gs.allyPCids.Contains(c));

            bool pcIsUnmarried = string.IsNullOrWhiteSpace(gs.myPC.spouse)
                              && string.IsNullOrWhiteSpace(gs.myPC.fiancee);
            gs.myCharsWhoProposedThisSeason.TryGetValue(gs.myPC.charID, out bool pcAlreadyProposed);
            RedProtoNPC marriageTarget = pcIsUnmarried && !pcAlreadyProposed
                ? gs.notMyNPCs.FirstOrDefault(n =>
                    !n.isMale
                    && string.IsNullOrWhiteSpace(n.spouse)
                    && string.IsNullOrWhiteSpace(n.fiancee)
                    && string.IsNullOrWhiteSpace(n.captor)
                    && !string.IsNullOrWhiteSpace(n.familyID)
                    && n.familyID != gs.myPC.familyID
                    && n.birthYear > 0
                    && (gs.currentYear - n.birthYear) >= 14)
                : null;

            RedProtoNPC femaleForChild = gs.notMyNPCs.FirstOrDefault(n =>
                !n.isMale
                && string.IsNullOrWhiteSpace(n.spouse)
                && string.IsNullOrWhiteSpace(n.fiancee)
                && string.IsNullOrWhiteSpace(n.captor)
                && !string.IsNullOrWhiteSpace(n.familyID)
                && n.familyID != gs.myPC.familyID
                && n.birthYear > 0
                && (gs.currentYear - n.birthYear) >= 14);

            RedProtoNPC childGroom = null;
            if (femaleForChild != null)
            {
                childGroom = gs.myFamilyNPCs.FirstOrDefault(n =>
                {
                    gs.myCharsWhoProposedThisSeason.TryGetValue(n.charID, out bool alreadyProp);
                    return n.isMale
                        && string.IsNullOrWhiteSpace(n.spouse)
                        && string.IsNullOrWhiteSpace(n.fiancee)
                        && !alreadyProp
                        && n.birthYear > 0
                        && (gs.currentYear - n.birthYear) >= 14;
                });
            }

            bool hasReceivedProposal = gs.receivedProposalFromThisChar != null && pcIsUnmarried;

            // Entourage: first owned NPC in the current fief not yet in the entourage and not
            // already added this season (the server's inEntourage refresh is unreliable).
            var entourageNPC = gs.myFamilyNPCs.Concat(gs.myEmployeeNPCs)
                .FirstOrDefault(n => !n.inEntourage && n.location == current.fiefID
                                  && (entouragedIDs == null || !entouragedIDs.Contains(n.charID)));

            var dip = new DiplomaticContext
            {
                CanTryForChild = canTryForChild,
                HasUnappointedHeir = hasUnappointedHeir,
                HeirCandidateID = heirCandID,
                HasHirableNPC = hirableNPC != null,
                HirableNPCID = hirableNPC?.charID,
                HireOfferAmount = hireOffer,
                EmployeeCount = gs.myEmployeeNPCs.Count,
                FamilyMemberCount = gs.myFamilyNPCs.Count,
                LivingSonCount = gs.myFamilyNPCs.Count(n =>
                    n.isMale && !string.IsNullOrWhiteSpace(n.familyID)
                    && n.familyID == gs.myPC.familyID),
                HasFirableEmployee = fireable != null,
                FireableEmployeeID = fireable?.charID,
                HasAlliedCaptives = alliedCaptiveID != null,
                AlliedCaptiveID = alliedCaptiveID,
                HasMarriageOpportunity = marriageTarget != null,
                MarriageCandidateID = marriageTarget?.charID,
                HasChildMarriageOpportunity = childGroom != null,
                ChildForMarriageID = childGroom?.charID,
                ChildMarriageCandidateID = femaleForChild?.charID,
                HasReceivedProposal = hasReceivedProposal,
                HasEntourageCandidate = entourageNPC != null,
                EntourageCandidateID = entourageNPC?.charID,
            };

            return new AgentState(chr, econ, mil, dip, gs.currentYear);
        }

        // Best eligible employee for a bailiff post: the one holding the fewest fiefs already,
        // breaking ties on fit. Ordering on fit first would give every post in the realm to the
        // same man, which is both fragile (see bailiffLoad above) and no better run day to day.
        private static RedProtoNPC FindBailiffCandidate(GameState gs, Dictionary<string, int> bailiffLoad)
        {
            return gs.myEmployeeNPCs
                .Where(n => n.isMale && string.IsNullOrWhiteSpace(n.captor)
                         && (gs.currentYear - n.birthYear) >= 14)
                .OrderBy(n => bailiffLoad != null && bailiffLoad.TryGetValue(n.charID, out int held) ? held : 0)
                .ThenByDescending(n => RoleFit.Bailiff(n))
                .FirstOrDefault();
        }
    }

    // Different roles reward different stats: a fief's income and loyalty scale with its
    // bailiff's (stature + management), while battle resolution scales with the leader's
    // combat rating, so it's worth matching the person to the job.
    internal static class RoleFit
    {
        public static double Bailiff(RedProtoCharacter n) => (n.stature + n.management) / 2.0;
        public static double Leader(RedProtoCharacter n) => n.combat;
    }
}

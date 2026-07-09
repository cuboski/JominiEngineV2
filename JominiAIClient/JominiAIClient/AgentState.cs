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
            Func<RedProtoNPC, double> estimateSalary)
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
                FamilyID = gs.myPC.familyID,
            };

            int totalTreasury = (int)gs.myFiefs.Sum(f => f.treasury);
            int fiefsInDeficit = gs.myFiefs.Count(f => f.treasury < 0);
            int fiefsWithBailiff = gs.myFiefs.Count(f => !string.IsNullOrWhiteSpace(f.bailiffID));

            var needAdj = gs.myFiefs
                .Where(f => f.treasury < 0 && !adjustedFiefIDs.Contains(f.fiefID))
                .Select(f => f.fiefID)
                .ToList();

            RedProtoNPC homeCandNpc = string.IsNullOrWhiteSpace(home.bailiffID)
                ? FindBailiffCandidate(gs, home.fiefID) : null;
            string homeCand = homeCandNpc?.charID;
            double homeCandFit = homeCandNpc != null ? RoleFit.Bailiff(homeCandNpc) : 0;

            string otherFief = null, otherCand = null;
            foreach (var fief in gs.myFiefs)
            {
                if (!string.IsNullOrWhiteSpace(fief.bailiffID) || fief.fiefID == home.fiefID)
                    continue;
                RedProtoNPC c = FindBailiffCandidate(gs, fief.fiefID);
                if (c != null) { otherFief = fief.fiefID; otherCand = c.charID; break; }
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
            bool armyNeedsMaint = false, armyCanRecruit = false;
            if (gs.tryGetMainArmy(out RedProtoArmy main))
            {
                hasArmy = true;
                mainArmyID = main.armyID;
                mainArmyTroops = Tools.CalculSumUintArray(main.troops);
                armyNeedsMaint = !main.isMaintained && home.treasury >= (int)main.maintCost;
                armyCanRecruit = mainArmyTroops < 50
                              && !current.hasRecruited
                              && current.ownerID == gs.myPC.charID
                              && gs.tryGetArmyFiefID(main.armyID, out string armyFief)
                              && armyFief == current.fiefID;
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
            if (hasArmy && string.IsNullOrWhiteSpace(main?.leaderName)
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

            // Hirable NPC: male, unattached, adult, in the current fief.
            var hirableNPC = gs.notMyNPCs.FirstOrDefault(n =>
                n.isMale && string.IsNullOrWhiteSpace(n.captor)
                && string.IsNullOrWhiteSpace(n.familyID)
                && n.birthYear > 0 && (gs.currentYear - n.birthYear) >= 14);
            uint hireOffer = 0;
            if (hirableNPC != null && estimateSalary != null)
                hireOffer = (uint)Math.Ceiling(estimateSalary(hirableNPC) * 1.1);

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

        private static RedProtoNPC FindBailiffCandidate(GameState gs, string fiefID)
        {
            return gs.myEmployeeNPCs
                .Where(n => n.isMale && string.IsNullOrWhiteSpace(n.captor)
                         && (gs.currentYear - n.birthYear) >= 14
                         && n.location == fiefID)
                .OrderByDescending(n => RoleFit.Bailiff(n))
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

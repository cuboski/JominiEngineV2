using ProtoMessageClient;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JominiServer.AI
{
    public static class StateEncoder
    {
        public static GameState Encode(
            ProtoClient clientState,
            List<ProtoFief> ownedFiefs,
            List<ProtoArmy> ownedArmies,
            List<ProtoArmyOverview> visibleArmies)
        {
            var state = new GameState
            {
                Year = clientState.Year,
                Season = clientState.Season,
                Purse = clientState.Purse,
                Treasury = clientState.HomeFiefTreasury
            };

            if (ownedFiefs != null && ownedFiefs.Count > 0)
            {
                state.Loyalty = ownedFiefs.Average(f => f.Loyalty);
                state.OwnedFiefCount = ownedFiefs.Count;
            }

            if (ownedArmies != null)
            {
                foreach (var army in ownedArmies)
                {
                    if (army.Troops != null)
                    {
                        foreach (uint t in army.Troops)
                            state.ArmySize += (int)t;
                    }
                }
            }

            string playerID = clientState.PlayerCharacter?.ID;
            if (visibleArmies != null && playerID != null)
            {
                state.ThreatNearby = visibleArmies.Any(a =>
                    a.OwnerID != null && !a.OwnerID.Equals(playerID));
            }

            if (ownedArmies != null)
            {
                state.AtWar = ownedArmies.Any(a =>
                    !string.IsNullOrEmpty(a.SiegeStatus));
            }

            if (!state.AtWar && ownedFiefs != null)
            {
                state.AtWar = ownedFiefs.Any(f =>
                    !string.IsNullOrEmpty(f.SiegeID));
            }

            return state;
        }

        public static int DiscretizeTreasury(double treasury)
        {
            if (treasury <= 0) return 0;
            if (treasury < 500) return 1;
            if (treasury < 2000) return 2;
            return 3;
        }

        public static int DiscretizeLoyalty(double loyalty)
        {
            if (loyalty < 3.0) return 0;
            if (loyalty < 5.0) return 1;
            if (loyalty < 7.0) return 2;
            return 3;
        }

        public static int DiscretizeArmy(int armySize)
        {
            if (armySize == 0) return 0;
            if (armySize < 500) return 1;
            if (armySize < 2000) return 2;
            return 3;
        }

        public static int DiscretizeThreat(bool threat)
        {
            return threat ? 1 : 0;
        }

        public static int EncodeDiscreteState(GameState state)
        {
            int t = DiscretizeTreasury(state.Treasury);
            int l = DiscretizeLoyalty(state.Loyalty);
            int a = DiscretizeArmy(state.ArmySize);
            int th = DiscretizeThreat(state.ThreatNearby || state.AtWar);

            return t * 32 + l * 8 + a * 2 + th;
        }

        public const int StateCount = 4 * 4 * 4 * 2;

        public static readonly int ActionCount = Enum.GetValues(typeof(MacroAction)).Length;
    }
}
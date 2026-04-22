using System;

namespace JominiServer.AI
{
    public enum FSMState
    {
        Stable,
        EconomicCrisis,
        UnderThreat,
        AtWar
    }

    public class FSMAgent : IAgent
    {
        public string Name => "FSM Agent";

        public FSMState CurrentState { get; private set; } = FSMState.Stable;

        private const double TreasuryEnterCrisis = 200.0;
        private const double TreasuryExitCrisis = 500.0;
        private const double LoyaltyEnterCrisis = 3.0;
        private const double LoyaltyExitCrisis = 4.0;

        public MacroAction DecideAction(GameState state)
        {
            UpdateState(state);
            return GetActionForState(state);
        }

        public void ReceiveReward(GameState previousState, MacroAction action, double reward, GameState newState)
        {
        }

        private void UpdateState(GameState state)
        {
            if (state.AtWar)
            {
                CurrentState = FSMState.AtWar;
                return;
            }

            if (CurrentState == FSMState.AtWar && !state.AtWar)
            {
            }

            if (state.ThreatNearby)
            {
                CurrentState = FSMState.UnderThreat;
                return;
            }

            if (CurrentState == FSMState.EconomicCrisis)
            {
                if (state.Treasury > TreasuryExitCrisis && state.Loyalty > LoyaltyExitCrisis)
                {
                    CurrentState = FSMState.Stable;
                }
                return;
            }

            if (state.Treasury < TreasuryEnterCrisis || state.Loyalty < LoyaltyEnterCrisis)
            {
                CurrentState = FSMState.EconomicCrisis;
                return;
            }

            CurrentState = FSMState.Stable;
        }

        private MacroAction GetActionForState(GameState state)
        {
            switch (CurrentState)
            {
                case FSMState.Stable:
                    if (state.ArmySize < 500)
                        return MacroAction.RecruitTroops;
                    if (state.Treasury > 2000)
                        return MacroAction.InvestInfrastructure;
                    return MacroAction.InvestGarrison;

                case FSMState.EconomicCrisis:
                    if (state.Treasury <= 0)
                        return MacroAction.IncreaseTax;
                    if (state.Loyalty < LoyaltyEnterCrisis)
                        return MacroAction.DecreaseTax;
                    return MacroAction.IncreaseTax;

                case FSMState.UnderThreat:
                    if (state.ArmySize > 0)
                        return MacroAction.MaintainArmy;
                    return MacroAction.RecruitTroops;

                case FSMState.AtWar:
                    if (state.ArmySize > 1000)
                        return MacroAction.AttackEnemy;
                    if (state.ArmySize > 0)
                        return MacroAction.MaintainArmy;
                    return MacroAction.RecruitTroops;

                default:
                    return MacroAction.Camp;
            }
        }
    }
}
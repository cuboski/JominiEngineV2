using ProtoMessageClient;
using System;
using System.Collections.Generic;

namespace JominiServer.AI
{
    public enum AgentMode
    {
        None,
        FSM,
        QLearning
    }

    public enum MacroAction
    {
        IncreaseTax,
        DecreaseTax,
        InvestInfrastructure,
        InvestGarrison,
        RecruitTroops,
        MaintainArmy,
        AttackEnemy,
        BesiegeFief,
        HireNPC,
        SpyOnEnemy,
        Camp,
        TransferFundsToHome
    }

    public class GameState
    {
        public double Treasury { get; set; }
        public double Loyalty { get; set; }
        public int ArmySize { get; set; }
        public bool ThreatNearby { get; set; }
        public bool AtWar { get; set; }
        public uint Purse { get; set; }
        public byte Season { get; set; }
        public uint Year { get; set; }
        public int OwnedFiefCount { get; set; }

        public override string ToString()
        {
            return $"[Treasury={Treasury:F0}, Loyalty={Loyalty:F1}, Army={ArmySize}, " +
                   $"Threat={ThreatNearby}, AtWar={AtWar}, Purse={Purse}, " +
                   $"Season={Season}, Year={Year}, Fiefs={OwnedFiefCount}]";
        }
    }

    public interface IAgent
    {
        MacroAction DecideAction(GameState state);

        void ReceiveReward(GameState previousState, MacroAction action, double reward, GameState newState);

        string Name { get; }
    }
}
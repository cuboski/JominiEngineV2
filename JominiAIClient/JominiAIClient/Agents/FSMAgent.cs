using System;
using System.Linq;
using ProtoMessageClient;

namespace JominiAI
{
    public enum FSMState
    {
        Idle,
        Expand,
        Attack,
        Defend
    }

    public class FSMAgent : Agent
    {
        public FSMState CurrentState { get; private set; } = FSMState.Idle;

 
        public FSMAgent(string username, string password)
            : base(username, password)
        {
        }


        public override MainActions findNextAction(GameState gameState, out string[] arguments)
        {
            arguments = Array.Empty<string>();

            if (gameState.myPC == null || !gameState.myPC.isAlive || gameState.myPC.days <= 0)
            {
                Console.WriteLine("[FSM] PC unavailable or out of days — returning None.");
                Transition(FSMState.Idle);
                return MainActions.None;
            }

            bool hasMyArmies = gameState.myArmies != null && gameState.myArmies.Count > 0;
            bool hasEnemies  = gameState.enemyPCids != null && gameState.enemyPCids.Count > 0;

            RedProtoFief currentFief = gameState.getCurrentFief();

            string localEnemyArmyID = null;
            if (hasEnemies && currentFief.armyIDs != null)
            {
                foreach (string armyIDinFief in currentFief.armyIDs)
                {
                    if (!gameState.tryGetArmy(armyIDinFief, out RedProtoArmy armyInFief))
                        continue;
                    if (gameState.enemyPCids.Contains(armyInFief.ownerName))
                    {
                        localEnemyArmyID = armyIDinFief;
                        break;
                    }
                }
            }

            if (hasMyArmies && localEnemyArmyID != null)
            {
                Transition(FSMState.Attack);
                gameState.tryGetMainArmy(out RedProtoArmy mainArmy);
                Console.WriteLine($"[FSM] Attack: army={mainArmy?.armyID ?? "?"} → target={localEnemyArmyID}");
                arguments = new string[] { mainArmy?.armyID ?? string.Empty, localEnemyArmyID };
                return MainActions.ArmyAttack;
            }

            if (!hasMyArmies)
            {
                Transition(FSMState.Expand);
                if (currentFief.armyIDs != null && currentFief.armyIDs.Length > 0)
                {
                    string targetArmyID = currentFief.armyIDs[0];
                    Console.WriteLine($"[FSM] Expand: Recruit 50 troops into army={targetArmyID}");
                    arguments = new string[] { targetArmyID, "50" };
                    return MainActions.ArmyRecruit;
                }
                Console.WriteLine("[FSM] Expand: No army in fief, falling back to Idle.");
            }

            if (hasMyArmies)
            {
                Transition(FSMState.Defend);
                if (gameState.tryGetMainArmy(out RedProtoArmy mainArmy))
                {
                    if (!mainArmy.isMaintained)
                    {
                        Console.WriteLine($"[FSM] Defend: Maintain army={mainArmy.armyID}");
                        arguments = new string[] { mainArmy.armyID };
                        return MainActions.ArmyMaintain;
                    }
                    foreach (RedProtoArmy myArmy in gameState.myArmies)
                    {
                        if (!myArmy.isMaintained)
                        {
                            Console.WriteLine($"[FSM] Defend: Maintain secondary army={myArmy.armyID}");
                            arguments = new string[] { myArmy.armyID };
                            return MainActions.ArmyMaintain;
                        }
                    }
                    Console.WriteLine("[FSM] Defend: All armies maintained, falling back to Idle.");
                }
            }

            Transition(FSMState.Idle);
            byte daysToCamp = (byte)Math.Max(1, Math.Min((int)gameState.myPC.days, 5));
            Console.WriteLine($"[FSM] Idle: Camp for {daysToCamp} day(s) (char={gameState.myPC.charID})");
            arguments = new string[] { gameState.myPC.charID, daysToCamp.ToString() };
            return MainActions.NPCcamp;
        }

        private void Transition(FSMState newState)
        {
            if (CurrentState != newState)
            {
                Console.WriteLine($"[FSM] State transition: {CurrentState} → {newState}");
                CurrentState = newState;
            }
        }
    }
}
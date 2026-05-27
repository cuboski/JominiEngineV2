using System;
using ProtoMessageClient;

namespace JominiAI
{

    public static class FSMActionExecutor
    {

        public static ProtoMessage Convert(
            MainActions action,
            string[] arguments,
            GameState gameState)
        {
            if (arguments == null)
                arguments = Array.Empty<string>();

            switch (action)
            {

                case MainActions.ArmyRecruit:
                {
                    if (arguments.Length < 2)
                    {
                        Console.WriteLine(
                            "[FSMActionExecutor] ArmyRecruit needs 2 arguments " + "(armyID, amount)");
                        return CreateNoOp();
                    }

                    string armyID = arguments[0];
                    uint amount   = uint.TryParse(arguments[1], out uint a) ? a : 10u;

                    ProtoRecruit protoRecruit = new ProtoRecruit
                    {
                        armyID    = armyID,
                        amount    = amount,
                        isConfirm = true
                    };
                    protoRecruit.ActionType = Actions.RecruitTroops;

                    Console.WriteLine(
                        $"[FSMActionExecutor] ArmyRecruit  " + $"Actions.RecruitTroops, armyID={armyID}, amount={amount}");
                    return protoRecruit;
                }


                case MainActions.ArmyAttack:
                {
                    if (arguments.Length < 2)
                    {
                        Console.WriteLine(
                            "[FSMActionExecutor]  ArmyAttack needs 2 arguments " + "(myArmyID, targetArmyID)");
                        return CreateNoOp();
                    }

                    string myArmyID     = arguments[0];
                    string targetArmyID = arguments[1];

                    ProtoMessage msg = new ProtoMessage
                    {
                        ActionType    = Actions.Attack,
                        Message       = myArmyID,
                        MessageFields = new[] { targetArmyID }
                    };

                    Console.WriteLine(
                        $"[FSMActionExecutor] ArmyAttack → " + $"Actions.Attack, myArmy={myArmyID}, target={targetArmyID}");
                    return msg;
                }


                case MainActions.ArmyMaintain:
                {
                    if (arguments.Length < 1)
                    {
                        Console.WriteLine(
                            "[FSMActionExecutor] ArmyMaintain needs 1 argument " + "(armyID)");
                        return CreateNoOp();
                    }

                    string armyID = arguments[0];

                    ProtoMessage msg = new ProtoMessage
                    {
                        ActionType    = Actions.MaintainArmy,
                        Message       = armyID,
                        MessageFields = null
                    };

                    Console.WriteLine(
                        $"[FSMActionExecutor] ArmyMaintain → " + $"Actions.MaintainArmy, armyID={armyID}");
                    return msg;
                }


                case MainActions.NPCcamp:
                {
                    if (arguments.Length < 2)
                    {
                        Console.WriteLine(
                            "[FSMActionExecutor] NPCcamp needs 2 arguments " + "(charID, days)");
                        return CreateNoOp();
                    }

                    string charID = arguments[0];
                    string days   = arguments[1];

                    ProtoMessage msg = new ProtoMessage
                    {
                        ActionType    = Actions.Camp,
                        Message       = charID,
                        MessageFields = new[] { days }
                    };

                    Console.WriteLine(
                        $"[FSMActionExecutor] NPCcamp → " +  $"Actions.Camp, charID={charID}, days={days}");
                    return msg;
                }


                default:
                    Console.WriteLine(
                        $"[FSMActionExecutor] No conversion defined for {action}. ");
                    return CreateNoOp();
            }
        }


        private static ProtoMessage CreateNoOp()
        {
            return new ProtoMessage
            {
                ActionType    = Actions.ViewMyFiefs,
                Message       = string.Empty,
                MessageFields = Array.Empty<string>()
            };
        }
    }
}
using System;
using ProtoMessageClient;

namespace JominiAI
{
    public static class FSMActionExecutor
    {
        public static ProtoMessage Convert(
            MainActions action, string[] arguments, GameState gameState)
        {
            arguments ??= Array.Empty<string>();

            switch (action)
            {
                case MainActions.ArmyRecruit:
                    Require(action, arguments, 2);
                    return new ProtoRecruit
                    {
                        ActionType = Actions.RecruitTroops,
                        armyID = arguments[0],
                        amount = uint.TryParse(arguments[1], out var amt) ? amt : 10u,
                        isConfirm = true
                    };

                case MainActions.ArmyMaintain:
                    Require(action, arguments, 1);
                    return Msg(Actions.MaintainArmy, arguments[0]);

                case MainActions.ArmyAttack:
                    Require(action, arguments, 2);
                    return new ProtoMessage
                    {
                        ActionType = Actions.Attack,
                        Message = arguments[0],
                        MessageFields = new[] { arguments[1] }
                    };

                case MainActions.ArmyPillageCurrentFief:
                    Require(action, arguments, 1);
                    return Msg(Actions.PillageFief, arguments[0]);

                case MainActions.ArmyDisband:
                    Require(action, arguments, 1);
                    return Msg(Actions.DisbandArmy, arguments[0]);

                case MainActions.SiegeBesiegeCurrentFief:
                    Require(action, arguments, 1);
                    return Msg(Actions.BesiegeFief, arguments[0]);

                case MainActions.SiegeStorm:
                    Require(action, arguments, 1);
                    return Msg(Actions.SiegeRoundStorm, arguments[0]);

                case MainActions.SiegeReduction:
                    Require(action, arguments, 1);
                    return Msg(Actions.SiegeRoundReduction, arguments[0]);

                case MainActions.SiegeNegotiation:
                    Require(action, arguments, 1);
                    return Msg(Actions.SiegeRoundNegotiate, arguments[0]);

                case MainActions.SiegeEnd:
                    Require(action, arguments, 1);
                    return Msg(Actions.EndSiege, arguments[0]);

                case MainActions.NPCappointBaillif:
                    Require(action, arguments, 2);
                    return new ProtoMessage
                    {
                        ActionType = Actions.AppointBailiff,
                        Message = arguments[0],
                        MessageFields = new[] { arguments[1] }
                    };

                case MainActions.NPCremoveBaillif:
                    Require(action, arguments, 1);
                    return Msg(Actions.RemoveBailiff, arguments[0]);

                // NPCmarryPC: arguments[0]=groomID, arguments[1]=brideID
                // Note: Agent.ExecuteAction uses currentPCid as groom implicitly, so only brideID is in args there.
                case MainActions.NPCmarryPC:
                    Require(action, arguments, 2);
                    return new ProtoMessage
                    {
                        ActionType = Actions.ProposeMarriage,
                        Message = arguments[0],
                        MessageFields = new[] { arguments[1] }
                    };

                // NPCmarryChild: arguments[0]=groomID (family NPC), arguments[1]=brideID
                case MainActions.NPCmarryChild:
                    Require(action, arguments, 2);
                    return new ProtoMessage
                    {
                        ActionType = Actions.ProposeMarriage,
                        Message = arguments[0],
                        MessageFields = new[] { arguments[1] }
                    };

                // NPCacceptRejectProposal: arguments[0]="true"/"false"
                case MainActions.NPCacceptRejectProposal:
                    Require(action, arguments, 1);
                    return new ProtoMessage
                    {
                        ActionType = Actions.AcceptRejectProposal,
                        Message = arguments[0]
                    };

                // ArmyAppointLeader: arguments[0]=armyID, arguments[1]=leaderCharID
                case MainActions.ArmyAppointLeader:
                    Require(action, arguments, 2);
                    return new ProtoMessage
                    {
                        ActionType = Actions.AppointLeader,
                        Message = arguments[0],
                        MessageFields = new[] { arguments[1] }
                    };

                // NPCaddToEntourage / NPCremoveFromEntourage: arguments[0]=charID
                case MainActions.NPCaddToEntourage:
                case MainActions.NPCremoveFromEntourage:
                    Require(action, arguments, 1);
                    return Msg(Actions.AddRemoveEntourage, arguments[0]);

                case MainActions.NPCfire:
                    Require(action, arguments, 1);
                    return Msg(Actions.FireNPC, arguments[0]);

                case MainActions.NPChire:
                    Require(action, arguments, 2);
                    return new ProtoMessage
                    {
                        ActionType = Actions.HireNPC,
                        Message = arguments[0],
                        MessageFields = new[] { arguments[1] }
                    };

                case MainActions.NPCcamp:
                    Require(action, arguments, 2);
                    return new ProtoMessage
                    {
                        ActionType = Actions.Camp,
                        Message = arguments[0],
                        MessageFields = new[] { arguments[1] }
                    };

                case MainActions.FiefAutoAdjustExpenditure:
                    Require(action, arguments, 1);
                    return Msg(Actions.AdjustExpenditure, arguments[0]);

                case MainActions.FiefTransferFunds:
                    Require(action, arguments, 3);
                    return new ProtoTransfer
                    {
                        ActionType = Actions.TransferFunds,
                        fiefFrom = arguments[0],
                        fiefTo = arguments[1],
                        amount = int.TryParse(arguments[2], out var tr) ? tr : 0
                    };

                case MainActions.KidnapReleaseCaptive:
                    Require(action, arguments, 1);
                    return Msg(Actions.ReleaseCaptive, arguments[0]);

                default:
                    Console.WriteLine($"[FSMActionExecutor] No mapping for {action} : no-op.");
                    return NoOp();
            }
        }

        private static ProtoMessage Msg(Actions type, string message) =>
            new ProtoMessage { ActionType = type, Message = message };

        private static ProtoMessage NoOp() =>
            new ProtoMessage { ActionType = Actions.ViewMyFiefs, Message = string.Empty };

        private static void Require(MainActions action, string[] args, int count)
        {
            if (args.Length < count)
                Console.WriteLine(
                    $"[FSMActionExecutor] {action} needs {count} arg(s) " +
                    $"but got {args.Length}.");
        }
    }
}

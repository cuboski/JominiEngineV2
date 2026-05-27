using ProtoMessageClient;
using System;
using System.Threading;

// ---------------------------------------------------------------------------
// FSM demo :
//
// Pipeline per iteration:
//   Server Update
//     
//   Build GameState 
//     
//   FSM Agent        
//     
//   Choose Action    
//     
//   Convert Action  
//     
//   Execute / Send  
//
// All network code comes from TextTestClient and Player
// All protocol types come from ProtoMessageClient
// ---------------------------------------------------------------------------

namespace JominiAI
{
    class Program
    {
        // Maximum number of action iterations in a single run.
        private const int MaxIterations = 20;

        private const int PauseBetweenActionsMs = 250;

        static void Main(string[] args)
        {
            Console.WriteLine(" JominiAIClient FSM Demo");
            Console.WriteLine();


            Console.WriteLine("[Main] Creating FSMAgent and connecting to server...");
            FSMAgent agent;
            try
            {
                // agent = new FSMAgent("ai_fsm", "ai_fsm_pass");
                agent = new FSMAgent("acc1", "pass1");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Main] ERROR: Could not create FSMAgent: {ex.Message}");
                return;
            }


            Console.WriteLine("[Main] Connected");
            Console.WriteLine($"[Main] FSM starting state: {agent.CurrentState}");
            Console.WriteLine();


            Console.WriteLine("[Main] Requesting initial GameState from server...");
            GameState gameState;
            try
            {
                gameState = agent.ObtainCurrentGSandUpdateAgentLists();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Main] ERROR building GameState: {ex.Message}");
                return;
            }

            PrintGameStateSummary(gameState);


            int iteration = 0;

            while (iteration < MaxIterations)
            {
                iteration++;
                Console.WriteLine();
                Console.WriteLine($"Iteration {iteration} / {MaxIterations}");
                Console.WriteLine($"    PC days remaining : {gameState.myPC?.days:F1}");
                Console.WriteLine($"    My armies         : {gameState.myArmies?.Count ?? 0}");
                Console.WriteLine($"    Enemy PCs known   : {gameState.enemyPCids?.Count ?? 0}");
                Console.WriteLine($"    FSM current state : {agent.CurrentState}");


                MainActions chosenAction = agent.findNextAction(gameState, out string[] arguments);

                if (chosenAction == MainActions.None)
                {
                    Console.WriteLine("[Main] FSM returned None");
                    break;
                }

                // Log action

                Console.WriteLine($"[Main] Sending {chosenAction}");


                ProtoMessage protoMsg = FSMActionExecutor.Convert(chosenAction, arguments, gameState);
                Console.WriteLine(
                    $"[Main] ProtoMessage built: " +
                    $"ActionType={protoMsg.ActionType}, " +
                    $"Message=\"{protoMsg.Message ?? string.Empty}\", " +
                    $"Fields=[{FormatFields(protoMsg.MessageFields)}]");

                
                try
                {
                    gameState = agent.playOneAction(
                        gameState,
                        out MainActions executed,
                        out string[] executedArgs,
                        out bool executionSuccess,
                        out bool endOfSeason
                    );

                    if (endOfSeason)
                    {
                        Console.WriteLine("[Main] End of season");
                        break;
                    }

                    Console.WriteLine(
                        $"[Main] Execution result: {(executionSuccess ? "SUCCESS" : "FAILURE")} " +
                        $"(action={executed})");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Main] ERROR during action execution: {ex.Message}");
                    Console.WriteLine("[Main] Aborting demo.");
                    break;
                }

                Thread.Sleep(PauseBetweenActionsMs);

                try
                {
                    gameState = agent.ObtainCurrentGSandUpdateAgentLists(gameState);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Main] ERROR refreshing GameState: {ex.Message}");
                    break;
                }
            }


            Console.WriteLine();
            Console.WriteLine(" FSM Demo ended");
            Console.WriteLine($" Final FSM state : {agent.CurrentState}");
        }

        /// <summary>
        /// Print a compact summary of the current GameState.
        /// </summary>
        private static void PrintGameStateSummary(GameState gs)
        {
            Console.WriteLine("[Main] GameState summary:");
            Console.WriteLine($"  PC id          : {gs.myPC?.charID ?? "?"}");
            Console.WriteLine($"  PC days        : {gs.myPC?.days:F1}");
            Console.WriteLine($"  My armies      : {gs.myArmies?.Count ?? 0}");
            Console.WriteLine($"  Enemy PCs      : {gs.enemyPCids?.Count ?? 0}");
            Console.WriteLine($"  My fiefs       : {gs.myFiefs?.Count ?? 0}");
            Console.WriteLine($"  Not-my armies  : {gs.notMyArmies?.Count ?? 0}");
        }

        /// <summary>
        /// Formats a MessageFields array for console output.
        /// </summary>
        private static string FormatFields(string[] fields)
        {
            if (fields == null || fields.Length == 0)
                return string.Empty;
            return string.Join(", ", fields);
        }

        public static ProtoMessage GetActionReply(Actions action, TextTestClient client)
        {
            bool receivedActionReply = false;
            bool receivedUpdateReply = (action == Actions.LogIn);
            ProtoMessage actionReply = null;

            int attempts = 0;

            do
            {
                Console.WriteLine($"  [GetActionReply] Waiting for reply (attempt {++attempts})...");

                ProtoMessage reply = client.CheckForProtobufMessage();

                if (reply == null)
                {
                    Console.WriteLine("  [GetActionReply] Timeout — no reply received.");
                    client.LogOut();
                    return null;
                }

                if (reply.ActionType == action)
                {
                    Console.WriteLine(
                        $"  [GetActionReply] Got expected reply: " +
                        $"{reply.ActionType} / {reply.ResponseType}");
                    actionReply          = reply;
                    receivedActionReply  = true;
                    receivedUpdateReply  = true;
                }
                else if (reply.ActionType == Actions.Update)
                {
                    Console.WriteLine(
                        $"  [GetActionReply] Got Update: {reply.ResponseType}");

                    if (reply.ResponseType == DisplayMessages.ErrorGenericMessageInvalid)
                    {
                        actionReply         = reply;
                        receivedActionReply = true;
                    }
                    else if (reply.ResponseType == DisplayMessages.Success)
                    {
                        receivedUpdateReply = true;
                    }
                }
                else
                {
                    Console.WriteLine(
                        $"  [GetActionReply] Unexpected reply: " +
                        $"{reply.ActionType} / {reply.ResponseType} — ignoring.");
                }
            }
            while (!receivedActionReply || !receivedUpdateReply);

            Console.WriteLine("  [GetActionReply] Done.");
            return actionReply;
        }
    }
}
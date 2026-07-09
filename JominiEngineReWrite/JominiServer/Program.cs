using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace JominiServer
{
    public static class Program
    {

        //private static Server _Server;
        private static JominiGame.Game game;

        public static void Main(string[] args)
        {
            // Parse --scenario <name> to select a CSV folder under Scenarios/
            string scenarioName = null;
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "--scenario") { scenarioName = args[i + 1]; break; }

            string csvPath = scenarioName != null
                ? Path.Combine(Directory.GetCurrentDirectory(), "Scenarios", scenarioName)
                : null;   // null → Game uses default CSVs/ folder

            if (scenarioName != null)
                Console.WriteLine($"[Server] Scenario: {scenarioName}  →  {csvPath}");

            if (args.Length > 0 && args[0] == "--ai")
            {
                string mode = args.Length > 1 ? args[1] : "FSM";
                string username = args.Length > 2 ? args[2] : "ai_fsm";
                string password = args.Length > 3 ? args[3] : "ai_fsm_pass";
                string charID = args.Length > 4 ? args[4] : "Char_007";

                AgentMode agentMode = Enum.TryParse(mode, out AgentMode parsed)
                    ? parsed : AgentMode.FSM;

                Console.WriteLine($"Starting AI client — mode={agentMode} user={username}");
                using var aiClient = new JominiEngine.AI.AIClient(
                    agentMode, username, password, charID,
                    host: "10.130.61.5",
                    port: 8080, // match server port from server.cs
                    seasonDelayMs: 3000
                );
                Console.CancelKeyPress += (_, e) => { e.Cancel = true; aiClient.Stop(); };
                int seasons = aiClient.Run();
                Console.WriteLine($"AI client finished after {seasons} seasons.");
                return;  // does NOT start the server
            }

            Console.WriteLine("Jomini Engine Started2");
            game = new JominiGame.Game(csvPath);
            Server server = new(game);
            Console.WriteLine("Jomini Engine Ended");
            //_Server = new Server();

        }
    }
}
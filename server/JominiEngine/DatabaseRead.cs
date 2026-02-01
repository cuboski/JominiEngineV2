using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using QuickGraph;
using RiakClient.Models;

namespace JominiEngine
{
    public static class DatabaseRead
    {
        /// <summary>
        /// Reads all objects for a particular game from database
        /// </summary>
        /// <param name="gameID">ID of game (database bucket)</param>
        public static void DatabaseReadAll(string gameID)
        {

            // ========= load KEY LISTS (to ensure efficient retrieval of specific game objects)
            DatabaseRead.DatabaseRead_keyLists(gameID);

            // ========= load CLOCK
            Globals_Game.clock = DatabaseRead.DatabaseRead_clock(gameID, "gameClock");

            // ========= load GLOBAL_SERVER/GAME DICTIONARIES
            Globals_Server.combatValues = DatabaseRead.DatabaseRead_dictStringUint(gameID, "combatValues");
            Globals_Server.recruitRatios = DatabaseRead.DatabaseRead_dictStringDouble(gameID, "recruitRatios");
            Globals_Server.battleProbabilities = DatabaseRead.DatabaseRead_dictStringDouble(gameID, "battleProbabilities");
            Globals_Server.gameTypes = DatabaseRead.DatabaseRead_dictIntString(gameID, "gameTypes");
            Globals_Game.jEntryPriorities = DatabaseRead.DatabaseRead_dictStringByte(gameID, "jEntryPriorities");
            Globals_Game.ownershipChallenges = DatabaseRead.DatabaseRead_dictStringOwnCh(gameID, "ownershipChallenges");

            // ========= load GLOBAL_GAME/SERVER newID VARIABLES
            // newCharID
            Globals_Game.newCharID = DatabaseRead.DatabaseRead_newIDs(gameID, "newCharID");
            // newArmyID
            Globals_Game.newArmyID = DatabaseRead.DatabaseRead_newIDs(gameID, "newArmyID");
            // newDetachmentID
            Globals_Game.newDetachmentID = DatabaseRead.DatabaseRead_newIDs(gameID, "newDetachmentID");
            // newAilmentID
            Globals_Game.newAilmentID = DatabaseRead.DatabaseRead_newIDs(gameID, "newAilmentID");
            // newSiegeID
            Globals_Game.newSiegeID = DatabaseRead.DatabaseRead_newIDs(gameID, "newSiegeID");
            // newJournalEntryID
            Globals_Game.newJournalEntryID = DatabaseRead.DatabaseRead_newIDs(gameID, "newJournalEntryID");
            // gameType
            Globals_Game.gameType = DatabaseRead.DatabaseRead_newIDs(gameID, "currentVictoryType");
            // duration
            Globals_Game.duration = DatabaseRead.DatabaseRead_newIDs(gameID, "duration");
            // startYear
            Globals_Game.startYear = DatabaseRead.DatabaseRead_newIDs(gameID, "startYear");
            // newGameID
            Globals_Server.newGameID = DatabaseRead.DatabaseRead_newIDs(gameID, "newGameID");
            // newOwnChallengeID
            Globals_Game.newOwnChallengeID = DatabaseRead.DatabaseRead_newIDs(gameID, "newOwnChallengeID");

            // ========= load JOURNALS
            Globals_Game.scheduledEvents = DatabaseRead.DatabaseRead_journal(gameID, "serverScheduledEvents");
            Globals_Game.pastEvents = DatabaseRead.DatabaseRead_journal(gameID, "serverPastEvents");

            // ========= load victoryData
            foreach (string element in Globals_Game.victoryDataKeys)
            {
                VictoryData vicData = DatabaseRead.DatabaseRead_victoryData(gameID, element);
                // add VictoryData to Globals_Game.victoryData
                Globals_Game.victoryData.Add(vicData.playerID, vicData);
            }

            // ========= read GLOBALS_GAME/CLIENT/SERVER BOOL VARIABLES
            Globals_Game.loadFromDatabase = DatabaseRead.DatabaseRead_bool(gameID, "loadFromDatabase");
            Globals_Game.loadFromCSV = DatabaseRead.DatabaseRead_bool(gameID, "loadFromCSV");
            Globals_Game.writeToDatabase = DatabaseRead.DatabaseRead_bool(gameID, "writeToDatabase");
            Globals_Game.statureCapInForce = DatabaseRead.DatabaseRead_bool(gameID, "statureCapInForce");

            // ========= load TRAITS
            foreach (string element in Globals_Game.traitKeys)
            {
                Trait trait = DatabaseRead.DatabaseRead_trait(gameID, element);
                // add Trait to traitMasterList
                Globals_Game.traitMasterList.Add(trait.id, trait);
            }

            // ========= load BASELANGUAGES
            foreach (string element in Globals_Game.baseLangKeys)
            {
                BaseLanguage bLang = DatabaseRead.DatabaseRead_baseLanguage(gameID, element);
                // add BaseLanguage to baseLanguageMasterList
                Globals_Game.baseLanguageMasterList.Add(bLang.id, bLang);
            }

            // ========= load LANGUAGES
            foreach (string element in Globals_Game.langKeys)
            {
                Language lang = DatabaseRead.DatabaseRead_language(gameID, element);
                // add Language to languageMasterList
                Globals_Game.languageMasterList.Add(lang.id, lang);
            }

            // ========= load NATIONALITY OBJECTS
            foreach (string element in Globals_Game.nationalityKeys)
            {
                Nationality nat = DatabaseRead.DatabaseRead_nationality(gameID, element);
                // add Nationality to nationalityMasterList
                Globals_Game.nationalityMasterList.Add(nat.natID, nat);
            }

            // ========= load RANKS
            foreach (byte element in Globals_Game.rankKeys)
            {
                Rank rank = DatabaseRead.DatabaseRead_rank(gameID, element.ToString());
                // add Rank to rankMasterList
                Globals_Game.rankMasterList.Add(rank.id, rank);
            }

            // ========= load POSITIONS
            foreach (byte element in Globals_Game.positionKeys)
            {
                Position pos = DatabaseRead.DatabaseRead_position(gameID, element.ToString());
                // add Position to positionMasterList
                Globals_Game.positionMasterList.Add(pos.id, pos);
            }

            // ========= load SIEGES
            foreach (string element in Globals_Game.siegeKeys)
            {
                Siege s = DatabaseRead.DatabaseRead_Siege(gameID, element);
                // add Siege to siegeMasterList
                Globals_Game.siegeMasterList.Add(s.siegeID, s);
            }

            // ========= load ARMIES
            foreach (string element in Globals_Game.armyKeys)
            {
                Army a = DatabaseRead.DatabaseRead_Army(gameID, element);
                // add Army to armyMasterList
                Globals_Game.armyMasterList.Add(a.armyID, a);
            }

            // ========= load NPCs
            foreach (string element in Globals_Game.npcKeys)
            {
                NonPlayerCharacter npc = DatabaseRead.DatabaseRead_NPC(gameID, element);
                // add NPC to npcMasterList
                Globals_Game.npcMasterList.Add(npc.charID, npc);
            }

            // ========= load PCs
            foreach (string element in Globals_Game.pcKeys)
            {
                PlayerCharacter pc = DatabaseRead.DatabaseRead_PC(gameID, element);
                // add PC to pcMasterList
                Globals_Game.pcMasterList.Add(pc.charID, pc);
            }

            // ========= load KINGDOMS
            foreach (string element in Globals_Game.kingKeys)
            {
                Kingdom king = DatabaseRead.DatabaseRead_Kingdom(gameID, element);
                // add Kingdom to kingdomMasterList
                Globals_Game.kingdomMasterList.Add(king.id, king);
            }

            // ========= load PROVINCES
            foreach (string element in Globals_Game.provKeys)
            {
                Province prov = DatabaseRead.DatabaseRead_Province(gameID, element);
                // add Province to provinceMasterList
                Globals_Game.provinceMasterList.Add(prov.id, prov);
            }

            // ========= load TERRAINS
            foreach (string element in Globals_Game.terrKeys)
            {
                Terrain terr = DatabaseRead.DatabaseRead_terrain(gameID, element);
                // add Terrain to terrainMasterList
                Globals_Game.terrainMasterList.Add(terr.id, terr);
            }

            // ========= load FIEFS
            foreach (string element in Globals_Game.fiefKeys)
            {
                Fief f = DatabaseRead.DatabaseRead_Fief(gameID, element);
                // add Fief to fiefMasterList
                Globals_Game.fiefMasterList.Add(f.id, f);
            }
            // ========== load CLIENTS
            foreach (string username in Globals_Server.client_keys)
            {
                Client c = DatabaseRead_Client(gameID, username);
                Globals_Server.Clients.Add(c.username, c);
            }
            // ========= process any CHARACTER goTo QUEUES containing entries
            if (Globals_Game.goToList.Count > 0)
            {
                for (int i = 0; i < Globals_Game.goToList.Count; i++)
                {
                    DatabaseRead.Populate_goTo(Globals_Game.goToList[i]);
                }
                Globals_Game.goToList.Clear();
            }

            // ========= read GLOBALS_GAME CHARACTER VARIABLES
            Globals_Game.sysAdmin = DatabaseRead.DatabaseRead_PcVariable(gameID, "sysAdmin");
            Globals_Game.kingOne = DatabaseRead.DatabaseRead_PcVariable(gameID, "kingOne");
            Globals_Game.kingTwo = DatabaseRead.DatabaseRead_PcVariable(gameID, "kingTwo");
            Globals_Game.princeOne = DatabaseRead.DatabaseRead_PcVariable(gameID, "princeOne");
            Globals_Game.princeTwo = DatabaseRead.DatabaseRead_PcVariable(gameID, "princeTwo");
            Globals_Game.heraldOne = DatabaseRead.DatabaseRead_PcVariable(gameID, "heraldOne");
            Globals_Game.heraldTwo = DatabaseRead.DatabaseRead_PcVariable(gameID, "heraldTwo");

            // ========= load MAP
            Globals_Game.gameMap = DatabaseRead.DatabaseRead_map(gameID, "mapEdges");
        }

		public static void DatabaseRead_Test(string gameID) {
			var result = Globals_Server.rClient.Get (gameID, "test");
			if (!result.IsSuccess) {
				Console.WriteLine ("Failed to get test object : " + result.ErrorMessage);
			}
		}
        //TODO
        public static Client DatabaseRead_Client(string gameID, string clientID)
        {
            var newClient = Globals_Server.rClient.Get(gameID, clientID);
            if (newClient.IsSuccess)
            {
                Client_Serialized client_ser = newClient.Value.GetObject<Client_Serialized>();

                return client_ser.deserialise();
            }
            else
            {
                Globals_Server.logError("Failed to read client: " + clientID);
                return null;
            }
        }

        /// <summary>
        /// Reads all key lists from the database
        /// </summary>
        /// <param name="gameID">Game for which key lists to be retrieved</param>
        public static void DatabaseRead_keyLists(string gameID)
        {
			// populate nationalityKeys
			var natKeyResult = Globals_Server.rClient.Get(gameID, "nationalityKeys");
			if (natKeyResult.IsSuccess)
			{
				Globals_Game.nationalityKeys = natKeyResult.Value.GetObject<List<string>>();
			}
			else
			{
				string toDisplay = "InitialDBload: Unable to retrieve nationalityKeys from database."+natKeyResult.ErrorMessage;
				Globals_Server.logError(toDisplay);
			}
			Console.WriteLine("Reading traitkeys- bucket = "+gameID + ", key = traitKeys");
            // populate traitKeys
            var traitKeyResult = Globals_Server.rClient.Get(gameID, "traitKeys");
            if (traitKeyResult.IsSuccess)
            {
                Globals_Game.traitKeys = traitKeyResult.Value.GetObject<List<string>>();
            }
            else
            {
				string toDisplay = "InitialDBload: Unable to retrieve traitKeys from database. "+traitKeyResult.ErrorMessage;
                Globals_Server.logError(toDisplay);
            }

           

            // populate langKeys
            var langKeyResult = Globals_Server.rClient.Get(gameID, "langKeys");
            if (langKeyResult.IsSuccess)
            {
                Globals_Game.langKeys = langKeyResult.Value.GetObject<List<string>>();
            }
            else
            {
                string toDisplay = "InitialDBload: Unable to retrieve langKeys from database.";
                Globals_Server.logError(toDisplay);
            }

            // populate baseLangKeys
            var bLangKeyResult = Globals_Server.rClient.Get(gameID, "baseLangKeys");
            if (bLangKeyResult.IsSuccess)
            {
                Globals_Game.baseLangKeys = bLangKeyResult.Value.GetObject<List<string>>();
            }
            else
            {
                string toDisplay ="InitialDBload: Unable to retrieve baseLangKeys from database.";
                Globals_Server.logError(toDisplay);
            }

            // populate rankKeys
            var rankKeyResult = Globals_Server.rClient.Get(gameID, "rankKeys");
            if (rankKeyResult.IsSuccess)
            {
                Globals_Game.rankKeys = rankKeyResult.Value.GetObject<List<byte>>();
            }
            else
            {
                string toDisplay ="InitialDBload: Unable to retrieve rankKeys from database.";
                Globals_Server.logError(toDisplay);
            }

            // populate positionKeys
            var positionKeyResult = Globals_Server.rClient.Get(gameID, "positionKeys");
            if (positionKeyResult.IsSuccess)
            {
                Globals_Game.positionKeys = positionKeyResult.Value.GetObject<List<byte>>();
            }
            else
            {
                string toDisplay ="InitialDBload: Unable to retrieve positionKeys from database.";
                Globals_Server.logError(toDisplay);
            }

            // populate npcKeys
            var npcKeyResult = Globals_Server.rClient.Get(gameID, "npcKeys");
            if (npcKeyResult.IsSuccess)
            {
                Globals_Game.npcKeys = npcKeyResult.Value.GetObject<List<string>>();
            }
            else
            {
                string toDisplay ="InitialDBload: Unable to retrieve npcKeys from database.";
                Globals_Server.logError(toDisplay);
            }

            // populate pcKeys
            var pcKeyResult = Globals_Server.rClient.Get(gameID, "pcKeys");
            if (pcKeyResult.IsSuccess)
            {
                Globals_Game.pcKeys = pcKeyResult.Value.GetObject<List<string>>();
            }
            else
            {
                string toDisplay ="InitialDBload: Unable to retrieve pcKeys from database.";
                Globals_Server.logError(toDisplay);
            }

            // populate kingKeys
            var kingKeyResult = Globals_Server.rClient.Get(gameID, "kingKeys");
            if (kingKeyResult.IsSuccess)
            {
                Globals_Game.kingKeys = kingKeyResult.Value.GetObject<List<string>>();
            }
            else
            {
                string toDisplay ="InitialDBload: Unable to retrieve kingKeys from database.";
                Globals_Server.logError(toDisplay);
            }

            // populate provKeys
            var provKeyResult = Globals_Server.rClient.Get(gameID, "provKeys");
            if (provKeyResult.IsSuccess)
            {
                Globals_Game.provKeys = provKeyResult.Value.GetObject<List<string>>();
            }
            else
            {
                string toDisplay ="InitialDBload: Unable to retrieve provKeys from database.";
                Globals_Server.logError(toDisplay);
            }

            // populate terrKeys
            var terrKeyResult = Globals_Server.rClient.Get(gameID, "terrKeys");
            if (terrKeyResult.IsSuccess)
            {
                Globals_Game.terrKeys = terrKeyResult.Value.GetObject<List<string>>();
            }
            else
            {
                string toDisplay ="InitialDBload: Unable to retrieve terrKeys from database.";
                Globals_Server.logError(toDisplay);
            }

            // populate victoryDataKeys
            var vicDataResult = Globals_Server.rClient.Get(gameID, "victoryDataKeys");
            if (vicDataResult.IsSuccess)
            {
                Globals_Game.victoryDataKeys = vicDataResult.Value.GetObject<List<string>>();
            }
            else
            {
                string toDisplay ="InitialDBload: Unable to retrieve victoryDataKeys from database.";
                Globals_Server.logError(toDisplay);
            }

            // populate fiefKeys
            var fiefKeyResult = Globals_Server.rClient.Get(gameID, "fiefKeys");
            if (fiefKeyResult.IsSuccess)
            {
                Globals_Game.fiefKeys = fiefKeyResult.Value.GetObject<List<string>>();
            }
            else
            {
                string toDisplay ="InitialDBload: Unable to retrieve fiefKeys from database.";
                Globals_Server.logError(toDisplay);
            }

            // populate armyKeys
            var armyKeyResult = Globals_Server.rClient.Get(gameID, "armyKeys");
            if (armyKeyResult.IsSuccess)
            {
                Globals_Game.armyKeys = armyKeyResult.Value.GetObject<List<string>>();
            }
            else
            {
                string toDisplay ="InitialDBload: Unable to retrieve armyKeys from database.";
                Globals_Server.logError(toDisplay);
            }

            // populate siegeKeys
            var siegeKeyResult = Globals_Server.rClient.Get(gameID, "siegeKeys");
            if (siegeKeyResult.IsSuccess)
            {
                Globals_Game.siegeKeys = siegeKeyResult.Value.GetObject<List<string>>();
            }
            else
            {
                string toDisplay ="InitialDBload: Unable to retrieve siegeKeys from database.";
                Globals_Server.logError(toDisplay);
            }

            var clientKeyResult = Globals_Server.rClient.Get(gameID, "clientKeys");
            if (clientKeyResult.IsSuccess)
            {
                Globals_Server.client_keys = clientKeyResult.Value.GetObject<List<string>>();
            }
            else
            {
                Globals_Server.logError("InitialDBload: unable to retrieve clientKeys from database.");
            }

        }

        public static void Cass_DatabaseRead_keyLists()
        {
            // populate nationalityKeys

            HeldStrings[] natKeys = Globals_Server.cMapper.Fetch<HeldStrings>("WHERE id = ?", "nationalityKeys").ToArray();
            HeldStrings readN = natKeys[0];

            Globals_Game.nationalityKeys = JsonConvert.DeserializeObject<List<string>>(readN.value);


            // populate traitKeys

            HeldStrings[] traitKeys = Globals_Server.cMapper.Fetch<HeldStrings>("WHERE id = ?", "traitKeys").ToArray();
            HeldStrings readT = traitKeys[0];

            Globals_Game.traitKeys = JsonConvert.DeserializeObject<List<string>>(readT.value);

            // populate langKeys

            HeldStrings[] langKeys = Globals_Server.cMapper.Fetch<HeldStrings>("WHERE id = ?", "langKeys").ToArray();
            HeldStrings readL = langKeys[0];

            Globals_Game.langKeys = JsonConvert.DeserializeObject<List<string>>(readL.value);

            // populate baseLangKeys

            HeldStrings[] baseLangKeys = Globals_Server.cMapper.Fetch<HeldStrings>("WHERE id = ?", "baseLangKeys").ToArray();
            HeldStrings readBl = baseLangKeys[0];

            Globals_Game.baseLangKeys = JsonConvert.DeserializeObject<List<string>>(readBl.value);

            // populate rankKeys

            HeldStrings[] rankKeys = Globals_Server.cMapper.Fetch<HeldStrings>("WHERE id = ?", "rankKeys").ToArray();
            HeldStrings readR = rankKeys[0];

            Globals_Game.rankKeys = JsonConvert.DeserializeObject<List<byte>>(readR.value);

            // populate positionKeys

            HeldStrings[] positionKeys = Globals_Server.cMapper.Fetch<HeldStrings>("WHERE id = ?", "positionKeys").ToArray();
            HeldStrings readP = positionKeys[0];

            Globals_Game.positionKeys = JsonConvert.DeserializeObject<List<byte>>(readP.value);



            // populate npcKeys

            HeldStrings[] npcKeys = Globals_Server.cMapper.Fetch<HeldStrings>("WHERE id = ?", "npcKeys").ToArray();
            HeldStrings readNPC = npcKeys[0];

            Globals_Game.npcKeys = JsonConvert.DeserializeObject<List<string>>(readNPC.value);

            // populate pcKeys

            HeldStrings[] pcKeys = Globals_Server.cMapper.Fetch<HeldStrings>("WHERE id = ?", "pcKeys").ToArray();
            HeldStrings readPC = pcKeys[0];

            Globals_Game.pcKeys = JsonConvert.DeserializeObject<List<string>>(readPC.value);

            // populate kingKeys

            HeldStrings[] kingKeys = Globals_Server.cMapper.Fetch<HeldStrings>("WHERE id = ?", "kingKeys").ToArray();
            HeldStrings readK = kingKeys[0];

            Globals_Game.kingKeys = JsonConvert.DeserializeObject<List<string>>(readK.value);

            // populate provKeys

            HeldStrings[] provKeys = Globals_Server.cMapper.Fetch<HeldStrings>("WHERE id = ?", "provKeys").ToArray();
            HeldStrings readPr = provKeys[0];

            Globals_Game.provKeys = JsonConvert.DeserializeObject<List<string>>(readPr.value);

            // populate terrKeys

            HeldStrings[] terrKeys = Globals_Server.cMapper.Fetch<HeldStrings>("WHERE id = ?", "terrKeys").ToArray();
            HeldStrings readTer = terrKeys[0];

            Globals_Game.terrKeys = JsonConvert.DeserializeObject<List<string>>(readTer.value);

            // populate victoryDataKeys

            HeldStrings[] victoryDataKeys = Globals_Server.cMapper.Fetch<HeldStrings>("WHERE id = ?", "victoryDataKeys").ToArray();
            HeldStrings readV = victoryDataKeys[0];

            Globals_Game.victoryDataKeys = JsonConvert.DeserializeObject<List<string>>(readV.value);

            // populate fiefKeys

            HeldStrings[] fiefKeys = Globals_Server.cMapper.Fetch<HeldStrings>("WHERE id = ?", "fiefKeys").ToArray();
            HeldStrings readFi = fiefKeys[0];

            Globals_Game.fiefKeys = JsonConvert.DeserializeObject<List<string>>(readFi.value);

            // populate armyKeys

            HeldStrings[] armyKeys = Globals_Server.cMapper.Fetch<HeldStrings>("WHERE id = ?", "armyKeys").ToArray();
            HeldStrings readA = armyKeys[0];

            Globals_Game.armyKeys = JsonConvert.DeserializeObject<List<string>>(readA.value);

            // populate siegeKeys

            HeldStrings[] siegeKeys = Globals_Server.cMapper.Fetch<HeldStrings>("WHERE id = ?", "siegeKeys").ToArray();
            HeldStrings readS = siegeKeys[0];

            Globals_Game.siegeKeys = JsonConvert.DeserializeObject<List<string>>(readS.value);

            // populate clientKeys

            HeldStrings[] clientKeys = Globals_Server.cMapper.Fetch<HeldStrings>("WHERE id = ?", "clientKeys").ToArray();
            HeldStrings readCl = clientKeys[0];

            Globals_Server.client_keys = JsonConvert.DeserializeObject<List<string>>(readCl.value);

        }

        /// <summary>
        /// Reads a GameClock object from the database
        /// </summary>
        /// <returns>GameClock object</returns>
        /// <param name="gameID">Game for which clock to be retrieved</param>
        /// <param name="clockID">ID of clock to be retrieved</param>
        public static GameClock DatabaseRead_clock(string gameID, string clockID)
        {
            var clockResult = Globals_Server.rClient.Get(gameID, clockID);
            var newClock = new GameClock();

            if (clockResult.IsSuccess)
            {
                newClock = clockResult.Value.GetObject<GameClock>();
            }
            else
            {
                string toDisplay = "InitialDBload: Unable to retrieve GameClock " + clockID;
                Globals_Server.logError(toDisplay);
            }

            return newClock;
        }

        public static GameClock Cass_DatabaseRead_GameClock(string clockID)
        {
            GameClock_Simple[] retrievedClock = Globals_Server.cMapper.Fetch<GameClock_Simple>("WHERE id = ?", clockID).ToArray();
            GameClock_Simple gc = retrievedClock[0];
            GameClock newClock = new GameClock(gc);

            return newClock;
        }

        /// <summary>
        /// Reads a Dictionary(string, uint[]) from the database
        /// </summary>
        /// <returns>Dictionary(string, uint[]) object</returns>
        /// <param name="gameID">Game for which Dictionary to be retrieved</param>
        /// <param name="dictID">ID of Dictionary to be retrieved</param>
        public static Dictionary<string, uint[]> DatabaseRead_dictStringUint(string gameID, string dictID)
        {
            var dictResult = Globals_Server.rClient.Get(gameID, dictID);
            var newDict = new Dictionary<string, uint[]>();

            if (dictResult.IsSuccess)
            {
                newDict = dictResult.Value.GetObject<Dictionary<string, uint[]>>();
            }
            else
            {
                string toDisplay = "InitialDBload: Unable to retrieve Dictionary " + dictID;
                Globals_Server.logError(toDisplay);
            }

            return newDict;
        }

        public static Dictionary<string, uint[]> Cass_DatabaseRead_dictStringUint(string dictID)
        {
            HeldStrings[] retrieveddictStringUint = Globals_Server.cMapper.Fetch<HeldStrings>("WHERE id = ?", dictID).ToArray();
            HeldStrings read = retrieveddictStringUint[0];

            return JsonConvert.DeserializeObject<Dictionary<string, uint[]>>(read.value);

        }

        /// <summary>
        /// Reads a Dictionary(string, double[]) from the database
        /// </summary>
        /// <returns>Dictionary(string, double[]) object</returns>
        /// <param name="gameID">Game for which Dictionary to be retrieved</param>
        /// <param name="dictID">ID of Dictionary to be retrieved</param>
        public static Dictionary<string, double[]> DatabaseRead_dictStringDouble(string gameID, string dictID)
        {
            var dictResult = Globals_Server.rClient.Get(gameID, dictID);
            var newDict = new Dictionary<string, double[]>();

            if (dictResult.IsSuccess)
            {
                newDict = dictResult.Value.GetObject<Dictionary<string, double[]>>();
            }
            else
            {
                string toDisplay = "InitialDBload: Unable to retrieve Dictionary " + dictID;
                Globals_Server.logError(toDisplay);
            }

            return newDict;
        }

        public static Dictionary<string, double[]> Cass_DatabaseRead_dictStringDouble(string dictID)
        {
            HeldStrings[] retrieveddictStringdouble = Globals_Server.cMapper.Fetch<HeldStrings>("WHERE id = ?", dictID).ToArray();
            HeldStrings read = retrieveddictStringdouble[0];

            return JsonConvert.DeserializeObject<Dictionary<string, double[]>>(read.value);

        }

        /// <summary>
        /// Reads a Dictionary(uint, string) from the database
        /// </summary>
        /// <returns>Dictionary(uint, string) object</returns>
        /// <param name="gameID">Game for which Dictionary to be retrieved</param>
        /// <param name="dictID">ID of Dictionary to be retrieved</param>
        public static Dictionary<uint, string> DatabaseRead_dictIntString(string gameID, string dictID)
        {
            var dictResult = Globals_Server.rClient.Get(gameID, dictID);
            var newDict = new Dictionary<uint, string>();

            if (dictResult.IsSuccess)
            {
                newDict = dictResult.Value.GetObject<Dictionary<uint, string>>();
            }
            else
            {
                string toDisplay = "InitialDBload: Unable to retrieve Dictionary " + dictID;
                Globals_Server.logError(toDisplay);
            }

            return newDict;
        }

        public static Dictionary<uint, string> Cass_DatabaseRead_dictIntString(string dictID)
        {
            HeldStrings[] retrieveddictIntString = Globals_Server.cMapper.Fetch<HeldStrings>("WHERE id = ?", dictID).ToArray();
            HeldStrings read = retrieveddictIntString[0];

            return JsonConvert.DeserializeObject<Dictionary<uint, string>>(read.value);

        }

        /// <summary>
        /// Reads a Dictionary(string, byte) from the database
        /// </summary>
        /// <returns>Dictionary(string, byte) object</returns>
        /// <param name="gameID">Game for which Dictionary to be retrieved</param>
        /// <param name="dictID">ID of Dictionary to be retrieved</param>
        public static Dictionary<string[], byte> DatabaseRead_dictStringByte(string gameID, string dictID)
        {
            Dictionary<string[], byte> dictOut = new Dictionary<string[], byte>();
            var dictResult = Globals_Server.rClient.Get(gameID, dictID);
            var tempDict = new Dictionary<string, byte>();

            if (dictResult.IsSuccess)
            {
                tempDict = dictResult.Value.GetObject<Dictionary<string, byte>>();
                dictOut = DatabaseRead.JentryPriorities_deserialise(tempDict);
            }
            else
            {
                string toDisplay = "InitialDBload: Unable to retrieve Dictionary " + dictID;
                Globals_Server.logError(toDisplay);
            }

            return dictOut;
        }

        public static Dictionary<string[], byte> Cass_DatabaseRead_dictStringByte(string dictID)
        {
            HeldStrings[] retrieveddictStringByte = Globals_Server.cMapper.Fetch<HeldStrings>("WHERE id = ?", dictID).ToArray();
            HeldStrings read = retrieveddictStringByte[0];

            Dictionary<string, byte> tempDict = JsonConvert.DeserializeObject<Dictionary<string, byte>>(read.value);

            Dictionary<string[], byte> deSerDSB = JentryPriorities_deserialise(tempDict);

            return deSerDSB;

        }

        /// <summary>
        /// Deserialises a jEntryPriorities Dictionary
        /// </summary>
        /// <returns>Dictionary<string[], byte> for game use</returns>
        /// <param name="dictToConvert">The Dictionary to convert</param>
        public static Dictionary<string[], byte> JentryPriorities_deserialise(Dictionary<string, byte> dictToConvert)
        {
            Dictionary<string[], byte> dictOut = new Dictionary<string[], byte>();

            if (dictToConvert.Count > 0)
            {
                foreach (KeyValuePair<string, byte> thisEntry in dictToConvert)
                {
                    string[] thisKey = thisEntry.Key.Split('|');
                    dictOut.Add(thisKey, thisEntry.Value);
                }
            }

            return dictOut;
        }

        /// <summary>
        /// Reads a Dictionary(string, OwnershipChallenge) from the database
        /// </summary>
        /// <returns>Dictionary(string, OwnershipChallenge) object</returns>
        /// <param name="gameID">Game for which Dictionary to be retrieved</param>
        /// <param name="dictID">ID of Dictionary to be retrieved</param>
        public static Dictionary<string, OwnershipChallenge> DatabaseRead_dictStringOwnCh(string gameID, string dictID)
        {
            Dictionary<string, OwnershipChallenge> dictOut = new Dictionary<string, OwnershipChallenge>();
            var dictResult = Globals_Server.rClient.Get(gameID, dictID);

            if (dictResult.IsSuccess)
            {
                dictOut = dictResult.Value.GetObject<Dictionary<string, OwnershipChallenge>>();
            }
            else
            {
                string toDisplay = "InitialDBload: Unable to retrieve Dictionary " + dictID;
                Globals_Server.logError(toDisplay);
            }

            return dictOut;
        }

        public static Dictionary<string, OwnershipChallenge> Cass_DatabaseRead_dictStringOwnCh(string dictID)
        {
            HeldStrings[] retrieveddictStringOwnCh = Globals_Server.cMapper.Fetch<HeldStrings>("WHERE id = ?", dictID).ToArray();
            HeldStrings read = retrieveddictStringOwnCh[0];

            return JsonConvert.DeserializeObject<Dictionary<string, OwnershipChallenge>>(read.value);

        }

        /// <summary>
        /// Reads a newID variable from the database
        /// </summary>
        /// <returns>uint</returns>
        /// <param name="gameID">Game for which newID variable to be retrieved</param>
        /// <param name="clockID">newID variable to be retrieved</param>
        public static uint DatabaseRead_newIDs(string gameID, string newID)
        {
            var newIDResult = Globals_Server.rClient.Get(gameID, newID);
            uint newIDout = 0;

            if (newIDResult.IsSuccess)
            {
                newIDout = newIDResult.Value.GetObject<uint>();
            }
            else
            {
                string toDisplay = "InitialDBload: Unable to retrieve newID variable " + newID;
                Globals_Server.logError(toDisplay);
            }

            return newIDout;
        }

        public static uint Cass_DatabaseRead_newIDs(string newID)
        {
            HeldStrings[] retrievedId = Globals_Server.cMapper.Fetch<HeldStrings>("WHERE id = ?", newID).ToArray();
            HeldStrings read = retrievedId[0];
            
            return UInt32.Parse(read.value);
        }

        /// <summary>
        /// Reads a Journal object from the database
        /// </summary>
        /// <returns>Journal object</returns>
        /// <param name="gameID">Game for which Journal to be retrieved</param>
        /// <param name="journalID">ID of Journal to be retrieved</param>
        public static Journal DatabaseRead_journal(string gameID, string journalID)
        {
            var journalResult = Globals_Server.rClient.Get(gameID, journalID);
            var newJournal = new Journal();

            if (journalResult.IsSuccess)
            {
                newJournal = journalResult.Value.GetObject<Journal>();
            }
            else
            {
                string toDisplay = "InitialDBload: Unable to retrieve Journal " + journalID;
                Globals_Server.logError(toDisplay);
            }

            return newJournal;
        }

        public static Journal Cass_DatabaseRead_Journal(string journalID)
        {
            Journal_Simple[] retrievedJournal = Globals_Server.cMapper.Fetch<Journal_Simple>("WHERE id = ?", journalID).ToArray();
            Journal_Simple read = retrievedJournal[0];
            Journal newJournal = new Journal(read);

            return newJournal;
        }


        /// <summary>
        /// Reads a VictoryData object from the database
        /// </summary>
        /// <returns>VictoryData object</returns>
        /// <param name="gameID">Game for which VictoryData to be retrieved</param>
        /// <param name="vicDataID">ID of VictoryData to be retrieved</param>
        public static VictoryData DatabaseRead_victoryData(string gameID, string vicDataID)
        {
            var vicDataResult = Globals_Server.rClient.Get(gameID, vicDataID);
            var newVictoryData = new VictoryData();

            if (vicDataResult.IsSuccess)
            {
                newVictoryData = vicDataResult.Value.GetObject<VictoryData>();
            }
            else
            {
                string toDisplay = "InitialDBload: Unable to retrieve VictoryData " + vicDataID;
                Globals_Server.logError(toDisplay);
            }

            return newVictoryData;
        }

        public static VictoryData Cass_DatabaseRead_VictoryData(string vicDataID)
        {
            VictoryData[] retrievedVData = Globals_Server.cMapper.Fetch<VictoryData>("WHERE playerid = ?", vicDataID).ToArray();
            VictoryData read = retrievedVData[0];

            return read;
        }

        /// <summary>
        /// Reads a Trait object from the database
        /// </summary>
        /// <returns>Trait object</returns>
        /// <param name="gameID">Game for which trait to be retrieved</param>
        /// <param name="traitID">ID of trait to be retrieved</param>
        public static Trait DatabaseRead_trait(string gameID, string traitID)
        {
            var traitResult = Globals_Server.rClient.Get(gameID, traitID);
            var newTrait = new Trait();

            if (traitResult.IsSuccess)
            {
                newTrait = traitResult.Value.GetObject<Trait>();
            }
            else
            {
                string toDisplay = "InitialDBload: Unable to retrieve trait " + traitID;
                Globals_Server.logError(toDisplay);
            }

            return newTrait;
        }

        public static Trait Cass_DatabaseRead_Trait(string traitID)
        {
            Trait_Simple[] retrievedTrait = Globals_Server.cMapper.Fetch<Trait_Simple>("WHERE id = ?", traitID).ToArray();
            Trait_Simple tr = retrievedTrait[0];
            Trait newTrait = new Trait(tr);

            return newTrait;
        }

        /// <summary>
        /// Reads a BaseLanguage object from the database
        /// </summary>
        /// <returns>BaseLanguage object</returns>
        /// <param name="gameID">Game for which BaseLanguage to be retrieved</param>
        /// <param name="bLangID">ID of Language to be retrieved</param>
        public static BaseLanguage DatabaseRead_baseLanguage(string gameID, string bLangID)
        {
            var bLangResult = Globals_Server.rClient.Get(gameID, bLangID);
            var newBaseLang = new BaseLanguage();

            if (bLangResult.IsSuccess)
            {
                newBaseLang = bLangResult.Value.GetObject<BaseLanguage>();
            }
            else
            {
                string toDisplay = "InitialDBload: Unable to retrieve BaseLanguage " + bLangID;
                Globals_Server.logError(toDisplay);
            }

            return newBaseLang;
        }

        public static BaseLanguage Cass_DatabaseRead_BaseLanguage(string bLangID)
        {
            BaseLanguage[] retrievedBaseLanguage = Globals_Server.cMapper.Fetch<BaseLanguage>("WHERE id = ?", bLangID).ToArray();
            BaseLanguage newBaseLanguage = new BaseLanguage();
            newBaseLanguage = retrievedBaseLanguage[0];

            return newBaseLanguage;
        }

        /// <summary>
        /// Reads a Language object from the database
        /// </summary>
        /// <returns>Language object</returns>
        /// <param name="gameID">Game for which Language to be retrieved</param>
        /// <param name="langID">ID of Language to be retrieved</param>
        public static Language DatabaseRead_language(string gameID, string langID)
        {
            var languageResult = Globals_Server.rClient.Get(gameID, langID);
            var langSer = new Language_Serialised();
            var newLanguage = new Language();

            if (languageResult.IsSuccess)
            {
                // extract Language_Serialised
                langSer = languageResult.Value.GetObject<Language_Serialised>();

                // create Language from Language_Serialised
                newLanguage = DatabaseRead.Language_deserialise(langSer);
            }
            else
            {
                string toDisplay = "InitialDBload: Unable to retrieve Language " + langID;
                Globals_Server.logError(toDisplay);
            }

            return newLanguage;
        }

        public static Language Cass_DatabaseRead_Language(string langID)
        {
            Language_Serialised[] retrievedLanguage = Globals_Server.cMapper.Fetch<Language_Serialised>("WHERE id = ?", langID).ToArray();
            Language_Serialised newSLanguage = new Language_Serialised();
            newSLanguage = retrievedLanguage[0];

            Language newLanguage = new Language();
            newLanguage = DatabaseRead.Temp_Language_deserialise(newSLanguage);

            return newLanguage;
        }

        //while Globals_game not ready use this instead
        public static Language Temp_Language_deserialise(Language_Serialised ls)
        {
            Language langOut = null;

            // create Language from Language_Serialised
            langOut = new Language(ls);

            langOut.baseLanguage = Cass_DatabaseRead_BaseLanguage(ls.baseLanguage);

            return langOut;
        }

        /// <summary>
        /// Deserialises a Language object
        /// </summary>
        /// <returns>Language object</returns>
        /// <param name="ls">Language_Serialised object to be converted</param>
        public static Language Language_deserialise(Language_Serialised ls)
        {
            Language langOut = null;

            // create Language from Language_Serialised
            langOut = new Language(ls);

            // insert BaseLanguage
            langOut.baseLanguage = Globals_Game.baseLanguageMasterList[ls.baseLanguage];

            return langOut;
        }

        /// <summary>
        /// Reads a Nationality object from the database
        /// </summary>
        /// <returns>Nationality object</returns>
        /// <param name="gameID">Game for which Nationality to be retrieved</param>
        /// <param name="natID">ID of Nationality to be retrieved</param>
        public static Nationality DatabaseRead_nationality(string gameID, string natID)
        {
            var natResult = Globals_Server.rClient.Get(gameID, natID);
            var newNat = new Nationality();

            if (natResult.IsSuccess)
            {
                newNat = natResult.Value.GetObject<Nationality>();
            }
            else
            {
                string toDisplay = "InitialDBload: Unable to retrieve Nationality " + natID;
                Globals_Server.logError(toDisplay);
            }

            return newNat;
        }

        public static Nationality Cass_DatabaseRead_Nationality(string natID)
        {
            Nationality[] retrievedNationality = Globals_Server.cMapper.Fetch<Nationality>("WHERE natID = ?", natID).ToArray();
            Nationality newNationality = new Nationality();
            newNationality = retrievedNationality[0];

            return newNationality;
        }

        /// <summary>
        /// Reads a Rank object from the database
        /// </summary>
        /// <returns>Rank object</returns>
        /// <param name="gameID">Game for which Rank to be retrieved</param>
        /// <param name="rankID">ID of Rank to be retrieved</param>
        public static Rank DatabaseRead_rank(string gameID, string rankID)
        {
            var rankResult = Globals_Server.rClient.Get(gameID, rankID);
            var newRank = new Rank();

            if (rankResult.IsSuccess)
            {
                newRank = rankResult.Value.GetObject<Rank>();
            }
            else
            {
                string toDisplay = "InitialDBload: Unable to retrieve Rank " + rankID;
                Globals_Server.logError(toDisplay);
            }

            return newRank;
        }

        public static Rank Cass_DatabaseRead_Rank(string rankID)
        {
            Rank_Simple[] retrievedRank = Globals_Server.cMapper.Fetch<Rank_Simple>("WHERE id = ?", rankID).ToArray();
            Rank_Simple read = retrievedRank[0];
            Rank newRank = new Rank(read);

            return newRank;
        }

        /// <summary>
        /// Reads a Position object from the database
        /// </summary>
        /// <returns>Position object</returns>
        /// <param name="gameID">Game for which Position to be retrieved</param>
        /// <param name="posID">ID of Position to be retrieved</param>
        public static Position DatabaseRead_position(string gameID, string posID)
        {
            var posResult = Globals_Server.rClient.Get(gameID, posID);
            var posSer = new Position_Serialised();
            var newPos = new Position();

            if (posResult.IsSuccess)
            {
                // extract Position_Serialised object
                posSer = posResult.Value.GetObject<Position_Serialised>();

                // create Position from Position_Serialised
                newPos = DatabaseRead.Position_deserialise(posSer);
            }
            else
            {
                string toDisplay = "InitialDBload: Unable to retrieve Position " + posID;
                Globals_Server.logError(toDisplay);
            }

            return newPos;
        }

        public static Position Cass_DatabaseRead_Position(string posID)
        {
            Position_Simple[] retrievedRank = Globals_Server.cMapper.Fetch<Position_Simple>("WHERE id = ?", posID).ToArray();
            Position_Simple read = retrievedRank[0];
            Position_Serialised newPosSer = new Position_Serialised(read);
            Position newPosition = new Position(newPosSer);
            //will be better to use deserialise once full database in operation
            newPosition.nationality = Cass_DatabaseRead_Nationality(read.nationality);
            return newPosition;
        }

        /// <summary>
        /// Deserialises a Position object
        /// </summary>
        /// <returns>Position object</returns>
        /// <param name="ps">Position_Serialised object to be converted</param>
        public static Position Position_deserialise(Position_Serialised ps)
        {
            Position posOut = null;

            // create Position from Position_Serialised
            posOut = new Position(ps);

            // insert nationality
            if (Globals_Game.nationalityMasterList.ContainsKey(ps.nationality))
            {
                posOut.nationality = Globals_Game.nationalityMasterList[ps.nationality];
            }

            return posOut;
        }

        /// <summary>
        /// Reads a Siege object from the database
        /// </summary>
        /// <returns>Siege object</returns>
        /// <param name="gameID">Game for which Siege to be retrieved</param>
        /// <param name="siegeID">ID of Siege to be retrieved</param>
        public static Siege DatabaseRead_Siege(string gameID, string siegeID)
        {
            var siegeResult = Globals_Server.rClient.Get(gameID, siegeID);
            Siege mySiege = new Siege();

            if (siegeResult.IsSuccess)
            {
                // extract Army object
                mySiege = siegeResult.Value.GetObject<Siege>();
            }
            else
            {
                string toDisplay = "InitialDBload: Unable to retrieve Siege " + siegeID;
                Globals_Server.logError(toDisplay);
            }

            return mySiege;
        }

        public static Siege Cass_DatabaseRead_Siege(string siegeID)
        {
            Siege_Simple[] retrievedSiege = Globals_Server.cMapper.Fetch<Siege_Simple>("WHERE siegeID = ?", siegeID).ToArray();
            Siege_Simple read = retrievedSiege[0];
            Siege newSiege = new Siege(read);

            return newSiege;
        }

        /// <summary>
        /// Reads a Siege object from the database
        /// </summary>
        /// <returns>Siege object</returns>
        /// <param name="gameID">Game for which Siege to be retrieved</param>
        /// <param name="siegeID">ID of Siege to be retrieved</param>
        public static Faction DatabaseRead_Faction(string factID)
        {
            var factResult = Globals_Server.rClient.Get(BucketTypes.FactionBucket.ToString(), factID);
            Faction faction = new Faction();

            if (factResult.IsSuccess)
            {
                // extract Army object
                faction = factResult.Value.GetObject<Faction>();
            }
            else
            {
                string toDisplay = "InitialDBload: Unable to retrieve faction request: " + factID;
                Globals_Server.logError(toDisplay);
                faction = null;
            }

            return faction;
        }

        public static Faction Cass_DatabaseRead_Faction(string factID)
        {
            Faction_Simple[] retrievedFaction = Globals_Server.cMapper.Fetch<Faction_Simple>("WHERE factionReqID = ?", factID).ToArray();
            Faction_Simple read = retrievedFaction[0];
            Faction newFaction = new Faction(read);

            return newFaction;
        }

        /// <summary>
        /// Reads a Siege object from the database
        /// </summary>
        /// <returns>Siege object</returns>
        /// <param name="gameID">Game for which Siege to be retrieved</param>
        /// <param name="siegeID">ID of Siege to be retrieved</param>
        public static Tuple<byte[], byte[]> DatabaseRead_User(string username)
        {
            var userResult = Globals_Server.rClient.Get(BucketTypes.AccountBucket.ToString(), username);

            if (!userResult.IsSuccess) {
                string toDisplay = "InitialDBload: Unable to retrieve faction request: " + username;
                Globals_Server.logError(toDisplay);

                return null;
            }

            Tuple<byte[], byte[]> hashes = userResult.Value.GetObject<Tuple<byte[], byte[]>>();

            return hashes;
        }

        /// <summary>
        /// Reads an Army object from the database
        /// </summary>
        /// <returns>Army object</returns>
        /// <param name="gameID">Game for which Army to be retrieved</param>
        /// <param name="armyID">ID of Army to be retrieved</param>
        public static Army DatabaseRead_Army(string gameID, string armyID)
        {
            var armyResult = Globals_Server.rClient.Get(gameID, armyID);
            Army myArmy = new Army();

            if (armyResult.IsSuccess)
            {
                // extract Army object
                myArmy = armyResult.Value.GetObject<Army>();
            }
            else
            {
                string toDisplay = "InitialDBload: Unable to retrieve Army " + armyID;
                Globals_Server.logError(toDisplay);
            }

            return myArmy;
        }

        public static Army Cass_DatabaseRead_Army(string armyID)
        {
            Army_Simple[] retrievedArmy = Globals_Server.cMapper.Fetch<Army_Simple>("WHERE armyID = ?", armyID).ToArray();
            Army_Simple read = retrievedArmy[0];
            Army newArmy = new Army(read);

            return newArmy;
        }

        /// <summary>
        /// Reads an NPC object from the database
        /// </summary>
        /// <returns>NonPlayerCharacter object</returns>
        /// <param name="gameID">Game for which NPC to be retrieved</param>
        /// <param name="npcID">ID of NPC to be retrieved</param>
        public static NonPlayerCharacter DatabaseRead_NPC(string gameID, string npcID)
        {
            var npcResult = Globals_Server.rClient.Get(gameID, npcID);
            var npcSer = new NonPlayerCharacter_Serialised();
            NonPlayerCharacter myNPC = new NonPlayerCharacter();

            if (npcResult.IsSuccess)
            {
                // extract NonPlayerCharacter_Serialised object
                npcSer = npcResult.Value.GetObject<NonPlayerCharacter_Serialised>();

                // if NonPlayerCharacter_Serialised goTo queue contains entries, store for later processing
                if (npcSer.goTo.Count > 0)
                {
                    Globals_Game.goToList.Add(npcSer);
                }

                // create NonPlayerCharacter from NonPlayerCharacter_Serialised
                myNPC = DatabaseRead.NPC_deserialise(npcSer);
            }
            else
            {
                string toDisplay = "InitialDBload: Unable to retrieve NonPlayerCharacter " + npcID;
                Globals_Server.logError(toDisplay);
            }

            return myNPC;
        }

        public static NonPlayerCharacter Cass_DatabaseRead_NonPlayerCharacter(string npcID)
        {
            NonPlayerCharacter_Simple[] retrievedNPC = Globals_Server.cMapper.Fetch<NonPlayerCharacter_Simple>("WHERE charID = ?", npcID).ToArray();
            NonPlayerCharacter_Simple read = retrievedNPC[0];
            NonPlayerCharacter_Serialised newNonPlayerCharacterSer = new NonPlayerCharacter_Serialised(read);
            // if NonPlayerCharacter_Serialised goTo queue contains entries, store for later processing
            if (newNonPlayerCharacterSer.goTo.Count > 0)
            {
                Globals_Game.goToList.Add(newNonPlayerCharacterSer);
            }
            NonPlayerCharacter newNonPlayerCharacter = Temp_NPC_Deserialise(newNonPlayerCharacterSer);
            return newNonPlayerCharacter;
        }

        public static NonPlayerCharacter Temp_NPC_Deserialise(NonPlayerCharacter_Serialised npcs)
        {
            // create NonPlayerCharacter from NonPlayerCharacter_Serialised
            NonPlayerCharacter npcFull = new NonPlayerCharacter(npcs);

            npcFull.language = DatabaseRead.Cass_DatabaseRead_Language(npcs.language);

            npcFull.nationality = DatabaseRead.Cass_DatabaseRead_Nationality(npcs.nationality);

            if (npcs.traits.Length > 0)
            {
                for (int i = 0; i < npcs.traits.Length; i++)
                {
                    npcFull.traits[i] = new Tuple<Trait, int>(DatabaseRead.Cass_DatabaseRead_Trait(npcs.traits[i].Item1), npcs.traits[i].Item2);
                }
            }

            return npcFull;
        }

        /// <summary>
        /// Deserialises a NonPlayerCharacter object
        /// </summary>
        /// <returns>NonPlayerCharacter object</returns>
        /// <param name="npcs">NonPlayerCharacter_Serialised object to be converted</param>
        public static NonPlayerCharacter NPC_deserialise(NonPlayerCharacter_Serialised npcs)
        {
            NonPlayerCharacter npcOut = null;
            // create NonPlayerCharacter from NonPlayerCharacter_Serialised
            npcOut = new NonPlayerCharacter(npcs);

            // insert language
            npcOut.language = Globals_Game.languageMasterList[npcs.language];

            // insert nationality
            npcOut.nationality = Globals_Game.nationalityMasterList[npcs.nationality];

            // insert traits
            if (npcs.traits.Length > 0)
            {
                for (int i = 0; i < npcs.traits.Length; i++)
                {
                    npcOut.traits[i] = new Tuple<Trait, int>(Globals_Game.traitMasterList[npcs.traits[i].Item1], npcs.traits[i].Item2);
                }
            }

            return npcOut;
        }

        /// <summary>
        /// Reads a PC object from the database
        /// </summary>
        /// <returns>PlayerCharacter object</returns>
        /// <param name="gameID">Game for which PC to be retrieved</param>
        /// <param name="pcID">ID of PC to be retrieved</param>
        public static PlayerCharacter DatabaseRead_PC(string gameID, string pcID)
        {
            var pcResult = Globals_Server.rClient.Get(gameID, pcID);
            var pcSer = new PlayerCharacter_Serialised();
            PlayerCharacter myPC = new PlayerCharacter();

            if (pcResult.IsSuccess)
            {
                // extract PlayerCharacter_Serialised object
                pcSer = pcResult.Value.GetObject<PlayerCharacter_Serialised>();

                // if PlayerCharacter_Serialised goTo queue contains entries, store for later processing
                if (pcSer.goTo.Count > 0)
                {
                    Globals_Game.goToList.Add(pcSer);
                }

                // create PlayerCharacter from PlayerCharacter_Serialised
                myPC = DatabaseRead.PC_deserialise(pcSer);
            }
            else
            {
				string toDisplay = "InitialDBload: Unable to retrieve PlayerCharacter " + pcID + ": "+pcResult.ErrorMessage;
                Globals_Server.logError(toDisplay);
            }

            return myPC;
        }

        public static PlayerCharacter Cass_DatabaseRead_PlayerCharacter(string pcID)
        {
            PlayerCharacter_Simple[] retrievedNPC = Globals_Server.cMapper.Fetch<PlayerCharacter_Simple>("WHERE charID = ?", pcID).ToArray();
            PlayerCharacter_Simple read = retrievedNPC[0];
            PlayerCharacter_Serialised newPlayerCharacterSer = new PlayerCharacter_Serialised(read);
            // if PlayerCharacter_Serialised goTo queue contains entries, store for later processing
            if (newPlayerCharacterSer.goTo.Count > 0)
            {
                Globals_Game.goToList.Add(newPlayerCharacterSer);
            }
            PlayerCharacter newPlayerCharacter = Temp_PC_Deserialise(newPlayerCharacterSer);
            return newPlayerCharacter;
        }

        public static PlayerCharacter Temp_PC_Deserialise(PlayerCharacter_Serialised pcs)
        {
            // create NonPlayerCharacter from NonPlayerCharacter_Serialised
            PlayerCharacter pcFull = new PlayerCharacter(pcs);

            pcFull.language = DatabaseRead.Cass_DatabaseRead_Language(pcs.language);

            pcFull.nationality = DatabaseRead.Cass_DatabaseRead_Nationality(pcs.nationality);

            if (pcs.traits.Length > 0)
            {
                for (int i = 0; i < pcs.traits.Length; i++)
                {
                    pcFull.traits[i] = new Tuple<Trait, int>(DatabaseRead.Cass_DatabaseRead_Trait(pcs.traits[i].Item1), pcs.traits[i].Item2);
                }
            }

            // insert employees/family
            if (pcs.myNPCs.Count > 0)
            {
                for (int i = 0; i < pcs.myNPCs.Count; i++)
                {
                    pcFull.myNPCs[i] = DatabaseRead.Cass_DatabaseRead_NonPlayerCharacter(pcs.myNPCs[i]);
                }
            }

            // insert armies
            if (pcs.myArmies.Count > 0)
            {
                for (int i = 0; i < pcs.myArmies.Count; i++)
                {
                    pcFull.myArmies[i] = DatabaseRead.Cass_DatabaseRead_Army(pcs.myArmies[i]);
                }
            }

            return pcFull;
        }

        /// <summary>
        /// Deserialises a PlayerCharacter object
        /// </summary>
        /// <returns>PlayerCharacter object</returns>
        /// <param name="pcs">PlayerCharacter_Serialised object to be converted</param>
        public static PlayerCharacter PC_deserialise(PlayerCharacter_Serialised pcs)
        {
            PlayerCharacter pcOut = null;

            // create PlayerCharacter from PlayerCharacter_Serialised
            pcOut = new PlayerCharacter(pcs);

            // insert language
            pcOut.language = Globals_Game.languageMasterList[pcs.language];

            // insert nationality
            pcOut.nationality = Globals_Game.nationalityMasterList[pcs.nationality];

            // insert traits
            if (pcs.traits.Length > 0)
            {
                for (int i = 0; i < pcs.traits.Length; i++)
                {
                    pcOut.traits[i] = new Tuple<Trait, int>(Globals_Game.traitMasterList[pcs.traits[i].Item1], pcs.traits[i].Item2);
                }
            }

            // insert employees/family
            if (pcs.myNPCs.Count > 0)
            {
                for (int i = 0; i < pcs.myNPCs.Count; i++)
                {
                    pcOut.myNPCs.Add(Globals_Game.npcMasterList[pcs.myNPCs[i]]);
                }
            }

            // insert armies
            if (pcs.myArmies.Count > 0)
            {
                for (int i = 0; i < pcs.myArmies.Count; i++)
                {
                    pcOut.myArmies.Add(Globals_Game.armyMasterList[pcs.myArmies[i]]);
                }
            }

            return pcOut;
        }

        /// <summary>
        /// Reads a Kingdom object from the database
        /// </summary>
        /// <returns>Kingdom object</returns>
        /// <param name="gameID">Game for which Kingdom to be retrieved</param>
        /// <param name="kingID">ID of Kingdom to be retrieved</param>
        public static Kingdom DatabaseRead_Kingdom(string gameID, string kingID)
        {
            var kingResult = Globals_Server.rClient.Get(gameID, kingID);
            var kingSer = new Kingdom_Serialised();
            Kingdom myKing = new Kingdom();

            if (kingResult.IsSuccess)
            {
                // extract Kingdom_Serialised object
                kingSer = kingResult.Value.GetObject<Kingdom_Serialised>();

                // create Kingdom from Kingdom_Serialised
                myKing = DatabaseRead.Kingdom_deserialise(kingSer);
            }
            else
            {
                string toDisplay = "InitialDBload: Unable to retrieve Province " + kingID;
                Globals_Server.logError(toDisplay);
            }

            return myKing;
        }

        public static Kingdom Cass_DatabaseRead_Kingdom(string kingID)
        {
            Kingdom_Simple[] retrievedKingdom = Globals_Server.cMapper.Fetch<Kingdom_Simple>("WHERE id = ?", kingID).ToArray();
            Kingdom_Simple newSKingdom = retrievedKingdom[0];
            Kingdom_Serialised newSerKingdom = new Kingdom_Serialised(newSKingdom);

            Kingdom newKingdom = new Kingdom();
            newKingdom = DatabaseRead.Kingdom_deserialise(newSerKingdom);

            return newKingdom;
        }


        /// <summary>
        /// Deserialises a Kingdom object
        /// </summary>
        /// <returns>Kingdom object</returns>
        /// <param name="ks">Kingdom_Serialised to be converted</param>
        public static Kingdom Kingdom_deserialise(Kingdom_Serialised ks)
        {
            Kingdom kOut = null;
            kOut = new Kingdom(ks);

            // insert king
            if (!String.IsNullOrWhiteSpace(ks.owner))
            {
                if (Globals_Game.pcMasterList.ContainsKey(ks.owner))
                {
                    kOut.owner = Globals_Game.pcMasterList[ks.owner];
                }
                else
                {
                    string toDisplay = "Kingdom " + ks.id + ": King not found (" + ks.owner + ")";
                    Globals_Server.logError(toDisplay);
                }
            }

            // insert rank
            if (ks.rank > 0)
            {
                if (Globals_Game.rankMasterList.ContainsKey(ks.rank))
                {
                    kOut.rank = Globals_Game.rankMasterList[ks.rank];
                }
                else
                {
                    string toDisplay = "Kingdom " + ks.id + ": Rank not found (" + ks.rank + ")";
                    Globals_Server.logError(toDisplay);
                }
            }

            // insert nationality
            if (!String.IsNullOrWhiteSpace(ks.nationality))
            {
                if (Globals_Game.nationalityMasterList.ContainsKey(ks.nationality))
                {
                    kOut.nationality = Globals_Game.nationalityMasterList[ks.nationality];
                }
                else
                {
                    string toDisplay = "Kingdom " + ks.id + ": Nationality not found (" + ks.nationality + ")";
                    Globals_Server.logError(toDisplay);
                }
            }

            return kOut;
        }

        /// <summary>
        /// Reads a Province object from the database
        /// </summary>
        /// <returns>Province object</returns>
        /// <param name="gameID">Game for which Province to be retrieved</param>
        /// <param name="provID">ID of Province to be retrieved</param>
        public static Province DatabaseRead_Province(string gameID, string provID)
        {
            var provResult = Globals_Server.rClient.Get(gameID, provID);
            var provSer = new Province_Serialised();
            Province myProv = new Province();

            if (provResult.IsSuccess)
            {
                // extract Province_Serialised object
                provSer = provResult.Value.GetObject<Province_Serialised>();

                // create Province from Province_Serialised
                myProv = DatabaseRead.Province_deserialise(provSer);
            }
            else
            {
                string toDisplay = "InitialDBload: Unable to retrieve Province " + provID;
                Globals_Server.logError(toDisplay);
            }

            return myProv;
        }

        public static Province Cass_DatabaseRead_Province(string provID)
        {
            Province_Simple[] retrievedProvince = Globals_Server.cMapper.Fetch<Province_Simple>("WHERE id = ?", provID).ToArray();
            Province_Simple newSProvince = retrievedProvince[0];
            Province_Serialised newSerProvince = new Province_Serialised();
            Province newProvince = new Province();
            newProvince = DatabaseRead.Province_deserialise(newSerProvince);

            return newProvince;
        }

        /// <summary>
        /// Deserialises a Province object
        /// </summary>
        /// <returns>Province object</returns>
        /// <param name="ps">Province_Serialised to be converted</param>
        public static Province Province_deserialise(Province_Serialised ps)
        {
            Province provOut = null;
            provOut = new Province(ps);

            // insert overlord using overlordID
            if (!String.IsNullOrWhiteSpace(ps.owner))
            {
                if (Globals_Game.pcMasterList.ContainsKey(ps.owner))
                {
                    provOut.owner = Globals_Game.pcMasterList[ps.owner];
                }
                else
                {
                    string toDisplay = "Province " + ps.id + ": Overlord not found (" + ps.owner + ")";
                    Globals_Server.logError(toDisplay);
                }
            }

            // check if province is in owner's list of provinces owned
            bool provInList = provOut.owner.ownedProvinces.Any(item => item.id == provOut.id);
            // if not, add it
            if (!provInList)
            {
                provOut.owner.ownedProvinces.Add(provOut);
            }

            // insert kingdom using kingdomID
            if (!String.IsNullOrWhiteSpace(ps.kingdom))
            {
                if (Globals_Game.kingdomMasterList.ContainsKey(ps.kingdom))
                {
                    provOut.kingdom = Globals_Game.kingdomMasterList[ps.kingdom];
                }
                else
                {
                    string toDisplay = "Province " + ps.id + ": Kingdom not found (" + ps.kingdom + ")";
                    Globals_Server.logError(toDisplay);
                }
            }

            // insert rank using rankID
            if (ps.rank > 0)
            {
                if (Globals_Game.rankMasterList.ContainsKey(ps.rank))
                {
                    provOut.rank = Globals_Game.rankMasterList[ps.rank];
                }
                else
                {
                    string toDisplay = "Province " + ps.id + ": Rank not found (" + ps.rank + ")";
                    Globals_Server.logError(toDisplay);
                }
            }

            return provOut;
        }

        /// <summary>
        /// Reads a Terrain object from the database
        /// </summary>
        /// <returns>Terrain object</returns>
        /// <param name="gameID">Game for which Terrain to be retrieved</param>
        /// <param name="terrID">ID of Terrain to be retrieved</param>
        public static Terrain DatabaseRead_terrain(string gameID, string terrID)
        {
            var terrainResult = Globals_Server.rClient.Get(gameID, terrID);
            var newTerrain = new Terrain();

            if (terrainResult.IsSuccess)
            {
                newTerrain = terrainResult.Value.GetObject<Terrain>();
            }
            else
            {
                string toDisplay = "InitialDBload: Unable to retrieve Terrain " + terrID;
                Globals_Server.logError(toDisplay);
            }

            return newTerrain;
        }

        public static Terrain Cass_DatabaseRead_terrain(string terrID)
        {
            Terrain[] retrievedTerrain = Globals_Server.cMapper.Fetch<Terrain>("WHERE id = ?", terrID).ToArray();
            Terrain newTerrain = new Terrain();
            newTerrain = retrievedTerrain[0];

            return newTerrain;
        }

        /// <summary>
        /// Reads a Fief object from the database
        /// </summary>
        /// <returns>Fief object</returns>
        /// <param name="gameID">Game for which Fief to be retrieved</param>
        /// <param name="fiefID">ID of Fief to be retrieved</param>
        public static Fief DatabaseRead_Fief(string gameID, string fiefID)
        {
            var fiefResult = Globals_Server.rClient.Get(gameID, fiefID);
            var fiefSer = new Fief_Serialised();
            Fief myFief = new Fief();

            if (fiefResult.IsSuccess)
            {
                // extract Fief_Serialised object
                fiefSer = fiefResult.Value.GetObject<Fief_Serialised>();

                // create Fief from Fief_Serialised
                myFief = DatabaseRead.Fief_deserialise(fiefSer);
            }
            else
            {
                string toDisplay = "InitialDBload: Unable to retrieve Fief " + fiefID;
                Globals_Server.logError(toDisplay);
            }

            return myFief;
        }

        public static Fief Cass_DatabaseRead_Fief(string fiefID)
        {
            Fief_Simple[] retrievedFief = Globals_Server.cMapper.Fetch<Fief_Simple>("WHERE id = ?", fiefID).ToArray();
            Fief_Simple read = retrievedFief[0];
            Fief_Serialised serialised = new Fief_Serialised(read);
            Fief newFief = new Fief();
            newFief = Fief_deserialise(serialised);

            return newFief;
        }

        /// <summary>
        /// Deserialises a Fief object
        /// Also inserts Fief into appropriate PlayerCharacter and NonPlayerCharacter objects.
        /// </summary>
        /// <returns>Fief object</returns>
        /// <param name="fs">Fief_Serialised object to be converted</param>
        public static Fief Fief_deserialise(Fief_Serialised fs)
        {
            Fief fOut = null;
            // create Fief from Fief_Serialised
            fOut = new Fief(fs);

            // insert province
            fOut.province = Globals_Game.provinceMasterList[fs.province];

            // insert language
            fOut.language = Globals_Game.languageMasterList[fs.language];

            // insert owner
            fOut.owner = Globals_Game.pcMasterList[fs.owner];
            // check if fief is in owner's list of fiefs owned
            bool fiefInList = fOut.owner.ownedFiefs.Any(item => item.id == fOut.id);
            // if not, add it
            if (!fiefInList)
            {
                fOut.owner.ownedFiefs.Add(fOut);
            }

            // insert ancestral owner
            fOut.ancestralOwner = Globals_Game.pcMasterList[fs.ancestralOwner];

            // insert bailiff (PC or NPC)
            if (!String.IsNullOrWhiteSpace(fs.bailiff))
            {
                if (Globals_Game.npcMasterList.ContainsKey(fs.bailiff))
                {
                    fOut.bailiff = Globals_Game.npcMasterList[fs.bailiff];
                }
                else if (Globals_Game.pcMasterList.ContainsKey(fs.bailiff))
                {
                    fOut.bailiff = Globals_Game.pcMasterList[fs.bailiff];
                }
                else
                {
                    fOut.bailiff = null;
                    string toDisplay = "Unable to identify bailiff (" + fs.bailiff + ") for Fief " + fOut.id;
                    Globals_Server.logError(toDisplay);
                }
            }

            //insert terrain
            fOut.terrain = Globals_Game.terrainMasterList[fs.terrain];

            // insert characters
            if (fs.charactersInFief.Count > 0)
            {
                for (int i = 0; i < fs.charactersInFief.Count; i++)
                {
                    if (Globals_Game.npcMasterList.ContainsKey(fs.charactersInFief[i]))
                    {
                        fOut.charactersInFief.Add(Globals_Game.npcMasterList[fs.charactersInFief[i]]);
                        Globals_Game.npcMasterList[fs.charactersInFief[i]].location = fOut;
                    }
                    else if (Globals_Game.pcMasterList.ContainsKey(fs.charactersInFief[i]))
                    {
                        fOut.charactersInFief.Add(Globals_Game.pcMasterList[fs.charactersInFief[i]]);
                        Globals_Game.pcMasterList[fs.charactersInFief[i]].location = fOut;
                    }
                    else
                    {
                        string toDisplay = "Unable to identify character (" + fs.charactersInFief[i] + ") for Fief " + fOut.id;
                        Globals_Server.logError(toDisplay);
                    }

                }
            }
            // populate gaol
            if (fs.gaol.Count > 0)
            {
                foreach (string charID in fs.gaol)
                {
                    Character prisoner = Globals_Game.getCharFromID(charID);
                    if (prisoner != null)
                    {
                        fOut.gaol.Add(prisoner);
                    }
                    else
                    {
                        Globals_Server.logError("Read from database: character unidentified- " + charID);
                    }
                }
            }
            // insert rank using rankID
            if (fs.rank > 0)
            {
                if (Globals_Game.rankMasterList.ContainsKey(fs.rank))
                {
                    fOut.rank = Globals_Game.rankMasterList[fs.rank];
                }
                else
                {
                    string toDisplay = "Fief " + fs.id + ": Rank not found (" + fs.rank + ")";
                    Globals_Server.logError(toDisplay);
                }
            }

            return fOut;
        }

        /// <summary>
        /// Inserts Fief objects into a Character's goTo Queue
        /// </summary>
        /// <returns>bool indicating success</returns>
        /// <param name="cs">Character_Serialised containing goTo Queue</param>
        public static bool Populate_goTo(Character_Serialised cs)
        {
            bool success = false;
            Character myCh = null;

            if (cs is PlayerCharacter_Serialised)
            {
                if (Globals_Game.pcMasterList.ContainsKey(cs.charID))
                {
                    myCh = Globals_Game.pcMasterList[cs.charID];
                    success = true;
                }
            }
            else if (cs is NonPlayerCharacter_Serialised)
            {
                if (Globals_Game.npcMasterList.ContainsKey(cs.charID))
                {
                    myCh = Globals_Game.npcMasterList[cs.charID];
                    success = true;
                }
            }
            else
            {
                string toDisplay = "goTo queue processing: Character not found (" + cs.charID + ")";
                Globals_Server.logError(toDisplay);
            }

            if (success)
            {
                foreach (string value in cs.goTo)
                {
                    myCh.goTo.Enqueue(Globals_Game.fiefMasterList[value]);
                }
            }

            return success;
        }

        /// <summary>
        /// Reads a PlayerCharacter variable from the database
        /// </summary>
        /// <returns>PlayerCharacter object</returns>
        /// <param name="gameID">Game for which PlayerCharacter variable to be retrieved</param>
        /// <param name="charVarID">ID of PlayerCharacter variable to be retrieved</param>
        public static PlayerCharacter DatabaseRead_PcVariable(string gameID, string charVarID)
        {
            var charVarResult = Globals_Server.rClient.Get(gameID, charVarID);
            String pcID = "";
            PlayerCharacter newPC = null;

            if (charVarResult.IsSuccess)
            {
                pcID = charVarResult.Value.GetObject<String>();
                if (pcID != "")
                {
                    if (Globals_Game.pcMasterList.ContainsKey(pcID))
                    {
                        newPC = Globals_Game.pcMasterList[pcID];
                    }
                }
            }
            else
            {
                string toDisplay = "InitialDBload: Unable to retrieve Character variable " + charVarID;
                Globals_Server.logError(toDisplay);
            }

            return newPC;
        }

        public static PlayerCharacter Cass_DatabaseRead_PcVariable(string charVarID)
        {
            HeldStrings[] retrievedPcV = Globals_Server.cMapper.Fetch<HeldStrings>("WHERE id = ?", charVarID).ToArray();
            HeldStrings read = retrievedPcV[0];
            PlayerCharacter newPC = null;
            newPC = Globals_Game.pcMasterList[read.value];

            return newPC;

        }

        /// <summary>
        /// Reads a bool variable from the database
        /// </summary>
        /// <returns>bool variable</returns>
        /// <param name="gameID">Game for which bool variable to be retrieved</param>
        /// <param name="boolID">ID of bool variable to be retrieved</param>
        public static bool DatabaseRead_bool(string gameID, string boolID)
        {
            var boolResult = Globals_Server.rClient.Get(gameID, boolID);
            bool newBool = true; ;

            if (boolResult.IsSuccess)
            {
                newBool = boolResult.Value.GetObject<bool>();
            }
            else
            {
                string toDisplay = "InitialDBload: Unable to retrieve bool variable " + boolID;
                Globals_Server.logError(toDisplay);
            }

            return newBool;
        }

        public static bool Cass_DatabaseRead_Bool(string boolID)
        {
            HeldStrings[] retrievedBool = Globals_Server.cMapper.Fetch<HeldStrings>("WHERE id = ?", boolID).ToArray();
            HeldStrings read = retrievedBool[0];

            return Convert.ToBoolean(read.value);
        }

        public static List<string> DatabaseRead_List(string gameID, string listID)
        {
            var listResult = Globals_Server.rClient.Get(gameID, listID);
            var newList = new List<string>();

            if(listResult.IsSuccess)
            {
                newList = listResult.Value.GetObject<List<string>>();
            }
            else
            {
                string toDisplay = "InitialDBload: Unable to retrieve bool variable " + listID;
                Globals_Server.logError(toDisplay);
            }

            return newList;
        }

        /// <summary>
        /// Reads a map edge collection from the database and uses it to create a HexMapGraph
        /// </summary>
        /// <returns>HexMapGraph object</returns>
        /// <param name="gameID">Game for which map to be created</param>
        /// <param name="mapEdgesID">ID of map edges collection to be retrieved</param>
        public static HexMapGraph DatabaseRead_map(string gameID, string mapEdgesID)
        {
            var mapResult = Globals_Server.rClient.Get(gameID, mapEdgesID);
            List<TaggedEdge<string, string>> edgesList = new List<TaggedEdge<string, string>>();
            var newMap = new HexMapGraph();

            if (mapResult.IsSuccess)
            {
                edgesList = mapResult.Value.GetObject<List<TaggedEdge<string, string>>>();
                TaggedEdge<Fief, string>[] edgesArray = DatabaseRead.EdgeCollection_deserialise(edgesList);

                // create map from edges collection
                newMap = new HexMapGraph("map001", edgesArray);
            }
            else
            {
                string toDisplay = "InitialDBload: Unable to retrieve map edges " + mapEdgesID;
                Globals_Server.logError(toDisplay);
            }

            return newMap;
        }

        public static HexMapGraph Cass_DatabaseRead_map(string mapEdgesID)
        {
            HeldStrings[] retrievedEdges = Globals_Server.cMapper.Fetch<HeldStrings>("WHERE id = ?", mapEdgesID).ToArray();
            HeldStrings read = retrievedEdges[0];

            List<TaggedEdge<string, string>> edgesList = JsonConvert.DeserializeObject<List<TaggedEdge<string, string>>>(read.value);
            HexMapGraph newMap = new HexMapGraph();

            TaggedEdge<Fief, string>[] edgesArray = DatabaseRead.EdgeCollection_deserialise(edgesList);

            // create map from edges collection
            newMap = new HexMapGraph("map001", edgesArray);

            return newMap;
        }

        /// <summary>
        /// Converts serialised edges collection into HexMapGraph edges collection
        /// </summary>
        /// <returns>HexMapGraph edges collection</returns>
        /// <param name="edgesIn">'String-ified' edges collection to be converted</param>
        public static TaggedEdge<Fief, string>[] EdgeCollection_deserialise(List<TaggedEdge<string, string>> edgesIn)
        {
            TaggedEdge<Fief, string>[] edgesOut = new TaggedEdge<Fief, string>[edgesIn.Count];

            int i = 0;
            foreach (TaggedEdge<string, string> element in edgesIn)
            {
                // convert to HexMapGraph edge
                edgesOut[i] = DatabaseRead.EdgeString_deserialise(element);
                i++;
            }

            return edgesOut;
        }

        /// <summary>
        /// Converts serialised edge into HexMapGraph edge
        /// </summary>
        /// <returns>HexMapGraph edge</returns>
        /// <param name="te">'String-ified' edge to be converted</param>
        public static TaggedEdge<Fief, string> EdgeString_deserialise(TaggedEdge<string, string> te)
        {
            TaggedEdge<Fief, string> edgeOut = new TaggedEdge<Fief, string>(Globals_Game.fiefMasterList[te.Source], Globals_Game.fiefMasterList[te.Target], te.Tag);
            return edgeOut;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cassandra;
using Cassandra.Data.Linq;
using Cassandra.Mapping;
using static System.Collections.Specialized.BitVector32;

namespace JominiEngine
{
    public static class CassandraInitialisation
    {
        public static void ConnectDb()
        {
            
            Globals_Server.cCluster = Cluster.Builder().AddContactPoints("127.0.0.1").Build();
            //Create connections to the nodes using a keyspace
            //ISession cSession;
            //cSession = new Session();
            //Session crSession;
            //Console.WriteLine("Session created");
            //Dictionary<string, string> replicationInfo = new Dictionary<string, string>();
            //replicationInfo.Add("NetworkTopologyStrategy", "2");
            Globals_Server.cSession = (Session)Globals_Server.cCluster.Connect();
            //cSession = cluster.Connect("test");
            Globals_Server.logEvent("Session connected");
            Globals_Server.cSession.CreateKeyspaceIfNotExists("CassDatabase");
            //cSession.CreateKeyspace("CassDatabase", replicationInfo, true);
            //cSession.ChangeKeyspace("CassDatabase");
            Globals_Server.cSession.ChangeKeyspace("CassDatabase");
            Console.WriteLine("Database alocated");

            //cSession.Execute("CREATE TYPE IF NOT EXISTS journal ()");
            //Globals_Server.cSession.Execute("CREATE TYPE IF NOT EXISTS titlename (langID text, name text)");

            Globals_Server.cSession.Execute("CREATE TABLE IF NOT EXISTS ailments (ailmentID text, description text, when text, effect int, minimumEffect int, PRIMARY KEY (ailmentID))");
            Globals_Server.cSession.Execute("CREATE TABLE IF NOT EXISTS armies (armyID text, leader text, owner text, days double, location text, isMaintained boolean, aggression text, combatOdds text, troops list<int>, PRIMARY KEY (armyID))");
            Globals_Server.cSession.Execute("CREATE TABLE IF NOT EXISTS baselanguages (id text, name text, PRIMARY KEY (id))");
            //cSession.Execute("CREATE TABLE IF NOT EXISTS client (user text, pcID text, activeChar text, myPastEvents journal, PRIMARY KEY (user))");
            Globals_Server.cSession.Execute("CREATE TABLE IF NOT EXISTS factions (factionReqID text, leader text, factionName text, members text, PaymentDue list<text>, highestStature list<text>, foundingLocation text, alliedFiefs list<text>, proposedTax int, statureBonus double, treasury int, PRIMARY KEY (factionReqID))");
            Globals_Server.cSession.Execute("CREATE TABLE IF NOT EXISTS fiefs (id text, name text, owner text, titleHolder text, rank text, province text, population int, fields double, industry double, troops int, taxrate double, taxratenext double, officialSpendNext int, garrisonSpendNext int, infrastructureSpendNext int, keepSpendNext int, keyStatsCurrent list<double>, keyStatsPrevious list<double>, keepLevel double, loyalty double, status text, language text, terrain text, charactersInFief list<text>, barredCharacters list<text>, barredNationalities list<text>, ancestralOwner text, bailiff text, bailiffDaysInFief double, treasury int, armies list<text>, hasRecruited boolean, isPillaged boolean, troopTransfer text, siege text, PRIMARY KEY (id))");
            Globals_Server.cSession.Execute("CREATE TABLE IF NOT EXISTS gameClocks (id text, seasons list<text>, currentYear int, currentSeason text, PRIMARY KEY (id))");
            Globals_Server.cSession.Execute("CREATE TABLE IF NOT EXISTS kingdoms (id text, name text, owner text, titleHolder text, rank text, nationality text, PRIMARY KEY (id))");
            Globals_Server.cSession.Execute("CREATE TABLE IF NOT EXISTS languages (id text, baseLanguage text, dialect int, PRIMARY KEY (id))");
            Globals_Server.cSession.Execute("CREATE TABLE IF NOT EXISTS nationalities (natID text, name text, PRIMARY KEY (natId))");
            Globals_Server.cSession.Execute("CREATE TABLE IF NOT EXISTS nonplayercharacters (charID text, firstname text, familyName text, purse int, birthdate tuple<int, string>, isMale boolean, nationalities text, isAlive boolean, maxHealth double, virility double, goTo list<text>, language text, days double, statureModifier double, management double, combat double, traits list<tuple<text, int>>, inKeep boolean, isPregnant boolean, location text, spouse text, father text, mother text, familyID text, fiancee text, mtTitles list<text>, armyID text, ailments text, captorID text, ransom text, employer text, salary int, lastOffer text, inEntourage boolean, isHeir boolean, PRIMARY KEY (charID))");
            //Globals_Server.cSession.Execute("CREATE TABLE IF NOT EXISTS places (id text, name text, owner text, titleHolder text, rank text, PRIMARY KEY (id))");
            Globals_Server.cSession.Execute("CREATE TABLE IF NOT EXISTS playercharacters (charID text, firstname text, familyName text, purse int, birthdate tuple<int, string>, isMale boolean, nationalities text, isAlive boolean, maxHealth double, virility double, goTo list<text>, language text, days double, statureModifier double, management double, combat double, traits list<tuple<text, int>>, inKeep boolean, isPregnant boolean, location text, spouse text, father text, mother text, familyID text, fiancee text, mtTitles list<text>, armyID text, ailments text, captorID text, ransom text, isOutlawed boolean, myNPCs list<text>, ownedFiefs list<text>, ownedProvinces list<text>, homeFief text, ancestralHomeFief text, playerID text, myarmies list<text>, mySieges list<text>, PRIMARY KEY (charID))");
            Globals_Server.cSession.Execute("CREATE TABLE IF NOT EXISTS positions (id text, langID list<text>, name list<text>, stature text, officeHolder text, nationality text, PRIMARY KEY (id))");
            Globals_Server.cSession.Execute("CREATE TABLE IF NOT EXISTS provinces (id text, name text, owner text, titleHolder text, rank text, kingdomId text, taxRate double, PRIMARY KEY (id))");
            Globals_Server.cSession.Execute("CREATE TABLE IF NOT EXISTS ranks (id text, langID list<text>, name list<text>, stature text, PRIMARY KEY (id))");
            Globals_Server.cSession.Execute("CREATE TABLE IF NOT EXISTS sieges (siegeID text, startYear int, startSeason text, besiegingPlayer text, defendingPlayer text, besiegerArmy text, defenderGarrison text, besiegedFief text, days double, startKeepLevel double, totalCasualtiesAttacker int, totalCasualtiesDefender int, totalDays double, defenderAdditional text, endDate text, PRIMARY KEY (siegeID))");
            Globals_Server.cSession.Execute("CREATE TABLE IF NOT EXISTS terrain (id text, description text, travelCost double, PRIMARY KEY (id))");
            Globals_Server.cSession.Execute("CREATE TABLE IF NOT EXISTS traits (id text, name text, effects text, PRIMARY KEY (id))");
            
            Globals_Server.cMapper = new Mapper(Globals_Server.cSession);
            JominiMapper jMapper = new JominiMapper();
            Terrain testTerrain = new Terrain("terr_dust", "Quite glittery", 1.1);
            Globals_Server.cMapper.InsertIfNotExists(testTerrain);
            MappingConfiguration.Global.Define<JominiMapper>();
            String tt = "terr_dust";
            Terrain[] t = Globals_Server.cMapper.Fetch<Terrain>("WHERE id = ?", tt).ToArray();
            Terrain ti = new Terrain();
            ti = t[0];
            Console.WriteLine(ti.id + " " + ti.description + " " + ti.travelCost);

            Nationality testN = new Nationality("abc", "Scot");
            Globals_Server.cMapper.InsertIfNotExists(testN);
            String nt = "1";
            Nationality[] n = Globals_Server.cMapper.Fetch<Nationality>("WHERE natID = ?", nt).ToArray();
            Nationality ni = new Nationality();
            ni = n[0];
            Console.WriteLine(ni.natID + " " + ni.name);

            Terrain testT2 = new Terrain("terr_aqua", "Quite supreme", 1.3);
            DatabaseWrite.Cass_DatabaseWrite_Terrain(testT2);

            Terrain readT = DatabaseRead.Cass_DatabaseRead_terrain("terr_aqua");
            Console.WriteLine(readT.id + " " + readT.description + " " + readT.travelCost);

            
            uint[] a = new uint[7] { 1, 2, 3, 4, 5, 6, 7 };
            Army testArmy = new Army("Army_1", "Char_1", "Char_2", 10.0, "Arm12", false, 1, 9, a);
            DatabaseWrite.Cass_DatabaseWrite_Army(testArmy);
            Army testArmy2 = new Army("Army_2", "Char_1", "Char_2", 10.0, "Arm12", false, 1, 9, a);
            DatabaseWrite.Cass_DatabaseWrite_Army(testArmy2);

            Globals_Server.logEvent("before read");
            String aa = "Army_1";
            Army readA = DatabaseRead.Cass_DatabaseRead_Army(aa);
            Console.WriteLine(readA.armyID + " " + readA.leader);

            Dictionary<Globals_Game.Stats, double> effects = new Dictionary<Globals_Game.Stats, double>();
            effects.Add(Globals_Game.Stats.BATTLE, 0.88);
            Trait testTrait = new Trait("trait_11", "fun", effects);
            DatabaseWrite.Cass_DatabaseWrite_Trait(testTrait);

            Trait readTr = DatabaseRead.Cass_DatabaseRead_Trait("trait_11");
            Console.WriteLine(readTr.id + " " + readTr.name);

            BaseLanguage testBl = new BaseLanguage("lang_aa", "French");
            DatabaseWrite.Cass_DatabaseWrite_BaseLanguage(testBl);

            BaseLanguage readBl = DatabaseRead.Cass_DatabaseRead_BaseLanguage("aaa");
            Console.WriteLine(readBl.id + " " + readBl.name);

            Language_Serialised testL = new Language_Serialised("lang_aa12", "lang_aa", 1);
            Language testLl = new Language(testL);
            testLl.baseLanguage = testBl;
            DatabaseWrite.Cass_DatabaseWrite_Language(testLl);

            Language readL = DatabaseRead.Cass_DatabaseRead_Language("lang_aa12");
            Console.WriteLine(readL.id + " " + readL.dialect);

            //Faction testFaction = new Faction("Fact_a", "Char_1", "Here", 10);
            //DatabaseWrite.Cass_DatabaseWrite_Faction(testFaction);

            //Faction readTf = DatabaseRead.Cass_DatabaseRead_Faction("Fact_a");
            //Console.WriteLine(readTf.factionReqID + " " + readTf.leader);
            TitleName[] title = new TitleName[4];
            title[0] = new TitleName("lang_C1", "Prince");
            title[1] = new TitleName("lang_C2", "Prince");
            title[2] = new TitleName("lang_E1", "Prince");
            title[3] = new TitleName("lang_F1", "Prince");

            Rank testRank = new Rank(2, title, 4);
            DatabaseWrite.Cass_DatabaseWrite_Rank(testRank);

            Rank readR = DatabaseRead.Cass_DatabaseRead_Rank("2");
            Console.WriteLine(readR.id + " " + readR.stature);

            Position testPosition = new Position(3, title, 5, "Char_5", testN);
            DatabaseWrite.Cass_DatabaseWrite_Position(testPosition);

            Position readP = DatabaseRead.Cass_DatabaseRead_Position("3");
            Console.WriteLine(readP.id + " " + readP.stature + " " + readP.officeHolder);

            Siege testSiege = new Siege("Siege_1", 1390, 1, "Char_1", "Char_2", "Army_1", "Army_2", "Fef21", 15.0, 1.0, 1, 1, 16.0, "Army_3", "April_1400");
            DatabaseWrite.Cass_DatabaseWrite_Siege(testSiege);

            Siege readSi = DatabaseRead.Cass_DatabaseRead_Siege("Siege_1");
            Console.WriteLine(readSi.siegeID + " " + readSi.startYear + " " + readSi.besiegingPlayer);

            //Fief testFief = new Fief("Fif17", "Noveria", "Char_1", "Char_2");
            //DatabaseWrite.Cass_DatabaseWrite_Fief(testFief);

            //Fief readFi = DatabaseRead.Cass_DatabaseRead_Fief("Fif12");
            //Console.WriteLine(readFi.id + " " + readFi.loyalty + " " + readFi.bailiff);

            //IEnumerable<User> users = mapper.Fetch<User>();
            //IEnumerable<User> users = mapper.Fetch<User>("WHERE name = ?", someName);

            Globals_Server.cCluster.Shutdown();


            //var terrains = new Table<Terrain>(cSession);




        }


    }
}

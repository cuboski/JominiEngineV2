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
    public static class CassandraConnection
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
            Console.WriteLine("Session connected");
            Globals_Server.cSession.CreateKeyspaceIfNotExists("CassDatabase");
            //cSession.CreateKeyspace("CassDatabase", replicationInfo, true);
            //cSession.ChangeKeyspace("CassDatabase");
            Globals_Server.cSession.ChangeKeyspace("CassDatabase");
            Console.WriteLine("Database alocated");

            //cSession.Execute("CREATE TYPE IF NOT EXISTS journal ()");


            Globals_Server.cSession.Execute("CREATE TABLE IF NOT EXISTS ailments (ailmentID text, description text, when text, effect int, minimumEffect int, PRIMARY KEY (ailmentID))");
            Globals_Server.cSession.Execute("CREATE TABLE IF NOT EXISTS armies (armyID text, troops list<int>, leader text, owner text, days double, location text, isMaintained boolean, aggression text, combatOdds text, PRIMARY KEY (armyID))");
            //cSession.Execute("CREATE TABLE IF NOT EXISTS client (user text, pcID text, activeChar text, myPastEvents journal, PRIMARY KEY (user))");
            Globals_Server.cSession.Execute("CREATE TABLE IF NOT EXISTS factions (factionReqID text, leader text, factionName text, members map<text,text>, PaymentDue list<text>, highestStature list<text>, foundingLocation text, alliedFiefs list<text>, proposedTax int, statureBonus double, treasury int, PRIMARY KEY (factionReqID))");
            Globals_Server.cSession.Execute("CREATE TABLE IF NOT EXISTS gameClocks (id text, seasons list<text>, currentYear int, currentSeason text, PRIMARY KEY (id))");
            Globals_Server.cSession.Execute("CREATE TABLE IF NOT EXISTS kingdoms (id text, name text, owner text, titleHolder text, rank text, nationality text, PRIMARY KEY (id))");
            Globals_Server.cSession.Execute("CREATE TABLE IF NOT EXISTS languages (id text, baseLanguage text, dialect int, PRIMARY KEY (id))");
            Globals_Server.cSession.Execute("CREATE TABLE IF NOT EXISTS nationalities (natID text, name text, PRIMARY KEY (natId))");
            Globals_Server.cSession.Execute("CREATE TABLE IF NOT EXISTS places (id text, name text, owner text, titleHolder text, rank text, PRIMARY KEY (id))");
            Globals_Server.cSession.Execute("CREATE TABLE IF NOT EXISTS provinces (kingdomId text, taxRate double, PRIMARY KEY (kingdomId))");
            Globals_Server.cSession.Execute("CREATE TABLE IF NOT EXISTS ranks (id text, langID text, name text, stature text, PRIMARY KEY (id))");
            Globals_Server.cSession.Execute("CREATE TABLE IF NOT EXISTS sieges (siegeID text, startYear int, startSeason text, besiegingPlayer text, defendingPlayer text, besiegerArmy text, defenderGarrison text, besiegedFief text, days double, startKeepLevel double, totalCasualtiesAttacker int, totalCasualtiesDefender int, totalDays double, defenderAdditional text, endDate text, PRIMARY KEY (siegeID))");
            Globals_Server.cSession.Execute("CREATE TABLE IF NOT EXISTS terrain (id text, description text, travelCost double, PRIMARY KEY (id))");
            Globals_Server.cSession.Execute("CREATE TABLE IF NOT EXISTS traits (id text, name text, effects map<text,double>, PRIMARY KEY (id))");
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

            Nationality testN = new Nationality("1", "Scot");
            Globals_Server.cMapper.InsertIfNotExists(testN);
            String nt = "1";
            Nationality[] n = Globals_Server.cMapper.Fetch<Nationality>("WHERE natID = ?", nt).ToArray();
            Nationality ni = new Nationality();
            ni = n[0];
            Console.WriteLine(ni.natID + " " + ni.name);
            //IEnumerable<User> users = mapper.Fetch<User>();
            //IEnumerable<User> users = mapper.Fetch<User>("WHERE name = ?", someName);

            Globals_Server.cCluster.Shutdown();


            //var terrains = new Table<Terrain>(cSession);




        }


    }
}

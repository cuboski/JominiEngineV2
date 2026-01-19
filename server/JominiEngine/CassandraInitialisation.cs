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
            
            Cluster cluster = Cluster.Builder().AddContactPoints("127.0.0.1").Build();
            //Create connections to the nodes using a keyspace
            //ISession cSession;
            //cSession = new Session();
            //Session crSession;
            //Console.WriteLine("Session created");
            //Dictionary<string, string> replicationInfo = new Dictionary<string, string>();
            //replicationInfo.Add("NetworkTopologyStrategy", "2");
            Session cSession = (Session)cluster.Connect();
            //cSession = cluster.Connect("test");
            Console.WriteLine("Session connected");
            cSession.CreateKeyspaceIfNotExists("CassDatabase");
            //cSession.CreateKeyspace("CassDatabase", replicationInfo, true);
            //cSession.ChangeKeyspace("CassDatabase");
            cSession.ChangeKeyspace("CassDatabase");
            Console.WriteLine("Database alocated");

            //cSession.Execute("CREATE TYPE IF NOT EXISTS journal ()");


            cSession.Execute("CREATE TABLE IF NOT EXISTS ailments (ailmentID text, description text, when text, effect int, minimumEffect int, PRIMARY KEY (ailmentID))");
            cSession.Execute("CREATE TABLE IF NOT EXISTS armies (armyID text, troops list<int>, leader text, owner text, days double, location text, isMaintained boolean, aggression text, combatOdds text, PRIMARY KEY (armyID))");
            //cSession.Execute("CREATE TABLE IF NOT EXISTS client (user text, pcID text, activeChar text, myPastEvents journal, PRIMARY KEY (user))");
            cSession.Execute("CREATE TABLE IF NOT EXISTS factions (factionReqID text, leader text, factionName text, members map<text,text>, PaymentDue list<text>, highestStature list<text>, foundingLocation text, alliedFiefs list<text>, proposedTax int, statureBonus double, treasury int, PRIMARY KEY (factionReqID))");
            cSession.Execute("CREATE TABLE IF NOT EXISTS gameClocks (id text, seasons list<text>, currentYear int, currentSeason text, PRIMARY KEY (id))");
            cSession.Execute("CREATE TABLE IF NOT EXISTS kingdoms (id text, name text, owner text, titleHolder text, rank text, nationality text, PRIMARY KEY (id))");
            cSession.Execute("CREATE TABLE IF NOT EXISTS languages (id text, baseLanguage text, dialect int, PRIMARY KEY (id))");
            cSession.Execute("CREATE TABLE IF NOT EXISTS nationalities (natID text, name text, PRIMARY KEY (natId))");
            cSession.Execute("CREATE TABLE IF NOT EXISTS places (id text, name text, owner text, titleHolder text, rank text, PRIMARY KEY (id))");
            cSession.Execute("CREATE TABLE IF NOT EXISTS provinces (kingdomId text, taxRate double, PRIMARY KEY (kingdomId))");
            cSession.Execute("CREATE TABLE IF NOT EXISTS ranks (id text, langID text, name text, stature text, PRIMARY KEY (id))");
            cSession.Execute("CREATE TABLE IF NOT EXISTS sieges (siegeID text, startYear int, startSeason text, besiegingPlayer text, defendingPlayer text, besiegerArmy text, defenderGarrison text, besiegedFief text, days double, startKeepLevel double, totalCasualtiesAttacker int, totalCasualtiesDefender int, totalDays double, defenderAdditional text, endDate text, PRIMARY KEY (siegeID))");
            cSession.Execute("CREATE TABLE IF NOT EXISTS terrain (id text, description text, travelCost double, PRIMARY KEY (id))");
            cSession.Execute("CREATE TABLE IF NOT EXISTS traits (id text, name text, effects map<text,double>, PRIMARY KEY (id))");
            Mapper cMapper = new Mapper(cSession);
            JominiMapper jMapper = new JominiMapper();
            Terrain testTerrain = new Terrain("terr_dust", "Quite glittery", 1.1);
            cMapper.InsertIfNotExists(testTerrain);
            MappingConfiguration.Global.Define<JominiMapper>();
            //Terrain t = cMapper.Fetch<Terrain>();

            cluster.Shutdown();


            //var terrains = new Table<Terrain>(cSession);




        }


    }
}

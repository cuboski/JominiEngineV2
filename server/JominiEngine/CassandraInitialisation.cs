using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cassandra;
using Cassandra.Data.Linq;
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

            //var terrains = new Table<Terrain>(cSession);




        }


    }
}

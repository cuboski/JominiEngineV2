using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cassandra;

namespace JominiEngine
{
    internal class CassandraInitialisation
    {
        public CassandraInitialisation() { }

        public void connectDb()
        {
            Cluster cluster = Cluster.Builder().AddContactPoints("host1").Build();
            //Create connections to the nodes using a keyspace
            ISession session;
            
            session.CreateKeyspace(String CassDatabase, 'class': 'SimpleStrategy',
                             'replication_factor': '1')
            //= cluster.Connect("sample_keyspace");
        }


    }
}

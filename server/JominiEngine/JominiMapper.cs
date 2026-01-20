using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cassandra.Data.Linq;
using Cassandra.Mapping;

namespace JominiEngine {
    public class JominiMapper : Mappings {

        public JominiMapper()
        {

            //Defines the mappings from tables in the cassandra db to classes in the server
            For<Terrain>().TableName("terrain");
            For<Nationality>().TableName("nationalities");

        }
    }
}

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

            //Defines the mappings from classes to tables in the cassandra db
            For<Terrain>().TableName("Terrains");

        }
    }
}

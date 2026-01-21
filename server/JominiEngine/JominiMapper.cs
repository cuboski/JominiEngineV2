using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cassandra.Data.Linq;
using Cassandra.Mapping;
using ProtoMessageClient;

namespace JominiEngine {
    public class JominiMapper : Mappings {

        public JominiMapper()
        {

            //Defines the mappings from tables in the cassandra db to classes in the server
            For<Army>().TableName("armies");
            For<BaseLanguage>().TableName("baselanguages");
            For<Faction>().TableName("factions");
            For<GameClock>().TableName("gameclocks");
            For<Kingdom_Serialised>().TableName("kingdoms");
            For<Language>().TableName("languages");
            For<Nationality>().TableName("nationalities");
            For<Province>().TableName("provinces");
            For<Rank>().TableName("ranks");
            For<Siege>().TableName("sieges");
            For<Terrain>().TableName("terrain");
            For<Trait>().TableName("traits");

        }
    }
}

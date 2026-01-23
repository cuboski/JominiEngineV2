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
            For<Army_Simple>().TableName("armies");
            For<BaseLanguage>().TableName("baselanguages");
            For<Faction_Simple>().TableName("factions");
            For<GameClock_Simple>().TableName("gameclocks");
            For<Kingdom_Serialised>().TableName("kingdoms");
            For<Language_Serialised>().TableName("languages");
            For<Nationality>().TableName("nationalities");
            For<Position_Simple>().TableName("positions");
            //For<Province>().TableName("provinces");
            For<Rank_Simple>().TableName("ranks");
            //For<Siege>().TableName("sieges");
            For<Terrain>().TableName("terrain");
            For<Trait_Simple>().TableName("traits");

        }
    }
}

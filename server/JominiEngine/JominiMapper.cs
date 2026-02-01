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
            For<Fief_Simple>().TableName("fiefs");
            For<GameClock_Simple>().TableName("gameclocks");
            For<HeldStrings>().TableName("heldStrings");
            For<HexMapGraph_Simple>().TableName("hexmapgraphs");
            For<Journal_Simple>().TableName("journals");
            //For<JournalEntry_Simple>().TableName("journalentries");
            For<Kingdom_Simple>().TableName("kingdoms");
            For<Language_Serialised>().TableName("languages");
            For<Nationality>().TableName("nationalities");
            For<NonPlayerCharacter_Simple>().TableName("nonplayercharacters");
            For<PlayerCharacter_Simple>().TableName("playercharacters");
            For<Position_Simple>().TableName("positions");
            For<Province_Simple>().TableName("provinces");
            For<Rank_Simple>().TableName("ranks");
            For<Siege_Simple>().TableName("sieges");
            For<Terrain>().TableName("terrain");
            For<Trait_Simple>().TableName("traits");
            For<VictoryData>().TableName("victorydata");

        }
    }
}

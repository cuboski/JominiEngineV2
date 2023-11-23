using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JominiEngine
{
    /// <summary>
    /// Before an organisation can be founded a set of members must agree
    /// </summary>
    public class Faction
    {
        public enum factionRank
        {
            Leader, Member, Prospect
        }

        public string factionReqID { get; set; }
        public string leader { get; set; }
        public string factionName { get; set; }
        public Dictionary<string, string> members = new Dictionary<string, string>();
        public string foundingLocation { get; set; }
        public Double proposedTax { get; set; }

        public Faction()
        {

        }

        public Faction(string factionName, string leader, string foundingLocation, Double proposedTax)
        {
            this.factionName = factionName;
            this.leader = leader;
            this.foundingLocation = foundingLocation;
            this.proposedTax = proposedTax;
        }

        public void signFactionCharter(string newMember, string rank)
        {
            members.Add(newMember, rank);
        }
    }
}
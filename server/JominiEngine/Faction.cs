using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JominiEngine
{
    /// <summary>
    /// A faction is an organisation made of player characters that allows for them to work together to achieve certain goals
    /// In the initial phase it will exist to test the membershipTax system.
    /// </summary>
    public class Faction
    {
        /// <summary>
        /// Leader of the faction stored as their playerID
        /// </summary>
        public string factionLeader { get; set; }

        public string factionName { get; set; }

        /// <summary>
        /// The collection of members stored as their playerID
        ///     (need a record of when a player joins)
        /// </summary>
        public List<string> members = new List<string>();

        /// <summary>
        /// ID of areas in the game that are allied with the faction
        /// </summary>
        public string[] locations { get; set; }

        /// <summary>
        /// The payment each member must pay into the faction
        /// </summary>
        public Double membershipTax { get; set; }

        /// <summary>
        /// The shared resources for each member
        /// </summary>
        public Double fortune { get; set; }

        public Faction()
        {

        }
    }

    /// <summary>
    /// Before an organisation can be founded a set of members must agree
    /// </summary>
    public class FactionRequest
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

        public FactionRequest()
        {

        }

        public FactionRequest(string factionName, string leader, string foundingLocation, Double proposedTax)
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
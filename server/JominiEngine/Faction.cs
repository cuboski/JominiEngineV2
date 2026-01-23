using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

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
        public List<string> PaymentDue = new List<string>();
        public string[] highestStature = new string[5];
        public string foundingLocation { get; set; }
        public List<string> alliedFiefs = new List<string>();
        public uint proposedTax { get; set; }
        public double statureBonus = 0;
        public uint treasury { get; set; }

        public Faction()
        {

        }

        public Faction(string factionName, string leader, string foundingLocation, uint proposedTax)
        {
            this.factionName = factionName;
            this.leader = leader;
            this.foundingLocation = foundingLocation;
            this.proposedTax = proposedTax;
            highestStature[0] = leader;
            alliedFiefs.Add(foundingLocation);
            this.treasury = 0;

            CalculateStatureBonus();

            // apply base modifier to leader
            DisplayMessages err;
            Utility_Methods.GetCharacter(leader, out err).statureModifier += statureBonus;
        }

        public Faction(Faction_Simple fact)
        {
            this.factionReqID = fact.factionReqID;
            this.leader = fact.leader;
            this.factionName = fact.factionName;
            this.members = JsonConvert.DeserializeObject<Dictionary<string, string>>(fact.members);
            this.PaymentDue = fact.PaymentDue;
            this.highestStature = fact.highestStature;
            this.foundingLocation = fact.foundingLocation;
            this.alliedFiefs = fact.alliedFiefs;
            this.proposedTax = (uint)(fact.proposedTax);
            this.statureBonus = fact.statureBonus;
            this.treasury = (uint)(fact.treasury);
        }

        public void AdjustTreasury(uint amount)
        {
            treasury += amount; 
        }

        // Signs the new faction member as a prospect
        public void signFactionCharter(string newMember, string rank)
        {
            members.Add(newMember, rank);
        }

        /// <summary>
        /// This function checks if a members stature value is within the top rankings
        /// </summary>
        /// <param name="newMember"></param>
        public void CheckStatureRank(string newMember)
        {
            double newStature = GetMemberStature(newMember);
            int tmp = -1;
            double tmpStature = 10;

            // check the members stature ranking against the already established high rankers
            for (int i = 0; i < highestStature.Length; i++)
            {
                // if there is a lack of high rank characters, new member is automatically granted place
                if (highestStature[i] == null)
                {
                    highestStature[i] = newMember;
                    break;
                }
                    
                double thisStature = GetMemberStature(highestStature[i]);
                if (newStature >= thisStature && tmpStature > thisStature)
                {
                   // record current lowest stature holder
                   tmp = i;
                   tmpStature = thisStature;
                }
            }

            // stature ranking has changed, replace
            if (tmp != -1)
            {
                highestStature[tmp] = newMember;
                CalculateStatureBonus();
            }

            PrintHighestStature();
        }

        /// <summary>
        /// Calculate how much of a stature bonus should be granted to faction members.
        /// This is a number between 0 and 2
        /// </summary>
        private void CalculateStatureBonus()
        {
            // record previous bonus
            double prevBonus = statureBonus;

            /*
             * calculate the stature ranking bonus.
             * This is calculated from the highest statures taking the average and weighting between 0 and 6
             */
            double rankingsBonus = 0;
            foreach (string member in highestStature)
            {
                // if the faction is still small enough to lack high ranks, penalise the bonus
                // this is to incentivise players to recruit
                if(member == null)
                {
                    rankingsBonus /= 2;
                    continue;
                }

                // important to remove the current statureBonus to get the value prior
                rankingsBonus += GetMemberStature(member) - statureBonus;
            }
                
            rankingsBonus /= highestStature.Length;
            rankingsBonus /= 6;

            Console.WriteLine($"Ranking Bonus: {rankingsBonus}");

            /*
             * calculate the fief bonus
             * This is calculated by the amount of fiefs the faction owns
             */
            double fiefBonus = alliedFiefs.Count() / Globals_Game.fiefMasterList.Count;
            Console.WriteLine($"Ranking Bonus: {fiefBonus}");

            statureBonus = Math.Round((rankingsBonus + fiefBonus), 2);

            UpdateMemberStatures(prevBonus);
        }

        private void UpdateMemberStatures(double prevBonus)
        {
            double newBonus = statureBonus - prevBonus;

            DisplayMessages err;
            foreach(string member in members.Keys) {
                Character thisMember = Utility_Methods.GetCharacter(member, out err);
                thisMember.statureModifier += newBonus; 
            }
        }

        private void PrintHighestStature()
        {
            Console.WriteLine("New Stature Ranks:");
            foreach (string member in highestStature)
            {
                if (member == null)
                    continue;

                Console.WriteLine($"{member}: {GetMemberStature(member)}");
            }
        }

        private double GetMemberStature(string member)
        {
            DisplayMessages err;

            return Utility_Methods.GetCharacter(member, out err).statureModifier;
        }
    }

    public class Faction_Simple
    {
        public string factionReqID { get; set; }
        public string leader { get; set; }
        public string factionName { get; set; }
        public string members {  get; set; }
        public List<string> PaymentDue = new List<string>();
        public string[] highestStature = new string[5];
        public string foundingLocation { get; set; }
        public List<string> alliedFiefs = new List<string>();
        public int proposedTax { get; set; }
        public double statureBonus = 0;
        public int treasury { get; set; }

        //Used implicitly when getting object from database
        public Faction_Simple()
        {

        }
        public Faction_Simple(Faction fact)
        {
            this.factionReqID = fact.factionReqID;
            this.leader = fact.leader;
            this.factionName = fact.factionName;
            this.members = JsonConvert.SerializeObject(fact.members);
            this.PaymentDue = fact.PaymentDue;
            this.highestStature = fact.highestStature;
            this.foundingLocation = fact.foundingLocation;
            this.alliedFiefs = fact.alliedFiefs;
            this.proposedTax = Convert.ToInt32(fact.proposedTax);
            this.statureBonus = fact.statureBonus;
            this.treasury = Convert.ToInt32(fact.treasury);
        }

    }
}
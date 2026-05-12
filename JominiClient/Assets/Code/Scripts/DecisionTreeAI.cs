using System;
using System.Collections.Generic;
using ProtoMessageClient;
using UnityEngine;
using UnityEngine.UIElements;


//public class AgentGameData
//{

//}

public class DecisionTreeAI
{
    //Call login
    //"acc2", "pass2"

    //somesort of bool to check if to launch ai

    //A data structure to remember important info

    //might need to create a new client

    //public int daySplit { get; set; }
    public static Dictionary<string, bool> isBailiff = new Dictionary<string, bool>();

    public static int officialSpendThisTurn = 0;
    public static int garrisonSpendThisTurn = 0;
    public static int infrasteuctureSpendThisTurn = 0;
    public static int keepSpendThisTurn = 0;

    public static int changeLimit = 4000;
    public static int changeAmount = 2000;

    public static double lastCheckDays = 0;

    public static int expensesChangedThisSeason = 0;

    public static bool hasHired = false;

    public DecisionTreeAI()
    {
        
    }

    public static void agentSetup()
    {
        /*
        string username = "acc2";
        string password = "pass2";
         if(Controller.Login(username, password, out string reason)) {
            Debug.Log("logging in");
            //might not need
            Controller.GoToScene(Controller.SceneName.Map);
        }
        else {
            Debug.Log(reason);
            //lblErrorMessage.text = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + reason;
        }
        */


    }

    public static void TakeTurn()
    {
        //bool done = testActions();
        
        int split = DecideDayDivision();
        //For now split = 0
        split = 0;
        if (split == 0)
        {
            FiefFocused();
        }
        else if (split == 1)
        {
            ArmyFocused();
        }
        else
        {
            SplitFocus(45, 45);
        }
    }

    public static int DecideDayDivision()
    {
        //different int depending on outcome
        // 0 = 90 days of fief managment
        // 1 = 90 days of army mangement
        // 2 = 45 day split
        int splitNum = 0;
        
        ProtoGenericArray<ProtoFief> allFiefs = Controller.ViewMyFiefs(Controller.tclient);
        ProtoFief[] fiefs = allFiefs.fields;
        bool Siege = false;
        List<string> siegeIDs = new List<string>();
        for (int i = 0; i < fiefs.Length; i++)
        {
            if(fiefs[i].siege != null)
            {
                siegeIDs.Add(fiefs[i].siege);
                Siege = true;
            }
                
        }
        if (!Siege)
        {
            double sumIncome = 0;
            double expenses = 0;

            for (int j = 0; j < fiefs.Length; j++)
            {
                sumIncome += fiefs[j].keyStatsCurrent[1] * (fiefs[j].taxRate / 100);
                expenses += fiefs[j].keyStatsCurrent[3];
                expenses += fiefs[j].keyStatsCurrent[4];
                expenses += fiefs[j].keyStatsCurrent[5];
                expenses += fiefs[j].keyStatsCurrent[6];
            }

            sumIncome  /= fiefs.Length;
            expenses /= fiefs.Length;

            if (sumIncome > 2 * expenses)
            {
                splitNum = 2;
            }

        }
        else
        {
            ProtoCharacter Eustace = Controller.GetCharacterDetails("Char_126", Controller.tclient);
            int defenderSize = 0;
            int attackerSize = 0;
            bool playerArmyInvolved = false;
            for (int k = 0; k < siegeIDs.Count; k++)
            {
                ProtoSiegeDisplay currSiege = (ProtoSiegeDisplay)Controller.ViewSiege(siegeIDs[k], Controller.tclient);
                //defenderSize += currSiege.defenderGarrison;
                ProtoArmy currDef = (ProtoArmy)Controller.GetArmyDetails(currSiege.defenderGarrison, Controller.tclient);
                if (currDef.armyID == Eustace.armyID)
                {
                    playerArmyInvolved = true;
                }
                ProtoArmy currAtt = (ProtoArmy)Controller.GetArmyDetails(currSiege.besiegerArmy, Controller.tclient);
                for (int l = 0; l < 7; l++)
                {
                    defenderSize += Convert.ToInt32(currDef.troops[l]);
                    attackerSize += Convert.ToInt32(currAtt.troops[l]);
                }
            }
            if (playerArmyInvolved == false)
            {
                ProtoArmy playArmy = (ProtoArmy)Controller.GetArmyDetails(Eustace.armyID, Controller.tclient);
                for (int m = 0; m < 7; m++)
                {
                    defenderSize += Convert.ToInt32(playArmy.troops[m]);
                }
            }

            if (attackerSize > defenderSize)
            {
                splitNum = 1;
            }
            else
            {
                splitNum = 2;
            }
             

        }

        return splitNum;
    }

    public static void FiefFocused()
    {
        double days = 90;
        bool alreadyIncreased = false;

        bool fiefUnrest = false;

        officialSpendThisTurn = 0;
        garrisonSpendThisTurn = 0;
        infrasteuctureSpendThisTurn = 0;
        keepSpendThisTurn = 0;

        
        expensesChangedThisSeason = 0;
        int failedActions = 0;
        int decisions = 0;
        lastCheckDays = 0;
        bool failedOne = false;
        bool failedTwo = false;

        bool hasMoved = false;

        string season = Controller.SeasonToString(Controller.protoClient.Season);
        string year = Controller.protoClient.Year.ToString();


        while (days > 0 && failedActions < 3 && decisions < 5 && !hasMoved)
        {
            decisions++;
            ProtoCharacter Eustace = Controller.GetCharacterDetails("Char_126", Controller.tclient);
            if (Eustace.days < lastCheckDays)
            {
                days -= (lastCheckDays - Eustace.days);
                lastCheckDays = Eustace.days;
            }
            ProtoFief currFief = new ProtoFief();
            ProtoGenericArray<ProtoFief> allFiefs = Controller.ViewMyFiefs(Controller.tclient);
            ProtoFief[] fiefs = allFiefs.fields;
            for (int i = 0; i < fiefs.Length; i++)
            {
                if (fiefs[i].fiefID == Eustace.location)
                {
                    currFief = fiefs[i];
                }
                
            }

            if (currFief.treasury < 0 && currFief.fiefID != "ENU02")
            {
                Debug.Log("AGENT ACTION: Tranfering funds " + season + " " + year);
                Controller.TransferFunds("ENU02", currFief.fiefID, 120000, Controller.tclient);
            }
            fiefUnrest = false;

            if (currFief.bailiff != null || failedOne)
            {
                //Debug.Log("Has Bailiff");
                double sumIncome = 0;
                double expenses = 0;

                sumIncome += currFief.keyStatsCurrent[1] * (currFief.taxRate / 100);
                expenses += currFief.keyStatsCurrent[3];
                expenses += currFief.keyStatsCurrent[4];
                expenses += currFief.keyStatsCurrent[5];
                expenses += currFief.keyStatsCurrent[6];

                if (sumIncome < expenses && !failedTwo)
                {
                    //Debug.Log("Try fix income");
                    if (!alreadyIncreased)
                    {
                        if (currFief.status.Equals('u') || currFief.status.Equals('r'))
                        {
                            fiefUnrest = true;
                        }
                        if (!fiefUnrest)
                        {
                            Debug.Log("AGENT ACTION: Increasing tax 10% " + season + " " + year);
                            //Debug.Log(currFief.keyStatsNext[2]);
                            currFief.keyStatsNext[2] += 10;
                            //Debug.Log(currFief.keyStatsNext[2]);
                            if (currFief.keyStatsNext[2] > 100)
                            {
                                currFief.keyStatsNext[2] = 100;
                            }
                            double[] newEx = {currFief.keyStatsNext[2], currFief.keyStatsNext[3], currFief.keyStatsNext[4], currFief.keyStatsNext[5], currFief.keyStatsNext[6]};
                            ProtoMessage changeEx = Controller.AdjustExpenditure(currFief.fiefID, newEx, Controller.tclient);
                            alreadyIncreased = true;
                        }
                        else
                        {
                            if (!TryAdjustExpenses(currFief, season, year))
                            {
                                failedActions++;
                                failedTwo = true;
                            }
                        }
                    }
                    else
                    {
                        if (TryAdjustExpenses(currFief, season, year))
                        {
                            failedActions++;
                            failedTwo = true;
                        }
                    }
                }
                else
                {
                    
                    string newFief = "";
                    string fiefName = "";
                    System.Random random = new System.Random();
                    
                    do
                    {
                        int newFiefNum = random.Next(0, fiefs.Length);
                        newFief = fiefs[newFiefNum].fiefID;
                        fiefName = fiefs[newFiefNum].FiefName;
                    }
                    while (newFief.Equals(currFief.fiefID));
                    //string userMessageOnSceneLoad = "a";
                    ProtoMessage movement = Controller.TravelTo("Char_126", newFief, Controller.tclient);
                    Debug.Log("AGENT ACTION: Move to new fief " + season + " " + year + " next season they will be performing actions in " + fiefName);
                    hasMoved = true;
                    /*
                    if(movement.ResponseType == DisplayMessages.Success) {
                        userMessageOnSceneLoad = Eustace.firstName + " has arrived in " + newFief;
                    }
                    else if(movement.ResponseType == DisplayMessages.CharacterDaysJourney) {
                        userMessageOnSceneLoad = Eustace.firstName + "only had enough days left in the season to make it as far as ";
                    }
                    if (newFief.Equals(""))
                    {
                        newFief = "b";
                    }
                    numMovesThisTurn++;
                    Debug.Log(userMessageOnSceneLoad + " " + numMovesThisTurn + newFief);
                    */
                }
            }
            else
            {
                //Debug.Log("No Bailiff");
                ProtoGenericArray<ProtoCharacterOverview> npcs = Controller.GetNPCList("Family Employ", Controller.tclient);
                ProtoCharacterOverview[] npcMembers = npcs.fields;

                string id = "";
                bool found = false;
                int i = 0;

                //for (int m = 0; m < npcMembers.Length; m++)
                //{
                    
                //}
                
                while (i < npcMembers.Length && found == false)
                {
                    ProtoCharacterOverview memToCheck = npcMembers[i];
                    bool alreadyBailiff = false;
                    foreach (var posBailiff in isBailiff)
                    {
                        if (posBailiff.Value == true)
                        {
                            alreadyBailiff = true;
                        }
                    }
                    if (memToCheck.isMale && memToCheck.charID != "Char_126" && !alreadyBailiff)
                    {   
                        id = memToCheck.charID;
                        //Debug.Log(id);
                        found = true;
                        
                    }
                    i++;
                }

                if (!id.Equals(""))
                {
                    Debug.Log("AGENT ACTION: Apointing Bailiff " + season + " " + year);
                    ProtoMessage appointed = Controller.AppointBailiff(id, currFief.fiefID, Controller.tclient);
                    isBailiff.Remove(id);
                    isBailiff.Add(id, true);
                }
                else
                {
                    bool foundHire = false;
                    bool notOwned = true;
                    ProtoCharacterOverview[] charsInFief = currFief.charactersInFief;
                    int foundChar = 0;
                    int attemptNum = 0;

                    int j = 0;
                    while (j < charsInFief.Length && foundHire == false)
                    {
                        
                        for (int k = 0; k < npcMembers.Length; k++)
                        {
                            if ((npcMembers[k].charID == charsInFief[j].charID))
                            {
                                notOwned = false;
                            }
                        }
                        
                        if (foundHire == false && notOwned == true)
                        {
                            foundHire = true;
                            foundChar = j;
                        }
                        j++;
                        
                        
                    }
                    if (foundHire == true && hasHired == false)
                    {
                        attemptNum++;  
                        Debug.Log("AGENT ACTION: Hiring NPC " + charsInFief[foundChar].charID + " " + season + " " + year);
                        ProtoMessage hired = Controller.HireNPC(charsInFief[foundChar].charID, "20000", Controller.tclient);
                        hasHired = true;
                    }
                    else
                    {
                        failedActions++;
                        failedOne = true;
                    }
                    
                }
                

            }
        }

    }

    public static bool TryAdjustExpenses(ProtoFief currFief, string season, string year)
    {
        
        bool madeChange = true; 
        if ((currFief.keyStatsCurrent[4] - garrisonSpendThisTurn) > changeLimit)
        {
            currFief.keyStatsNext[4] -= changeAmount;
            garrisonSpendThisTurn += changeAmount;
        }
        else
        {
            if ((currFief.keyStatsCurrent[6] - keepSpendThisTurn) > changeLimit)
            {
                currFief.keyStatsNext[6] -= changeAmount;
                keepSpendThisTurn += changeAmount;
            }
            else
            {
                if ((currFief.keyStatsCurrent[3] - officialSpendThisTurn) > changeLimit)
                {
                    currFief.keyStatsNext[3] -= changeAmount;
                    officialSpendThisTurn += changeAmount;
                }
                else
                {
                    if ((currFief.keyStatsCurrent[5] - infrasteuctureSpendThisTurn) > changeLimit)
                    {
                        currFief.keyStatsNext[5] -= changeAmount;
                        infrasteuctureSpendThisTurn += changeAmount;
                    }
                    else
                    {
                        madeChange = false;

                    }
                }
            }

        }
        if (madeChange)
        {
            expensesChangedThisSeason++;
            Debug.Log("AGENT ACTION: Adjusting expenses, has adjusted " + expensesChangedThisSeason + " time(s)  in " + season + " " + year);
        }
        double[] newEx = {currFief.keyStatsNext[2], currFief.keyStatsNext[3], currFief.keyStatsNext[4], currFief.keyStatsNext[5], currFief.keyStatsNext[6]};
        ProtoMessage changeEx = Controller.AdjustExpenditure(currFief.fiefID, newEx, Controller.tclient);
        return madeChange;
    }

    public static void ArmyFocused()
    {
        //int days = 90;
    }

    public static void SplitFocus(int fiefSplit, int armySplit)
    {
        int fiefDays = fiefSplit;
        int armyDays = armySplit; 
    }

    public static bool testActions()
    {
        ProtoMessage a = Controller.HireTroops(5, Controller.tclient);
        Debug.Log(a);
        ProtoCharacter Eustace = Controller.GetCharacterDetails("Char_126", Controller.tclient);
        Debug.Log(Eustace);
        ProtoMessage Alnwick = Controller.GetFiefDetails(Eustace.location, Controller.tclient);
        Debug.Log(Alnwick);
        ProtoMessage AlnwickDirect = Controller.GetFiefDetails("Alnwick", Controller.tclient);
        Debug.Log(AlnwickDirect);
        //ProtoGenericArray<ProtoCharacterOverview> entourage = (ProtoGenericArray<ProtoCharacterOverview>)Controller.ListEntourageMembers();
        //Debug.Log(entourage);
        ProtoGenericArray<ProtoCharacterOverview> npcs = Controller.GetNPCList("Family Employ", Controller.tclient);
        Debug.Log(npcs);
        //check for null bailiff
        //make list of appointed characters

        //ProtoCharacterOverview[] entMembers = entourage.fields;
        ProtoCharacterOverview[] npcMembers = npcs.fields;
        //Debug.Log(entMembers);
        Debug.Log(npcMembers);
        string id = "";
        bool found = false;
        int i = 0;
        
        while (i < npcMembers.Length && found == false)
        {
            ProtoCharacterOverview memToCheck = npcMembers[i];
            if (memToCheck.isMale && memToCheck.charID != "Char_126")
            {   
                id = memToCheck.charID;
                Debug.Log(id);
                found = true;
                
            }
            i++;
        }
        ProtoMessage appointed = Controller.AppointBailiff(id, "ENU02", Controller.tclient);
        if (appointed.ResponseType == DisplayMessages.Success)
        {
            Controller.GoToScene(Controller.SceneName.ViewFief);
        }
        else
        {
            Debug.Log("ERROR: Response type: " + appointed.ResponseType.ToString());
        }
        if (found == false)
        {
            Debug.Log("HERE         HERE");
        }
        else
        {   ProtoFief AlnwickDirectAfter = new ProtoFief();
            ProtoGenericArray<ProtoFief> allFiefs = Controller.ViewMyFiefs(Controller.tclient);
            ProtoFief[] fiefs = allFiefs.fields;
            for (int j = 0; j < fiefs.Length; j++)
            {
                if (fiefs[j].fiefID == "ENU02")
                {
                    AlnwickDirectAfter = fiefs[j];
                }
                
            }
            Debug.Log(AlnwickDirectAfter);
            if (AlnwickDirectAfter.bailiff != null)
            {
                
            }
            else
            {
                Debug.Log("EMPTY           EMPTY");
            }
            
        }
        return true;
    }

    //public void Main()
    //{
        //bool test = testActions();
    //}
}
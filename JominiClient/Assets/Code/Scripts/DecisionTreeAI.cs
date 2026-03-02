using System;
using System.Collections.Generic;
using ProtoMessageClient;
using UnityEngine;

public static class DecisionTreeAI
{
    //Call login
    //"acc2", "pass2"


    //somesort of bool to check if to launch ai

    //A data structure to remember important info

    //might need to create a new client

    public static void agentSetup()
    {
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

    }

    public static void TakeTurn()
    {
        bool done = testActions();
        int split = DecideDayDivision();
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
        bool noSiege = false;
        List<string> siegeIDs = new List<string>();
        for (int i = 0; i < fiefs.Length; i++)
        {
            if(fiefs[i].siege != null)
            {
                siegeIDs.Add(fiefs[i].siege);
            }
            else
            {
                noSiege = true;
            }
                
        }
        if (noSiege)
        {
            double sumIncome = 0;
            double expenses = 0;

            for (int j = 0; j < fiefs.Length; j++)
            {
                sumIncome += (fiefs[j].keyStatsCurrent[1] * fiefs[j].taxRate);
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
        int days = 90;
    }

    public static void ArmyFocused()
    {
        int days = 90;
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

    //public static void Main()
    //{
        //bool test = testActions();
    //}
}
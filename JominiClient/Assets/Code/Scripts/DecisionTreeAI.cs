using System;
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
    }

    public static int DecideDayDivision()
    {
        //different int depending on outcome
        return 0;
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
        int fiefDays;
        int armyDays; 
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
        //check for null bailiff
        //make list of appointed characters

        //ProtoCharacterOverview[] entMembers = entourage.fields;
        //Debug.Log(entMembers);
        string id = "";
        bool found = false;
        int i = 0;
        /*
        while (i < entMembers.Length && found == false)
        {
            ProtoCharacterOverview memToCheck = entMembers[i];
            if (memToCheck.isMale)
            {   
                id = memToCheck.charID;
                found = true;
                
            }
            i++;
        }
        ProtoMessage appointed = Controller.AppointBailiff(id, "Alnwick", Controller.tclient);
        */
        return true;
    }

    //public static void Main()
    //{
        //bool test = testActions();
    //}
}
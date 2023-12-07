using ProtoMessageClient;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

public class ViewFactionRequest : Controller
{
    [SerializeField] private Button btnViewMembers;
    [SerializeField] private Button btnSignCharter;
    [SerializeField] private Button btnJoinRequests;
    [SerializeField] private Button btnEstablishPost;
    [SerializeField] private Button btnViewAlliedFiefs;

    [SerializeField] private Text lblPageTitle;
    [SerializeField] private Text lblMessageForUser;
    [SerializeField] private Text lblStatureBonus;
    [SerializeField] private Text lblFactionInfo;

    private ProtoFaction viewedFaction;
    private ProtoFief homeFief;

    private bool isLeader;
    private bool isMember;

    // Start is called before the first frame update
    public void Start()
    {
        viewedFaction = (ProtoFaction)GetFactionRequest(factionToView);

        CheckMembership();

        btnViewMembers.onClick.AddListener(BtnViewMembers);
        btnSignCharter.onClick.AddListener(BtnSignCharter);
        btnJoinRequests.onClick.AddListener(BtnJoinRequests);
        btnEstablishPost.onClick.AddListener(BtnEstablishOutpost);
        btnViewAlliedFiefs.onClick.AddListener(BtnViewAlliedFiefs);

        lblPageTitle.text = viewedFaction.factionName;
        homeFief = (ProtoFief)GetFiefDetails(viewedFaction.factionBaseID, tclient);

        lblFactionInfo.text = ""
             + "\nLeader:\t\t\t" + GetCharName(viewedFaction.factionLeader)
             + "\nHome Fief:\t\t" + homeFief.FiefName
             + "\n"
             + "\nMember Count:\t" + viewedFaction.factionMembers.Length
             + "\nMembership Tax:\t\t\t" + viewedFaction.factionTax
             + "\nStature Bonus:\t\t" + viewedFaction.statureBonus
             + "\n"
             ;
    }

    private void CheckMembership()
    {
        string thisChar = protoClient.playerChar.charID;

        isLeader = (thisChar == viewedFaction.factionLeader);
        isMember = isLeader;

        // search for membership in the faction members collection
        if (!isLeader) {
            ProtoGenericArray<ProtoCharacterOverview> members = (ProtoGenericArray<ProtoCharacterOverview>)ListFactionMembers(factionToView);
            foreach (ProtoCharacterOverview member in members.fields)
                isMember = (thisChar == member.charID);
        }

        btnJoinRequests.gameObject.SetActive(isLeader);
        btnSignCharter.gameObject.SetActive(!isMember);
        btnViewMembers.gameObject.SetActive(isMember);
    }

    private void BtnViewMembers()
    {
        viewingListSelectAction = ViewingList.FactionMembers;
        GoToScene(SceneName.ViewingList);
    }

    private void BtnJoinRequests()
    {
        viewingListSelectAction = ViewingList.FactionJoinRequests;
        GoToScene(SceneName.ViewingList);
    }

    /// <summary>
    /// Attempts to add a player character to a faction
    /// </summary>
    private void BtnSignCharter()
    {
        ProtoMessage result = SignFactionCharter(viewedFaction.factionName);

        // character is already in the faction
        if (result.ResponseType == DisplayMessages.ErrorGenericCharacterUnidentified)
            DisplayMessage($"{protoClient.activeChar.firstName} {protoClient.activeChar.familyName} " +
                $"is already a member of {viewedFaction.factionName}");
        // character successfully applied
        else
            DisplayMessage($"{protoClient.activeChar.firstName} {protoClient.activeChar.familyName}'s " +
                $"request to join {viewedFaction.factionName} has been sent");
    }

    private void BtnEstablishOutpost()
    {
        viewingListSelectAction = ViewingList.EstablishFactionPost;
        GoToScene(SceneName.ViewingList);
    }

    private void BtnViewAlliedFiefs()
    {
        viewingListSelectAction = ViewingList.FactionFiefs;
        GoToScene(SceneName.ViewingList);
    }

    private void DisplayMessage(string message)
    {
        lblMessageForUser.text = "[" + DateTime.Now.ToString("h:mm:ss tt") + "] " + message;
    }
}

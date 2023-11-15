using ProtoMessageClient;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.UI;

public class ViewFactionRequest : Controller
{
    [SerializeField] private Button btnViewMembers;
    [SerializeField] private Button btnSignCharter;

    [SerializeField] private Text lblPageTitle;
    [SerializeField] private Text lblMessageForUser;

    private ProtoFactionRequest viewedFaction;

    // Start is called before the first frame update
    public void Start()
    {
        btnViewMembers.onClick.AddListener(BtnViewMembers);
        btnSignCharter.onClick.AddListener(BtnSignCharter);

        btnViewMembers.interactable = true;
        btnSignCharter.interactable = true;

        viewedFaction = (ProtoFactionRequest) GetFactionRequest(factionToView);

        lblPageTitle.text = viewedFaction.factionName;
    }

    private void BtnViewMembers()
    {
        viewingListSelectAction = ViewingList.FactionMembers;
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

    private void DisplayMessage(string message)
    {
        lblMessageForUser.text = "[" + DateTime.Now.ToString("h:mm:ss tt") + "] " + message;
    }
}

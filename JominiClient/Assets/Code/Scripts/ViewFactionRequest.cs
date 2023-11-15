using ProtoMessageClient;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ViewFactionRequest : Controller
{
    [SerializeField] private Button btnViewMembers;
    [SerializeField] private Button btnSignCharter;

    [SerializeField] private Text lblPageTitle;

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
        viewingListSelectAction = "ListChars";
        GoToScene(SceneName.ViewingList);
    }

    private void BtnSignCharter()
    {
        SignFactionCharter(viewedFaction.factionName);
    }
}

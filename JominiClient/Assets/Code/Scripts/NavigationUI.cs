using ProtoMessageClient;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class NavigationUI : Controller
{
    // test button that actions can be routed to before further integrated
    [SerializeField] private Button btnTest;

    [SerializeField] private Button btnMap;
    [SerializeField] private Button btnViewEntourage;
    [SerializeField] private Button btnViewLeader;
    [SerializeField] private Button btnViewMyArmies;
    [SerializeField] private Button btnViewMyFiefs;
    [SerializeField] private Button btnViewJournal;
    [SerializeField] private Button btnSwitchCharacter;
    [SerializeField] private Button btnEndSeason;
    [SerializeField] private Button btnLogOut;

    [SerializeField] private Text lblGameDate;
    [SerializeField] private Text lblRealTime;
    [SerializeField] private Text lblBuildVersion;

    public Text textEventMessage;
    public GameObject eventBox;

    // Start is called before the first frame update
    public void Start()
    {
        btnTest.onClick.AddListener(BtnTest);

        btnMap.onClick.AddListener(BtnMap);
        btnViewEntourage.onClick.AddListener(BtnViewEntourage);
        btnViewLeader.onClick.AddListener(BtnViewLeader);
        btnViewMyArmies.onClick.AddListener(BtnViewMyArmies);
        btnViewMyFiefs.onClick.AddListener(BtnViewMyFiefs);
        btnViewJournal.onClick.AddListener(BtnViewJournal);
        btnSwitchCharacter.onClick.AddListener(BtnSwitchCharacter);
        btnEndSeason.onClick.AddListener(BtnEndSeason);
        btnLogOut.onClick.AddListener(BtnLogOut);

        string season = SeasonToString(protoClient.Season);
        lblGameDate.text = season + "\n" + protoClient.Year.ToString();
        lblBuildVersion.text = "Client Version: " + buildVersion;
    }

    // This function returns true for a test communication
    private void BtnTest() {
        prevScene = (SceneName)Enum.Parse(typeof(SceneName), SceneManager.GetActiveScene().name);
        GoToScene(SceneName.Options);
    }

    public void BtnShowEvent(string message)
    {
        textEventMessage.text = message;
        eventBox.SetActive(true);        
    }

    private void BtnEndSeason() {
        ProtoMessage reply = SeasonUpdate(tclient);
        if(reply.ResponseType == DisplayMessages.Success) {
            viewingListSelectAction = ViewingList.Journal;
            GoToScene(SceneName.ViewingList);
        }
    }

    private void BtnLogOut() {
        tclient.LogOut();
        GoToScene(SceneName.LogIn);
    }

    private void BtnMap() {
        SceneManager.LoadScene("Map");
    }

    private void BtnSwitchCharacter() {
        viewingListSelectAction = ViewingList.SwitchCharacter;
        GoToScene(SceneName.ViewingList);
    }

    private void BtnViewEntourage() {
        // chars to view
        ProtoGenericArray<ProtoCharacterOverview> entourageList = GetNPCList("Entourage", tclient);
        //foreach(ProtoCharacterOverview character in possibleBailiffs.fields) {

        //}
        characterList = entourageList;
        viewingListSelectAction = ViewingList.Entourage;

        GoToScene(SceneName.ViewingList);
    }

    private void BtnViewJournal() {
        viewingListSelectAction = ViewingList.Journal;
        GoToScene(SceneName.ViewingList);
    }

    private void BtnViewLeader() {
        characterToViewID = protoClient.playerChar.charID;
        GoToScene(SceneName.ViewCharacter);
    }

    private void BtnViewMyArmies() {
        viewingListSelectAction = ViewingList.YourArmies;
        GoToScene(SceneName.ViewingList);
    }

    private void BtnViewMyFiefs() {
        viewingListSelectAction = ViewingList.MyFiefs;
        GoToScene(SceneName.ViewingList);
    }

    // Update is called once per frame
    void Update()
    {
        lblRealTime.text = DateTime.Now.ToString("HH:mm:ss");
    }
}

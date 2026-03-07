using ProtoMessageClient;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System;
using UnityEngine.WSA;

public class LogIn : Controller
{
    [SerializeField] private InputField txtUsername;
    [SerializeField] private InputField txtPassword;
    [SerializeField] private Text lblErrorMessage;
    [SerializeField] private Button btnLogIn;
    [SerializeField] private Button btnQuit;
    [SerializeField] private Button btnOption;
    [SerializeField] private Text lblBuildVersion;

    public bool launchWithAIAgent = true;
    //public bool launchWithAIAgent = false;
    public DecisionTreeAI agent;

    // Start is called before the first frame update
    void Start()
    {
        if(tclient == null) {
            Initialise();
        }

        btnLogIn.onClick.AddListener(BtnLogIn);
        btnQuit.onClick.AddListener(BtnQuit);
        btnOption.onClick.AddListener(BtnOption);

        if (string.IsNullOrWhiteSpace(globalString)) {
            lblErrorMessage.text = "";
        }
        else {
            lblErrorMessage.text = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + globalString;
        }

        lblBuildVersion.text = "Client Version: " + buildVersion;

        //might need to move
        Debug.Log(launchWithAIAgent);
        agent = new DecisionTreeAI();
        if(launchWithAIAgent)
        {
            //agent.agentSetup();
            DecisionTreeAI.agentSetup();
        }
    }

    private void BtnLogIn()
    {
        Debug.Log("Button Clicked");
        string username = txtUsername.text;
        string password = txtPassword.text;
        if(Login(username, password, out string reason)) {
            Debug.Log("logging in");
            GoToScene(SceneName.Map);
        }
        else {
            Debug.Log(reason);
            lblErrorMessage.text = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + reason;
        }
    }

    private void BtnQuit() {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
        //make sure this is the correct version
        UnityEngine.Application.Quit();
    }

    private void BtnOption()
    {
        prevScene = SceneName.LogIn;
        GoToScene(SceneName.Options);
    }
}

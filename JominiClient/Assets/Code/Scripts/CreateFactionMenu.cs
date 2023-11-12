using ProtoMessageClient;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CreateFactionMenu : Controller
{
    // Buttons for the player to set the parameters of their faction
    [SerializeField] private InputField txtFactionName;
    [SerializeField] private InputField txtFactionDesc;
    [SerializeField] private InputField txtMemberTax;

    // button that initialises the faction creation
    [SerializeField] private Button btnCreateFaction;

    // Start is called before the first frame update
    void Start()
    {
        btnCreateFaction.onClick.AddListener(CreateFactionButton);
    }

    private void CreateFactionButton()
    {
        string[] inputs = new string[3];
        inputs[0] = txtFactionName.text;
        inputs[1] = txtFactionDesc.text;
        inputs[2] = txtMemberTax.text;

        ProtoMessage response = CreateFaction(inputs, tclient);

        Debug.Log(response);
    }
}

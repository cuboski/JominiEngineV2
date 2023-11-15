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
    [SerializeField] private Button btnAssignHomeFief;

    // dropdowns
    [SerializeField] private Dropdown drpdwnFief;

    ProtoGenericArray<ProtoFief> ownedFiefs;
    private string selectedFief;

    // Start is called before the first frame update
    void Start()
    {
        btnCreateFaction.onClick.AddListener(CreateFactionButton);

        PopulateFiefDropdown();
    }

    /// <summary>
    /// Add the current players owned fiefs to a list of possible home fiefs
    /// </summary>
    private void PopulateFiefDropdown()
    {
        ownedFiefs = ViewMyFiefs(tclient);

        foreach(ProtoFief f in ownedFiefs.fields)
        {
            drpdwnFief.options.Add(new Dropdown.OptionData() { text = f.FiefName });
        }

        drpdwnFief.onValueChanged.AddListener(delegate { DropdownItemSelected(); });
    }

    private void DropdownItemSelected()
    {
        Debug.Log(ownedFiefs.fields[drpdwnFief.value].FiefName);
    }

    private void CreateFactionButton()
    {
        string[] inputs = new string[4];
        inputs[0] = txtFactionName.text;
        inputs[1] = txtFactionDesc.text;
        inputs[2] = txtMemberTax.text;
        inputs[3] = ownedFiefs.fields[drpdwnFief.value].fiefID;

        ProtoMessage response = CreateFaction(inputs, tclient);

        Debug.Log(response);
    }
}

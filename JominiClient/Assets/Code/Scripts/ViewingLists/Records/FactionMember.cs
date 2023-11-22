using ProtoMessageClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class FactionMember : Controller
{
    [SerializeField] TextMeshProUGUI txtCharName;
    [SerializeField] TextMeshProUGUI txtGender;
    [SerializeField] TextMeshProUGUI txtRole;
    [SerializeField] TextMeshProUGUI txtLocation;
    [SerializeField] Dropdown dropDown;

    Transform parentCanvas;
    string charID = null;
    string charName = null;
    string charSex = null;
    string charLocation = null;
    string charRole = null;
    string[] dropDownOptions = null;

    public void SetValues(Transform parentCanvas, string charID, string charName, 
        string charSex, string charLocation, string[] options)
    {
        // assign values
        this.parentCanvas = parentCanvas;
        this.charID = charID;
        this.charSex = charSex;
        this.charName = charName;
        this.charLocation = charLocation;

        // create the game object
        this.transform.SetParent(parentCanvas);
        txtCharName.SetText(charName);
        txtGender.SetText(charSex);
        txtRole.SetText(charRole);
        txtLocation.SetText(charLocation);
        SetDropDownOptions(options);
        this.transform.localScale = Vector3.one;
    }

    public void SetDropDownOptions(string[] options)
    {
        dropDownOptions = options;

        foreach (string option in dropDownOptions)
        {
            dropDown.options.Add(new Dropdown.OptionData { text = option });
        }

        dropDown.onValueChanged.AddListener(delegate { DropdownItemSelected(); });
    }

    private void DropdownItemSelected()
    {
        Debug.Log(dropDown.options[dropDown.value].text);

        switch (dropDown.options[dropDown.value].text)
        {
            case "approve":
                {
                    ChangeFactionRank(factionToView, charID, "Member");
                    break;
                }
            case "view":
                {
                    characterToViewID = charID;
                    GoToScene(SceneName.ViewCharacter);
                    break;
                }
            case "remove":
                {
                    ChangeFactionRank(factionToView, charID, "");
                    break;
                }
        }
    }
}

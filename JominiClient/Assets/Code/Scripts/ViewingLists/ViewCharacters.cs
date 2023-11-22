using ProtoMessageClient;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ViewCharacters : Controller
{
    public Transform CanvasParent;
    public Text lblPageTitle;

    public GameObject prefabCharacter;
    public GameObject prefabFactionMember;
    private GameObject activePrefab;

    private Dropdown factionDrpDwn;

    private List<string> charIDList = new List<string>();
    

    // Start is called before the first frame update
    void Start()
    {
        ProtoMessage reply;

        // Decide between the location list or the list of faction members
        switch (viewingListSelectAction)
        {
            case ViewingList.ListChars:
                {
                    lblPageTitle.text = "Persons present in the " + globalString + " of " + fiefNames[fiefToViewID];

                    reply = ListCharsInMeetingPlace(globalString, protoClient.activeChar.charID, tclient);

                    activePrefab = prefabCharacter;
                    break;
                }
            case ViewingList.FactionJoinRequests:
            case ViewingList.FactionMembers:
                {
                    reply = ListFactionMembers(factionToView);
                    activePrefab = prefabFactionMember;
                    break;
                }
            default:
                {
                    reply = null;
                    break;  
                }
        }
        
        if (reply.ResponseType == DisplayMessages.Success)
        {
            characterList = (ProtoGenericArray<ProtoCharacterOverview>)reply;
            if(characterList.fields != null)
            {
                foreach (ProtoCharacterOverview character in characterList.fields)
                {
                    if (activePrefab != prefabCharacter)
                    {
                        // transform some variables into cleaner values before creating record object
                        string location = string.IsNullOrWhiteSpace(character.locationID) ? "Unknown" : fiefNames[character.locationID];
                        string sex = character.isMale ? "Male" : "Female";
                        string[] options;

                        if (viewingListSelectAction == ViewingList.FactionJoinRequests)
                            options = new string[] { "approve", "view" };
                        else
                            options = new string[] { "view", "remove" };

                        GameObject recordGameObject = Instantiate(activePrefab);
                        FactionMember thisRecord = recordGameObject.GetComponent<FactionMember>();
                        thisRecord.SetValues(CanvasParent, character.charID, character.charName,
                            sex, location, options);
                    }
                    else
                    {
                        string location = string.IsNullOrWhiteSpace(character.locationID) ? "Unknown" : fiefNames[character.locationID];

                        string sex = character.isMale ? "Male" : "Female";

                        GameObject UIElement = Instantiate(activePrefab);
                        UIElement.transform.SetParent(CanvasParent);
                        UIElement.transform.GetChild(0).GetComponent<TMPro.TMP_Text>().SetText(character.charName);
                        UIElement.transform.GetChild(1).GetComponent<TMPro.TMP_Text>().SetText(sex);
                        UIElement.transform.GetChild(2).GetComponent<TMPro.TMP_Text>().SetText(character.role);
                        UIElement.transform.GetChild(3).GetComponent<TMPro.TMP_Text>().SetText(location);
                        UIElement.transform.localScale = Vector3.one;

                        UIElement.transform.GetChild(4).GetComponent<Button>().onClick.AddListener(() => { BtnSwitchCharacter(character.charID); });
                    }

                    charIDList.Add(character.charID);
                    Debug.Log($"{character.charName} {character.charID}");
                }
            }
            

        }
        else
        {
            Debug.Log("Failed to retrieve list of characters");
        }


    }

    private void BtnSwitchCharacter(string charID)
    {
        characterToViewID = charID;
        GoToScene(SceneName.ViewCharacter);
    }
}

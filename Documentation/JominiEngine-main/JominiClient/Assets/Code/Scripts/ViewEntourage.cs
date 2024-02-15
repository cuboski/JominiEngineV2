using ProtoMessageClient;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ViewEntourage : MonoBehaviour
{
    [SerializeField] private Text lblEntourage;
    private List<ProtoCharacterOverview> entourageList = new List<ProtoCharacterOverview>();

    // A method for adding a role to a queue
    public void AddToEntourage(ProtoCharacterOverview character)
    {
        // Adds the role to the queue and displays it on the label
        entourageList.Add(character);
        UpdateEntourageLabel();
    }

    // Method to remove a role from a queue
    public void RemoveFromEntourage(ProtoCharacterOverview character)
    {
        // Removes the role from the queue and displays it on the label
        entourageList.Remove(character);
        UpdateEntourageLabel();
    }

    // Method of clearing the queue
    public void ClearEntourage()
    {
        // Clear the queue and clear the label
        entourageList.Clear();
        UpdateEntourageLabel();
    }

    // Updates the label that displays the queue
    private void UpdateEntourageLabel()
    {
        lblEntourage.text = "Entourage:";
        foreach (var character in entourageList)
        {
            GameObject UIElement = new GameObject("CharacterEntry");
            UIElement.transform.SetParent(lblEntourage.transform);
            
            Text characterName = UIElement.AddComponent<Text>();
            characterName.text = character.charName;

            Button deleteButton = UIElement.AddComponent<Button>();
            deleteButton.onClick.AddListener(() => RemoveCharacter(character));

            Button showButton = UIElement.AddComponent<Button>();
            showButton.onClick.AddListener(() => ShowCharacterInfo(character));
        }
    }

    private void RemoveCharacter(ProtoCharacterOverview character)
    {
        // Removes the role from the EntourageManager
        EntourageManager.Instance.RemoveFromEntourage(character);
        UpdateEntourageLabel();
    }

    private void ShowCharacterInfo(ProtoCharacterOverview character)
    {
        // Add the code to display the role properties here
    }
}

using System.Collections.Generic;
using UnityEngine;
using ProtoMessageClient;

public class EntourageManager : MonoBehaviour
{
    private static EntourageManager instance;

    public static EntourageManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject entourageManager = new GameObject("EntourageManager");
                instance = entourageManager.AddComponent<EntourageManager>();
            }
            return instance;
        }
    }

    private List<ProtoCharacterOverview> entourageList = new List<ProtoCharacterOverview>();

    public List<ProtoCharacterOverview> EntourageList
    {
        get { return entourageList; }
    }

    public void AddToEntourage(ProtoCharacterOverview character)
    {
        entourageList.Add(character);
    }

    public void RemoveFromEntourage(ProtoCharacterOverview character)
    {
        entourageList.Remove(character);
    }

    public void ClearEntourage()
    {
        entourageList.Clear();
    }
}

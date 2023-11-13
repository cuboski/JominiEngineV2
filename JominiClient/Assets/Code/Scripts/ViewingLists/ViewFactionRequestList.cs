using ProtoMessageClient;
using UnityEngine;
using UnityEngine.UI;

public class ViewFactionRequestList : Controller
{
    public Transform CanvasParent;
    public GameObject prefab;
    public Text lblPageTitle;

    // Start is called before the first frame update
    void Start()
    {

        lblPageTitle.text = "Faction Requests";

        // ask the server for list of faction requests
        ProtoMessage reply = new ProtoMessage();

        if (reply.ResponseType == DisplayMessages.Success)
        {
            ProtoGenericArray<ProtoFactionRequest>  factionReqList = (ProtoGenericArray<ProtoFactionRequest>)reply;
            if (factionReqList.fields != null)
            {
                foreach (ProtoFactionRequest factionReq in factionReqList.fields)
                {

                    GameObject UIElement = Instantiate(prefab);
                    UIElement.transform.SetParent(CanvasParent);
                    UIElement.transform.GetChild(0).GetComponent<TMPro.TMP_Text>().SetText(factionReq.factionName);
                    UIElement.transform.GetChild(1).GetComponent<TMPro.TMP_Text>().SetText(factionReq.factionLeader);
                    UIElement.transform.GetChild(2).GetComponent<TMPro.TMP_Text>().SetText(factionReq.factionTax.ToString());
                    UIElement.transform.localScale = Vector3.one;

                    UIElement.transform.GetChild(4).GetComponent<Button>().onClick.AddListener(() => { BtnGoToFactioReq(factionReq.factionName); });
                }
            }

        }
        else
        {
            Debug.Log("Failed to retrieve list of faction requests");
        }
    }

    private void BtnGoToFactioReq(string factionName)
    {
        Debug.Log($"go to faction: {factionName}");
    }
}

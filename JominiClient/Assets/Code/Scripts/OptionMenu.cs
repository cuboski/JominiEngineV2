using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OptionMenu : Controller
{
    [SerializeField] private Dropdown drpdwnRes;
    [SerializeField] private Button btnFullscreen;
    [SerializeField] private Button btnBack;

    private Resolution[] resolutions;
    private int currentResolution = 0;
    private float currentRefreshRate;

    // Start is called before the first frame update
    void Start()
    {
        btnFullscreen.onClick.AddListener(FullScreen);
        btnBack.onClick.AddListener(BtnBack);

        resolutions = Screen.resolutions;
        currentRefreshRate = Screen.currentResolution.refreshRate;

        drpdwnRes.ClearOptions();

        List<string> options = new List<string>();
        for(int i = 0; i < resolutions.Length; i++) { 
            string resOption = resolutions[i].width + "x" + resolutions[i].height + " " + resolutions[i].refreshRate + " Hz";
            options.Add(resOption);

            if (resolutions[i].width != Screen.width || resolutions[i].height != Screen.height)
                continue;

            if (resolutions[i].refreshRate != currentRefreshRate)
                continue;

            currentResolution = i;
        }

        drpdwnRes.AddOptions(options);
        drpdwnRes.value = currentResolution;
        drpdwnRes.RefreshShownValue();

        drpdwnRes.onValueChanged.AddListener(delegate { SetResolution(); });
    }

    public void SetResolution()
    {
        Resolution res = resolutions[drpdwnRes.value];
        Screen.SetResolution(res.width, res.height, true);
    }

    private void FullScreen()
    {
        Screen.fullScreen = !Screen.fullScreen;
    }

    private void BtnBack()
    {
        GoToScene(prevScene);
    }
}

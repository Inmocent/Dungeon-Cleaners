using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Drives the Main Menu scene.</summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Buttons")]
    public Button startRunButton;
    public Button squadButton;
    public Button formationButton;
    public Button settingsButton;

    [Header("Settings Panel")]
    public GameObject settingsPanel;

    void Start()
    {
        Time.timeScale = 1f;

        if (settingsPanel) settingsPanel.SetActive(false);

        if (startRunButton) startRunButton.onClick.AddListener(OnStartRun);
        if (squadButton) squadButton.onClick.AddListener(OnSquad);
        if (formationButton) formationButton.onClick.AddListener(OnFormation);
        if (settingsButton) settingsButton.onClick.AddListener(OnSettings);

        SetupBanner();
    }

    void SetupBanner()
    {
        try
        {
            Sprite bannerSpr = Resources.Load<Sprite>("Art/dungeon_cleaners_banner");
            if (bannerSpr == null)
            {
                string path = System.IO.Path.Combine(Application.dataPath, "Sprites", "dungeon_cleaners_banner.png");
                if (System.IO.File.Exists(path))
                {
                    byte[] bytes = System.IO.File.ReadAllBytes(path);
                    Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(bytes))
                    {
                        tex.filterMode = FilterMode.Point;
                        bannerSpr = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    }
                }
            }

            if (bannerSpr != null)
            {
                var menuPanel = GameObject.Find("MenuPanel") ?? GameObject.Find("Canvas");
                if (menuPanel != null && menuPanel.transform.Find("GameBanner") == null)
                {
                    var bannerGo = new GameObject("GameBanner", typeof(RectTransform));
                    bannerGo.transform.SetParent(menuPanel.transform, false);
                    bannerGo.transform.SetAsFirstSibling();

                    var rt = bannerGo.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.5f, 0.65f);
                    rt.anchorMax = new Vector2(0.5f, 0.65f);
                    rt.anchoredPosition = new Vector2(0f, 0f);
                    rt.sizeDelta = new Vector2(360f, 140f);

                    var img = bannerGo.AddComponent<Image>();
                    img.sprite = bannerSpr;
                    img.preserveAspect = true;

                    var outline = bannerGo.AddComponent<Outline>();
                    outline.effectColor = new Color(0.2f, 0.8f, 1f, 0.8f);
                    outline.effectDistance = new Vector2(2f, 2f);
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[MainMenuController] SetupBanner: {ex.Message}");
        }
    }

    void OnStartRun()
    {
        if (GameRunData.Instance != null)
            GameRunData.Instance.StartNewRun();
        SceneNav.GoTo(SceneNames.SquadBuilder);
    }

    void OnSquad()
    {
        SceneNav.GoTo(SceneNames.SquadBuilder);
    }

    void OnFormation()
    {
        if (GameRunData.Instance != null)
            GameRunData.Instance.EnsureSquadAndFormation();
        SceneNav.GoTo(SceneNames.Formation);
    }

    void OnSettings()
    {
        if (settingsPanel)
            settingsPanel.SetActive(!settingsPanel.activeSelf);
    }
}
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Victory / Game Over screen.</summary>
public class GameOverController : MonoBehaviour
{
    [Header("Labels")]
    public TextMeshProUGUI titleLabel;          // "DUNGEON CLEARED!" or "SQUAD WIPED"
    public TextMeshProUGUI roomsClearedLabel;
    public TextMeshProUGUI survivorsLabel;
    public TextMeshProUGUI goldLabel;
    public TextMeshProUGUI upgradesLabel;

    [Header("Survivors Panel")]
    public Transform survivorContainer;
    public GameObject survivorPortraitPrefab;

    [Header("Buttons")]
    public Button playAgainButton;
    public Button mainMenuButton;

    void Start()
    {
        GameRunData.Instance?.EnsureSquadAndFormation();
        playAgainButton.onClick.AddListener(OnPlayAgain);
        mainMenuButton.onClick.AddListener(OnMainMenu);
        PopulateStats();
    }

    void PopulateStats()
    {
        var data = GameRunData.Instance;
        if (data == null) return;

        bool isVictory = data.currentRoom >= data.totalRooms;
        titleLabel.text = isVictory ? "DUNGEON CLEARED!" : "SQUAD WIPED";
        titleLabel.color = isVictory ? Color.yellow : Color.red;

        int roomsCleared = Mathf.Max(0, data.currentRoom);
        roomsClearedLabel.text = $"Rooms Cleared: {roomsCleared}/{data.totalRooms}";
        goldLabel.text = $"Gold Earned: {data.gold}";

        int alive = data.AliveCount;
        survivorsLabel.text = $"Heroes Survived: {alive}/{data.squad.Count}";

        int upgCount = 0;
        foreach (var hero in data.squad) upgCount += hero.upgrades.Count;
        upgradesLabel.text = $"Upgrades Used: {upgCount}";

        // Survivor portraits
        foreach (var hero in data.squad)
        {
            if (survivorPortraitPrefab == null || survivorContainer == null) break;
            var go = Instantiate(survivorPortraitPrefab, survivorContainer);
            var img = go.GetComponentInChildren<Image>();
            var lbl = go.GetComponentInChildren<TextMeshProUGUI>();

            if (img) img.color = hero.isAlive ? hero.data.classColor : new Color(0.2f, 0.2f, 0.2f);
            if (lbl) lbl.text = hero.isAlive ? hero.displayName : $"{hero.displayName}\n(Fallen)";
        }
    }

    void OnPlayAgain()
    {
        Time.timeScale = 1f;
        if (GameRunData.Instance) GameRunData.Instance.StartNewRun();
        SceneNav.GoTo(SceneNames.SquadBuilder);
    }

    void OnMainMenu()
    {
        Time.timeScale = 1f;
        SceneNav.GoTo(SceneNames.MainMenu);
    }
}
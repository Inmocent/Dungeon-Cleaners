using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Singleton that persists across scenes, holding all run state.
/// </summary>
public class GameRunData : MonoBehaviour
{
    private static GameRunData _instance;
    public static GameRunData Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<GameRunData>();
                if (_instance == null)
                {
                    var go = new GameObject("GameRunData");
                    _instance = go.AddComponent<GameRunData>();
                    if (Application.isPlaying) DontDestroyOnLoad(go);
                    _instance.LoadDefaultHeroes();
                }
            }
            return _instance;
        }
    }

    [Header("Hero Roster")]
    public List<HeroData> availableHeroes = new();

    [Header("Current Run")]
    public List<HeroInstance> squad = new();           // 5 selected heroes
    public HeroInstance[] formation = new HeroInstance[9]; // 3x3 grid
    public int currentRoom = 0;
    public int totalRooms = 7;
    public int gold = 100;

    [Header("In-Combat Quick Buffs State")]
    public bool isPowerBuffActive = false; // lasts 1 room (+35% ATK)
    public int speedBuffRoomsRemaining = 0; // lasts 2-3 rooms (+40% SPD)
    public bool hasBoughtPowerThisRoom = false; // only 1 purchase per room
    public bool hasBoughtSpeedThisRoom = false;
    public bool hasBoughtReviveThisRoom = false;

    public const int POWER_BUFF_COST = 40;
    public const int SPEED_BUFF_COST = 50;
    public const int REVIVE_BUFF_COST = 80;

    public bool IsPowerBuffActive => isPowerBuffActive;
    public bool IsSpeedBuffActive => speedBuffRoomsRemaining > 0;

    public bool CanAfford(int amount) => gold >= amount;

    public bool SpendGold(int amount)
    {
        if (gold >= amount)
        {
            gold -= amount;
            return true;
        }
        return false;
    }

    public void AddGold(int amount)
    {
        gold += Mathf.Max(0, amount);
    }

    public void OnRoomCleared(int enemiesDefeated = 0)
    {
        // 1. Expire Power buff (lasts 1 room)
        isPowerBuffActive = false;

        // 2. Decrement Speed buff (lasts 3 rooms)
        if (speedBuffRoomsRemaining > 0)
        {
            speedBuffRoomsRemaining--;
        }

        // 3. Reset per-room purchase flags
        hasBoughtPowerThisRoom = false;
        hasBoughtSpeedThisRoom = false;
        hasBoughtReviveThisRoom = false;

        // 4. Calculate and award room clearance gold bounty
        // Base gold + progression bonus + bounty per enemy
        int roomBounty = 25 + (currentRoom * 6) + (enemiesDefeated * 8);
        AddGold(roomBounty);
    }

    public void ResetRunBuffs()
    {
        isPowerBuffActive = false;
        speedBuffRoomsRemaining = 0;
        hasBoughtPowerThisRoom = false;
        hasBoughtSpeedThisRoom = false;
        hasBoughtReviveThisRoom = false;
    }

    [Header("Healer Priority")]
    public int healerPriorityIndex = -1; // index into squad

    [Header("Reward")]
    public List<RewardData> pendingRewards = new();

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        if (Application.isPlaying) DontDestroyOnLoad(gameObject);

        // Mobile readiness: lock 60 FPS, prevent device sleep during runs, enforce landscape
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.orientation = ScreenOrientation.LandscapeLeft;

        if (availableHeroes.Count == 0)
        {
            LoadDefaultHeroes();
        }
    }

    public void LoadDefaultHeroes()
    {
        var loaded = Resources.LoadAll<HeroData>("Heroes");
        if (loaded != null && loaded.Length > 0)
        {
            availableHeroes = new List<HeroData>(loaded);
        }
    }

    public void EnsureSquadAndFormation()
    {
        if (availableHeroes.Count == 0)
        {
            LoadDefaultHeroes();
        }

        if (squad.Count == 0)
        {
            for (int i = 0; i < Mathf.Min(5, availableHeroes.Count); i++)
            {
                squad.Add(new HeroInstance(availableHeroes[i]));
            }
        }

        bool hasAnyFormation = false;
        for (int i = 0; i < 9; i++)
        {
            if (formation[i] != null) hasAnyFormation = true;
        }

        if (!hasAnyFormation)
        {
            // Default 3x3 layout: K1 (0), K2 (6), M1 (2), M2 (8), H1 (4 - center)
            if (squad.Count >= 5)
            {
                formation[0] = squad[0]; squad[0].gridIndex = 0;
                formation[6] = squad[1]; squad[1].gridIndex = 6;
                formation[4] = squad[2]; squad[2].gridIndex = 4;
                formation[2] = squad[3]; squad[3].gridIndex = 2;
                formation[8] = squad[4]; squad[4].gridIndex = 8;
            }
        }
    }

    public void StartNewRun()
    {
        squad.Clear();
        formation = new HeroInstance[9];
        currentRoom = 0;
        gold = 100;
        healerPriorityIndex = -1;
        ResetRunBuffs();
    }

    public bool IsBossRoom => currentRoom == totalRooms - 1;

    public int AliveCount => squad.FindAll(h => h != null && h.isAlive).Count;
}
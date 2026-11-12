using UnityEngine;
using System.Collections.Generic;

public enum RoomRouteType
{
    Skirmish,       // Standard encounter, normal enemies, safe floor
    Horde,          // High enemy density (6-8 enemies), +50% Gold
    Elite,          // Tough champion enemy (+50% HP, +30% ATK), +75% Gold
    TrappedCrypt    // Floor Spikes hazard, +100% Gold bonus treasure
}

[System.Serializable]
public class RouteOption
{
    public RoomRouteType routeType;
    public string title;
    public string pathName;           // e.g. "LEFT ROUTE" / "RIGHT ROUTE"
    public string enemyPreview;       // e.g. "☠ 3-4 Skeletons"
    public string hazardNotice;       // e.g. "🛡 Clean Floor" or "⚠ Floor Spikes"
    public string rewardPreview;      // e.g. "💰 45 - 60 Gold"
    public int enemyCount = 4;
    public bool hasTraps = false;
    public float goldMultiplier = 1.0f;
    public float enemyHpMultiplier = 1.0f;
    public float enemyDmgMultiplier = 1.0f;
    public Color cardAccentColor = new Color(0.2f, 0.45f, 0.7f);
}

/// <summary>
/// Represents a single room in the procedurally generated dungeon floor.
/// </summary>
[System.Serializable]
public class DungeonRoom
{
    public int index;
    public RectInt bounds;            // tile-space rectangle
    public Vector2 center;            // world center
    public bool isCleared;
    public bool isActive;             // enemies currently alive here
    public bool isBossRoom;
    public List<int> connectedRooms = new();  // indices of connected rooms

    // 2-Road Route Choice Options for this room
    public RouteOption routeOptionA;
    public RouteOption routeOptionB;
    public RouteOption activeRoute;

    // Door positions between this room and connected rooms
    public List<Vector2> doorPositions = new();

    public DungeonRoom(int idx, RectInt rect)
    {
        index = idx;
        bounds = rect;
        center = new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f);
        isCleared = false;
        isActive = false;
        isBossRoom = false;
    }
}

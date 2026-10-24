using UnityEngine;
using System.Collections.Generic;

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

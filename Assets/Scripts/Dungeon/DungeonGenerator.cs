using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Generates a linear chain of rooms for a single dungeon floor.
/// Rooms are placed left-to-right with corridors connecting them.
/// Lightweight CPU cost — suitable for mobile.
/// </summary>
public class DungeonGenerator : MonoBehaviour
{
    [Header("Generation Settings")]
    public int minRooms = 5;
    public int maxRooms = 7;
    public int minRoomWidth = 8;
    public int maxRoomWidth = 12;
    public int minRoomHeight = 6;
    public int maxRoomHeight = 10;
    public int corridorWidth = 3;
    public int gapBetweenRooms = 4;  // horizontal gap for the corridor

    [Header("Room Colors (Simple Rects)")]
    public Color floorColor = new Color(0.22f, 0.18f, 0.15f);
    public Color wallColor = new Color(0.35f, 0.28f, 0.22f);
    public Color corridorColor = new Color(0.18f, 0.15f, 0.12f);
    public Color doorColor = new Color(0.55f, 0.4f, 0.2f);
    public Color bossRoomColor = new Color(0.3f, 0.1f, 0.1f);

    private List<DungeonRoom> rooms = new();
    private List<GameObject> roomVisuals = new();

    public List<DungeonRoom> Rooms => rooms;
    public int RoomCount => rooms.Count;

    /// <summary>
    /// Generates a floor layout and returns the list of rooms.
    /// </summary>
    public List<DungeonRoom> GenerateFloor(int floorNumber)
    {
        ClearPreviousFloor();

        int roomCount = Random.Range(minRooms, maxRooms + 1);
        rooms = new List<DungeonRoom>(roomCount);

        int currentX = 0;
        int currentY = 0;

        for (int i = 0; i < roomCount; i++)
        {
            int w, h;
            if (i == 0)
            {
                // Starting Entrance Chamber
                w = 11;
                h = 8;
            }
            else if (i == roomCount - 1)
            {
                // Grand Colossal Boss Sanctum
                w = Random.Range(22, 26);
                h = Random.Range(14, 17);
            }
            else
            {
                // High Variety Room Sizing: Small Skirmish, Medium Hall, or Huge Grand Arena
                int sizeArchetype = (i + floorNumber) % 3;
                if (sizeArchetype == 0)
                {
                    // Small / Tight Skirmish Chamber (Close-quarters frantic combat)
                    w = Random.Range(8, 10);
                    h = Random.Range(6, 8);
                }
                else if (sizeArchetype == 1)
                {
                    // Medium Tactical Chamber (Balanced maneuvering space)
                    w = Random.Range(13, 16);
                    h = Random.Range(9, 12);
                }
                else
                {
                    // Huge Grand Arena (Epic battlefield for swarms, traps & maneuvering)
                    w = Random.Range(20, 24);
                    h = Random.Range(13, 16);
                }
            }

            var rect = new RectInt(currentX, currentY, w, h);
            var room = new DungeonRoom(i, rect);

            // Last room is boss room
            if (i == roomCount - 1)
                room.isBossRoom = true;

            GenerateRouteOptions(room, floorNumber);
            rooms.Add(room);

            // Directional branching for next room placement: North (+Y), South (-Y), or East (+0)
            int branchGap = 6;
            currentX += w + branchGap;

            if (i < roomCount - 1)
            {
                // Alternate directional shifts so the dungeon climbs and descends organically
                if (i % 2 == 1)
                {
                    currentY += Random.Range(5, 8); // Climb North
                }
                else
                {
                    currentY -= Random.Range(4, 7); // Descend South
                }
            }
        }

        // Connect rooms linearly
        for (int i = 0; i < rooms.Count - 1; i++)
        {
            rooms[i].connectedRooms.Add(i + 1);
            rooms[i + 1].connectedRooms.Add(i);

            // Calculate door position (right edge of current room, left edge of next)
            var r1 = rooms[i].bounds;
            var r2 = rooms[i + 1].bounds;

            float doorY1 = r1.y + r1.height * 0.5f;
            float doorY2 = r2.y + r2.height * 0.5f;

            rooms[i].doorPositions.Add(new Vector2(r1.xMax, doorY1));
            rooms[i + 1].doorPositions.Add(new Vector2(r2.xMin, doorY2));
        }

        BuildVisuals();
        return rooms;
    }

    void GenerateRouteOptions(DungeonRoom room, int floorNumber)
    {
        if (room.index == 0)
        {
            room.activeRoute = new RouteOption
            {
                routeType = RoomRouteType.Skirmish,
                title = "ENTRANCE CHAMBER",
                pathName = "START",
                enemyPreview = "3 Skeletons",
                hazardNotice = "Safe Entrance",
                rewardPreview = "30 Gold",
                enemyCount = 3,
                hasTraps = false,
                goldMultiplier = 1.0f,
                cardAccentColor = new Color(0.2f, 0.45f, 0.7f)
            };
            room.routeOptionA = room.activeRoute;
            room.routeOptionB = room.activeRoute;
            return;
        }

        if (room.isBossRoom)
        {
            room.activeRoute = new RouteOption
            {
                routeType = RoomRouteType.Elite,
                title = "THRONE OF BONES",
                pathName = "BOSS",
                enemyPreview = "Skeleton King + Minions",
                hazardNotice = "Shadow Awakening",
                rewardPreview = "Floor Clearance Trophy",
                enemyCount = 1,
                hasTraps = false,
                goldMultiplier = 2.5f,
                cardAccentColor = new Color(0.75f, 0.15f, 0.15f)
            };
            room.routeOptionA = room.activeRoute;
            room.routeOptionB = room.activeRoute;
            return;
        }

        // Direction-aware route options based on dungeon architecture
        string dirA = "NORTH ROUTE";
        string dirB = "DIRECT PASSAGE";
        if (room.index % 2 == 0)
        {
            dirA = "SOUTH DESCENT";
            dirB = "EAST VAULT";
        }

        string sizeTag = room.bounds.width >= 20 ? " [GRAND ARENA]" : (room.bounds.width <= 10 ? " [SKIRMISH]" : "");

        bool optAIsSwarm = Random.value > 0.55f;
        var optA = new RouteOption
        {
            pathName = dirA,
            routeType = optAIsSwarm ? RoomRouteType.Horde : RoomRouteType.Skirmish,
            title = (optAIsSwarm ? "SWARM CORRIDOR" : "CRYPT PASSAGE") + sizeTag,
            enemyPreview = optAIsSwarm ? "6-7 Swarm Fiends" : "3-4 Skeletons",
            hazardNotice = optAIsSwarm ? "High Density Encounter" : "Clear Chamber (No Traps)",
            rewardPreview = optAIsSwarm ? "+75 Gold (+70%)" : "+45 Gold",
            enemyCount = optAIsSwarm ? Random.Range(6, 8) : Random.Range(3, 5),
            hasTraps = false,
            goldMultiplier = optAIsSwarm ? 1.7f : 1.0f,
            enemyHpMultiplier = 1.0f,
            enemyDmgMultiplier = 1.0f,
            cardAccentColor = optAIsSwarm ? new Color(0.85f, 0.45f, 0.15f) : new Color(0.22f, 0.48f, 0.75f)
        };

        // Option B: Right/East Route (Trapped Crypt or Elite Champion)
        bool optBIsElite = Random.value > 0.5f;
        var optB = new RouteOption
        {
            pathName = dirB,
            routeType = optBIsElite ? RoomRouteType.Elite : RoomRouteType.TrappedCrypt,
            title = (optBIsElite ? "WARLORD'S VAULT" : "SPIKE CRYPT") + sizeTag,
            enemyPreview = optBIsElite ? "1 Skeletal Warlord (2x HP)" : "4-5 Skeletons",
            hazardNotice = optBIsElite ? "Heavy Striking Champion" : "Floor Spikes (10 Dmg)",
            rewardPreview = optBIsElite ? "+110 Gold (+120%)" : "+90 Gold (+100%)",
            enemyCount = optBIsElite ? Random.Range(2, 4) : Random.Range(4, 6),
            hasTraps = !optBIsElite,
            goldMultiplier = optBIsElite ? 2.2f : 2.0f,
            enemyHpMultiplier = optBIsElite ? 1.8f : 1.0f,
            enemyDmgMultiplier = optBIsElite ? 1.35f : 1.0f,
            cardAccentColor = optBIsElite ? new Color(0.65f, 0.22f, 0.85f) : new Color(0.82f, 0.2f, 0.2f)
        };

        room.routeOptionA = optA;
        room.routeOptionB = optB;
        room.activeRoute = optA; // Default fallback
    }

    void LoadSpriteAssets()
    {
        if (sprFloor != null) return;
        sprFloor = Resources.Load<Sprite>("Sprites/frames/floor_1");
        sprFloorBoss = Resources.Load<Sprite>("Sprites/frames/floor_8") ?? sprFloor;
        sprFloorMossy = Resources.Load<Sprite>("Sprites/frames/floor_2") ?? sprFloor;
        sprFloorDark = Resources.Load<Sprite>("Sprites/frames/floor_5") ?? sprFloor;
        sprFloorAncient = Resources.Load<Sprite>("Sprites/frames/floor_3") ?? sprFloor;
        sprWallTop = Resources.Load<Sprite>("Sprites/frames/wall_top_mid");
        sprWallMid = Resources.Load<Sprite>("Sprites/frames/wall_mid");
        sprWallLeft = Resources.Load<Sprite>("Sprites/frames/wall_left");
        sprWallRight = Resources.Load<Sprite>("Sprites/frames/wall_right");
        sprDoorClosed = Resources.Load<Sprite>("Sprites/frames/doors_leaf_closed");
        sprDoorOpen = Resources.Load<Sprite>("Sprites/frames/doors_leaf_open");
        sprCrate = Resources.Load<Sprite>("Sprites/frames/crate");
        sprSkull = Resources.Load<Sprite>("Sprites/frames/skull");
        sprColumn = Resources.Load<Sprite>("Sprites/frames/column_wall");
        sprBannerRed = Resources.Load<Sprite>("Sprites/frames/wall_banner_red");
        sprBannerBlue = Resources.Load<Sprite>("Sprites/frames/wall_banner_blue");
    }

    private Sprite sprFloor;
    private Sprite sprFloorBoss;
    private Sprite sprFloorMossy;
    private Sprite sprFloorDark;
    private Sprite sprFloorAncient;
    private Sprite sprWallTop;
    private Sprite sprWallMid;
    private Sprite sprWallLeft;
    private Sprite sprWallRight;
    private Sprite sprDoorClosed;
    private Sprite sprDoorOpen;
    private Sprite sprCrate;
    private Sprite sprSkull;
    private Sprite sprColumn;
    private Sprite sprBannerRed;
    private Sprite sprBannerBlue;

    /// <summary>
    /// Builds rich tiled dungeon visuals using imported 0x72 tileset sprites.
    /// Uses SpriteDrawMode.Tiled for pixel-perfect walls and floors.
    /// </summary>
    void BuildVisuals()
    {
        LoadSpriteAssets();
        GenerateWallColliders();

        foreach (var room in rooms)
        {
            // 1. Tiled Floor with style variety per room archetype
            Sprite floorSprite = sprFloor;
            if (room.isBossRoom)
            {
                floorSprite = sprFloorBoss;
            }
            else if (room.bounds.width >= 20)
            {
                floorSprite = sprFloorDark ?? sprFloor;
            }
            else if (room.bounds.width <= 10)
            {
                floorSprite = sprFloorMossy ?? sprFloor;
            }
            else if (room.index % 2 == 1)
            {
                floorSprite = sprFloorAncient ?? sprFloor;
            }

            var floorGo = CreateTiledQuad(
                $"Room_{room.index}_Floor",
                room.center,
                new Vector2(room.bounds.width, room.bounds.height),
                floorSprite,
                room.isBossRoom ? bossRoomColor : Color.white,
                0
            );
            roomVisuals.Add(floorGo);

            // 2. Top Wall (Back wall & Wall face)
            if (sprWallTop != null)
            {
                var wallTopGo = CreateTiledQuad(
                    $"Room_{room.index}_WallTop",
                    new Vector2(room.center.x, room.bounds.yMax + 0.5f),
                    new Vector2(room.bounds.width, 1f),
                    sprWallTop,
                    Color.white,
                    3
                );
                roomVisuals.Add(wallTopGo);
            }

            if (sprWallMid != null)
            {
                var wallMidGo = CreateTiledQuad(
                    $"Room_{room.index}_WallMid",
                    new Vector2(room.center.x, room.bounds.yMax - 0.5f),
                    new Vector2(room.bounds.width, 1f),
                    sprWallMid,
                    Color.white,
                    2
                );
                roomVisuals.Add(wallMidGo);
            }

            // 3. Side Walls
            if (sprWallLeft != null)
            {
                var wallLeftGo = CreateTiledQuad(
                    $"Room_{room.index}_WallLeft",
                    new Vector2(room.bounds.xMin - 0.5f, room.center.y),
                    new Vector2(1f, room.bounds.height),
                    sprWallLeft,
                    Color.white,
                    2
                );
                roomVisuals.Add(wallLeftGo);
            }

            if (sprWallRight != null)
            {
                var wallRightGo = CreateTiledQuad(
                    $"Room_{room.index}_WallRight",
                    new Vector2(room.bounds.xMax + 0.5f, room.center.y),
                    new Vector2(1f, room.bounds.height),
                    sprWallRight,
                    Color.white,
                    2
                );
                roomVisuals.Add(wallRightGo);
            }

            // 4. Props & Atmosphere (Columns, Banners, Crates, Skulls)
            if (room.isBossRoom)
            {
                // Boss room banners & skulls
                if (sprBannerRed != null)
                {
                    AddProp($"BossBanner_1_{room.index}", new Vector2(room.center.x - 2f, room.bounds.yMax - 0.5f), sprBannerRed, 4);
                    AddProp($"BossBanner_2_{room.index}", new Vector2(room.center.x + 2f, room.bounds.yMax - 0.5f), sprBannerRed, 4);
                }
                if (sprSkull != null)
                {
                    AddProp($"Skull_1_{room.index}", new Vector2(room.bounds.xMin + 1.2f, room.bounds.yMin + 1f), sprSkull, 1);
                    AddProp($"Skull_2_{room.index}", new Vector2(room.bounds.xMax - 1.2f, room.bounds.yMin + 1f), sprSkull, 1);
                }
            }
            else
            {
                // Huge Room Grand Columns for tactical cover and cathedral architecture
                if (room.bounds.width >= 19 && sprColumn != null)
                {
                    float cx = room.center.x;
                    float cy = room.center.y;
                    AddProp($"Col_{room.index}_1", new Vector2(cx - 5f, cy + 2.8f), sprColumn, 3);
                    AddProp($"Col_{room.index}_2", new Vector2(cx + 5f, cy + 2.8f), sprColumn, 3);
                    AddProp($"Col_{room.index}_3", new Vector2(cx - 5f, cy - 2.8f), sprColumn, 3);
                    AddProp($"Col_{room.index}_4", new Vector2(cx + 5f, cy - 2.8f), sprColumn, 3);
                }

                // Occasional regular room decorations
                if (sprBannerBlue != null && room.index % 2 == 1)
                {
                    AddProp($"Banner_{room.index}", new Vector2(room.center.x, room.bounds.yMax - 0.5f), sprBannerBlue, 4);
                }
                if (sprCrate != null && room.index % 2 == 0)
                {
                    AddProp($"Crate_{room.index}", new Vector2(room.bounds.xMin + 1.2f, room.bounds.yMax - 1.8f), sprCrate, 2);
                }
            }
        }

        // Corridors between rooms with directional elbow / L-bend support
        for (int i = 0; i < rooms.Count - 1; i++)
        {
            var r1 = rooms[i].bounds;
            var r2 = rooms[i + 1].bounds;

            float doorY1 = r1.y + r1.height * 0.5f;
            float doorY2 = r2.y + r2.height * 0.5f;
            float midX = (r1.xMax + r2.xMin) * 0.5f;

            // Segment 1: Horizontal exit from r1 to midX
            float seg1Width = midX - r1.xMax + 0.5f;
            var corrGo1 = CreateTiledQuad(
                $"Corridor_{i}_seg1",
                new Vector2((r1.xMax + midX) * 0.5f, doorY1),
                new Vector2(seg1Width, corridorWidth),
                sprFloor,
                new Color(0.85f, 0.85f, 0.85f),
                0
            );
            roomVisuals.Add(corrGo1);

            // Segment 2: Vertical shaft along midX if rooms are at different heights
            float dy = Mathf.Abs(doorY2 - doorY1);
            if (dy > 0.5f)
            {
                float minY = Mathf.Min(doorY1, doorY2);
                float maxY = Mathf.Max(doorY1, doorY2);
                var corrGo2 = CreateTiledQuad(
                    $"Corridor_{i}_seg2",
                    new Vector2(midX, (minY + maxY) * 0.5f),
                    new Vector2(corridorWidth, maxY - minY + corridorWidth),
                    sprFloor,
                    new Color(0.80f, 0.80f, 0.80f),
                    0
                );
                roomVisuals.Add(corrGo2);
            }

            // Segment 3: Horizontal entry from midX to r2
            float seg3Width = r2.xMin - midX + 0.5f;
            var corrGo3 = CreateTiledQuad(
                $"Corridor_{i}_seg3",
                new Vector2((midX + r2.xMin) * 0.5f, doorY2),
                new Vector2(seg3Width, corridorWidth),
                sprFloor,
                new Color(0.85f, 0.85f, 0.85f),
                0
            );
            roomVisuals.Add(corrGo3);

            // Door indicators
            var doorSprite = sprDoorOpen ?? sprDoorClosed;
            if (doorSprite != null)
            {
                var door1 = AddProp(
                    $"Door_{i}_exit",
                    new Vector2(r1.xMax, doorY1),
                    doorSprite,
                    4
                );
                var door2 = AddProp(
                    $"Door_{i + 1}_entry",
                    new Vector2(r2.xMin, doorY2),
                    doorSprite,
                    4
                );
            }
        }
    }

    GameObject CreateTiledQuad(string name, Vector2 position, Vector2 size, Sprite sprite, Color tint, int sortOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(position.x, position.y, 0);

        var sr = go.AddComponent<SpriteRenderer>();
        if (sprite != null)
        {
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = size;
        }
        else
        {
            sr.sprite = CreateRectSprite();
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        sr.color = tint;
        sr.sortingOrder = sortOrder;
        return go;
    }

    GameObject AddProp(string name, Vector2 position, Sprite sprite, int sortOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(position.x, position.y, 0);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = sortOrder;
        roomVisuals.Add(go);
        return go;
    }

    // Cached white 1x1 sprite fallback
    private static Sprite _cachedRect;
    static Sprite CreateRectSprite()
    {
        if (_cachedRect != null) return _cachedRect;

        var tex = new Texture2D(4, 4);
        var pixels = new Color[16];
        for (int i = 0; i < 16; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Point;

        _cachedRect = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        return _cachedRect;
    }

    void ClearPreviousFloor()
    {
        foreach (var go in roomVisuals)
        {
            if (go) Destroy(go);
        }
        roomVisuals.Clear();
        rooms.Clear();
    }

    /// <summary>
    /// Gets the world-space entry point (center) of a room.
    /// </summary>
    public Vector2 GetRoomCenter(int roomIndex)
    {
        if (roomIndex < 0 || roomIndex >= rooms.Count) return Vector2.zero;
        return rooms[roomIndex].center;
    }

    /// <summary>
    /// Gets a world-space spawn position within a room for an entity.
    /// </summary>
    public Vector2 GetSpawnPosition(int roomIndex, bool isEnemy, int entityIndex, int totalEntities)
    {
        if (roomIndex < 0 || roomIndex >= rooms.Count) return Vector2.zero;

        var room = rooms[roomIndex];
        float halfW = room.bounds.width * 0.35f;
        float halfH = room.bounds.height * 0.35f;

        if (isEnemy)
        {
            // Enemies spawn on the right side of the room
            float x = room.center.x + Random.Range(halfW * 0.2f, halfW);
            float y = room.center.y + Random.Range(-halfH, halfH);
            return new Vector2(x, y);
        }
        else
        {
            // Heroes enter from the left side in clear formation
            // Spread them in a 2-column pattern with generous spacing
            int col = entityIndex % 2;
            int row = entityIndex / 2;
            float spacingX = 1.3f;
            float spacingY = 1.1f;
            float x = room.center.x - halfW * 0.7f + col * spacingX;
            float y = room.center.y + 0.9f - row * spacingY;
            return new Vector2(x, y);
        }
    }

    /// <summary>
    /// Returns the door/corridor exit position to walk to for the next room.
    /// </summary>
    public Vector2 GetDoorExitPosition(int currentRoomIndex)
    {
        if (currentRoomIndex < 0 || currentRoomIndex >= rooms.Count) return Vector2.zero;
        var room = rooms[currentRoomIndex];
        return new Vector2(room.bounds.xMax + 0.5f, room.center.y);
    }

    /// <summary>
    /// Returns the door entry position when entering a room.
    /// </summary>
    public Vector2 GetDoorEntryPosition(int roomIndex)
    {
        if (roomIndex < 0 || roomIndex >= rooms.Count) return Vector2.zero;
        var room = rooms[roomIndex];
        return new Vector2(room.bounds.xMin + 1f, room.center.y);
    }

    /// <summary>
    /// Returns 4 waypoints for heroes to physically walk through the directional corridor:
    ///   [0] = exit door of currentRoom
    ///   [1] = corridor turn point 1 (aligned with exit door Y)
    ///   [2] = corridor turn point 2 (aligned with entry door Y)
    ///   [3] = entry door of nextRoom
    /// </summary>
    public Vector2[] GetCorridorWaypoints(int currentRoomIndex, int nextRoomIndex)
    {
        if (currentRoomIndex < 0 || currentRoomIndex >= rooms.Count) return null;
        if (nextRoomIndex < 0 || nextRoomIndex >= rooms.Count) return null;

        var r1 = rooms[currentRoomIndex].bounds;
        var r2 = rooms[nextRoomIndex].bounds;

        float doorY1 = r1.y + r1.height * 0.5f;
        float doorY2 = r2.y + r2.height * 0.5f;
        float midX = (r1.xMax + r2.xMin) * 0.5f;

        Vector2 exitDoor = new Vector2(r1.xMax + 0.5f, doorY1);
        Vector2 turn1 = new Vector2(midX, doorY1);
        Vector2 turn2 = new Vector2(midX, doorY2);
        Vector2 entryDoor = new Vector2(r2.xMin + 0.5f, doorY2);

        if (Mathf.Abs(doorY1 - doorY2) < 0.6f)
        {
            return new Vector2[] { exitDoor, turn1, entryDoor };
        }

        return new Vector2[] { exitDoor, turn1, turn2, entryDoor };
    }

    /// <summary>
    /// Returns the inner walkable rectangle for a room (with wall margin).
    /// Used for combat boundary clamping so characters never drift into walls.
    /// </summary>
    public Rect GetInnerRoomBounds(int roomIndex)
    {
        if (roomIndex < 0 || roomIndex >= rooms.Count) return new Rect();

        var b = rooms[roomIndex].bounds;
        float margin = 0.8f;
        return new Rect(
            b.xMin + margin,
            b.yMin + margin,
            b.width - margin * 2f,
            b.height - margin * 2f
        );
    }

    /// <summary>
    /// Generates BoxCollider2D on all room perimeter walls.
    /// Acts as a physics safety net to prevent any character from clipping through.
    /// </summary>
    void GenerateWallColliders()
    {
        foreach (var room in rooms)
        {
            var b = room.bounds;
            float wallThickness = 1f;

            // Top wall
            CreateWallCollider($"WallCol_Top_{room.index}",
                new Vector2(b.x + b.width * 0.5f, b.yMax + wallThickness * 0.5f),
                new Vector2(b.width + 2f, wallThickness));

            // Bottom wall
            CreateWallCollider($"WallCol_Bot_{room.index}",
                new Vector2(b.x + b.width * 0.5f, b.yMin - wallThickness * 0.5f),
                new Vector2(b.width + 2f, wallThickness));

            // Left wall (leave door opening if not room 0)
            if (room.index > 0)
            {
                float doorY = b.y + b.height * 0.5f;
                float doorBottom = doorY - corridorWidth * 0.5f;
                float doorTop = doorY + corridorWidth * 0.5f;

                float topH = b.yMax - doorTop;
                if (topH > 0.1f)
                {
                    CreateWallCollider($"WallCol_LeftTop_{room.index}",
                        new Vector2(b.xMin - wallThickness * 0.5f, doorTop + topH * 0.5f),
                        new Vector2(wallThickness, topH));
                }

                float botH = doorBottom - b.yMin;
                if (botH > 0.1f)
                {
                    CreateWallCollider($"WallCol_LeftBot_{room.index}",
                        new Vector2(b.xMin - wallThickness * 0.5f, b.yMin + botH * 0.5f),
                        new Vector2(wallThickness, botH));
                }
            }
            else
            {
                CreateWallCollider($"WallCol_Left_{room.index}",
                    new Vector2(b.xMin - wallThickness * 0.5f, b.y + b.height * 0.5f),
                    new Vector2(wallThickness, b.height + 2f));
            }

            // Right wall (leave door opening if not last room)
            if (room.index < rooms.Count - 1)
            {
                float doorY = b.y + b.height * 0.5f;
                float doorBottom = doorY - corridorWidth * 0.5f;
                float doorTop = doorY + corridorWidth * 0.5f;

                float topH = b.yMax - doorTop;
                if (topH > 0.1f)
                {
                    CreateWallCollider($"WallCol_RightTop_{room.index}",
                        new Vector2(b.xMax + wallThickness * 0.5f, doorTop + topH * 0.5f),
                        new Vector2(wallThickness, topH));
                }

                float botH = doorBottom - b.yMin;
                if (botH > 0.1f)
                {
                    CreateWallCollider($"WallCol_RightBot_{room.index}",
                        new Vector2(b.xMax + wallThickness * 0.5f, b.yMin + botH * 0.5f),
                        new Vector2(wallThickness, botH));
                }
            }
            else
            {
                CreateWallCollider($"WallCol_Right_{room.index}",
                    new Vector2(b.xMax + wallThickness * 0.5f, b.y + b.height * 0.5f),
                    new Vector2(wallThickness, b.height + 2f));
            }
        }

        // Corridor walls (top and bottom of corridor passages)
        for (int i = 0; i < rooms.Count - 1; i++)
        {
            var r1 = rooms[i].bounds;
            var r2 = rooms[i + 1].bounds;

            float corrStartX = r1.xMax;
            float corrEndX = r2.xMin;
            float corrWidth = corrEndX - corrStartX;
            float corrMidX = corrStartX + corrWidth * 0.5f;
            float corrY = Mathf.Lerp(
                r1.y + r1.height * 0.5f,
                r2.y + r2.height * 0.5f,
                0.5f);
            float halfCorrH = corridorWidth * 0.5f;

            // Top corridor wall
            CreateWallCollider($"CorrWallCol_Top_{i}",
                new Vector2(corrMidX, corrY + halfCorrH + 0.5f),
                new Vector2(corrWidth, 1f));

            // Bottom corridor wall
            CreateWallCollider($"CorrWallCol_Bot_{i}",
                new Vector2(corrMidX, corrY - halfCorrH - 0.5f),
                new Vector2(corrWidth, 1f));
        }
    }

    void CreateWallCollider(string name, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(position.x, position.y, 0f);
        go.isStatic = true;

        var col = go.AddComponent<BoxCollider2D>();
        col.size = size;

        roomVisuals.Add(go); // tracked for cleanup
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

public class LevelGenerator : MonoBehaviour
{
    public enum RoomType
    {
        Random = 0,
        Corridor = 1,
        DropFrom = 2,
        DropTo = 3
    }

    public enum TileType
    { Empty, Dirt, Entrance, Exit, Spike, Ladder, Bomb, Coin, Damsel, Snake, Bat, Wall }

    public class Coordinates
    {
        public int x { get; set; }
        public int y { get; set; }
    }

    public class Room
    {
        public RoomType Type { get; set; }
        public GameObject[,] Grid { get; set; }
        public int X { get; set; }
        public int Y { get; set; }

        public Vector2 Center()
        {
            int offsetX = X * 10; //Left to right
            int offsetY = Y * 8; //Top to bottom
            return new Vector2(offsetX + 10 / 2, offsetY + 8 / 2);
        }
    }

    private int roomWidth = 10;
    private int roomHeight = 8;
    private int levelWidth = 5;
    private int levelHeight = 5;

    public GameObject air;
    float xMove = 1;
    float yMove = 1;
    float xStart = 0.5f;
    float yStart = 0.5f;

    public List<GameObject> topWalls = new();
    public List<GameObject> leftWalls = new();
    public List<GameObject> rightWalls = new();
    public List<GameObject> bottomWalls = new();

    List<Coordinates> RoomPath;
    public List<GameObject> TilePath;

    public Room[,] rooms;
    List<Room> optionalRooms;
    GameObject[,] allTiles;

    public int RoomPathLength;
    public int TilePathLength;
    public int VerticalCorridors;
    public int NumberOfSpikes;
    public int NumberAndTypeOfEnemies;

    public int UniqueTiles;

    public int DifficultyScore;
    public int FunScore;

    public bool CalibrationMode = false;

    // 10%, 50%, 90%, 1% value
    public float[,] DiffScores = new float[3, 4] {
   {289.5f, 467.5f, 645.5f, 4.45f} , // 3x3
   {532.5f, 842.5f, 1152.5f, 7.75f} , // 4x4
   {712f, 1040f, 1368f, 8.2f}  // 5x5
};

    public float DiffScoreToGenerate = 0;
    public float PercentValue = 0;

    [SerializeField]
    private TMP_Dropdown sizeDropdown;
    [SerializeField]
    private TMP_Dropdown difficultyDropdown;
    [SerializeField]
    private TMP_Text _diff;


    private void GetNormalizedDifficultyValues()
    {
        int min = int.MaxValue;
        int max = int.MinValue;
        for (int i = 0; i < 100; i++)
        {
            RegenerateLevel(levelWidth, levelHeight);
            if (DifficultyScore > max)
            {
                max = DifficultyScore;
            }

            if (DifficultyScore < min)
            {
                min = DifficultyScore;
            }
        }

        Debug.Log($"min = {min}, max = {max}");
        // min = 0%, max = 100%

        float percent = (max - min) / 100f;

        float ten = min + percent * 10f;
        float fifty = min + percent * 50f;
        float ninety = min + percent * 90f;

        Debug.Log($"10% = {ten}, 50% = {fifty}, 90% = {ninety}, 1% size = {percent}");
    }

    private void ResetLevel()
    {
        RoomPath = new();
        TilePath = new();

        rooms = new Room[levelWidth, levelHeight];
        optionalRooms = new();

        if (allTiles is not null)
        {
            foreach (var tile in allTiles)
            {
                Destroy(tile);
            }
        }

        allTiles = new GameObject[roomWidth * levelWidth, roomHeight * levelHeight];

        RoomPathLength = 0;
        TilePathLength = 0;
        VerticalCorridors = 0;
        NumberOfSpikes = 0;
        NumberAndTypeOfEnemies = 0;
        DifficultyScore = 0;
        FunScore = 0;
    }

    private void Start()
    {
        //GetNormalizedDifficultyValues();
    }

    void BuildAndDigOutRooms(int[,] board)
    {
        float xP;
        float yP = yStart;
        for (int y = 0; y < levelWidth; y++)
        {
            xP = xStart;
            for (int x = 0; x < levelHeight; x++)
            {
                var r = BuildARoom(xP, yP, (RoomType)board[x, y], 30);
                r.X = x;
                r.Y = y;
                rooms[x, y] = r;
                xP += 10;
            }
            yP += 8;
        }

        for (int y = levelWidth - 1; y >= 0; y--)
        {
            for (int x = 0; x < levelHeight; x++)
            {
                if (rooms[x, y].Type is not RoomType.Random)
                {
                    DigOutWallsWithDigger(rooms[x, y], x, y);
                }
            }
        }
    }

    public void GenerateLevel()
    {
        if (CalibrationMode is false)
        {
            switch (sizeDropdown.value)
            {
                case 0:
                    return;
                case 1:
                    levelWidth = 3;
                    levelHeight = 3;
                    break;
                case 2:
                    levelWidth = 4;
                    levelHeight = 4;
                    break;
                case 3:
                    levelWidth = 5;
                    levelHeight = 5;
                    break;
            }

            switch (difficultyDropdown.value)
            {
                case 0:
                    return;
                case 1:
                    DiffScoreToGenerate = DiffScores[sizeDropdown.value - 1, 0];
                    break;
                case 2:
                    DiffScoreToGenerate = DiffScores[sizeDropdown.value - 1, 1];
                    break;
                case 3:
                    DiffScoreToGenerate = DiffScores[sizeDropdown.value - 1, 2];
                    break;
            }
        PercentValue = DiffScores[sizeDropdown.value - 1, 3];
        }


        Debug.Log($"Generating a level with {DiffScoreToGenerate} diff score.");
        var watch = System.Diagnostics.Stopwatch.StartNew();

        bool GeneratedLevelWithinDifficulty = false;

        for (int i = 0; i < 100; i++)
        {
            ResetLevel();

            var board = GenerateRoomPath();

            BuildAndDigOutRooms(board);

            Coordinates startRoom = RoomPath.First();
            Coordinates endRoom = RoomPath.Last();
            Room roomUnder = startRoom.y > 0 ? rooms[startRoom.x, startRoom.y - 1] : null;

            PutEntrance(rooms[startRoom.x, startRoom.y], roomUnder);
            PutExit(rooms[endRoom.x, endRoom.y]);


            //BreakingUpRandomGrid();
            //BuildWalls();
            GenerateLadders();
            GenerateItems();
            GenerateDamsel();
            GenerateBats();
            GenerateSpikes();
            GenerateSnakes();
            PathFindUsingBreadthFirstSearch();
            RemoveRandomDirtTiles();

            CalculateDifficultyScore();
            _diff.text = DifficultyScore.ToString();

            CalculateFunScore();

            Debug.Log($"{Math.Abs(DiffScoreToGenerate - DifficultyScore)} < {PercentValue} * 5");
            if (DiffScoreToGenerate == 0 || Math.Abs(DiffScoreToGenerate - DifficultyScore) < PercentValue * 5 )
            {
                GeneratedLevelWithinDifficulty = true;
                break;
            }
        }

            watch.Stop();
            Debug.Log("Generation Time: " + watch.ElapsedMilliseconds + "ms");

        if(GeneratedLevelWithinDifficulty is false)
        {
            Debug.Log($"ERROR, couldn't generate a level in 100 tries");
        }
    }

    private void RemoveRandomDirtTiles()
    {
        foreach (var tile in allTiles)
        {
            if (tile.GetComponent<TileScript>().Type == TileType.Dirt && Random.Range(1, 21) == 1)
            {
                tile.GetComponent<TileScript>().Create(TileType.Empty);
            }
        }
    }

    private void CalculateDifficultyScore()
    {
        RoomPathLength = RoomPath.Count;
        TilePathLength = TilePath.Count;
        VerticalCorridors = 0;
        NumberOfSpikes = 0;
        NumberAndTypeOfEnemies = 0;

        for (int i = 0; i < allTiles.GetLength(0); i++)
        {
            for (int j = 0; j < allTiles.GetLength(1); j++)
            {
                if (allTiles[i, j].GetComponent<TileScript>().Type == TileType.Spike)
                {
                    NumberOfSpikes++;
                }
                else if (allTiles[i, j].GetComponent<TileScript>().Type == TileType.Snake)
                {
                    NumberAndTypeOfEnemies++;
                }
                else if (allTiles[i, j].GetComponent<TileScript>().Type == TileType.Bat)
                {
                    NumberAndTypeOfEnemies += 2;
                }
            }
        }

        for (int i = 0; i < RoomPath.Count - 2; i++)
        {
            if (RoomPath[i].y != RoomPath[i + 1].y && RoomPath[i].y != RoomPath[i + 2].y && RoomPath[i + 1].y != RoomPath[i + 2].y)
            {
                VerticalCorridors++;
            }
        }

        DifficultyScore = 20 * RoomPathLength + 5 * TilePathLength + 20 * NumberOfSpikes + 10 * NumberAndTypeOfEnemies + 100 * VerticalCorridors;
    }

    private void CalculateFunScore()
    {
        List<TileType> tileTypes = new();

        foreach (var tile in allTiles)
        {
            if (!tileTypes.Contains(tile.GetComponent<TileScript>().Type))
            {
                tileTypes.Add(tile.GetComponent<TileScript>().Type);
            }
        }

        FunScore = 10 * tileTypes.Count;
    }

    void GenerateBats()
    {
        foreach (var r in rooms)
        {
            if (Random.Range(1, 7) != 1)
            {
                continue;
            }

            Coordinates pos;
            if (r.Y == levelHeight - 1)
            {
                pos = GetPositionOnRoof(r, null);
            }
            else
            {
                pos = GetPositionOnRoof(r, rooms[r.X, r.Y + 1]);
            }

            if (pos is null)
            {
                return;
            }

            r.Grid[pos.x, pos.y].GetComponent<TileScript>().Create(TileType.Bat);
        }
    }

    void GenerateSnakes()
    {
        foreach (var r in rooms)
        {
            var grid = r.Grid;

            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    if (grid[x, y].GetComponent<TileScript>().Type == TileType.Empty
                        && grid[x + 1, y].GetComponent<TileScript>().Type == TileType.Empty
                        && grid[x + 2, y].GetComponent<TileScript>().Type == TileType.Empty)
                    {
                        if (Random.Range(1, 4) != 1)
                        {
                            continue;
                        }

                        if (y is 0 && r.Y is not 0)
                        {
                            var bottomGrid = rooms[r.X, r.Y - 1].Grid;
                            if (bottomGrid[x, 7].GetComponent<TileScript>().Type == TileType.Dirt
                                && bottomGrid[x + 1, 7].GetComponent<TileScript>().Type == TileType.Dirt
                                && bottomGrid[x + 2, 7].GetComponent<TileScript>().Type == TileType.Dirt)
                            {
                                grid[x + Random.Range(0, 3), y].GetComponent<TileScript>().Create(TileType.Snake);
                            }
                        }
                        else if (y is 0 && r.Y is 0)
                        {
                            grid[x + Random.Range(0, 3), y].GetComponent<TileScript>().Create(TileType.Snake);
                        }
                    }
                }
            }
        }
    }

    void GenerateSpikes()
    {
        foreach (var r in rooms)
        {
            if (Random.Range(1, 4) != 1)
            {
                continue;
            }

            Coordinates pos;
            if (r.Y == 0)
            {
                pos = GetPositionOnGround(r, null, 3);
            }
            else
            {
                pos = GetPositionOnGround(r, rooms[r.X, r.Y - 1], 3);
            }

            if (pos is null)
            {
                return;
            }

            if (pos.y != roomHeight - 1 && r.Grid[pos.x, pos.y + 1].GetComponent<TileScript>().Type == TileType.Empty &&
                pos.x != 0 && r.Grid[pos.x - 1, pos.y + 1].GetComponent<TileScript>().Type == TileType.Empty &&
                pos.x != roomWidth - 1 && r.Grid[pos.x + 1, pos.y + 1].GetComponent<TileScript>().Type == TileType.Empty)
            {
                r.Grid[pos.x, pos.y].GetComponent<TileScript>().Create(TileType.Spike);
            }
        }
    }

    void GenerateItems()
    {
        foreach (var r in rooms)
        {
            if (Random.Range(1, 4) != 1)
            {
                continue;
            }

            Coordinates pos;
            if (r.Y == 0)
            {
                pos = GetPositionOnGround(r, null, 8);
            }
            else
            {
                pos = GetPositionOnGround(r, rooms[r.X, r.Y - 1], 8);
            }

            if (pos is null)
            {
                return;
            }

            TileType typeToCreate = TileType.Coin;

            if (Random.Range(1, 4) == 1)
            {
                typeToCreate = TileType.Bomb;
            }

            r.Grid[pos.x, pos.y].GetComponent<TileScript>().Create(typeToCreate);
        }
    }

    void GenerateLaddersOld()
    {
        foreach (var r in rooms)
        {
            Room roomUnder = null;

            if (r.Y > 0)
            {
                roomUnder = rooms[r.X, r.Y - 1];
            }

            var grid = r.Grid;

            for (int x = 0; x < 10; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    if (grid[x, y + 3].GetComponent<TileScript>().Type == TileType.Dirt
                        && grid[x, y + 4].GetComponent<TileScript>().Type == TileType.Empty)
                    {
                        if (x is not 9)
                        {
                            if (y == 0)
                            {
                                if (roomUnder is not null || r.Y == 0)
                                {
                                    if ((r.Y == 0 || roomUnder.Grid[x + 1, 7].GetComponent<TileScript>().Type == TileType.Dirt)
                                    && grid[x + 1, y].GetComponent<TileScript>().Type == TileType.Empty
                                    && grid[x + 1, y + 1].GetComponent<TileScript>().Type == TileType.Empty
                                    && grid[x + 1, y + 2].GetComponent<TileScript>().Type == TileType.Empty)
                                    {
                                        grid[x + 1, y].GetComponent<TileScript>().Create(TileType.Ladder);
                                        grid[x + 1, y + 1].GetComponent<TileScript>().Create(TileType.Ladder);
                                        grid[x + 1, y + 2].GetComponent<TileScript>().Create(TileType.Ladder);
                                    }
                                }
                            }
                            else
                            {
                                if (grid[x + 1, y].GetComponent<TileScript>().Type == TileType.Dirt
                                    && grid[x + 1, y + 1].GetComponent<TileScript>().Type == TileType.Empty
                                    && grid[x + 1, y + 2].GetComponent<TileScript>().Type == TileType.Empty
                                    && grid[x + 1, y + 3].GetComponent<TileScript>().Type == TileType.Empty)
                                {
                                    grid[x + 1, y + 1].GetComponent<TileScript>().Create(TileType.Ladder);
                                    grid[x + 1, y + 2].GetComponent<TileScript>().Create(TileType.Ladder);
                                    grid[x + 1, y + 3].GetComponent<TileScript>().Create(TileType.Ladder);
                                }
                            }
                        }
                        else if (x is not 0)
                        {
                            if (y == 0)
                            {
                                if (roomUnder is not null || r.Y == 0)
                                {
                                    if ((r.Y == 0 || roomUnder.Grid[x - 1, 7].GetComponent<TileScript>().Type == TileType.Dirt)
                                    && grid[x - 1, y].GetComponent<TileScript>().Type == TileType.Empty
                                    && grid[x - 1, y + 1].GetComponent<TileScript>().Type == TileType.Empty
                                    && grid[x - 1, y + 2].GetComponent<TileScript>().Type == TileType.Empty)
                                    {
                                        grid[x - 1, y].GetComponent<TileScript>().Create(TileType.Ladder);
                                        grid[x - 1, y + 1].GetComponent<TileScript>().Create(TileType.Ladder);
                                        grid[x - 1, y + 2].GetComponent<TileScript>().Create(TileType.Ladder);
                                    }
                                }
                            }
                            else
                            {
                                if (grid[x - 1, y].GetComponent<TileScript>().Type == TileType.Dirt
                                    && grid[x - 1, y + 1].GetComponent<TileScript>().Type == TileType.Empty
                                    && grid[x - 1, y + 2].GetComponent<TileScript>().Type == TileType.Empty
                                    && grid[x - 1, y + 3].GetComponent<TileScript>().Type == TileType.Empty)
                                {
                                    grid[x - 1, y + 1].GetComponent<TileScript>().Create(TileType.Ladder);
                                    grid[x - 1, y + 2].GetComponent<TileScript>().Create(TileType.Ladder);
                                    grid[x - 1, y + 3].GetComponent<TileScript>().Create(TileType.Ladder);
                                }
                            }
                        }
                    }
                    else if (grid[x, y + 2].GetComponent<TileScript>().Type == TileType.Dirt
                        && grid[x, y + 3].GetComponent<TileScript>().Type == TileType.Empty)
                    {
                        if (x is not 9)
                        {
                            if (y == 0)
                            {
                                if (roomUnder is not null || r.Y == 0)
                                {
                                    if ((r.Y == 0 || roomUnder.Grid[x + 1, 7].GetComponent<TileScript>().Type == TileType.Dirt)
                                    && grid[x + 1, y].GetComponent<TileScript>().Type == TileType.Empty
                                    && grid[x + 1, y + 1].GetComponent<TileScript>().Type == TileType.Empty
                                    && grid[x + 1, y + 2].GetComponent<TileScript>().Type == TileType.Empty)
                                    {
                                        grid[x + 1, y].GetComponent<TileScript>().Create(TileType.Ladder);
                                        grid[x + 1, y + 1].GetComponent<TileScript>().Create(TileType.Ladder);
                                        grid[x + 1, y + 2].GetComponent<TileScript>().Create(TileType.Ladder);
                                    }
                                }
                            }
                            else if (x is not 0)
                            {
                                if (y == 0)
                                {
                                    if (roomUnder is not null || r.Y == 0)
                                    {
                                        if ((r.Y == 0 || roomUnder.Grid[x - 1, 7].GetComponent<TileScript>().Type == TileType.Dirt)
                                        && grid[x - 1, y].GetComponent<TileScript>().Type == TileType.Empty
                                        && grid[x - 1, y + 1].GetComponent<TileScript>().Type == TileType.Empty
                                        && grid[x - 1, y + 2].GetComponent<TileScript>().Type == TileType.Empty)
                                        {
                                            grid[x - 1, y].GetComponent<TileScript>().Create(TileType.Ladder);
                                            grid[x - 1, y + 1].GetComponent<TileScript>().Create(TileType.Ladder);
                                            grid[x - 1, y + 2].GetComponent<TileScript>().Create(TileType.Ladder);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    void GenerateLadders()
    {
        foreach (var r in rooms)
        {
            Room roomUnder = null;

            if (r.Y > 0)
            {
                roomUnder = rooms[r.X, r.Y - 1];
            }

            var grid = r.Grid;

            for (int x = 0; x < roomWidth; x++)
                for (int y = 3; y < roomHeight; y++)
                {
                    if (grid[x, y].GetComponent<TileScript>().Type == TileType.Empty && grid[x, y - 1].GetComponent<TileScript>().Type == TileType.Dirt)
                    {
                        if (x != 0 && grid[x - 1, y].GetComponent<TileScript>().Type == TileType.Empty
                            && grid[x - 1, y - 1].GetComponent<TileScript>().Type == TileType.Empty
                            && grid[x - 1, y - 2].GetComponent<TileScript>().Type == TileType.Empty
                            && grid[x - 1, y - 3].GetComponent<TileScript>().Type == TileType.Empty
                            && ((y != 3 && grid[x - 1, y - 4].GetComponent<TileScript>().Type == TileType.Dirt)
                            || (roomUnder is not null && y == 3 && roomUnder.Grid[x - 1, roomHeight - 1].GetComponent<TileScript>().Type == TileType.Dirt)))
                        {
                            grid[x - 1, y - 1].GetComponent<TileScript>().Create(TileType.Ladder);
                            grid[x - 1, y - 2].GetComponent<TileScript>().Create(TileType.Ladder);
                            grid[x - 1, y - 3].GetComponent<TileScript>().Create(TileType.Ladder);
                        }
                        else if (x != 9 && grid[x + 1, y].GetComponent<TileScript>().Type == TileType.Empty
                            && grid[x + 1, y - 1].GetComponent<TileScript>().Type == TileType.Empty
                            && grid[x + 1, y - 2].GetComponent<TileScript>().Type == TileType.Empty
                            && grid[x + 1, y - 3].GetComponent<TileScript>().Type == TileType.Empty
                            && ((y != 3 && grid[x + 1, y - 4].GetComponent<TileScript>().Type == TileType.Dirt)
                            || (roomUnder is not null && y == 3 && roomUnder.Grid[x + 1, roomHeight - 1].GetComponent<TileScript>().Type == TileType.Dirt)))
                        {
                            grid[x + 1, y - 1].GetComponent<TileScript>().Create(TileType.Ladder);
                            grid[x + 1, y - 2].GetComponent<TileScript>().Create(TileType.Ladder);
                            grid[x + 1, y - 3].GetComponent<TileScript>().Create(TileType.Ladder);
                        }
                    }
                }
        }
    }

    void GenerateDamsel()
    {
        var path = RoomPath.ToList();
        path.Remove(path.First());
        path.Remove(path.Last());

        var chosenRoom = path[Random.Range(0, path.Count)];

        Room roomUnder = null;
        if (chosenRoom.y > 0)
        {
            roomUnder = rooms[chosenRoom.x, chosenRoom.y - 1];
        }

        var damselPos = GetPositionOnGround(rooms[chosenRoom.x, chosenRoom.y], roomUnder, 8);
        rooms[chosenRoom.x, chosenRoom.y].Grid[damselPos.x, damselPos.y].GetComponent<TileScript>().Create(TileType.Damsel);
    }

    public void RegenerateLevel(int width, int height)
    {
        if (rooms is not null)
        {
            foreach (var room in rooms)
            {
                if (room is not null)
                {
                    foreach (var t in room.Grid)
                    {
                        Destroy(t);
                    }
                }
            }
            foreach (var room in optionalRooms)
            {
                if (room is not null)
                {
                    foreach (var t in room.Grid)
                    {
                        Destroy(t);
                    }
                }
            }
        }
        GenerateLevel();
    }

    void PutEntrance(Room room, Room roomUnder)
    {
        var pos = GetPositionOnGround(room, roomUnder, 8);
        room.Grid[pos.x, pos.y].GetComponent<TileScript>().Create(TileType.Entrance);
    }

    void PutExit(Room room)
    {
        var pos = GetPositionOnGround(room, null, 8);
        room.Grid[pos.x, pos.y].GetComponent<TileScript>().Create(TileType.Exit);
    }

    Coordinates GetPositionOnRoof(Room room, Room roomAbove)
    {
        List<Coordinates> availablePos = new();
        for (int x = 0; x < 10; x++)
        {
            for (int y = 5; y < 8; y++)
            {
                if (y == 7)
                {
                    if (roomAbove is not null && room.Grid[x, y].GetComponent<TileScript>().Type == TileType.Empty && roomAbove.Grid[x, 0].GetComponent<TileScript>().Type == TileType.Dirt)
                    {
                        availablePos.Add(new Coordinates { x = x, y = y });
                    }
                    else if (roomAbove is null && room.Grid[x, y].GetComponent<TileScript>().Type == TileType.Empty)
                    {
                        availablePos.Add(new Coordinates { x = x, y = y });
                    }
                }
                else if (room.Grid[x, y].GetComponent<TileScript>().Type == TileType.Empty && room.Grid[x, y + 1].GetComponent<TileScript>().Type == TileType.Dirt)
                {
                    availablePos.Add(new Coordinates { x = x, y = y });
                }
            }
        }

        if (availablePos.Count == 0)
        {
            return null;
        }

        Coordinates roofPos = availablePos[Random.Range(0, availablePos.Count)];

        return roofPos;
    }

    Coordinates GetPositionOnGround(Room room, Room roomUnder, int maxHeight)
    {
        List<Coordinates> availablePos = new();
        for (int x = 0; x < 10; x++)
        {
            for (int y = 0; y < maxHeight; y++) // maxHeight 8 means position can be in any height
            {
                if (y == 0)
                {
                    if (roomUnder is not null && room.Grid[x, y].GetComponent<TileScript>().Type == TileType.Empty && roomUnder.Grid[x, 7].GetComponent<TileScript>().Type == TileType.Dirt)
                    {
                        availablePos.Add(new Coordinates { x = x, y = y });
                    }
                    else if (roomUnder is null && room.Grid[x, y].GetComponent<TileScript>().Type == TileType.Empty)
                    {
                        availablePos.Add(new Coordinates { x = x, y = y });
                    }
                }
                else if (room.Grid[x, y].GetComponent<TileScript>().Type == TileType.Empty && room.Grid[x, y - 1].GetComponent<TileScript>().Type == TileType.Dirt)
                {
                    availablePos.Add(new Coordinates { x = x, y = y });
                }
            }
        }

        if (availablePos.Count == 0)
        {
            return null;
        }

        Coordinates groundPos = availablePos[Random.Range(0, availablePos.Count)];

        return groundPos;
    }

    Room BuildARoom(float xS, float yS, RoomType type, int digCount)
    {
        Room roomToReturn = new();
        roomToReturn.Type = type;
        var grid = new GameObject[roomWidth, roomHeight];

        float xPos = xS;
        float yPos = yS;
        for (int x = 0; x < roomWidth; x++)
        {
            yPos = yS;
            for (int y = 0; y < roomHeight; y++)
            {
                var obj = Instantiate(air);
                obj.GetComponent<TileScript>().Create(TileType.Dirt);
                obj.transform.position = new Vector2(xPos, yPos);
                grid[x, y] = obj;
                yPos += yMove;
            }
            xPos += xMove;
        }

        roomToReturn.Grid = DigOutRoom(grid, digCount, false, Random.Range(3, 5), Random.Range(4, 6));

        return roomToReturn;
    }

    GameObject[,] DigOutRoom(GameObject[,] grid, int digCount, bool DigUntilEmpty, int startX, int startY)
    {
        int diggerX = startX;
        int diggerY = startY;
        int remainingDigs = digCount;

        if (DigUntilEmpty)
        {
            remainingDigs = 30;
        }

        List<GameObject> alreadyVisited = new();

        grid[diggerX, diggerY].GetComponent<TileScript>().Visited = true;
        alreadyVisited.Add(grid[diggerX, diggerY]);
        remainingDigs--;


        while (remainingDigs > 0)
        {
            Vector2 dir = GetRandomDirection();
            while (diggerX + dir.x >= roomWidth || diggerY + dir.y >= roomHeight || diggerX + dir.x < 0 || diggerY + dir.y < 0)
            {
                dir = GetRandomDirection();
            }

            //Debug.Log($"{dir.x} {dir.y}");
            diggerX += (int)dir.x;
            diggerY += (int)dir.y;

            if (DigUntilEmpty)
            {
                if (grid[diggerX, diggerY].GetComponent<TileScript>().Type == TileType.Empty)
                {
                    foreach (var v in alreadyVisited)
                    {
                        v.GetComponent<TileScript>().Create(TileType.Empty);
                    }

                    return grid;
                }
            }

            if (grid[diggerX, diggerY].GetComponent<TileScript>().Visited == false)
            {
                grid[diggerX, diggerY].GetComponent<TileScript>().Visited = true;
                alreadyVisited.Add(grid[diggerX, diggerY]);
                remainingDigs--;
            }
        }

        foreach (var v in alreadyVisited)
        {
            v.GetComponent<TileScript>().Create(TileType.Empty);
        }

        return grid;
    }

    Vector2 GetRandomDirection()
    {
        int direction = Random.Range(1, 5);

        return direction switch
        {
            1 => new Vector2(1, 0),
            2 => new Vector2(0, 1),
            3 => new Vector2(-1, 0),
            4 => new Vector2(0, -1),
            _ => new Vector2(0, 0),
        };
    }

    void BuildWalls()
    {
        //var obj = Instantiate(air);
        float xPos = xStart - xMove;
        float yPos = yStart - yMove;
        //air.transform.position = new Vector2(xPos, yPos);
        //air.GetComponent<SpriteRenderer>().color = Color.black;

        for (int i = 0; i < roomHeight * 4 + 1; i++)
        {
            var til = Instantiate(air);
            til.transform.position = new Vector2(xPos, yPos);
            til.GetComponent<TileScript>().Create(TileType.Wall);
            yPos += yMove;

            if (i > 0)
            {
                leftWalls.Add(til);
            }
        }

        for (int j = 0; j < roomWidth * 4 + 1; j++)
        {
            var til = Instantiate(air);
            til.transform.position = new Vector2(xPos, yPos);
            til.GetComponent<TileScript>().Create(TileType.Wall);
            xPos += xMove;

            if (j > 0)
            {
                topWalls.Add(til);
            }
        }

        for (int k = 0; k < roomHeight * 4 + 1; k++)
        {
            var til = Instantiate(air);
            til.transform.position = new Vector2(xPos, yPos);
            til.GetComponent<TileScript>().Create(TileType.Wall);
            yPos -= yMove;

            if (k > 0)
            {
                rightWalls.Add(til);
            }
        }

        for (int l = 0; l < roomWidth * 4 + 1; l++)
        {
            var til = Instantiate(air);
            til.transform.position = new Vector2(xPos, yPos);
            til.GetComponent<TileScript>().Create(TileType.Wall);
            xPos -= xMove;

            if (l > 0)
            {
                bottomWalls.Add(til);
            }
        }
    }

    private int[,] GenerateRoomPath()
    {
        //Pick a room from the top row and place the entrance
        int[,] board = new int[levelWidth, levelHeight];
        int start = Random.Range(0, levelHeight);
        int x = start, prevX = start;
        int y = levelHeight - 1, prevY = levelHeight - 1;
        int exit = 0;

        board[x, y] = 1;
        //entrance = rooms[GetRoomID(x, y)];
        RoomPath.Add(new Coordinates { x = x, y = y });

        //Generate path until bottom floor
        while (y >= 0)
        {
            //Select next random direction to move          
            switch (RandomDirection())
            {
                case Direction.RIGHT:
                    if (x < levelWidth - 1 && board[x + 1, y] == 0) x++; //Check if room is empty and move to the right if it is
                    else if (x > 0 && board[x - 1, y] == 0) x--; //Move to the left 
                    else goto case Direction.DOWN;
                    board[x, y] = 1; //Corridor you run through
                    break;
                case Direction.LEFT:
                    if (x > 0 && board[x - 1, y] == 0) x--; //Move to the left 
                    else if (x < levelWidth - 1 && board[x + 1, y] == 0) x++; //Move to the right
                    else goto case Direction.DOWN;
                    board[x, y] = 1; //Corridor you run through
                    break;
                case Direction.DOWN:
                    y--;
                    //If not out of bounds
                    if (y >= 0)
                    {
                        board[prevX, prevY] = 2; //Room you fall from
                        board[x, y] = 3; //Room you drop into
                    }
                    else exit = board[x, y + 1]; //Place exit room     
                    break;
            }

            if (exit is 0)
            {
                RoomPath.Add(new Coordinates { x = x, y = y });
            }
            else
            {
                RoomPath.Add(new Coordinates { x = x, y = y + 1 });
            }
            prevX = x;
            prevY = y;
        }

        return board;
    }

    enum Direction
    {
        UP = 0,
        LEFT = 1,
        RIGHT = 2,
        DOWN = 3
    };

    //Pick random direction to go
    Direction RandomDirection()
    {
        int choice = Mathf.FloorToInt(Random.value * 4.99f);
        switch (choice)
        {
            //40% Chance to go right or left and 20% to go down
            case 0: case 1: return Direction.LEFT;
            case 2: case 3: return Direction.RIGHT;
            default: return Direction.DOWN;
        }
    }

    void DigOutWallsWithDigger(Room room, int x, int y)
    {
        bool hasExitRight = false;
        bool hasExitLeft = false;
        bool hasExitTop = false;
        bool hasExitBottom = false;

        bool rightExitGenerated = false;
        bool leftExitGenerated = false;
        bool topExitGenerated = false;
        bool bottomExitGenerated = false;

        var currentTiles = rooms[x, y].Grid;

        switch (room.Type)
        {
            case RoomType.Random:
                break;
            case RoomType.Corridor:
                hasExitLeft = true;
                hasExitRight = true;
                break;
            case RoomType.DropFrom:
                hasExitLeft = true;
                hasExitRight = true;
                hasExitBottom = true;
                //hasExitTop = true;
                break;
            case RoomType.DropTo:
                hasExitLeft = true;
                hasExitRight = true;
                hasExitTop = true;
                break;
        }

        if (x is 0)
        {
            hasExitLeft = false;
        }
        if (x == levelWidth - 1)
        {
            hasExitRight = false;
        }
        if (y is 0)
        {
            hasExitBottom = false;
        }
        if (y == levelHeight - 1)
        {
            hasExitTop = false;
        }

        if (hasExitRight)
        {
            var rightTiles = rooms[x + 1, y].Grid;

            for (int i = 0; i < roomHeight; i++)
            {
                if ((currentTiles[9, i].GetComponent<TileScript>().Type == TileType.Empty) && (rightTiles[0, i].GetComponent<TileScript>().Type == TileType.Empty))
                {
                    rightExitGenerated = true;
                }
            }

            if (!rightExitGenerated)
            {
                int rightMostRow = 0;
                bool found = false;
                List<int> rightMostIndexes = new();
                for (int i = roomWidth - 1; i > 0; i--)
                {
                    if (!found)
                    {
                        for (int j = 0; j < roomHeight; j++)
                        {
                            if (currentTiles[i, j].GetComponent<TileScript>().Type == TileType.Empty)
                            {
                                rightMostRow = i;
                                found = true;
                                rightMostIndexes.Add(j);
                            }
                        }
                    }
                }

                int k = rightMostIndexes[Random.Range(0, rightMostIndexes.Count)];
                if (currentTiles[rightMostRow, k].GetComponent<TileScript>().Type == TileType.Empty)
                {
                    while (rightMostRow < roomWidth)
                    {
                        currentTiles[rightMostRow, k].GetComponent<TileScript>().Create(TileType.Empty);
                        rightMostRow++;
                    }
                }

                DigOutRoom(rightTiles, 0, true, 0, k);
            }
        }

        if (hasExitLeft)
        {
            var leftTiles = rooms[x - 1, y].Grid;

            for (int i = 0; i < roomHeight; i++)
            {
                if ((currentTiles[0, i].GetComponent<TileScript>().Type == TileType.Empty) && (leftTiles[9, i].GetComponent<TileScript>().Type == TileType.Empty))
                {
                    leftExitGenerated = true;
                }
            }

            if (!leftExitGenerated)
            {
                int leftMostRow = 9;
                bool found = false;
                List<int> leftMostIndexes = new();
                for (int i = 0; i < roomWidth; i++)
                {
                    if (!found)
                    {
                        for (int j = 0; j < roomHeight; j++)
                        {
                            if (currentTiles[j, i].GetComponent<TileScript>().Type == TileType.Empty)
                            {
                                leftMostRow = i;
                                found = true;
                                leftMostIndexes.Add(j);
                            }
                        }
                    }
                }

                int k = leftMostIndexes[Random.Range(0, leftMostIndexes.Count)];
                if (currentTiles[leftMostRow, k].GetComponent<TileScript>().Type == TileType.Empty)
                {
                    while (leftMostRow >= 0)
                    {
                        currentTiles[leftMostRow, k].GetComponent<TileScript>().Create(TileType.Empty);
                        leftMostRow--;
                    }
                }

                DigOutRoom(leftTiles, 0, true, 9, k);
            }
        }

        if (hasExitTop)
        {
            var topTiles = rooms[x, y + 1].Grid;

            for (int i = 0; i < roomWidth; i++)
            {
                if ((currentTiles[i, 7].GetComponent<TileScript>().Type == TileType.Empty) && (topTiles[i, 0].GetComponent<TileScript>().Type == TileType.Empty))
                {
                    topExitGenerated = true;
                }
            }

            if (!topExitGenerated)
            {
                int topMostCol = 0;
                bool found = false;
                List<int> topMostIndexes = new();
                for (int i = roomHeight - 1; i > 0; i--)
                {
                    if (!found)
                    {
                        for (int j = 0; j < roomWidth; j++)
                        {
                            if (currentTiles[j, i].GetComponent<TileScript>().Type == TileType.Empty)
                            {
                                topMostCol = i;
                                found = true;
                                topMostIndexes.Add(j);
                            }
                        }
                    }
                }

                int k = topMostIndexes[Random.Range(0, topMostIndexes.Count)];
                if (currentTiles[k, topMostCol].GetComponent<TileScript>().Type == TileType.Empty)
                {
                    while (topMostCol < roomHeight)
                    {
                        currentTiles[k, topMostCol].GetComponent<TileScript>().Create(TileType.Empty);
                        topMostCol++;
                    }
                }

                DigOutRoom(topTiles, 0, true, k, 0);
            }
        }

        if (hasExitBottom)
        {
            var bottomTiles = rooms[x, y - 1].Grid;

            for (int i = 0; i < roomWidth; i++)
            {
                if ((currentTiles[i, 0].GetComponent<TileScript>().Type == TileType.Empty) && (bottomTiles[i, 7].GetComponent<TileScript>().Type == TileType.Empty))
                {
                    bottomExitGenerated = true;
                }
            }

            if (!bottomExitGenerated)
            {
                int bottomMostCol = 7;
                bool found = false;
                List<int> bottomMostIndexes = new();
                for (int i = 0; i < roomHeight; i++)
                {
                    if (!found)
                    {
                        for (int j = 0; j < roomWidth; j++)
                        {
                            if (currentTiles[j, i].GetComponent<TileScript>().Type == TileType.Empty)
                            {
                                bottomMostCol = i;
                                found = true;
                                bottomMostIndexes.Add(j);
                            }
                        }
                    }
                }

                int k = bottomMostIndexes[Random.Range(0, bottomMostIndexes.Count)];
                if (currentTiles[k, bottomMostCol].GetComponent<TileScript>().Type == TileType.Empty)
                {
                    while (bottomMostCol >= 0)
                    {
                        currentTiles[k, bottomMostCol].GetComponent<TileScript>().Create(TileType.Empty);
                        bottomMostCol--;
                    }
                }

                DigOutRoom(bottomTiles, 0, true, k, 7);
            }
        }
    }

    private void PathFindUsingBreadthFirstSearch()
    {
        foreach (var room in rooms)
        {
            for (int x = 0; x < 10; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    allTiles[x + (room.X * 10), y + (room.Y * 8)] = room.Grid[x, y];
                }
            }
        }

        GameObject head = null;
        GameObject tail = null;
        foreach (var tile in allTiles)
        {
            if (tile.GetComponent<TileScript>().Type == TileType.Entrance)
            {
                head = tile;
            }
            else if (tile.GetComponent<TileScript>().Type == TileType.Exit)
            {
                tail = tile;
            }
        }

        GameObject current;
        List<GameObject> frontier = new()
        {
            head
        };
        Dictionary<GameObject, GameObject> cameFrom = new()
        {
            [head] = null
        }; // path A->B is stored as cameFrom[B] == A

        while (frontier.Count is not 0)
        {
            current = frontier.First();
            frontier.Remove(current);
            foreach (var next in GetAdjacentNodes(allTiles, current))
            {
                if (!cameFrom.ContainsKey(next))
                {
                    frontier.Add(next);
                    cameFrom[next] = current;
                }
            }
        }

        current = tail;
        while (current != head) // WAS CAUSING ALL THE FREEZE ISSUES
        {
            TilePath.Add(current);
            current = cameFrom[current];
        }
        TilePath.Add(head);
        TilePath.Reverse();
        //PathDisplay.GetComponent<TextMeshProUGUI>().text = path.Count.ToString();
    }

    List<GameObject> GetAdjacentNodes(GameObject[,] allTiles, GameObject currentTile)
    {
        int curPosX = 0;
        int curPosY = 0;
        bool isFound = false;
        for (int x = 0; x < roomWidth * levelWidth; x++)
        {
            if (isFound is false)
            {
                for (int y = 0; y < roomHeight * levelHeight; y++)
                {
                    if (allTiles[x, y] == currentTile)
                    {
                        curPosX = x;
                        curPosY = y;
                        isFound = true;
                        break;
                    }
                }
            }
        }

        List<GameObject> returnList = new();
        List<GameObject> allAdjacentTiles = new();

        if (curPosX != 0)
        {
            allAdjacentTiles.Add(allTiles[curPosX - 1, curPosY]);
        }
        if (curPosX != roomWidth * levelWidth - 1)
        {
            allAdjacentTiles.Add(allTiles[curPosX + 1, curPosY]);
        }
        if (curPosY != 0)
        {
            allAdjacentTiles.Add(allTiles[curPosX, curPosY - 1]);
        }
        if (curPosY != roomHeight * levelHeight - 1)
        {
            allAdjacentTiles.Add(allTiles[curPosX, curPosY + 1]);
        }

        foreach (var tile in allAdjacentTiles)
        {
            if (!tile.GetComponent<TileScript>().IsSolid())
            {
                returnList.Add(tile);
            }
        }

        return returnList;
    }


#if UNITY_EDITOR
    [Header("Gizmos")]
    public GUIStyle style;

    //Do not allow one room levels we need at least two rooms
    //private void OnValidate()
    //{
    //    levelWidth = (levelHeight == 1 && levelWidth < 2) ? 2 : levelWidth;
    //    levelHeight = (levelWidth == 1 && levelHeight < 2) ? 2 : levelHeight;
    //}

    //Draw gizmos
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || rooms is null) return;
        DrawRooms();
        DrawPath();
        DrawTilePath();
    }

    void DrawRooms()
    {
        for (int x = 0; x < levelWidth; x++)
        {
            for (int y = 0; y < levelHeight; y++)
            {
                Gizmos.color = new Color32(255, 253, 0, 128);
                Gizmos.DrawWireCube(new Vector2(x * 10 + 5, y * 8 + 4), new Vector2(roomWidth, roomHeight));

            }
        }
        //foreach (Room r in rooms)
        //{
        //    //Draw room ID and boundary
        //    Gizmos.DrawWireCube(r.Center(), new Vector2(Config.ROOM_WIDTH, Config.ROOM_HEIGHT));
        //    Handles.Label(r.Origin() + new Vector2(.5f, -.5f), r.Type.ToString(), style);

        //    if (r == level.Entrance) Gizmos.color = Color.green;
        //    else if (r == level.Exit) Gizmos.color = Color.red;
        //    else continue;
        //    Gizmos.DrawWireCube(r.Center(), new Vector3(1, 1));
        //}
    }

    void DrawPath()
    {
        Room previous = null;
        foreach (Coordinates c in RoomPath)
        {
            var i = rooms[c.x, c.y];
            if (previous != null && previous != i)
            {
                Handles.color = Color.blue;
                Handles.DrawDottedLine(i.Center(), previous.Center(), 3);
                Handles.color = Color.magenta;
                Quaternion rot = Quaternion.LookRotation(i.Center() - previous.Center()).normalized;
                Handles.ConeHandleCap(0, (i.Center() + previous.Center()) / 2 + (previous.Center() - i.Center()).normalized, rot, 1f, EventType.Repaint);
            }
            previous = i;
        }
    }

    void DrawTilePath()
    {
        GameObject previous = null;
        foreach (GameObject t in TilePath)
        {
            var i = t;
            if (previous != null && previous != i)
            {
                Handles.color = Color.red;
                Handles.DrawDottedLine(i.transform.position, previous.transform.position, 3);
                //Handles.color = Color.black;
                //Quaternion rot = Quaternion.LookRotation(i.transform.position - previous.transform.position).normalized;
                //Handles.ConeHandleCap(0, (i.transform.position + previous.transform.position) / 2 + (previous.transform.position - i.transform.position).normalized, rot, 0.25f, EventType.Repaint);
            }
            previous = i;
        }
    }
#endif
}
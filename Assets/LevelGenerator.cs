using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;
using Gizmos = Popcron.Gizmos;
using System.Text;

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

    private readonly int roomWidth = 10;
    private readonly int roomHeight = 8;
    private int levelWidth = 4;
    private int levelHeight = 5;

    public GameObject air;
    private readonly float xMove = 1;
    private readonly float yMove = 1;
    private readonly float xStart = 0.5f;
    private readonly float yStart = 0.5f;

    List<Coordinates> RoomPath;
    public List<GameObject> TilePath;

    public GameObject ArrowPrefab;
    public List<GameObject> Arrows = new();

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

    private readonly int LevelsToGenerateForCalibration = 1000;
    private readonly bool CalibrationMode = false; // if we want to get new generted diff score ranges in the unity console log on start, according to defined width and height above

    private bool ShowOverlay = false;

    // min, max value
    public float[,] DifficultyRange = new float[6, 2] {
   {450, 980} , // 3x3
   {755, 1470} , // 4x4 // new
   {1155, 2010},  // 5x5
   {1550, 2635}, // 6x6
   {910, 1685}, // 5x4
   {1000, 1750 } // 4x5
};

    public float DiffScoreToGenerateMin = 0;
    public float DiffScoreToGenerateMax = 0;
    public int MinimumDiffPercent = 0;
    public int MaximumDiffPercent = 0;

    [SerializeField]
    private TMP_Dropdown sizeDropdown;
    [SerializeField]
    private TMP_Dropdown difficultyDropdown;
    [SerializeField]
    private TMP_Text DifficultyText;
    [SerializeField]
    private TMP_Text OutputText;

    private void GetNormalizedDifficultyValues()
    {
        List<int> DiffScores = new();
        for (int i = 0; i < LevelsToGenerateForCalibration; i++)
        {
            RegenerateLevel();
            DiffScores.Add(DifficultyScore);
        }

        var counts = GetNormalizedCounts(DiffScores);
        Debug.Log(GetSumArrayString(counts));

        // remove top and bottom 5%
        DiffScores.Sort();
        int countToRemove = (int)Math.Floor(DiffScores.Count * 0.05);

        DiffScores.RemoveRange(0, countToRemove);
        DiffScores.RemoveRange(DiffScores.Count - countToRemove, countToRemove);

        var newCounts = GetNormalizedCounts(DiffScores);
        Debug.Log(GetSumArrayString(newCounts));
    }

    public List<int> GetNormalizedCounts(List<int> input)
    {
        if (input == null || input.Count == 0)
        {
            throw new ArgumentException("The input list must not be null or empty.");
        }

        // Normalize the values
        var min = input.Min();
        var max = input.Max();
        var range = max - min;
        var normalizedInput = input.Select(i => (i - min) / (double)range).ToList();

        // Count how many values fall into each percentage range
        var counts = new int[101];
        foreach (var value in normalizedInput)
        {
            var percentage = value * 100;
            var index = (int)Math.Floor(percentage);
            counts[index]++;
        }

        Debug.Log($"10% = {range / 10 + min}, 50% {range / 2 + min}, 90% {range / 10 * 9 + min}, percent = {range / 100}");
        Debug.Log($"min = {min}, max = {max}");
        return counts.ToList();
    }


    public string GetSumArrayString(List<int> input)
    {
        var sb = new StringBuilder();
        var full = new StringBuilder();

        for(int i = 0; i < 101; i++)
        {
            sb.AppendFormat("({0},{1})", i, input[i]);

            for(int k = 0; k < input[i]; k++)
            {
                full.AppendFormat($"{i},");
            }
        }
        Debug.Log(full.ToString());
        return sb.ToString();
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
        if (CalibrationMode)
        {
            GetNormalizedDifficultyValues();
        }
    }

    void BuildAndDigOutRooms(int[,] board)
    {
        float xP;
        float yP = yStart;
        for (int y = 0; y < levelHeight; y++)
        {
            xP = xStart;
            for (int x = 0; x < levelWidth; x++)
            {
                var r = BuildARoom(xP, yP, (RoomType)board[x, y], 30);
                r.X = x;
                r.Y = y;
                rooms[x, y] = r;
                xP += 10;
            }
            yP += 8;
        }

        for (int y = levelHeight - 1; y >= 0; y--)
        {
            for (int x = 0; x < levelWidth; x++)
            {
                if (rooms[x, y].Type is not RoomType.Random)
                {
                    DigOutWallsWithDigger(rooms[x, y], x, y);
                }
            }
        }

        CreateAllTilesList();
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
                case 4:
                    levelWidth = 6;
                    levelHeight = 6;
                    break;
                case 5:
                    levelWidth = 5;
                    levelHeight = 4;
                    break;
                case 6:
                    levelWidth = 4;
                    levelHeight = 5;
                    break;
            }

            switch (difficultyDropdown.value)
            {
                case 0:
                    return;
                case 1:
                    MinimumDiffPercent = 5;
                    MaximumDiffPercent = 15;
                    break;
                case 2:
                    MinimumDiffPercent = 45;
                    MaximumDiffPercent = 55;
                    break;
                case 3:
                    MinimumDiffPercent = 85;
                    MaximumDiffPercent = 95;
                    break;
            }

            DiffScoreToGenerateMin = DifficultyRange[sizeDropdown.value - 1, 0];
            DiffScoreToGenerateMax = DifficultyRange[sizeDropdown.value - 1, 1];
        }

        if (DiffScoreToGenerateMin is not 0)
        {
            Debug.Log($"Generating a level within {MinimumDiffPercent} - {MaximumDiffPercent} diff score range.");
        }

        var watch = System.Diagnostics.Stopwatch.StartNew();

        bool GeneratedLevelWithinDifficulty = false;
        int tries = 0;

        for (int i = 0; i < 100; i++)
        {
            ResetLevel();

            var board = GenerateRoomPath();

            BuildAndDigOutRooms(board);

            Coordinates startRoom = RoomPath.First();
            Coordinates endRoom = RoomPath.Last();
            Room roomUnder = startRoom.y > 0 ? rooms[startRoom.x, startRoom.y - 1] : null;

            var entrancePos = GetPositionOnGround(rooms[startRoom.x, startRoom.y], roomUnder, roomHeight);
            var exitPos = GetPositionOnGround(rooms[endRoom.x, endRoom.y], null, roomHeight);

            if (entrancePos == null || exitPos == null) // trying to generate entrance or exit in a room with no spot to put on the ground.
            {
                continue;
            }

            PutEntrance(rooms[startRoom.x, startRoom.y], entrancePos);
            PutExit(rooms[endRoom.x, endRoom.y], exitPos);
            RemoveRandomDirtTiles();
            GenerateSpikes();
            bool hasExit = PathFindUsingBreadthFirstSearch();

            if(hasExit is false) // level is not beatable
            {
                continue;
            }

            GenerateLadders();
            GenerateDamsel();
            GenerateItems();
            GenerateBats();
            GenerateSnakes();
            CalculateDifficultyScore();
            CalculateFunScore();

            float levelDifficultyPercentage = 0f;
            if (DiffScoreToGenerateMin is not 0)
            {
                levelDifficultyPercentage = (DifficultyScore - DiffScoreToGenerateMin) / (DiffScoreToGenerateMax - DiffScoreToGenerateMin) * 100;
                Debug.Log($"generated level diff: {levelDifficultyPercentage}%");
                DifficultyText.text += $"({Math.Round(levelDifficultyPercentage, 1)}%)";
            }

            if (DiffScoreToGenerateMin == 0 || (levelDifficultyPercentage > MinimumDiffPercent && levelDifficultyPercentage < MaximumDiffPercent))
            {
                GeneratedLevelWithinDifficulty = true;
                tries = i + 1;
                break;
            }
        }

        watch.Stop();
        string triesText = tries > 0 ? " tries." : " try.";
        OutputText.text = $"Generation Time: {watch.ElapsedMilliseconds} ms in {tries} {triesText}";

        if (GeneratedLevelWithinDifficulty is false)
        {
            OutputText.text = $"ERROR: can't generate a level in 100 tries.";
        }
    }

    private void RemoveRandomDirtTiles()
    {
        foreach (var tile in allTiles)
        {
            if (tile.GetComponent<TileScript>().Type == TileType.Dirt && Random.Range(1, 51) == 1)
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

        DifficultyScore = 25 * RoomPathLength + 5 * TilePathLength + 20 * NumberOfSpikes + 40 * NumberAndTypeOfEnemies + 100 * VerticalCorridors;

        DifficultyText.text = $"Difficulty Score: {DifficultyScore}";
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
            if (Random.Range(1, 6) != 1)
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
                        && grid[x + 1, y].GetComponent<TileScript>().Type == TileType.Empty)
                    {
                        if (Random.Range(1, 4) != 1)
                        {
                            continue;
                        }

                        if (y is 0 && r.Y is not 0)
                        {
                            var bottomGrid = rooms[r.X, r.Y - 1].Grid;
                            if (bottomGrid[x, 7].GetComponent<TileScript>().Type == TileType.Dirt
                                && bottomGrid[x + 1, 7].GetComponent<TileScript>().Type == TileType.Dirt)
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
            if (Random.Range(1, 5) != 1)
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

            if (Random.Range(1, 8) == 1)
            {
                typeToCreate = TileType.Bomb;
            }

            r.Grid[pos.x, pos.y].GetComponent<TileScript>().Create(typeToCreate);
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

        if (damselPos is null)
        {
            var newRoom = path[Random.Range(0, path.Count)];
            if (newRoom != chosenRoom)
            {
                chosenRoom = newRoom;
            }
        }

        if(damselPos is null)
        {
            return;
        }

        rooms[chosenRoom.x, chosenRoom.y].Grid[damselPos.x, damselPos.y].GetComponent<TileScript>().Create(TileType.Damsel);
    }

    public void RegenerateLevel()
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

    void PutEntrance(Room room, Coordinates pos)
    {
        room.Grid[pos.x, pos.y].GetComponent<TileScript>().Create(TileType.Entrance);
    }

    void PutExit(Room room, Coordinates pos)
    {
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
        Room roomToReturn = new()
        {
            Type = type
        };

        var grid = new GameObject[roomWidth, roomHeight];

        float xPos = xS;
        for (int x = 0; x < roomWidth; x++)
        {
            float yPos = yS;
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

    private int[,] GenerateRoomPath()
    {
        //Pick a room from the top row and place the entrance
        int[,] board = new int[levelWidth, levelHeight];
        int start = Random.Range(0, levelWidth);
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
        return choice switch
        {
            //40% Chance to go right or left and 20% to go down
            0 or 1 => Direction.LEFT,
            2 or 3 => Direction.RIGHT,
            _ => Direction.DOWN,
        };
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

    private void CreateAllTilesList()
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
    }

    private bool PathFindUsingBreadthFirstSearch()
    {
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
            if(!cameFrom.ContainsKey(current))
            {
                return false;
            }
            current = cameFrom[current];
        }
        TilePath.Add(head);
        TilePath.Reverse();

        return true;
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

    private void Update()
    {
        if (!Application.isPlaying || rooms is null || ShowOverlay is false) return;
        DrawPath();
        DrawTilePath();
        DrawRooms();
    }

    void DrawRooms()
    {
        for (int x = 0; x < levelWidth; x++)
        {
            for (int y = 0; y < levelHeight; y++)
            {
                Gizmos.Material.color = Color.yellow;
                Gizmos.Square(new Vector2(x * 10 + 5, y * 8 + 4), new Vector2(roomWidth, roomHeight));
            }
        }
    }

    void DrawPath()
    {
        if (Arrows.Count > 0)
        {
            foreach (var a in Arrows)
            {
                Destroy(a);
            }
        }

        Arrows = new();

        GameObject previous = null;
        Coordinates previousCoords = null;
        for (int i = 0; i < RoomPath.Count - 1; i++)
        {
            var arrow = Instantiate(ArrowPrefab);
            arrow.transform.position = new Vector2(RoomPath[i].x * 10 + 5, RoomPath[i].y * 8 + 4);

            if (previous != null)
            {
                if (previousCoords.x - 1 == RoomPath[i].x)
                {
                    previous.transform.Rotate(0f, 0f, 90f, Space.World);
                }
                else if (previousCoords.x + 1 == RoomPath[i].x)
                {
                    previous.transform.Rotate(0f, 0f, -90f, Space.World);
                }
                else if (previousCoords.y - 1 == RoomPath[i].y)
                {
                    previous.transform.Rotate(0f, 0f, 180f, Space.World);
                }
            }

            if (i == RoomPath.Count - 2)
            {
                arrow.transform.Rotate(0f, 0f, 180f, Space.World);
            }

            previous = arrow;
            previousCoords = RoomPath[i];
            Arrows.Add(arrow);
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
                Gizmos.Line(i.transform.position, previous.transform.position, Color.red, false);
            }
            previous = i;
        }
    }

    public void ToggleOverlay()
    {
        ShowOverlay = !ShowOverlay;

        foreach (var a in Arrows)
        {
            a.SetActive(ShowOverlay);
        }
    }
}
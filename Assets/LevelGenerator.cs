using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Mathematics;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using static LevelGenerator;
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

    int height = 8;
    int width = 10;
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

    public Room[,] rooms;
    List<Room> optionalRooms;

    // Start is called before the first frame update
    void Start()
    {
        RoomPath = new();
        rooms = new Room[4, 4];
        optionalRooms = new();

        var board = GenerateRoomPath();

        Debug.Log($"{board[0, 3]} {board[1, 3]} {board[2, 3]} {board[3, 3]}");
        Debug.Log($"{board[0, 2]} {board[1, 2]} {board[2, 2]} {board[3, 2]}");
        Debug.Log($"{board[0, 1]} {board[1, 1]} {board[2, 1]} {board[3, 1]}");
        Debug.Log($"{board[0, 0]} {board[1, 0]} {board[2, 0]} {board[3, 0]}");

        float xP = xStart;
        float yP = yStart;
        for (int y = 0; y < 4; y++)
        {
            xP = xStart;
            for (int x = 0; x < 4; x++)
            {
                var r = BuildARoom(xP, yP, (RoomType)board[x, y], 30);
                r.X = x;
                r.Y = y;
                rooms[x, y] = r;
                xP += 10;
            }
            yP += 8;
        }

        for (int y = 3; y >= 0; y--)
        {
            for (int x = 0; x < 4; x++)
            {
                if (rooms[x, y].Type is not RoomType.Random)
                {
                    DigOutWallsWithDigger(rooms[x, y], x, y);
                }
            }
        }
        Coordinates startRoom = RoomPath.First();
        Coordinates endRoom = RoomPath.Last();
        Room roomUnder = null;
        if (startRoom.y > 0)
        {
            roomUnder = rooms[startRoom.x, startRoom.y - 1];
        }
        PutEntrance(rooms[startRoom.x, startRoom.y], roomUnder);
        PutExit(rooms[endRoom.x, endRoom.y]);

        BreakingUpRandomGrid();
        GenerateLadders();
        GenerateItems();
        GenerateDamsel();
        GenerateBats();
        GenerateSpikes();
        GenerateSnakes();
    }

    void GenerateBats()
    {
        foreach (var r in rooms)
        {
            if (Random.Range(1, 9) != 1) // 12.5% per room to have a bat
            {
                continue;
            }

            Coordinates pos;
            if (r.Y == 3)
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
                        if(Random.Range(1,4) != 1)
                        {
                            continue;
                        }

                        if (y is 0 && r.Y is not 0)
                        {
                            var bottomGrid = rooms[r.X,r.Y - 1].Grid;
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
            if (Random.Range(1, 6) != 1)
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

            r.Grid[pos.x, pos.y].GetComponent<TileScript>().Create(TileType.Spike);
        }
    }

    void GenerateItems()
    {
        foreach (var r in rooms)
        {
            if (Random.Range(1, 6) != 1)
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

            if(pos is null)
            {
                return;
            }

            TileType typeToCreate = TileType.Coin;

            if(Random.Range(1,4) == 1)
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
                        else if (x is not 0)
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
        if(chosenRoom.y > 0)
        {
            roomUnder = rooms[chosenRoom.x, chosenRoom.y - 1];
        }

        var damselPos = GetPositionOnGround(rooms[chosenRoom.x, chosenRoom.y], roomUnder, 8);
        rooms[chosenRoom.x, chosenRoom.y].Grid[damselPos.x, damselPos.y].GetComponent<TileScript>().Create(TileType.Damsel);
    }

    public void RegenerateLevel()
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
        Start();
    }

    void BreakingUpRandomGrid()
    {
        var randomY = Random.Range(0, 4);
        var randomX = Random.Range(1, 3);
        if (randomX == 1)
        {
            optionalRooms.Add(BuildARoom(-9.5f, 0.5f + randomY * 8, RoomType.Random, 40));
        }
        else if (randomX == 2)
        {
            optionalRooms.Add(BuildARoom(40.5f, 0.5f + randomY * 8, RoomType.Random, 40));
        }
    }
    void PutEntrance(Room room, Room roomUnder)
    {
        var pos = GetPositionOnGround(room, roomUnder, 8);
        room.Grid[pos.x, pos.y].GetComponent<TileScript>().Create(TileType.EntranceExit);
    }

    void PutExit(Room room)
    {
        var pos = GetPositionOnGround(room, null, 8);
        room.Grid[pos.x, pos.y].GetComponent<TileScript>().Create(TileType.EntranceExit);
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

        if(availablePos.Count == 0)
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
        var grid = new GameObject[width, height];

        float xPos = xS;
        float yPos = yS;
        for (int x = 0; x < width; x++)
        {
            yPos = yS;
            for (int y = 0; y < height; y++)
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
            while (diggerX + dir.x >= width || diggerY + dir.y >= height || diggerX + dir.x < 0 || diggerY + dir.y < 0)
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

        for (int i = 0; i < height * 4 + 1; i++)
        {
            var til = Instantiate(air);
            til.transform.position = new Vector2(xPos, yPos);
            til.GetComponent<SpriteRenderer>().color = Color.black;
            yPos += yMove;

            if (i > 0)
            {
                leftWalls.Add(til);
            }
        }

        for (int j = 0; j < width * 4 + 1; j++)
        {
            var til = Instantiate(air);
            til.transform.position = new Vector2(xPos, yPos);
            til.GetComponent<SpriteRenderer>().color = Color.black;
            xPos += xMove;

            if (j > 0)
            {
                topWalls.Add(til);
            }
        }

        for (int k = 0; k < height * 4 + 1; k++)
        {
            var til = Instantiate(air);
            til.transform.position = new Vector2(xPos, yPos);
            til.GetComponent<SpriteRenderer>().color = Color.black;
            yPos -= yMove;

            if (k > 0)
            {
                rightWalls.Add(til);
            }
        }

        for (int l = 0; l < width * 4 + 1; l++)
        {
            var til = Instantiate(air);
            til.transform.position = new Vector2(xPos, yPos);
            til.GetComponent<SpriteRenderer>().color = Color.black;
            xPos -= xMove;

            if (l > 0)
            {
                bottomWalls.Add(til);
            }
        }
    }

    void DigOutWalls()
    {
        int roomType = Random.Range(0, 4);

        RoomType room = (RoomType)roomType;

        switch (room)
        {
            case RoomType.Random:
                break;
            case RoomType.Corridor:
                leftWalls[Random.Range(0, leftWalls.Count)].GetComponent<SpriteRenderer>().color = Color.white;
                rightWalls[Random.Range(0, rightWalls.Count)].GetComponent<SpriteRenderer>().color = Color.white;
                break;
            case RoomType.DropFrom:
                leftWalls[Random.Range(0, leftWalls.Count)].GetComponent<SpriteRenderer>().color = Color.white;
                rightWalls[Random.Range(0, rightWalls.Count)].GetComponent<SpriteRenderer>().color = Color.white;
                leftWalls[Random.Range(0, leftWalls.Count)].GetComponent<SpriteRenderer>().color = Color.white;
                rightWalls[Random.Range(0, rightWalls.Count)].GetComponent<SpriteRenderer>().color = Color.white;
                break;
            case RoomType.DropTo:
                leftWalls[Random.Range(0, leftWalls.Count)].GetComponent<SpriteRenderer>().color = Color.white;
                rightWalls[Random.Range(0, rightWalls.Count)].GetComponent<SpriteRenderer>().color = Color.white;
                topWalls[Random.Range(0, topWalls.Count)].GetComponent<SpriteRenderer>().color = Color.white;
                break;
        }
    }

    private int[,] GenerateRoomPath()
    {
        //Pick a room from the top row and place the entrance
        int[,] board = new int[4, 4];
        int start = Random.Range(0, 4);
        int x = start, prevX = start;
        int y = 3, prevY = 3;
        int exit = 0;
        int levelWidth = 4;
        int levelHeight = 4;

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
        if (x is 3)
        {
            hasExitRight = false;
        }
        if (y is 0)
        {
            hasExitBottom = false;
        }
        if (y is 3)
        {
            hasExitTop = false;
        }

        if (hasExitRight)
        {
            var rightTiles = rooms[x + 1, y].Grid;

            for (int i = 0; i < height; i++)
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
                for (int i = width - 1; i > 0; i--)
                {
                    if (!found)
                    {
                        for (int j = 0; j < height; j++)
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
                    while (rightMostRow < width)
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

            for (int i = 0; i < height; i++)
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
                for (int i = 0; i < width; i++)
                {
                    if (!found)
                    {
                        for (int j = 0; j < height; j++)
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

            for (int i = 0; i < width; i++)
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
                for (int i = height - 1; i > 0; i--)
                {
                    if (!found)
                    {
                        for (int j = 0; j < width; j++)
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
                    while (topMostCol < height)
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

            for (int i = 0; i < width; i++)
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
                for (int i = 0; i < height; i++)
                {
                    if (!found)
                    {
                        for (int j = 0; j < width; j++)
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
        if (!Application.isPlaying) return;
        DrawRooms();
        DrawPath();
    }

    void DrawRooms()
    {
        for (int x = 0; x < 4; x++)
        {
            for (int y = 0; y < 4; y++)
            {
                Gizmos.color = new Color32(255, 253, 0, 128);
                Gizmos.DrawWireCube(new Vector2(x * 10 + 5, y * 8 + 4), new Vector2(10, 8));

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
#endif
}
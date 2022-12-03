using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
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

    struct Coordinates
    {
        public int x;
        public int y;
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

    List<Coordinates> RoomPath = new();

    public Room[,] rooms = new Room[4, 4];

    // Start is called before the first frame update
    void Start()
    {
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
                var r = (BuildARoom(xP, yP, (RoomType)board[x, y]));
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

        // calculate the path and make sure there is an available path build exits

        //BuildARoom(xP, yP, 0);
        //BuildWalls();
        //DigOutWalls();


    }

    Room BuildARoom(float xS, float yS, RoomType type)
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
                obj.transform.position = new Vector2(xPos, yPos);
                grid[x, y] = obj;
                yPos += yMove;
            }
            xPos += xMove;
        }

        roomToReturn.Grid = DigOutRoom(grid, 30, false, Random.Range(3, 5), Random.Range(4, 6));

        return roomToReturn;
    }

    GameObject[,] DigOutRoom(GameObject[,] grid, int digCount, bool DigUntilEmpty, int startX, int startY)
    {
        int diggerX = startX;
        int diggerY = startY;
        int remainingDigs = digCount;
        Color color = Color.white;

        if (DigUntilEmpty)
        {
            remainingDigs = 30;
            color = Color.white;
        }

        List<GameObject> alreadyVisited = new();

        grid[diggerX, diggerY].GetComponent<SpriteRenderer>().color = color;
        grid[diggerX, diggerY].GetComponent<TileScript>().type = 2;
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
                if (grid[diggerX, diggerY].GetComponent<TileScript>().type == 1)
                {
                    foreach (var v in alreadyVisited)
                    {
                        v.GetComponent<TileScript>().type = 1;
                    }

                    return grid;
                }
            }

            if (grid[diggerX, diggerY].GetComponent<TileScript>().type != 2)
            {
                grid[diggerX, diggerY].GetComponent<SpriteRenderer>().color = color;
                grid[diggerX, diggerY].GetComponent<TileScript>().type = 2;
                alreadyVisited.Add(grid[diggerX, diggerY]);
                remainingDigs--;
            }
        }

        foreach (var v in alreadyVisited)
        {
            v.GetComponent<TileScript>().type = 1;
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
                if ((currentTiles[9, i].GetComponent<TileScript>().type == 1) && (rightTiles[0, i].GetComponent<TileScript>().type == 1))
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
                            if (currentTiles[i, j].GetComponent<TileScript>().type == 1)
                            {
                                rightMostRow = i;
                                found = true;
                                rightMostIndexes.Add(j);
                            }
                        }
                    }
                }

                int k = rightMostIndexes[Random.Range(0, rightMostIndexes.Count)];
                if (currentTiles[rightMostRow, k].GetComponent<TileScript>().type == 1)
                {
                    while (rightMostRow < width)
                    {
                        currentTiles[rightMostRow, k].GetComponent<SpriteRenderer>().color = Color.white;
                        currentTiles[rightMostRow, k].GetComponent<TileScript>().type = 1;
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
                if ((currentTiles[0, i].GetComponent<TileScript>().type == 1) && (leftTiles[9, i].GetComponent<TileScript>().type == 1))
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
                            if (currentTiles[j, i].GetComponent<TileScript>().type == 1)
                            {
                                leftMostRow = i;
                                found = true;
                                leftMostIndexes.Add(j);
                            }
                        }
                    }
                }

                int k = leftMostIndexes[Random.Range(0, leftMostIndexes.Count)];
                if (currentTiles[leftMostRow, k].GetComponent<TileScript>().type == 1)
                {
                    while (leftMostRow >= 0)
                    {
                        currentTiles[leftMostRow, k].GetComponent<SpriteRenderer>().color = Color.white;
                        currentTiles[leftMostRow, k].GetComponent<TileScript>().type = 1;
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
                if ((currentTiles[i, 7].GetComponent<TileScript>().type == 1) && (topTiles[i, 0].GetComponent<TileScript>().type == 1))
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
                            if (currentTiles[j, i].GetComponent<TileScript>().type == 1)
                            {
                                topMostCol = i;
                                found = true;
                                topMostIndexes.Add(j);
                            }
                        }
                    }
                }

                int k = topMostIndexes[Random.Range(0, topMostIndexes.Count)];
                if (currentTiles[k, topMostCol].GetComponent<TileScript>().type == 1)
                {
                    while (topMostCol < height)
                    {
                        currentTiles[k, topMostCol].GetComponent<SpriteRenderer>().color = Color.white;
                        currentTiles[k, topMostCol].GetComponent<TileScript>().type = 1;
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
                if ((currentTiles[i, 0].GetComponent<TileScript>().type == 1) && (bottomTiles[i, 7].GetComponent<TileScript>().type == 1))
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
                            if (currentTiles[j, i].GetComponent<TileScript>().type == 1)
                            {
                                bottomMostCol = i;
                                found = true;
                                bottomMostIndexes.Add(j);
                            }
                        }
                    }
                }

                int k = bottomMostIndexes[Random.Range(0, bottomMostIndexes.Count)];
                if (currentTiles[k, bottomMostCol].GetComponent<TileScript>().type == 1)
                {
                    while (bottomMostCol >= 0)
                    {
                        currentTiles[k, bottomMostCol].GetComponent<SpriteRenderer>().color = Color.white;
                        currentTiles[k, bottomMostCol].GetComponent<TileScript>().type = 1;
                        bottomMostCol--;
                    }
                }

                DigOutRoom(bottomTiles, 0, true, k, 7);
            }
        }
    }

    void DigOutWalls(Room room, int x, int y)
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
                if ((currentTiles[9, i].GetComponent<TileScript>().type == 1) && (rightTiles[0, i].GetComponent<TileScript>().type == 1))
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
                            if (currentTiles[i, j].GetComponent<TileScript>().type == 1)
                            {
                                rightMostRow = i;
                                found = true;
                                rightMostIndexes.Add(j);
                            }
                        }
                    }
                }

                int k = rightMostIndexes[Random.Range(0, rightMostIndexes.Count)];
                if (currentTiles[rightMostRow, k].GetComponent<TileScript>().type == 1)
                {
                    while (rightMostRow < width)
                    {
                        currentTiles[rightMostRow, k].GetComponent<SpriteRenderer>().color = Color.yellow;
                        currentTiles[rightMostRow, k].GetComponent<TileScript>().type = 1;
                        rightMostRow++;
                    }
                }

                int xR = 0;
                while (!rightExitGenerated)
                {
                    if (rightTiles[xR, k].GetComponent<TileScript>().type == 0 && xR is not 9)
                    {
                        if (k is not 0 && xR > 0 && rightTiles[xR - 1, k - 1].GetComponent<TileScript>().type == 1)
                        {
                            rightExitGenerated = true;
                        }
                        else if (k is not 7 && xR > 0 && rightTiles[xR - 1, k + 1].GetComponent<TileScript>().type == 1)
                        {
                            rightExitGenerated = true;
                        }
                        else
                        {
                            rightTiles[xR, k].GetComponent<SpriteRenderer>().color = Color.blue;
                            rightTiles[xR, k].GetComponent<TileScript>().type = 1;
                            xR++;
                        }
                    }
                    else
                    {
                        rightExitGenerated = true;
                    }
                }
            }
        }

        if (hasExitLeft)
        {
            var leftTiles = rooms[x - 1, y].Grid;

            for (int i = 0; i < height; i++)
            {
                if ((currentTiles[0, i].GetComponent<TileScript>().type == 1) && (leftTiles[9, i].GetComponent<TileScript>().type == 1))
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
                            if (currentTiles[j, i].GetComponent<TileScript>().type == 1)
                            {
                                leftMostRow = i;
                                found = true;
                                leftMostIndexes.Add(j);
                            }
                        }
                    }
                }

                int k = leftMostIndexes[Random.Range(0, leftMostIndexes.Count)];
                if (currentTiles[leftMostRow, k].GetComponent<TileScript>().type == 1)
                {
                    while (leftMostRow >= 0)
                    {
                        currentTiles[leftMostRow, k].GetComponent<SpriteRenderer>().color = Color.green;
                        currentTiles[leftMostRow, k].GetComponent<TileScript>().type = 1;
                        leftMostRow--;
                    }
                }

                int xR = 9;
                while (!leftExitGenerated)
                {
                    if (leftTiles[xR, k].GetComponent<TileScript>().type == 0 && xR is not 0)
                    {
                        if (k is not 0 && xR < 9 && leftTiles[xR + 1, k - 1].GetComponent<TileScript>().type == 1)
                        {
                            leftExitGenerated = true;
                        }
                        else if (k is not 7 && xR < 9 && leftTiles[xR + 1, k + 1].GetComponent<TileScript>().type == 1)
                        {
                            leftExitGenerated = true;
                        }
                        else
                        {
                            leftTiles[xR, k].GetComponent<SpriteRenderer>().color = Color.red;
                            leftTiles[xR, k].GetComponent<TileScript>().type = 1;
                            xR--;
                        }
                    }
                    else
                    {
                        leftExitGenerated = true;
                    }
                }
            }
        }

        if (hasExitTop)
        {
            var topTiles = rooms[x, y + 1].Grid;

            for (int i = 0; i < width; i++)
            {
                if ((currentTiles[i, 7].GetComponent<TileScript>().type == 1) && (topTiles[i, 0].GetComponent<TileScript>().type == 1))
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
                            if (currentTiles[j, i].GetComponent<TileScript>().type == 1)
                            {
                                topMostCol = i;
                                found = true;
                                topMostIndexes.Add(j);
                            }
                        }
                    }
                }

                int k = topMostIndexes[Random.Range(0, topMostIndexes.Count)];
                if (currentTiles[k, topMostCol].GetComponent<TileScript>().type == 1)
                {
                    while (topMostCol < height)
                    {
                        currentTiles[k, topMostCol].GetComponent<SpriteRenderer>().color = Color.gray;
                        currentTiles[k, topMostCol].GetComponent<TileScript>().type = 1;
                        topMostCol++;
                    }
                }

                int yR = 0;
                while (!topExitGenerated)
                {
                    if (topTiles[k, yR].GetComponent<TileScript>().type == 0 && yR is not 7)
                    {
                        if (k is not 0 && yR > 0 && topTiles[k - 1, yR - 1].GetComponent<TileScript>().type == 1)
                        {
                            topExitGenerated = true;
                        }
                        else if (k is not 9 && yR > 0 && topTiles[k + 1, yR - 1].GetComponent<TileScript>().type == 1)
                        {
                            topExitGenerated = true;
                        }
                        else
                        {
                            topTiles[k, yR].GetComponent<SpriteRenderer>().color = Color.magenta;
                            topTiles[k, yR].GetComponent<TileScript>().type = 1;
                            yR++;
                        }
                    }
                    else
                    {
                        topExitGenerated = true;
                    }
                }
            }
        }

        if (hasExitBottom)
        {
            var bottomTiles = rooms[x, y - 1].Grid;

            for (int i = 0; i < width; i++)
            {
                if ((currentTiles[i, 0].GetComponent<TileScript>().type == 1) && (bottomTiles[i, 7].GetComponent<TileScript>().type == 1))
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
                            if (currentTiles[j, i].GetComponent<TileScript>().type == 1)
                            {
                                bottomMostCol = i;
                                found = true;
                                bottomMostIndexes.Add(j);
                            }
                        }
                    }
                }

                int k = bottomMostIndexes[Random.Range(0, bottomMostIndexes.Count)];
                if (currentTiles[k, bottomMostCol].GetComponent<TileScript>().type == 1)
                {
                    while (bottomMostCol >= 0)
                    {
                        currentTiles[k, bottomMostCol].GetComponent<SpriteRenderer>().color = Color.black;
                        currentTiles[k, bottomMostCol].GetComponent<TileScript>().type = 1;
                        bottomMostCol--;
                    }
                }

                int yR = 7;
                while (!bottomExitGenerated)
                {
                    if (bottomTiles[k, yR].GetComponent<TileScript>().type == 0 && yR is not 0)
                    {
                        if (k is not 9 && yR < 7 && bottomTiles[k + 1, yR + 1].GetComponent<TileScript>().type == 1)
                        {
                            bottomExitGenerated = true;
                        }
                        else if (k is not 0 && yR < 7 && bottomTiles[k - 1, yR + 1].GetComponent<TileScript>().type == 1)
                        {
                            bottomExitGenerated = true;
                        }
                        else
                        {
                            bottomTiles[k, yR].GetComponent<SpriteRenderer>().color = new Color32(14, 100, 150, 200);
                            bottomTiles[k, yR].GetComponent<TileScript>().type = 1;
                            yR--;
                        }
                    }
                    else
                    {
                        bottomExitGenerated = true;
                    }
                }
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
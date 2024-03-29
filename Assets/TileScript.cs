using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static LevelGenerator;

public class TileScript : MonoBehaviour
{
    public TileType Type { get; set; }
    public bool Visited { get; set; } = false;

    void Start()
    {
        var sprite = gameObject.GetComponent<SpriteRenderer>();
        switch (Type)
        {
            case TileType.Empty:
                sprite.color = Color.white;
                break;
        }
    }

    public void Create(TileType type)
    {
        Type = type;
        var sprite = gameObject.GetComponent<SpriteRenderer>();
        switch (Type)
        {
            case TileType.Empty:
                sprite.color = Color.white;
                break;
            case TileType.Dirt:
                sprite.color = new Color(0.588f, 0.294f, 0);
                break;
            case TileType.Entrance:
                sprite.color = Color.blue;
                break;
            case TileType.Exit:
                sprite.color = Color.blue;
                break;
            case TileType.Spike:
                sprite.color = new Color32(228, 108, 10, 255);
                break;
            case TileType.Ladder:
                sprite.color = Color.gray;
                break;
            case TileType.Bomb:
                sprite.color = Color.black;
                break;
            case TileType.Coin:
                sprite.color = Color.yellow;
                break;
            case TileType.Damsel:
                sprite.color = new Color32(213, 15, 5, 255);
                break;
            case TileType.Snake:
                sprite.color = new Color32(0, 128, 0, 255);
                break;
            case TileType.Bat:
                sprite.color = new Color32(102, 51, 153, 255);
                break;
            case TileType.Wall:
                sprite.color = Color.black;
                break;
        }
    }

    public bool IsSolid()
    {
        return Type switch
        {
            TileType.Dirt or TileType.Spike => true,
            _ => false,
        };
    }
}
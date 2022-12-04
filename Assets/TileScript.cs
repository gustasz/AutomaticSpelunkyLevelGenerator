using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TileScript : MonoBehaviour
{
    public TileType Type { get; set; }
    public bool Visited { get; set; } = false;

    void Start()
    {
        var sprite = gameObject.GetComponent<SpriteRenderer>();
        switch(Type)
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
            case TileType.EntranceExit:
                sprite.color = Color.blue;
                break;
            case TileType.Spike:
                break;
            case TileType.Ladder:
                sprite.color = Color.gray;
                break;
            case TileType.Bomb:
                break;
            case TileType.Coin:
                sprite.color = Color.yellow;
                break;
            case TileType.Damsel:
                sprite.color = Color.red;
                break;
            case TileType.Snake:
                break;
            case TileType.Bat:
                break;
        }
    }
}

public enum TileType 
{Empty, Dirt, EntranceExit, Spike, Ladder, Bomb, Coin, Damsel, Snake, Bat }
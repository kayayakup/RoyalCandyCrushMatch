// ============================================================
// TileSpawner.cs  –  object pool for Tile GameObjects
// ============================================================
using System.Collections.Generic;
using UnityEngine;

public class TileSpawner : MonoBehaviour
{
    public static TileSpawner Instance { get; private set; }

    private const int PREWARM = 80;
    private readonly Queue<Tile> _pool = new Queue<Tile>();

    private void Awake()
    {
        Instance = this;
        for (int i = 0; i < PREWARM; i++)
            _pool.Enqueue(CreateNew());
    }

    private Tile CreateNew()
    {
        var go  = new GameObject("Tile");
        go.transform.SetParent(transform, false);

        var col  = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;

        var tile = go.AddComponent<Tile>();
        tile.Initialize();
        go.SetActive(false);
        return tile;
    }

    public Tile Get(TileType type, ObstacleType obs, int gx, int gy, Vector3 worldPos)
    {
        var tile = (_pool.Count > 0) ? _pool.Dequeue() : CreateNew();
        tile.transform.SetParent(null, false);
        tile.transform.position   = worldPos;
        tile.transform.localScale = Vector3.one * GameConstants.TILE_VISUAL;
        tile.gameObject.SetActive(true);
        tile.Setup(type, obs, gx, gy);
        return tile;
    }

    public void Return(Tile tile)
    {
        if (tile == null) return;
        tile.StopAllCoroutines();
        tile.gameObject.SetActive(false);
        tile.transform.SetParent(transform, false);
        _pool.Enqueue(tile);
    }
}

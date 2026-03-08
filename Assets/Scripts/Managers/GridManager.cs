// ============================================================
// GridManager.cs
// ============================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    // Set by Bootstrap after camera is created so the grid is shifted
    // downward enough to sit below the HUD chrome.
    public float GridOffsetY { get; set; } = 0f;

    // ── Events ───────────────────────────────────────────────
    public event System.Action<int, Vector3, int> OnMatchScored;    // pts, worldPos, cascade
    public event System.Action                     OnCascadeComplete;
    public event System.Action                     OnNoMovesLeft;

    // ── Data ─────────────────────────────────────────────────
    private TileType    [,] _types;
    private ObstacleType[,] _obstacles;
    private Tile        [,] _tiles;
    private LevelConfig     _cfg;
    private bool            _processing;

    private static int W => GameConstants.GRID_WIDTH;
    private static int H => GameConstants.GRID_HEIGHT;

    private void Awake() => Instance = this;

    // ── Coordinate conversion ────────────────────────────────
    // CellToWorld and WorldToCell MUST use the same GridOffsetY so that
    // a screen tap on a tile maps back to that tile's grid cell.

    public Vector3 CellToWorld(int x, int y)
    {
        float ox = -(W - 1) * GameConstants.TILE_SPACING * 0.5f;
        float oy = -(H - 1) * GameConstants.TILE_SPACING * 0.5f + GridOffsetY;
        return new Vector3(ox + x * GameConstants.TILE_SPACING,
                           oy + y * GameConstants.TILE_SPACING, 0f);
    }

    public Vector2Int WorldToCell(Vector3 world)
    {
        float ox = -(W - 1) * GameConstants.TILE_SPACING * 0.5f;
        float oy = -(H - 1) * GameConstants.TILE_SPACING * 0.5f + GridOffsetY;
        int   x  = Mathf.FloorToInt((world.x - ox) / GameConstants.TILE_SPACING + 0.5f);
        int   y  = Mathf.FloorToInt((world.y - oy) / GameConstants.TILE_SPACING + 0.5f);
        if (x < 0 || x >= W || y < 0 || y >= H) return new Vector2Int(-1, -1);
        return new Vector2Int(x, y);
    }

    // ── Public API ───────────────────────────────────────────
    public bool IsProcessing() => _processing;

    // Build a fresh random grid
    public void BuildGrid(LevelConfig cfg)
    {
        _cfg = cfg;
        ClearVisuals();
        _types     = new TileType    [W, H];
        _obstacles = new ObstacleType[W, H];
        _tiles     = new Tile        [W, H];
        PlaceObstacles(cfg);
        FillNoMatches(cfg.NumColors);
        for (int x = 0; x < W; x++)
        for (int y = 0; y < H; y++)
            SpawnAt(x, y);
    }

    // Restore a previously saved grid (skips randomisation)
    public void RestoreGrid(LevelConfig cfg, int[] flatTypes, int[] flatObs)
    {
        _cfg = cfg;
        ClearVisuals();
        _types     = new TileType    [W, H];
        _obstacles = new ObstacleType[W, H];
        _tiles     = new Tile        [W, H];
        for (int x = 0; x < W; x++)
        for (int y = 0; y < H; y++)
        {
            _types    [x, y] = (TileType)    flatTypes[x * H + y];
            _obstacles[x, y] = (ObstacleType)flatObs  [x * H + y];
            SpawnAt(x, y);
        }
    }

    // Return flat int arrays suitable for serialisation
    public (int[] types, int[] obs) GetFlatArrays()
    {
        var types = new int[W * H];
        var obs   = new int[W * H];
        for (int x = 0; x < W; x++)
        for (int y = 0; y < H; y++)
        {
            types[x * H + y] = (int)_types    [x, y];
            obs  [x * H + y] = (int)_obstacles[x, y];
        }
        return (types, obs);
    }

    // ── Swap entry point (called by GameManager) ─────────────
    public void StartSwap(Vector2Int a, Vector2Int b, System.Action onDone)
    {
        if (_processing) { onDone?.Invoke(); return; }
        StartCoroutine(SwapCoroutine(a, b, onDone));
    }

    // ── Core swap + cascade coroutine ────────────────────────
    private IEnumerator SwapCoroutine(Vector2Int a, Vector2Int b, System.Action onDone)
    {
        _processing = true;

        // Blocked or locked tiles cannot participate in swaps
        if (_types[a.x, a.y] == TileType.Blocker ||
            _types[b.x, b.y] == TileType.Blocker ||
            _obstacles[a.x, a.y] == ObstacleType.Lock ||
            _obstacles[b.x, b.y] == ObstacleType.Lock)
        {
            _processing = false;
            onDone?.Invoke();
            yield break;
        }

        yield return StartCoroutine(AnimateSwap(a, b));

        var matches = FindAllMatches();
        if (matches.Count == 0)
        {
            // No match → swap back with no score
            yield return StartCoroutine(AnimateSwap(b, a));
            _processing = false;
            onDone?.Invoke();
            yield break;
        }

        AudioGenerator.Instance?.PlaySwap();

        int cascade = 0;
        while (matches.Count > 0)
        {
            yield return StartCoroutine(ProcessMatches(matches, cascade));
            yield return StartCoroutine(GravityAndFill());
            cascade++;
            matches = FindAllMatches();
        }

        // Cascade is done; notify GameManager (it checks for level complete)
        OnCascadeComplete?.Invoke();

        // Auto-shuffle if no moves are available; game-over if still no moves
        if (!HasAnyMove())
        {
            ShuffleGrid();
            yield return new WaitForSeconds(0.3f);
            if (!HasAnyMove())
            {
                _processing = false;
                OnNoMovesLeft?.Invoke();
                onDone?.Invoke();
                yield break;
            }
        }

        _processing = false;
        onDone?.Invoke();
    }

    // ── Match detection ──────────────────────────────────────
    public List<Vector2Int> FindAllMatches()
    {
        var set = new HashSet<Vector2Int>();

        // Horizontal
        for (int y = 0; y < H; y++)
        {
            int x = 0;
            while (x < W)
            {
                TileType t = _types[x, y];
                if (t == TileType.Empty || t == TileType.Blocker) { x++; continue; }
                int run = 1;
                while (x + run < W && _types[x + run, y] == t) run++;
                if (run >= 3)
                    for (int k = 0; k < run; k++) set.Add(new Vector2Int(x + k, y));
                x += run;
            }
        }

        // Vertical
        for (int x = 0; x < W; x++)
        {
            int y = 0;
            while (y < H)
            {
                TileType t = _types[x, y];
                if (t == TileType.Empty || t == TileType.Blocker) { y++; continue; }
                int run = 1;
                while (y + run < H && _types[x, y + run] == t) run++;
                if (run >= 3)
                    for (int k = 0; k < run; k++) set.Add(new Vector2Int(x, y + k));
                y += run;
            }
        }

        return new List<Vector2Int>(set);
    }

    public bool HasAnyMove()
    {
        for (int x = 0; x < W; x++)
        for (int y = 0; y < H; y++)
        {
            if (_types[x, y] == TileType.Blocker || _obstacles[x, y] == ObstacleType.Lock)
                continue;
            if (x + 1 < W && _types[x+1, y] != TileType.Blocker && SwapMakesMatch(x, y, x+1, y))
                return true;
            if (y + 1 < H && _types[x, y+1] != TileType.Blocker && SwapMakesMatch(x, y, x, y+1))
                return true;
        }
        return false;
    }

    // ── Animate visual + data swap ───────────────────────────
    private IEnumerator AnimateSwap(Vector2Int a, Vector2Int b)
    {
        TileType     ta = _types    [a.x, a.y], tb = _types    [b.x, b.y];
        ObstacleType oa = _obstacles[a.x, a.y], ob = _obstacles[b.x, b.y];
        Tile         va = _tiles    [a.x, a.y], vb = _tiles    [b.x, b.y];

        _types    [a.x, a.y] = tb; _types    [b.x, b.y] = ta;
        _obstacles[a.x, a.y] = ob; _obstacles[b.x, b.y] = oa;
        _tiles    [a.x, a.y] = vb; _tiles    [b.x, b.y] = va;

        if (va != null) { va.GridX = b.x; va.GridY = b.y; }
        if (vb != null) { vb.GridX = a.x; vb.GridY = a.y; }

        Coroutine ca = va?.AnimateTo(CellToWorld(b.x, b.y), GameConstants.DUR_SWAP);
        Coroutine cb = vb?.AnimateTo(CellToWorld(a.x, a.y), GameConstants.DUR_SWAP);
        if (ca != null) yield return ca;
        if (cb != null) yield return cb;
    }

    // ── Process matched cells ────────────────────────────────
    private IEnumerator ProcessMatches(List<Vector2Int> matches, int cascade)
    {
        int pts    = CalcScore(matches.Count, cascade);
        Vector3 ctr = MatchCentre(matches);
        OnMatchScored?.Invoke(pts, ctr, cascade);
        AudioGenerator.Instance?.PlayMatch();

        foreach (var cell in matches)
            if (_types[cell.x, cell.y] != TileType.Empty)
                ParticleEffectManager.Instance?.PlayMatchBurst(
                    _types[cell.x, cell.y], CellToWorld(cell.x, cell.y));

        // Collect adjacent obstacle cells before removing matched data
        var toBreak = new HashSet<Vector2Int>();
        foreach (var cell in matches)
            foreach (var n in Neighbours(cell))
                if (_obstacles[n.x, n.y] != ObstacleType.None) toBreak.Add(n);

        // Staggered remove animations
        var cors = new List<Coroutine>();
        float delay = 0f;
        foreach (var cell in matches)
        {
            Tile t = _tiles[cell.x, cell.y];
            if (t != null) cors.Add(StartCoroutine(DelayedRemove(t, delay)));
            delay += 0.032f;
        }
        foreach (var c in cors) if (c != null) yield return c;

        // Update data
        foreach (var cell in matches)
        {
            TileSpawner.Instance.Return(_tiles[cell.x, cell.y]);
            _tiles    [cell.x, cell.y] = null;
            _types    [cell.x, cell.y] = TileType.Empty;
            _obstacles[cell.x, cell.y] = ObstacleType.None;
        }

        // Break adjacent obstacles
        foreach (var cell in toBreak)
        {
            if (_obstacles[cell.x, cell.y] == ObstacleType.None) continue;
            var tile = _tiles[cell.x, cell.y];
            if (tile == null) continue;
            AudioGenerator.Instance?.PlayObstacle();
            ParticleEffectManager.Instance?.PlayObstacleBurst(CellToWorld(cell.x, cell.y));
            tile.TakeDamage();
            yield return tile.AnimateObstacleBreak();
        }
    }

    private IEnumerator DelayedRemove(Tile tile, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (tile != null) yield return tile.AnimateRemove();
    }

    // ── Gravity + fill ───────────────────────────────────────
    private IEnumerator GravityAndFill()
    {
        var fallCors  = new List<Coroutine>();

        for (int x = 0; x < W; x++)
        {
            int empty = 0;
            for (int y = 0; y < H; y++)
            {
                if (_types[x, y] == TileType.Empty) { empty++; continue; }
                if (empty == 0) continue;

                int destY = y - empty;
                _types    [x, destY] = _types    [x, y];
                _obstacles[x, destY] = _obstacles[x, y];
                _tiles    [x, destY] = _tiles    [x, y];
                _types    [x, y]     = TileType.Empty;
                _obstacles[x, y]     = ObstacleType.None;
                _tiles    [x, y]     = null;

                if (_tiles[x, destY] != null)
                {
                    _tiles[x, destY].GridX = x;
                    _tiles[x, destY].GridY = destY;
                    float dur = GameConstants.DUR_FALL * (1f + empty * 0.15f);
                    fallCors.Add(_tiles[x, destY].AnimateTo(CellToWorld(x, destY), dur, bounce: true));
                }
            }
        }

        if (fallCors.Count > 0) AudioGenerator.Instance?.PlayFall();
        foreach (var c in fallCors) if (c != null) yield return c;

        // Spawn new tiles
        var spawnCors = new List<Coroutine>();
        for (int x = 0; x < W; x++)
        {
            int spawnIdx = 0;
            for (int y = H - 1; y >= 0; y--)
            {
                if (_types[x, y] != TileType.Empty) continue;
                TileType nt = RandCandy(_cfg.NumColors);
                _types    [x, y] = nt;
                _obstacles[x, y] = ObstacleType.None;
                Vector3 spawnPos  = CellToWorld(x, H + spawnIdx++);
                var tile          = TileSpawner.Instance.Get(nt, ObstacleType.None, x, y, spawnPos);
                _tiles[x, y]      = tile;
                tile.AnimateSpawn();
                spawnCors.Add(tile.AnimateTo(CellToWorld(x, y), GameConstants.DUR_FALL * 1.3f, bounce: true));
            }
        }
        foreach (var c in spawnCors) if (c != null) yield return c;
    }

    // ── Grid helpers ─────────────────────────────────────────
    private void PlaceObstacles(LevelConfig cfg)
    {
        if (cfg.MaxObstacles == 0) return;
        var candidates = AllCells();
        Shuffle(candidates);
        int placed = 0;
        foreach (var cell in candidates)
        {
            if (placed >= cfg.MaxObstacles) break;
            float r   = Random.value;
            float sum = 0f;
            ObstacleType obs = ObstacleType.None;
            sum += cfg.LockChance;        if (r < sum) { obs = ObstacleType.Lock;        }
            else { sum += cfg.DoubleJellyChance; if (r < sum) { obs = ObstacleType.DoubleJelly; }
            else { sum += cfg.JellyChance;        if (r < sum) { obs = ObstacleType.Jelly; } } }
            if (obs != ObstacleType.None) { _obstacles[cell.x, cell.y] = obs; placed++; }
        }
        if (cfg.HasBlockers)
        {
            int want = Mathf.RoundToInt(W * H * 0.04f);
            foreach (var cell in candidates)
            {
                if (want <= 0) break;
                if (_obstacles[cell.x, cell.y] != ObstacleType.None) continue;
                _types[cell.x, cell.y] = TileType.Blocker;
                want--;
            }
        }
    }

    private void FillNoMatches(int numColors)
    {
        for (int x = 0; x < W; x++)
        for (int y = 0; y < H; y++)
        {
            if (_types[x, y] == TileType.Blocker) continue;
            TileType t;
            int tries = 0;
            do { t = RandCandy(numColors); tries++; }
            while (tries < 20 && WouldMatch(x, y, t));
            _types[x, y] = t;
        }
    }

    private bool WouldMatch(int x, int y, TileType t)
    {
        if (x >= 2 && _types[x-1, y] == t && _types[x-2, y] == t) return true;
        if (y >= 2 && _types[x, y-1] == t && _types[x, y-2] == t) return true;
        return false;
    }

    private void SpawnAt(int x, int y)
    {
        _tiles[x, y] = TileSpawner.Instance.Get(
            _types[x, y], _obstacles[x, y], x, y, CellToWorld(x, y));
    }

    private void ClearVisuals()
    {
        if (_tiles == null) return;
        for (int x = 0; x < W; x++)
        for (int y = 0; y < H; y++)
        {
            TileSpawner.Instance.Return(_tiles[x, y]);
            _tiles[x, y] = null;
        }
    }

    private void ShuffleGrid()
    {
        var pool = new List<TileType>(W * H);
        for (int x = 0; x < W; x++)
        for (int y = 0; y < H; y++)
            if (_types[x, y] != TileType.Blocker && _types[x, y] != TileType.Empty)
                pool.Add(_types[x, y]);
        Shuffle(pool);
        int idx = 0;
        for (int x = 0; x < W; x++)
        for (int y = 0; y < H; y++)
        {
            if (_types[x, y] == TileType.Blocker || _types[x, y] == TileType.Empty) continue;
            _types[x, y] = pool[idx++];
            _tiles[x, y]?.Setup(_types[x, y], _obstacles[x, y], x, y);
        }
    }

    private bool SwapMakesMatch(int x1, int y1, int x2, int y2)
    {
        TileType tmp    = _types[x1, y1];
        _types[x1, y1] = _types[x2, y2];
        _types[x2, y2] = tmp;
        bool found      = FindAllMatches().Count > 0;
        _types[x2, y2] = _types[x1, y1];
        _types[x1, y1] = tmp;
        return found;
    }

    // ── Static helpers ───────────────────────────────────────
    private static TileType RandCandy(int n) => (TileType)Random.Range(0, Mathf.Min(n, 7));

    private static int CalcScore(int count, int cascade)
    {
        int b;
        if      (count == 3) b = GameConstants.SCORE_3;
        else if (count == 4) b = GameConstants.SCORE_4;
        else if (count == 5) b = GameConstants.SCORE_5;
        else                 b = GameConstants.SCORE_6PLUS + (count - 6) * 100;
        return Mathf.RoundToInt(b * (1f + cascade * GameConstants.CASCADE_MULT));
    }

    private Vector3 MatchCentre(List<Vector2Int> cells)
    {
        if (cells.Count == 0) return Vector3.zero;
        Vector3 s = Vector3.zero;
        foreach (var c in cells) s += CellToWorld(c.x, c.y);
        return s / cells.Count;
    }

    private IEnumerable<Vector2Int> Neighbours(Vector2Int cell)
    {
        int[] dx = { 1, -1,  0,  0 };
        int[] dy = { 0,  0,  1, -1 };
        for (int i = 0; i < 4; i++)
        {
            int nx = cell.x + dx[i], ny = cell.y + dy[i];
            if (nx >= 0 && nx < W && ny >= 0 && ny < H)
                yield return new Vector2Int(nx, ny);
        }
    }

    private static List<Vector2Int> AllCells()
    {
        var l = new List<Vector2Int>(W * H);
        for (int x = 0; x < W; x++)
        for (int y = 0; y < H; y++)
            l.Add(new Vector2Int(x, y));
        return l;
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

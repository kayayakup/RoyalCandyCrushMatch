// ============================================================
// Tile.cs
// ============================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tile : MonoBehaviour
{
    // ── Public state ─────────────────────────────────────────
    public TileType     TileType     { get; private set; }
    public ObstacleType ObstacleType { get; private set; }
    public int          GridX        { get; set; }
    public int          GridY        { get; set; }

    // Base visual scale; may differ from TILE_VISUAL when a custom sprite
    // has a different native size (pixels-per-unit ratio).
    private float _baseScale = GameConstants.TILE_VISUAL;
    private int   _obstacleHealth;

    // ── Components ───────────────────────────────────────────
    private SpriteRenderer _sr;
    private SpriteRenderer _overlaySR;
    private bool           _initialized;

    // ── Shared texture cache ──────────────────────────────────
    private static readonly Dictionary<TileType, Texture2D> _texCache =
        new Dictionary<TileType, Texture2D>();
    private static Texture2D _jellyTex;
    private static Texture2D _lockTex;

    private static readonly Color[] CandyColors =
    {
        new Color(1.00f, 0.22f, 0.22f), // Red
        new Color(0.28f, 0.52f, 1.00f), // Blue
        new Color(0.18f, 0.80f, 0.32f), // Green
        new Color(1.00f, 0.90f, 0.08f), // Yellow
        new Color(0.72f, 0.18f, 1.00f), // Purple
        new Color(1.00f, 0.52f, 0.04f), // Orange
        new Color(1.00f, 0.40f, 0.80f), // Pink
        new Color(0.40f, 0.40f, 0.40f), // Blocker
    };

    // ─────────────────────────────────────────────────────────
    // Called once by TileSpawner right after AddComponent
    // ─────────────────────────────────────────────────────────
    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        _sr              = gameObject.AddComponent<SpriteRenderer>();
        _sr.sortingOrder = GameConstants.SORT_TILE;

        var ov = new GameObject("Overlay");
        ov.transform.SetParent(transform, false);
        ov.transform.localPosition = Vector3.zero;

        _overlaySR              = ov.AddComponent<SpriteRenderer>();
        _overlaySR.sortingOrder = GameConstants.SORT_OBSTACLE;
        _overlaySR.enabled      = false;

        if (_jellyTex == null) _jellyTex = BuildJellyTex();
        if (_lockTex  == null) _lockTex  = BuildLockTex();
    }

    // ─────────────────────────────────────────────────────────
    // Called every time the tile comes out of the pool
    // ─────────────────────────────────────────────────────────
    public void Setup(TileType type, ObstacleType obs, int gx, int gy)
    {
        TileType        = type;
        ObstacleType    = obs;
        GridX           = gx;
        GridY           = gy;
        _obstacleHealth = (obs == ObstacleType.DoubleJelly) ? 2 : 1;

        _sr.color   = Color.white;
        _sr.enabled = (type != TileType.Empty);

        // ── Sprite selection: custom > procedural ────────────
        if (type != TileType.Empty)
        {
            int     idx    = (int)type;
            Sprite  custom = GetCustomSprite(idx);
            if (custom != null)
            {
                _sr.sprite = custom;
                // Normalise scale so the sprite fits within TILE_VISUAL world units
                float maxSide = Mathf.Max(custom.bounds.size.x, custom.bounds.size.y);
                _baseScale    = maxSide > 0.01f
                              ? GameConstants.TILE_VISUAL / maxSide
                              : GameConstants.TILE_VISUAL;
            }
            else
            {
                _sr.sprite = SpriteFromTex(GetOrBuildTex(type));
                _baseScale = GameConstants.TILE_VISUAL;
            }
        }

        transform.localScale = Vector3.one * _baseScale;

        // ── Overlay ──────────────────────────────────────────
        _overlaySR.transform.localScale    = Vector3.one;
        _overlaySR.transform.localPosition = Vector3.zero;
        RefreshOverlay();
    }

    // ── Obstacle API ─────────────────────────────────────────
    public bool TakeDamage()
    {
        _obstacleHealth--;
        if (ObstacleType == ObstacleType.DoubleJelly && _obstacleHealth == 1)
        {
            ObstacleType = ObstacleType.Jelly;
            RefreshOverlay();
            return false;
        }
        ObstacleType = ObstacleType.None;
        RefreshOverlay();
        return true;
    }

    private void RefreshOverlay()
    {
        switch (ObstacleType)
        {
            case ObstacleType.Jelly:
                _overlaySR.sprite  = SpriteFromTex(_jellyTex);
                _overlaySR.color   = new Color(1f, 1f, 0.45f, 0.55f);
                _overlaySR.enabled = true;
                break;
            case ObstacleType.DoubleJelly:
                _overlaySR.sprite  = SpriteFromTex(_jellyTex);
                _overlaySR.color   = new Color(0.45f, 1f, 1f, 0.65f);
                _overlaySR.enabled = true;
                break;
            case ObstacleType.Lock:
                _overlaySR.sprite  = SpriteFromTex(_lockTex);
                _overlaySR.color   = new Color(0.78f, 0.78f, 0.78f, 0.85f);
                _overlaySR.enabled = true;
                break;
            default:
                _overlaySR.enabled = false;
                break;
        }
    }

    // ── Animations ───────────────────────────────────────────
    public Coroutine AnimateTo(Vector3 target, float duration, bool bounce = false)
        => StartCoroutine(MoveRoutine(target, duration, bounce));

    public Coroutine AnimateRemove()         => StartCoroutine(RemoveRoutine());
    public Coroutine AnimateSpawn()          => StartCoroutine(SpawnRoutine());
    public Coroutine AnimateObstacleBreak()  => StartCoroutine(ObstacleBreakRoutine());

    private IEnumerator MoveRoutine(Vector3 target, float dur, bool bounce)
    {
        Vector3 start = transform.position;
        float   t     = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(dur, 0.01f);
            float e = bounce ? EaseBounce(Mathf.Clamp01(t))
                             : EaseOutCubic(Mathf.Clamp01(t));
            transform.position = Vector3.LerpUnclamped(start, target, e);
            yield return null;
        }
        transform.position = target;
    }

    private IEnumerator RemoveRoutine()
    {
        Vector3 startScale = transform.localScale;
        float   t          = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / GameConstants.DUR_REMOVE;
            float e = EaseInBack(Mathf.Clamp01(t));
            transform.localScale = startScale * (1f - e);
            _sr.color            = new Color(1f, 1f, 1f, 1f - e);
            yield return null;
        }
        transform.localScale = Vector3.zero;
    }

    private IEnumerator SpawnRoutine()
    {
        _sr.color            = new Color(1f, 1f, 1f, 0f);
        transform.localScale = Vector3.zero;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / GameConstants.DUR_SPAWN;
            float e = EaseOutBack(Mathf.Clamp01(t));
            transform.localScale = Vector3.one * (_baseScale * Mathf.Max(e, 0f));
            _sr.color            = new Color(1f, 1f, 1f, Mathf.Clamp01(e));
            yield return null;
        }
        transform.localScale = Vector3.one * _baseScale;
        _sr.color            = Color.white;
    }

    private IEnumerator ObstacleBreakRoutine()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.24f;
            float e = EaseOutCubic(Mathf.Clamp01(t));
            _overlaySR.color                   = Color.Lerp(_overlaySR.color, Color.white, e);
            _overlaySR.transform.localScale    = Vector3.one * (1f + Mathf.Sin(e * Mathf.PI) * 0.28f);
            yield return null;
        }
        _overlaySR.transform.localScale = Vector3.one;
    }

    // ── Easing ───────────────────────────────────────────────
    private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
    private static float EaseInBack  (float t) { const float s = 1.70158f; return t*t*((s+1f)*t-s); }
    private static float EaseOutBack (float t) { t -= 1f; const float s = 1.70158f; return t*t*((s+1f)*t+s)+1f; }
    private static float EaseBounce  (float t)
    {
        if (t < 1f / 2.75f)         return 7.5625f * t * t;
        if (t < 2f / 2.75f)       { t -= 1.5f   / 2.75f; return 7.5625f*t*t + 0.75f;    }
        if (t < 2.5f / 2.75f)     { t -= 2.25f  / 2.75f; return 7.5625f*t*t + 0.9375f;  }
        t -= 2.625f / 2.75f;         return 7.5625f * t * t + 0.984375f;
    }

    // ── Texture / sprite helpers ─────────────────────────────
    private static Sprite GetCustomSprite(int idx)
    {
        var arr = Bootstrap.GlobalTileSprites;
        if (arr == null || idx < 0 || idx >= arr.Length) return null;
        return arr[idx];
    }

    private static Texture2D GetOrBuildTex(TileType t)
    {
        if (_texCache.TryGetValue(t, out var tex) && tex != null) return tex;
        int   idx = (int)t;
        Color col = (idx >= 0 && idx < CandyColors.Length) ? CandyColors[idx] : Color.magenta;
        tex = BuildCandyTex(col, t == TileType.Blocker);
        _texCache[t] = tex;
        return tex;
    }

    private static Texture2D BuildCandyTex(Color baseCol, bool isBlocker)
    {
        const int SZ = 64;
        var tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Bilinear };
        var px  = new Color[SZ * SZ];
        float cx = SZ * 0.5f, cy = SZ * 0.5f, r = SZ * 0.5f - 2f;

        for (int y = 0; y < SZ; y++)
        for (int x = 0; x < SZ; x++)
        {
            float dx = x - cx, dy = y - cy, dist = Mathf.Sqrt(dx*dx + dy*dy);
            if (dist > r) { px[y*SZ+x] = Color.clear; continue; }

            float alpha = Mathf.SmoothStep(r, r - 2f, dist);
            Color c;
            if (isBlocker)
            {
                bool cross = Mathf.Abs(dx) < 7f || Mathf.Abs(dy) < 7f;
                c = cross ? new Color(0.62f, 0.62f, 0.62f)
                          : new Color(0.38f, 0.38f, 0.38f);
            }
            else
            {
                float norm = dist / r;
                float dark = 1f - norm * norm * 0.28f;
                float hx   = (x - cx * 0.65f) / r;
                float hy   = (y + cy * 0.40f) / r;
                float hl   = Mathf.Clamp01(1f - (hx*hx + hy*hy) * 2.5f) * 0.44f;
                c = Color.Lerp(baseCol, Color.white, hl) * dark;
            }
            c.a = alpha;
            px[y*SZ+x] = c;
        }
        tex.SetPixels(px); tex.Apply(); return tex;
    }

    private static Texture2D BuildJellyTex()
    {
        const int SZ = 64;
        var tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
        var px  = new Color[SZ * SZ];
        float cx = SZ*0.5f, cy = SZ*0.5f, r = SZ*0.5f - 2f;
        for (int y = 0; y < SZ; y++)
        for (int x = 0; x < SZ; x++)
        {
            float d = Mathf.Sqrt((x-cx)*(x-cx)+(y-cy)*(y-cy));
            if (d > r) { px[y*SZ+x]=Color.clear; continue; }
            px[y*SZ+x] = d > r - 5f
                ? new Color(1f, 1f, 0.25f, 0.92f)
                : new Color(1f, 1f, 0.60f, 0.42f);
        }
        tex.SetPixels(px); tex.Apply(); return tex;
    }

    private static Texture2D BuildLockTex()
    {
        const int SZ = 64;
        var tex = new Texture2D(SZ, SZ, TextureFormat.RGBA32, false);
        var px  = new Color[SZ * SZ];
        float cx = SZ*0.5f, cy = SZ*0.5f;
        for (int y = 0; y < SZ; y++)
        for (int x = 0; x < SZ; x++)
        {
            float dx=x-cx, dy=y-cy;
            bool bar = (Mathf.Abs(dx)<7f && Mathf.Abs(dy)<18f)
                    || (Mathf.Abs(dy)<7f && Mathf.Abs(dx)<18f);
            px[y*SZ+x] = bar ? new Color(0.88f,0.88f,0.88f,0.92f) : Color.clear;
        }
        tex.SetPixels(px); tex.Apply(); return tex;
    }

    private static Sprite SpriteFromTex(Texture2D tex)
        => Sprite.Create(tex,
                         new Rect(0, 0, tex.width, tex.height),
                         new Vector2(0.5f, 0.5f),
                         tex.width);   // pixelsPerUnit = tex.width → 1×1 world unit at scale 1
}

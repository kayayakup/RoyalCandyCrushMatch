// ============================================================
// Bootstrap.cs
// Attach ONLY this script to ONE empty GameObject in an empty scene.
// Everything else (camera, grid, UI, audio, particles) is built at runtime.
// ============================================================
using UnityEngine;

public class Bootstrap : MonoBehaviour
{
    // ── Inspector: tile icons (optional – leave empty for procedural textures)
    [Header("Tile Icons  (optional – drag sprites here)")]
    [Tooltip("Index 0=Red  1=Blue  2=Green  3=Yellow  4=Purple  5=Orange  6=Pink  7=Blocker")]
    public Sprite[] TileSprites = new Sprite[8];

    // Static accessor so Tile.cs can read the sprites without a direct reference
    public static Sprite[] GlobalTileSprites { get; private set; }

    [Header("Background Sprites (optional)")]
    public Sprite[] BackgroundSprites;
    public static Sprite[] GlobalBackgroundSprites { get; private set; }

    [Header("Grid Frame (optional)")]
    public Sprite GridFrameSprite;
    public static Sprite GlobalGridFrameSprite { get; private set; }

    // ── Runtime references ────────────────────────────────────
    private Camera _cam;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        Screen.sleepTimeout         = SleepTimeout.NeverSleep;

        // Expose sprites globally before any Tile is created
        GlobalTileSprites = TileSprites;
        GlobalBackgroundSprites = BackgroundSprites;
        GlobalGridFrameSprite = GridFrameSprite;

        // 1. Camera (must be first so GridManager can compute world offsets)
        _cam = CreateCamera();

        // 2. Service managers – Awake() sets their Instance property synchronously
        Spawn<AudioGenerator>       ("AudioGenerator");
        Spawn<ParticleEffectManager>("ParticleEffectManager");
        Spawn<ScoreManager>         ("ScoreManager");
        Spawn<DifficultyManager>    ("DifficultyManager");
        Spawn<TileSpawner>          ("TileSpawner");

        // 3. Grid manager – needs camera ortho size to set world offset
        var grid = Spawn<GridManager>("GridManager");
        grid.GridOffsetY = ComputeGridOffsetY(_cam);

        // 4. Input + UI + game-flow
        var input = Spawn<InputHandler>("InputHandler");
        var ui    = Spawn<UIManager>   ("UIManager");
        var gm    = Spawn<GameManager> ("GameManager");
        gm.Inject(input, ui);
    }

    // ─────────────────────────────────────────────────────────
    private static T Spawn<T>(string name) where T : MonoBehaviour
    {
        var go = new GameObject(name);
        DontDestroyOnLoad(go);
        return go.AddComponent<T>();
    }

    public static Camera MainCamera { get; private set; }
    private static Camera CreateCamera()
    {
        // Destroy any cameras Unity may have added (e.g. from a non-empty scene template)
        foreach (var existing in Camera.allCameras)
            Destroy(existing.gameObject);

        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        DontDestroyOnLoad(go);

        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = CalcOrthoSize();
        cam.backgroundColor = new Color(0.06f, 0.03f, 0.14f, 1f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.nearClipPlane = -20f;
        cam.farClipPlane = 20f;
        go.transform.position = new Vector3(0f, 0f, -10f);

        MainCamera = cam;   // ← expose for other systems
        return cam;
    }

    // ── Ortho size fits the grid plus safe-area / HUD padding ─
    private static float CalcOrthoSize()
    {
        float gridH  = GameConstants.GRID_HEIGHT * GameConstants.TILE_SPACING;
        float gridW  = GameConstants.GRID_WIDTH  * GameConstants.TILE_SPACING;
        float aspect = (float)Screen.width / Mathf.Max(Screen.height, 1);

        float byH = gridH * 0.5f + 3.6f;          // vertical padding for HUD + safe area
        float byW = gridW / (2f * aspect) + 1.6f;
        return Mathf.Max(byH, byW);
    }

    // ── Shift grid downward so HUD chrome doesn't overlap tiles ─
    // HUD occupies the top ~(88+34)/1920 fraction of the canvas (reference res).
    // That fraction × (orthoSize×2) gives the world-unit height of the HUD.
    // We move the grid centre down by half that to keep it visually centred
    // in the remaining screen area.
    private static float ComputeGridOffsetY(Camera cam)
    {
        const float hudRefPx = 122f;     // bar (88) + target strip (34)
        const float refH     = 1920f;
        float hudWorld        = (hudRefPx / refH) * cam.orthographicSize * 2f;
        return -(hudWorld * 0.5f);       // shift centre down
    }
}

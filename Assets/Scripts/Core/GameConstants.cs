// ============================================================
// GameConstants.cs
// ============================================================

public static class GameConstants
{
    // ── Grid ─────────────────────────────────────────────────
    public const int   GRID_WIDTH   = 8;
    public const int   GRID_HEIGHT  = 8;
    public const float TILE_SPACING = 1.05f;
    public const float TILE_VISUAL  = 0.90f;

    // ── Durations (s) ────────────────────────────────────────
    public const float DUR_SWAP   = 0.20f;
    public const float DUR_REMOVE = 0.28f;
    public const float DUR_FALL   = 0.18f;
    public const float DUR_SPAWN  = 0.22f;
    public const float DUR_POPUP  = 0.85f;

    // ── Scoring ───────────────────────────────────────────────
    public const int   SCORE_3      = 3;
    public const int   SCORE_4      = 8;
    public const int   SCORE_5      = 20;
    public const int   SCORE_6PLUS  = 40;
    public const float CASCADE_MULT = 0.5f;

    // ── Sorting orders ────────────────────────────────────────
    public const int SORT_BG       = -5;
    public const int SORT_TILE     =  0;
    public const int SORT_OBSTACLE =  1;
    public const int SORT_POPUP    = 10;
}

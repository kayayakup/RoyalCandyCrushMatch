// ============================================================
// GameEnums.cs
// ============================================================

public enum TileType
{
    Empty   = -1,
    Red     =  0,
    Blue    =  1,
    Green   =  2,
    Yellow  =  3,
    Purple  =  4,
    Orange  =  5,
    Pink    =  6,
    Blocker =  7
}

public enum ObstacleType
{
    None        = 0,
    Jelly       = 1,
    DoubleJelly = 2,
    Lock        = 3
}

public enum GameState
{
    MainMenu,
    Playing,
    Animating,
    LevelComplete,
    GameOver
}

// ── Serialisable save payload ─────────────────────────────────
[System.Serializable]
public class SaveData
{
    public bool valid;
    public int  level;
    public int  score;
    public int  bestScore;
    public int[] gridTypes;      // GRID_WIDTH × GRID_HEIGHT, index = x*H + y
    public int[] gridObstacles;
}

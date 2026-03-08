// ============================================================
// DifficultyManager.cs
// ============================================================
using UnityEngine;

[System.Serializable]
public struct LevelConfig
{
    public int   Level;
    public int   NumColors;
    public int   TargetScore;
    public float JellyChance;
    public float DoubleJellyChance;
    public float LockChance;
    public int   MaxObstacles;
    public bool  HasBlockers;
}

public class DifficultyManager : MonoBehaviour
{
    public static DifficultyManager Instance { get; private set; }
    private void Awake() => Instance = this;

    public LevelConfig GetConfig(int level)
    {
        level = Mathf.Max(1, level);
        float t200 = Mathf.Clamp01((level - 1f) / 200f);

        return new LevelConfig
        {
            Level             = level,
            NumColors         = CalcColors(level),
            TargetScore       = CalcTarget(level),
            JellyChance       = level >= 6  ? Mathf.Lerp(0f, 0.22f, Ramp(level,  5, 65)) : 0f,
            DoubleJellyChance = level >= 15 ? Mathf.Lerp(0f, 0.10f, Ramp(level, 14, 80)) : 0f,
            LockChance        = level >= 25 ? Mathf.Lerp(0f, 0.08f, Ramp(level, 24, 80)) : 0f,
            MaxObstacles      = level >= 6  ? Mathf.RoundToInt(Mathf.Lerp(1, 18, t200))  : 0,
            HasBlockers       = level >= 30
        };
    }

    private static int CalcColors(int l)
    {
        if (l < 6)  return 4;
        if (l < 12) return 5;
        if (l < 25) return 6;
        return 7;
    }

    private static int CalcTarget(int l)
    {
        float raw = 800f + (l - 1f) * 420f + Mathf.Pow(l, 1.35f) * 60f;
        return Mathf.RoundToInt(raw / 100f) * 100;
    }

    private static float Ramp(int level, int start, int span)
        => Mathf.Clamp01((level - start) / (float)span);
}

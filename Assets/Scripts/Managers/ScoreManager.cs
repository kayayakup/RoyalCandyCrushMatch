// ============================================================
// ScoreManager.cs
// ============================================================
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    private const string KEY_BEST = "BestScore_v2";

    public int CurrentScore { get; private set; }
    public int BestScore    { get; private set; }
    public int CurrentLevel { get; private set; } = 1;

    private void Awake()
    {
        Instance  = this;
        BestScore = PlayerPrefs.GetInt(KEY_BEST, 0);
    }

    public void ResetForNewGame()
    {
        CurrentScore = 0;
        CurrentLevel = 1;
    }

    public void RestoreFromSave(int score, int best, int level)
    {
        CurrentScore = score;
        BestScore    = Mathf.Max(best, score);
        CurrentLevel = Mathf.Max(1, level);
        // Persist best in case it changed
        if (BestScore > PlayerPrefs.GetInt(KEY_BEST, 0))
        {
            PlayerPrefs.SetInt(KEY_BEST, BestScore);
            PlayerPrefs.Save();
        }
    }

    public void SetLevel(int level) => CurrentLevel = Mathf.Max(1, level);

    public void AddScore(int pts)
    {
        if (pts <= 0) return;
        CurrentScore += pts;
        if (CurrentScore > BestScore)
        {
            BestScore = CurrentScore;
            PlayerPrefs.SetInt(KEY_BEST, BestScore);
            PlayerPrefs.Save();
        }
    }

    public bool HasReachedTarget(int target) => CurrentScore >= target;
}

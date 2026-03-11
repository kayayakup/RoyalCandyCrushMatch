// ============================================================
// GameManager.cs  –  state machine + orchestrator
// ============================================================
using System.Collections;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private GameState _state = GameState.MainMenu;
    private LevelConfig _cfg;

    private InputHandler _input;
    private UIManager _ui;

    private void Awake() => Instance = this;

    // ── Bootstrap wiring (called once) ───────────────────────
    public void Inject(InputHandler input, UIManager ui)
    {
        _input = input;
        _ui = ui;

        // UI callbacks
        _ui.OnStartNewGame += StartNewGame;
        _ui.OnContinueGame += ContinueGame;
        _ui.OnRestartPressed += RestartGame;

        // Input
        _input.OnSwipeDetected += HandleSwipe;

        // Grid events
        GridManager.Instance.OnMatchScored += HandleMatchScored;
        GridManager.Instance.OnCascadeComplete += HandleCascadeComplete;
        GridManager.Instance.OnNoMovesLeft += HandleNoMoves;
    }

    // ── Save on app pause / focus loss ───────────────────────
    private void OnApplicationPause(bool paused)
    { if (paused && _state == GameState.Playing) TrySaveCurrentState(); }

    private void OnApplicationFocus(bool focus)
    { if (!focus && _state == GameState.Playing) TrySaveCurrentState(); }

    // ── Game flow ────────────────────────────────────────────
    private void StartNewGame()
    {
        SaveSystem.Delete();
        ScoreManager.Instance.ResetForNewGame();
        _ui.RefreshContinueButton();
        LoadLevel(1);
    }

    private void RestartGame()
    {
        SaveSystem.Delete();
        ScoreManager.Instance.ResetForNewGame();
        LoadLevel(1);
    }

    private void ContinueGame()
    {
        var save = SaveSystem.Load();
        if (save == null) { StartNewGame(); return; }

        _state = GameState.Animating;
        _input.SetEnabled(false);

        ScoreManager.Instance.RestoreFromSave(save.score, save.bestScore, save.level);

        _cfg = DifficultyManager.Instance.GetConfig(save.level);
        GridManager.Instance.RestoreGrid(_cfg, save.gridTypes, save.gridObstacles);

        _ui.ShowHUD();
        _ui.UpdateScore(_score);
        _ui.UpdateBest(ScoreManager.Instance.BestScore);
        _ui.UpdateLevel(ScoreManager.Instance.CurrentLevel);
        _ui.UpdateTarget(_cfg.TargetScore);

        StartCoroutine(EnableInputAfter(0.55f));
    }

    private void LoadLevel(int level)
    {
        _state = GameState.Animating;
        _input.SetEnabled(false);

        ScoreManager.Instance.SetLevel(level);
        _cfg = DifficultyManager.Instance.GetConfig(level);
        GridManager.Instance.BuildGrid(_cfg);

        _ui.ShowHUD();
        _ui.UpdateScore(_score);
        _ui.UpdateBest(ScoreManager.Instance.BestScore);
        _ui.UpdateLevel(level);
        _ui.UpdateTarget(_cfg.TargetScore);

        StartCoroutine(EnableInputAfter(0.25f));

        GoogleAdMobController.Instance.LoadBanner();
        if (level > 3)
            GoogleAdMobController.Instance.ShowInterstitialAd();
    }

    private IEnumerator EnableInputAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_state == GameState.Animating)
        {
            _state = GameState.Playing;
            _input.SetEnabled(true);
        }
    }

    // ── Swipe ────────────────────────────────────────────────
    private void HandleSwipe(Vector2Int from, Vector2Int to)
    {
        if (_state != GameState.Playing) return;
        if (GridManager.Instance.IsProcessing()) return;

        _state = GameState.Animating;
        _input.SetEnabled(false);
        GridManager.Instance.StartSwap(from, to, OnSwapFinished);
    }

    private void OnSwapFinished()
    {
        // If no other state transition occurred during the swap+cascade, resume playing.
        if (_state == GameState.Animating)
        {
            _state = GameState.Playing;
            _input.SetEnabled(true);
        }
    }

    // ── Grid event handlers ───────────────────────────────────
    private void HandleMatchScored(int pts, Vector3 worldPos, int cascade)
    {
        ScoreManager.Instance.AddScore(pts);
        _ui.UpdateScore(_score);
        _ui.UpdateBest(ScoreManager.Instance.BestScore);
        _ui.SpawnScorePopup(pts, worldPos);
    }

    // NOTE: OnCascadeComplete fires synchronously inside the GridManager
    // coroutine. StartCoroutine here executes synchronously up to its first
    // yield, so _state is set to LevelComplete BEFORE OnSwapFinished returns.
    private void HandleCascadeComplete()
    {
        if (!ScoreManager.Instance.HasReachedTarget(_cfg.TargetScore)) return;
        if (_state == GameState.LevelComplete) return;

        _state = GameState.LevelComplete;
        _input.SetEnabled(false);

        // Save progress so the player can continue from the next level
        TrySaveCurrentState();

        StartCoroutine(LevelCompleteRoutine());
    }

    private IEnumerator LevelCompleteRoutine()
    {
        AudioGenerator.Instance?.PlayLevelUp();
        ParticleEffectManager.Instance?.PlayLevelUpBurst(
            GridManager.Instance.CellToWorld(
                GameConstants.GRID_WIDTH / 2,
                GameConstants.GRID_HEIGHT / 2));

        _ui.ShowLevelComplete(ScoreManager.Instance.CurrentLevel);
        yield return new WaitForSeconds(2.8f);
        _ui.HideLevelComplete();

        LoadLevel(ScoreManager.Instance.CurrentLevel + 1);
    }

    private void HandleNoMoves()
    {
        if (_state != GameState.Playing && _state != GameState.Animating) return;
        _state = GameState.GameOver;
        _input.SetEnabled(false);
        StartCoroutine(GameOverRoutine());
    }

    private IEnumerator GameOverRoutine()
    {
        SaveSystem.Delete();        // wipe save on game-over
        _ui.RefreshContinueButton();
        AudioGenerator.Instance?.PlayGameOver();
        yield return new WaitForSeconds(0.85f);
        _ui.ShowGameOver(ScoreManager.Instance.CurrentScore);
    }

    // ── Save helper ──────────────────────────────────────────
    private void TrySaveCurrentState()
    {
        if (GridManager.Instance == null) return;
        var (types, obs) = GridManager.Instance.GetFlatArrays();

        SaveSystem.Save(new SaveData
        {
            valid = true,
            level = ScoreManager.Instance.CurrentLevel,
            score = ScoreManager.Instance.CurrentScore,
            bestScore = ScoreManager.Instance.BestScore,
            gridTypes = types,
            gridObstacles = obs
        });

        _ui.RefreshContinueButton();
    }

    private int _score => ScoreManager.Instance.CurrentScore;
}

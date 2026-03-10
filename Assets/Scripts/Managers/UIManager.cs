// ============================================================
// UIManager.cs  –  all UI built at runtime
// Features: Safe Area, animated background, floating orbs,
//           progress bar, score pulse, bigger text, Continue button
// ============================================================
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    // ── Callbacks wired by GameManager ────────────────────────
    public System.Action OnStartNewGame;
    public System.Action OnContinueGame;
    public System.Action OnRestartPressed;

    // ── Panels ───────────────────────────────────────────────
    private GameObject _mainMenuPanel;
    private GameObject _hudPanel;
    private GameObject _gameOverPanel;
    private GameObject _levelCompletePanel;

    // ── HUD labels ───────────────────────────────────────────
    private TextMeshProUGUI _scoreTxt;
    private TextMeshProUGUI _bestTxt;
    private TextMeshProUGUI _levelTxt;
    private TextMeshProUGUI _targetTxt;

    // ── Progress bar ─────────────────────────────────────────
    private RectTransform _progressFillRT;
    private Image _progressFillImg;
    private int _progressTarget;

    // ── Other panel labels ───────────────────────────────────
    private TextMeshProUGUI _goScoreTxt;
    private TextMeshProUGUI _lcLevelTxt;

    // ── Continue button (main menu) ──────────────────────────
    private GameObject _continueBtn;

    // ── Score pulse coroutine handle ─────────────────────────
    private Coroutine _scorePulseCo;



    // ── Canvas root ─────────────────────────────────────────
    private Canvas _canvas;

    // ── Safe area rect ───────────────────────────────────────
    private RectTransform _safeAreaRT;

    // ─────────────────────────────────────────────────────────
    private void Awake()
    {
        Instance = this;
        BuildCanvas();
        BuildBackgroundCanvas();
        BuildSafeAreaPanel();
        BuildHUD();
        BuildMainMenu();
        BuildGameOverPanel();
        BuildLevelCompletePanel();
        ShowMainMenu();
    }



    // ── Public API ───────────────────────────────────────────
    public void ShowMainMenu()
    {
        Activate(_mainMenuPanel, true);
        Activate(_hudPanel, false);
        Activate(_gameOverPanel, false);
        Activate(_levelCompletePanel, false);
        // Show/hide Continue depending on saved state
        if (_continueBtn != null)
            _continueBtn.SetActive(SaveSystem.HasSave());
    }

    public void ShowHUD()
    {
        Activate(_mainMenuPanel, false);
        Activate(_hudPanel, true);
        Activate(_gameOverPanel, false);
        Activate(_levelCompletePanel, false);
    }

    public void ShowGameOver(int finalScore)
    {
        _goScoreTxt.text = $"Score: {finalScore:N0}";
        Activate(_gameOverPanel, true);
        StartCoroutine(PopIn(_gameOverPanel.GetComponent<RectTransform>()));
    }

    public void ShowLevelComplete(int level)
    {
        _lcLevelTxt.text = $"Level {level} Complete!";
        Activate(_levelCompletePanel, true);
        StartCoroutine(PopIn(_levelCompletePanel.GetComponent<RectTransform>()));
    }

    public void HideLevelComplete() => Activate(_levelCompletePanel, false);

    public void UpdateScore(int v)
    {
        _scoreTxt.text = $"<b>{v:N0}</b>\n<size=65%>SCORE</size>";
        if (_scorePulseCo != null) StopCoroutine(_scorePulseCo);
        _scorePulseCo = StartCoroutine(PulseText(_scoreTxt.transform));
        UpdateProgressBar(v);
    }

    public void UpdateBest(int v) => _bestTxt.text = $"<b>{v:N0}</b>\n<size=65%>BEST</size>";
    public void UpdateLevel(int v) => _levelTxt.text = $"<size=70%>LEVEL</size>\n<b>{v}</b>";

    public void UpdateTarget(int v)
    {
        _progressTarget = v;
        _targetTxt.text = $"Target: <b>{v:N0}</b>";
    }

    public void SpawnScorePopup(int pts, Vector3 worldPos)
        => StartCoroutine(ScorePopupRoutine(pts, worldPos));

    // ── Refresh Continue button visibility (called by GameManager on save) ──
    public void RefreshContinueButton()
    {
        if (_continueBtn != null)
            _continueBtn.SetActive(SaveSystem.HasSave());
    }

    // ── Canvas ───────────────────────────────────────────────
    private void BuildCanvas()
    {
        var go = new GameObject("UICanvas");
        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;

        var cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1080, 1920);
        cs.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();
    }

    private Canvas _bgCanvas;
    private Image _bgCrossfade1;
    private Image _bgCrossfade2;

    private void BuildBackgroundCanvas()
    {
        var go = new GameObject("BgCanvas");
        _bgCanvas = go.AddComponent<Canvas>();
        _bgCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        _bgCanvas.worldCamera = Camera.main;
        _bgCanvas.planeDistance = 10f;
        _bgCanvas.sortingOrder = GameConstants.SORT_BG - 1; // Orbs are on same canvas, behind grid

        var cs = go.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1080, 1920);
        cs.matchWidthOrHeight = 0.5f;

        var sprites = Bootstrap.GlobalBackgroundSprites;
        if (sprites != null && sprites.Length > 0)
        {
            var bg1Go = MakeFullRect("BgCrossfade1", go.transform);
            _bgCrossfade1 = bg1Go.AddComponent<Image>();
            _bgCrossfade1.sprite = sprites[0];
            _bgCrossfade1.color = Color.white;
            _bgCrossfade1.preserveAspect = false;

            var bg2Go = MakeFullRect("BgCrossfade2", go.transform);
            _bgCrossfade2 = bg2Go.AddComponent<Image>();
            _bgCrossfade2.color = new Color(1f, 1f, 1f, 0f);
            _bgCrossfade2.preserveAspect = false;

            if (sprites.Length > 1)
                StartCoroutine(CrossfadeBackgroundSprites(sprites));
        }

        BuildFloatingOrbs(go.transform, 14);
    }



    // ── Safe-area panel (all interactive content lives here) ─
    private void BuildSafeAreaPanel()
    {
        var go = new GameObject("SafeArea");
        _safeAreaRT = go.AddComponent<RectTransform>();
        _safeAreaRT.SetParent(_canvas.transform, false);
        ApplySafeArea();
    }

    private void ApplySafeArea()
    {
        Rect sa = Screen.safeArea;
        Vector2 min = new Vector2(sa.x / Screen.width, sa.y / Screen.height);
        Vector2 max = new Vector2((sa.x + sa.width) / Screen.width,
                                    (sa.y + sa.height) / Screen.height);
        _safeAreaRT.anchorMin = min;
        _safeAreaRT.anchorMax = max;
        _safeAreaRT.offsetMin = Vector2.zero;
        _safeAreaRT.offsetMax = Vector2.zero;
    }

    // ── HUD ──────────────────────────────────────────────────
    private void BuildHUD()
    {
        _hudPanel = MakeFullRect("HUD", _safeAreaRT);

        // ── Top gradient bar ────────────────────────────────
        var barRT = MakeChildRT("TopBar", _hudPanel.transform);
        barRT.anchorMin = new Vector2(0f, 1f);
        barRT.anchorMax = new Vector2(1f, 1f);
        barRT.pivot = new Vector2(0.5f, 1f);
        barRT.anchoredPosition = Vector2.zero;
        barRT.sizeDelta = new Vector2(0f, 192f);

        // Gradient illusion: dark bar with a coloured left strip
        barRT.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

        // Coloured accent line at the very bottom of the bar
        var accent = MakeChildRT("BarAccent", barRT);
        accent.anchorMin = Vector2.zero;
        accent.anchorMax = new Vector2(1f, 0f);
        accent.pivot = new Vector2(0.5f, 0f);
        accent.anchoredPosition = Vector2.zero;
        accent.sizeDelta = new Vector2(0f, 3f);
        accent.gameObject.AddComponent<Image>().color = new Color(0.72f, 0.18f, 1f);

        // Score (left)
        _scoreTxt = MakeTMPStretch("ScoreTxt", barRT,
                                    "<b>0</b>\n<size=65%>SCORE</size>", 72f,
                                    0f, 0f, 0.33f, 1f, 8f, 6f, -4f, -6f);
        _scoreTxt.alignment = TextAlignmentOptions.Center;
        _scoreTxt.color = new Color(1f, 0.92f, 0.28f);

        // Level (centre)
        _levelTxt = MakeTMPStretch("LevelTxt", barRT,
                                    "<size=70%>LEVEL</size>\n<b>1</b>", 72f,
                                    0.33f, 0f, 0.67f, 1f, 0f, 6f, 0f, -6f);
        _levelTxt.alignment = TextAlignmentOptions.Center;
        _levelTxt.color = Color.white;

        // Best (right)
        _bestTxt = MakeTMPStretch("BestTxt", barRT,
                                   "<b>0</b>\n<size=65%>BEST</size>", 72f,
                                   0.67f, 0f, 1f, 1f, 4f, 6f, -8f, -6f);
        _bestTxt.alignment = TextAlignmentOptions.Center;
        _bestTxt.color = new Color(0.6f, 1f, 0.6f);

        // ── Target + progress strip (below bar) ─────────────
        var tgtRT = MakeChildRT("TargetStrip", _hudPanel.transform);
        tgtRT.anchorMin = new Vector2(0f, 1f);
        tgtRT.anchorMax = new Vector2(1f, 1f);
        tgtRT.pivot = new Vector2(0.5f, 1f);
        tgtRT.anchoredPosition = new Vector2(0f, -192f);
        tgtRT.sizeDelta = new Vector2(0f, 76f);
        tgtRT.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

        _targetTxt = MakeTMPStretch("TargetTxt", tgtRT,
                                     "Target: <b>1000</b>", 48f,
                                     0f, 0f, 0.55f, 1f, 10f, 2f, -4f, -2f);
        _targetTxt.alignment = TextAlignmentOptions.MidlineLeft;
        _targetTxt.color = new Color(1f, 0.92f, 0.28f);

        // ── Progress bar (right portion of target strip) ────
        var progBG = MakeChildRT("ProgressBG", tgtRT);
        progBG.anchorMin = new Vector2(0.55f, 0.1f);
        progBG.anchorMax = new Vector2(1f, 0.9f);
        progBG.offsetMin = new Vector2(0f, 0f);
        progBG.offsetMax = new Vector2(-10f, 0f);
        var progBGImg = progBG.gameObject.AddComponent<Image>();
        progBGImg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

        var progFill = MakeChildRT("ProgressFill", progBG);
        progFill.anchorMin = Vector2.zero;
        progFill.anchorMax = new Vector2(0f, 1f);   // width controlled at runtime
        progFill.offsetMin = Vector2.zero;
        progFill.offsetMax = Vector2.zero;
        _progressFillRT = progFill;
        _progressFillImg = progFill.gameObject.AddComponent<Image>();
        _progressFillImg.color = new Color(0.2f, 0.85f, 0.35f);
    }

    // ── Main Menu ────────────────────────────────────────────
    private void BuildMainMenu()
    {
        _mainMenuPanel = MakeFullRect("MainMenu", _safeAreaRT);

        // Semi-transparent overlay sits on top of the animated background
        _mainMenuPanel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);

        // ── Floating candy orbs (behind text, inside menu panel) ─
        BuildFloatingOrbs(_mainMenuPanel.transform, 14);

        // ── Title ───────────────────────────────────────────
        var title = MakeTMPCentre("Title", _mainMenuPanel.transform,
                                   "CANDY\nCRUSH", 116f, 0f, 200f, 820f, 310f);
        title.alignment = TextAlignmentOptions.Center;
        title.fontStyle = FontStyles.Bold | FontStyles.Italic;
        title.color = new Color(1f, 0.88f, 0.08f);
        StartCoroutine(RainbowText(title));

        // ── Subtitle ────────────────────────────────────────
        var sub = MakeTMPCentre("Sub", _mainMenuPanel.transform,
                                 "Match-3 Puzzle  ·  Endless", 34f,
                                 0f, 100f, 720f, 52f);
        sub.alignment = TextAlignmentOptions.Center;
        sub.color = new Color(0.82f, 0.82f, 1f, 0.82f);

        // ── Candy dot row ────────────────────────────────────
        Color[] cc = {
            new Color(1f,.25f,.25f), new Color(.3f,.55f,1f),
            new Color(.2f,.80f,.3f), new Color(1f,.90f,.08f),
            new Color(.72f,.18f,1f), new Color(1f,.54f,.04f)
        };
        for (int i = 0; i < cc.Length; i++)
        {
            var dGO = new GameObject("Dot_" + i);
            var dRT = dGO.AddComponent<RectTransform>();
            dRT.SetParent(_mainMenuPanel.transform, false);
            dRT.anchorMin = dRT.anchorMax = new Vector2(0.5f, 0.5f);
            dRT.pivot = new Vector2(0.5f, 0.5f);
            dRT.anchoredPosition = new Vector2(-125f + i * 50f, 38f);
            dRT.sizeDelta = new Vector2(40f, 40f);
            var img = dGO.AddComponent<Image>();
            img.color = cc[i];
            StartCoroutine(PulseGameObject(dGO.transform, 1.5f + i * 0.2f, 0.08f));
        }

        // ── Best-score display ───────────────────────────────
        var bestDisp = MakeTMPCentre("BestDisp", _mainMenuPanel.transform,
                                      $"Best: {ScoreManager.Instance.BestScore:N0}",
                                      30f, 0f, -52f, 500f, 46f);
        bestDisp.alignment = TextAlignmentOptions.Center;
        bestDisp.color = new Color(0.75f, 1f, 0.75f, 0.9f);

        bool hasSave = SaveSystem.HasSave();

        // ── START button (only if save exists) ──
        _continueBtn = MakeButtonGO("START", _mainMenuPanel.transform,
                                     new Color(0.18f, 0.65f, 0.90f),
                                     new Vector2(0f, -140f), new Vector2(440f, 100f));

        _continueBtn.GetComponentInChildren<Button>().onClick.AddListener(
            () => OnContinueGame?.Invoke());

        _continueBtn.SetActive(hasSave);

        StartCoroutine(PulseGameObject(_continueBtn.transform, 2.0f, 0.03f));


        // ── NEW GAME button (only first install) ──
        var newGameBtn = MakeButtonGO("NEW GAME", _mainMenuPanel.transform,
                                       new Color(0.15f, 0.72f, 0.30f),
                                       new Vector2(0f, -260f), new Vector2(440f, 100f));

        newGameBtn.GetComponentInChildren<Button>().onClick.AddListener(
            () => OnStartNewGame?.Invoke());

        newGameBtn.SetActive(!hasSave);

        StartCoroutine(PulseGameObject(newGameBtn.transform, 2.2f, 0.03f));
    }

    // ── Game Over ────────────────────────────────────────────
    private void BuildGameOverPanel()
    {
        _gameOverPanel = MakeFullRect("GameOver", _canvas.transform);
        _gameOverPanel.AddComponent<Image>().color = new Color(0.05f, 0.02f, 0.12f, 0.94f);
        _gameOverPanel.SetActive(false);

        var t1 = MakeTMPCentre("GOTitle", _gameOverPanel.transform,
                                 "GAME\nOVER", 88f, 0f, 290f, 740f, 240f);
        t1.alignment = TextAlignmentOptions.Center;
        t1.fontStyle = FontStyles.Bold | FontStyles.Italic;
        t1.color = new Color(1f, 0.28f, 0.28f);

        _goScoreTxt = MakeTMPCentre("GOScore", _gameOverPanel.transform,
                                      "Score: 0", 42f, 0f, 120f, 640f, 65f);
        _goScoreTxt.alignment = TextAlignmentOptions.Center;
        _goScoreTxt.color = new Color(1f, 0.92f, 0.28f);

        var restartGO = MakeButtonGO("PLAY AGAIN", _gameOverPanel.transform,
                                      new Color(0.82f, 0.20f, 0.12f),
                                      new Vector2(0f, -60f), new Vector2(440f, 100f));
        restartGO.GetComponentInChildren<Button>().onClick.AddListener(
            () => OnRestartPressed?.Invoke());
    }

    // ── Level Complete ───────────────────────────────────────
    private void BuildLevelCompletePanel()
    {
        _levelCompletePanel = MakeFullRect("LevelComplete", _canvas.transform);
        _levelCompletePanel.AddComponent<Image>().color = new Color(0.02f, 0.12f, 0.05f, 0.93f);
        _levelCompletePanel.SetActive(false);

        // Stars row
        for (int i = 0; i < 3; i++)
        {
            var sGO = new GameObject("Star_" + i);
            var sRT = sGO.AddComponent<RectTransform>();
            sRT.SetParent(_levelCompletePanel.transform, false);
            sRT.anchorMin = sRT.anchorMax = new Vector2(0.5f, 0.5f);
            sRT.pivot = new Vector2(0.5f, 0.5f);
            sRT.anchoredPosition = new Vector2(-80f + i * 80f, 300f);
            sRT.sizeDelta = new Vector2(64f, 64f);
            sGO.AddComponent<Image>().color = new Color(1f, 0.88f, 0.08f);
        }

        _lcLevelTxt = MakeTMPCentre("LCTitle", _levelCompletePanel.transform,
                                      "Level 1\nComplete!", 68f, 0f, 150f, 880f, 200f);
        _lcLevelTxt.alignment = TextAlignmentOptions.Center;
        _lcLevelTxt.fontStyle = FontStyles.Bold | FontStyles.Italic;
        _lcLevelTxt.color = new Color(0.22f, 1f, 0.50f);

        var lcSub = MakeTMPCentre("LCSub", _levelCompletePanel.transform,
                                   "Next level loading\u2026", 30f, 0f, 20f, 640f, 54f);
        lcSub.alignment = TextAlignmentOptions.Center;
        lcSub.color = new Color(0.80f, 0.95f, 0.80f, 0.90f);
    }

    // ── Floating orbs (main menu ambiance) ───────────────────
    public void BuildFloatingOrbs(Transform parent, int count)
    {
        Color[] cc = {
            new Color(1f,.25f,.25f,.0f), new Color(.3f,.55f,1f,.0f),
            new Color(.2f,.80f,.3f,.0f), new Color(1f,.90f,.08f,.0f),
            new Color(.72f,.18f,1f,.0f), new Color(1f,.54f,.04f,.0f),
            new Color(1f,.42f,.82f,.0f)
        };
        for (int i = 0; i < count; i++)
        {
            var orbGO = new GameObject("Orb_" + i);
            var orbRT = orbGO.AddComponent<RectTransform>();
            orbRT.SetParent(parent, false);
            orbRT.anchorMin = orbRT.anchorMax = new Vector2(0.5f, 0.5f);
            orbRT.pivot = new Vector2(0.5f, 0.5f);
            float sz = Random.Range(30f, 80f);
            orbRT.sizeDelta = new Vector2(sz, sz);
            var img = orbGO.AddComponent<Image>();
            img.color = cc[i % cc.Length];
            StartCoroutine(FloatOrb(orbRT, img, cc[i % cc.Length]));
        }
    }

    // ── Score popup (world-space TextMeshPro) ─────────────────
    private IEnumerator ScorePopupRoutine(int pts, Vector3 worldPos)
    {
        var go = new GameObject("ScorePopup");
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = $"+{pts}";
        tmp.fontSize = 3.8f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.outlineWidth = 0.24f;
        tmp.outlineColor = Color.black;
        go.transform.position = worldPos;
        var mr = go.GetComponent<MeshRenderer>();
        if (mr) mr.sortingOrder = GameConstants.SORT_POPUP;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / GameConstants.DUR_POPUP;
            float e = EaseOutCubic(Mathf.Clamp01(t));
            go.transform.position = worldPos + Vector3.up * (e * 1.8f);
            float sc = 1f + Mathf.Sin(e * Mathf.PI) * 0.35f;
            go.transform.localScale = Vector3.one * sc;
            float alpha = 1f - Mathf.Clamp01((t - 0.42f) * 1.7f);
            tmp.color = new Color(1f, Mathf.Lerp(1f, 0.78f, t), Mathf.Lerp(0.12f, 0.02f, t), alpha);
            yield return null;
        }
        Destroy(go);
    }

    // ── Progress bar update ──────────────────────────────────
    private void UpdateProgressBar(int current)
    {
        if (_progressFillRT == null) return;
        float p = _progressTarget > 0 ? Mathf.Clamp01((float)current / _progressTarget) : 0f;
        _progressFillRT.anchorMax = new Vector2(p, 1f);

        // Colour: red → orange → yellow → green
        Color from, to;
        if (p < 0.5f) { from = new Color(0.9f, 0.2f, 0.1f); to = new Color(1f, 0.75f, 0.1f); p = p * 2f; }
        else { from = new Color(1f, 0.75f, 0.1f); to = new Color(0.2f, 0.9f, 0.3f); p = (p - 0.5f) * 2f; }
        _progressFillImg.color = Color.Lerp(from, to, p);
    }

    // ── Panel pop-in ─────────────────────────────────────────
    private static IEnumerator PopIn(RectTransform rt)
    {
        rt.localScale = Vector3.zero;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.28f;
            rt.localScale = Vector3.one * EaseOutBack(Mathf.Clamp01(t));
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    // ── Animation coroutines ─────────────────────────────────
    private IEnumerator CrossfadeBackgroundSprites(Sprite[] sprites)
    {
        int idx = 0;
        while (true)
        {
            yield return new WaitForSeconds(6f); // 6 seconds per image

            int nextIdx = (idx + 1) % sprites.Length;
            _bgCrossfade2.sprite = sprites[nextIdx];

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 1.5f; // 1.5s crossfade duration
                _bgCrossfade2.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t));
                yield return null;
            }

            _bgCrossfade1.sprite = sprites[nextIdx];
            _bgCrossfade1.color = Color.white;
            _bgCrossfade2.color = new Color(1f, 1f, 1f, 0f);

            idx = nextIdx;
        }
    }

    private IEnumerator FloatOrb(RectTransform rt, Image img, Color col)
    {
        yield return new WaitForSeconds(Random.Range(0f, 3f));
        while (true)
        {
            float startX = Random.Range(-480f, 480f);
            float startY = -1050f;
            float endY = 1050f;
            float speed = Random.Range(60f, 130f);
            float wobble = Random.Range(15f, 45f);
            float wFreq = Random.Range(0.4f, 1.2f);
            float dur = (endY - startY) / speed;
            float el = 0f;

            while (el < dur)
            {
                el += Time.deltaTime;
                float progress = el / dur;
                float x = startX + Mathf.Sin(el * wFreq) * wobble;
                float y = Mathf.Lerp(startY, endY, progress);
                rt.anchoredPosition = new Vector2(x, y);
                float alpha = Mathf.Sin(progress * Mathf.PI) * 0.45f;
                img.color = new Color(col.r, col.g, col.b, alpha);
                yield return null;
            }
            yield return new WaitForSeconds(Random.Range(0f, 1.5f));
        }
    }

    private static IEnumerator RainbowText(TextMeshProUGUI tmp)
    {
        float hue = 0f;
        while (true)
        {
            hue = (hue + Time.deltaTime * 0.12f) % 1f;
            tmp.color = Color.HSVToRGB(hue, 0.72f, 1f);
            yield return null;
        }
    }

    private static IEnumerator PulseText(Transform t)
    {
        float time = 0f;
        while (time < 1f)
        {
            time += Time.deltaTime / 0.30f;
            float s = 1f + Mathf.Sin(time * Mathf.PI) * 0.18f;
            t.localScale = Vector3.one * s;
            yield return null;
        }
        t.localScale = Vector3.one;
    }

    private static IEnumerator PulseGameObject(Transform t, float period, float amount)
    {
        float time = Random.Range(0f, period);
        while (true)
        {
            time += Time.deltaTime;
            float s = 1f + Mathf.Sin(time / period * Mathf.PI * 2f) * amount;
            t.localScale = Vector3.one * s;
            yield return null;
        }
    }

    // ── Layout factories ─────────────────────────────────────
    private static GameObject MakeFullRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go;
    }

    private static RectTransform MakeChildRT(string name, Transform parent)
    {
        var go = new GameObject(name);
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    private static TextMeshProUGUI MakeTMPStretch(string name, RectTransform parent,
        string text, float fontSize,
        float minX, float minY, float maxX, float maxY,
        float offL, float offB, float offR, float offT)
    {
        var go = new GameObject(name);
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.offsetMin = new Vector2(offL, offB);
        rt.offsetMax = new Vector2(offR, offT);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        return tmp;
    }

    private static TextMeshProUGUI MakeTMPCentre(string name, Transform parent,
        string text, float fontSize,
        float ax, float ay, float width, float height)
    {
        var go = new GameObject(name);
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(ax, ay);
        rt.sizeDelta = new Vector2(width, height);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        return tmp;
    }

    // Returns the parent GO containing the button; Button component is on a child.
    private static GameObject MakeButtonGO(string label, Transform parent,
                                             Color col, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject("Btn_" + label);
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        // Glow behind button (slightly larger, faded version of col)
        var glow = new GameObject("Glow");
        var grt = glow.AddComponent<RectTransform>();
        grt.SetParent(go.transform, false);
        grt.anchorMin = new Vector2(-0.06f, -0.12f);
        grt.anchorMax = new Vector2(1.06f, 1.12f);
        grt.offsetMin = Vector2.zero;
        grt.offsetMax = Vector2.zero;
        glow.AddComponent<Image>().color = new Color(col.r, col.g, col.b, 0.30f);

        // Main button face
        var face = new GameObject("Face");
        var frt = face.AddComponent<RectTransform>();
        frt.SetParent(go.transform, false);
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.offsetMin = Vector2.zero;
        frt.offsetMax = Vector2.zero;
        face.AddComponent<Image>().color = col;

        var btn = face.AddComponent<Button>();
        var cb = btn.colors;
        cb.normalColor = col;
        cb.highlightedColor = col * 1.28f;
        cb.pressedColor = col * 0.68f;
        cb.selectedColor = col;
        btn.colors = cb;

        // Label
        var lgo = new GameObject("Label");
        var lrt = lgo.AddComponent<RectTransform>();
        lrt.SetParent(face.transform, false);
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
        var txt = lgo.AddComponent<TextMeshProUGUI>();
        txt.text = label;
        txt.fontSize = 44f;
        txt.fontStyle = FontStyles.Bold;
        txt.color = Color.white;
        txt.alignment = TextAlignmentOptions.Center;

        return go;
    }

    // ── Easing ───────────────────────────────────────────────
    private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
    private static float EaseOutBack(float t)
    { t -= 1f; const float s = 1.70158f; return t * t * ((s + 1f) * t + s) + 1f; }
    private static float EaseInOutSine(float t)
        => -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;

    // ── Utility ──────────────────────────────────────────────
    private static void Activate(GameObject go, bool on)
    { if (go != null) go.SetActive(on); }
}
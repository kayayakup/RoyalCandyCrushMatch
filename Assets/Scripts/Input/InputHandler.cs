// ============================================================
// InputHandler.cs
// Uses ONLY the New Input System (com.unity.inputsystem).
// No reference to UnityEngine.Input anywhere.
// ============================================================
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class InputHandler : MonoBehaviour
{
    public static InputHandler Instance { get; private set; }

    // ── Events ───────────────────────────────────────────────
    // Fired when the player completes a swipe gesture over the grid.
    public event Action<Vector2Int, Vector2Int> OnSwipeDetected;
    // Fired on a tap (short press without significant drag).
    public event Action<Vector2Int>             OnTapDetected;

    // ── Config ───────────────────────────────────────────────
    // Minimum pixel distance for a press→release to count as a swipe.
    private const float MIN_DRAG_PX = 22f;

    // ── Internal state ────────────────────────────────────────
    private Camera     _cam;
    private bool       _pressing;
    private Vector2    _pressScreenPos;
    private Vector2Int _pressCell;
    private bool       _enabled = true;

    private void Awake()
    {
        Instance   = this;
        _pressCell = new Vector2Int(-1, -1);
    }

    private void Start()
    {
        _cam = Bootstrap.MainCamera != null ? Bootstrap.MainCamera : Camera.main;
    }

    public void SetEnabled(bool on)
    {
        _enabled  = on;
        if (!on) _pressing = false;
    }

    // ── Update: poll whichever device is active ───────────────
    private void Update()
    {
        if (!_enabled) return;
        if (_cam == null)
        {
            _cam = Bootstrap.MainCamera != null ? Bootstrap.MainCamera : Camera.main;
            return;
        }
        PollMouse();
        PollTouch();
    }

    // ── Mouse ────────────────────────────────────────────────
    private void PollMouse()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
            BeginPress(mouse.position.ReadValue());

        if (mouse.leftButton.wasReleasedThisFrame && _pressing)
            EndPress(mouse.position.ReadValue());
    }

    // ── Touchscreen ──────────────────────────────────────────
    private void PollTouch()
    {
        var ts = Touchscreen.current;
        if (ts == null) return;

        var touch = ts.primaryTouch;

        if (touch.press.wasPressedThisFrame)
            BeginPress(touch.position.ReadValue());

        if (touch.press.wasReleasedThisFrame && _pressing)
            EndPress(touch.position.ReadValue());
    }

    // ── Common press / release logic ─────────────────────────
    private void BeginPress(Vector2 screenPos)
    {
        _pressScreenPos = screenPos;
        _pressCell      = ScreenToCell(screenPos);
        _pressing       = true;
    }

    private void EndPress(Vector2 releasePos)
    {
        _pressing = false;
        if (_pressCell.x < 0) return;          // press began outside grid

        Vector2 delta = releasePos - _pressScreenPos;

        if (delta.magnitude < MIN_DRAG_PX)
        {
            // Short press = tap
            OnTapDetected?.Invoke(_pressCell);
        }
        else
        {
            // Determine swipe direction from the dominant axis of the pixel delta
            Vector2Int dir     = PixelDeltaToDir(delta);
            Vector2Int endCell = _pressCell + dir;
            if (IsValid(_pressCell) && IsValid(endCell))
                OnSwipeDetected?.Invoke(_pressCell, endCell);
        }

        _pressCell = new Vector2Int(-1, -1);
    }

    // ── Coordinate conversion ────────────────────────────────
    // Converts a Unity screen-space position (Y=0 at bottom, Y=Screen.height at top)
    // to a grid cell index via the orthographic camera.
    private Vector2Int ScreenToCell(Vector2 tapScreenPos)
    {
        if (_cam == null || GridManager.Instance == null)
            return new Vector2Int(-1, -1);

        float bestDistSq = float.MaxValue;
        Vector2Int bestCell = new Vector2Int(-1, -1);

        for (int x = 0; x < GameConstants.GRID_WIDTH; x++)
            for (int y = 0; y < GameConstants.GRID_HEIGHT; y++)
            {
                Vector3 worldPos = GridManager.Instance.CellToWorld(x, y);
                Vector3 screenPos = _cam.WorldToScreenPoint(worldPos);
                float dx = screenPos.x - tapScreenPos.x;
                float dy = screenPos.y - tapScreenPos.y;
                float distSq = dx * dx + dy * dy;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestCell = new Vector2Int(x, y);
                }
            }

        // Reject taps that land outside the grid (e.g. in the HUD).
        // Threshold = 65 % of one tile's screen-pixel width.
        Vector3 s0 = _cam.WorldToScreenPoint(GridManager.Instance.CellToWorld(0, 0));
        Vector3 s1 = _cam.WorldToScreenPoint(GridManager.Instance.CellToWorld(1, 0));
        float maxDistPx = Mathf.Abs(s1.x - s0.x) * 0.65f;
        if (bestDistSq > maxDistPx * maxDistPx)
            return new Vector2Int(-1, -1);

        return bestCell;
    }

    // ── Helpers ──────────────────────────────────────────────
    // Maps a pixel-space drag delta to one of the four cardinal grid directions.
    private static Vector2Int PixelDeltaToDir(Vector2 delta)
    {
        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            return delta.x > 0f ? Vector2Int.right : Vector2Int.left;
        // Screen Y in Unity increases upward, which matches world Y → no flip needed.
        return delta.y > 0f ? Vector2Int.up : Vector2Int.down;
    }

    private static bool IsValid(Vector2Int c)
        => c.x >= 0 && c.x < GameConstants.GRID_WIDTH
        && c.y >= 0 && c.y < GameConstants.GRID_HEIGHT;
}

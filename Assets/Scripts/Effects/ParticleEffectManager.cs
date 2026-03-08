// ============================================================
// ParticleEffectManager.cs
// ============================================================
using System.Collections.Generic;
using UnityEngine;

public class ParticleEffectManager : MonoBehaviour
{
    public static ParticleEffectManager Instance { get; private set; }

    private readonly Dictionary<TileType, ParticleSystem> _bursts =
        new Dictionary<TileType, ParticleSystem>();

    private ParticleSystem _obstacleBurst;
    private ParticleSystem _levelUpBurst;

    private static readonly (TileType t, Color c)[] Map =
    {
        (TileType.Red,    new Color(1.00f, 0.24f, 0.24f)),
        (TileType.Blue,   new Color(0.28f, 0.54f, 1.00f)),
        (TileType.Green,  new Color(0.18f, 0.82f, 0.32f)),
        (TileType.Yellow, new Color(1.00f, 0.92f, 0.08f)),
        (TileType.Purple, new Color(0.72f, 0.18f, 1.00f)),
        (TileType.Orange, new Color(1.00f, 0.54f, 0.04f)),
        (TileType.Pink,   new Color(1.00f, 0.42f, 0.82f)),
    };

    private void Awake()
    {
        Instance = this;
        foreach (var (t, c) in Map)
            _bursts[t] = Build($"Burst_{t}", c, 14, 0.55f, 3.0f);
        _obstacleBurst = Build("ObstacleBurst", new Color(0.9f, 0.75f, 0.1f), 20, 0.65f, 3.2f);
        _levelUpBurst  = Build("LevelUpBurst",  Color.white,                   40, 1.20f, 5.0f);
    }

    public void PlayMatchBurst(TileType t, Vector3 pos)
    { if (_bursts.TryGetValue(t, out var ps)) Fire(ps, pos); }

    public void PlayObstacleBurst(Vector3 pos) => Fire(_obstacleBurst, pos);
    public void PlayLevelUpBurst (Vector3 pos) => Fire(_levelUpBurst,  pos);

    private static void Fire(ParticleSystem ps, Vector3 pos)
    {
        if (ps == null) return;
        ps.transform.position = pos;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.Play();
    }

    private ParticleSystem Build(string goName, Color col,
                                  int count, float life, float speed)
    {
        var go   = new GameObject(goName);
        go.transform.SetParent(transform, false);
        var ps   = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop            = false;
        main.playOnAwake     = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(life * 0.55f, life);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(speed * 0.45f, speed);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.05f, 0.22f);
        main.startColor      = new ParticleSystem.MinMaxGradient(col, Color.white);
        main.gravityModifier = 0.40f;
        main.maxParticles    = count * 2;

        var em = ps.emission;
        em.rateOverTime = 0;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius    = 0.22f;

        var clt = ps.colorOverLifetime;
        clt.enabled = true;
        var grad    = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f),          new GradientAlphaKey(0f, 1f) });
        clt.color = grad;

        var sz    = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size    = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.7f, 0.6f), new Keyframe(1f, 0f)));

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode   = ParticleSystemRenderMode.Billboard;
        rend.sortingOrder = 15;
        rend.material     = MakeParticleMat();
        return ps;
    }

    private static Material MakeParticleMat()
    {
        string[] candidates =
        {
            "Universal Render Pipeline/Particles/Unlit",
            "Particles/Standard Unlit",
            "Mobile/Particles/Additive",
            "Legacy Shaders/Particles/Additive",
            "Sprites/Default",
            "Unlit/Color"
        };
        foreach (var n in candidates)
        {
            var s = Shader.Find(n);
            if (s != null) return new Material(s);
        }
        return new Material(Shader.Find("Standard"));
    }
}

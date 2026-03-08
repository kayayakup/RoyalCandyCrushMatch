// ============================================================
// AudioGenerator.cs  –  all SFX generated from sine waves
// ============================================================
using UnityEngine;

public class AudioGenerator : MonoBehaviour
{
    public static AudioGenerator Instance { get; private set; }

    private const int SR = 44100;

    private AudioSource _src;
    private AudioClip _clipSwap, _clipMatch, _clipFall,
                      _clipObstacle, _clipLevelUp, _clipGameOver;

    private void Awake()
    {
        Instance         = this;
        _src             = gameObject.AddComponent<AudioSource>();
        _src.playOnAwake = false;
        _src.volume      = 0.70f;

        _clipSwap      = BuildSwap();
        _clipMatch     = BuildMatch();
        _clipFall      = BuildFall();
        _clipObstacle  = BuildObstacle();
        _clipLevelUp   = BuildLevelUp();
        _clipGameOver  = BuildGameOver();
    }

    public void PlaySwap()      => Play(_clipSwap,     0.55f);
    public void PlayMatch()     => Play(_clipMatch,    0.70f);
    public void PlayFall()      => Play(_clipFall,     0.38f);
    public void PlayObstacle()  => Play(_clipObstacle, 0.80f);
    public void PlayLevelUp()   => Play(_clipLevelUp,  0.90f);
    public void PlayGameOver()  => Play(_clipGameOver, 0.80f);

    private void Play(AudioClip clip, float vol)
    { if (clip != null) _src.PlayOneShot(clip, vol); }

    // ── Builders ─────────────────────────────────────────────
    private static AudioClip BuildSwap()
        => Sequence(new[] { 380f, 520f }, new[] { 0.08f, 0.09f }, DecayEnv);

    private static AudioClip BuildMatch()
        => Sequence(new[] { 261.6f, 329.6f, 392.0f, 523.2f },
                    new[] { 0.07f,  0.07f,  0.07f,  0.16f  }, DecayEnv);

    private static AudioClip BuildFall()
    {
        int n = (int)(0.13f * SR);
        float[] d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            float f = Mathf.Lerp(380f, 130f, (float)i / n);
            d[i] = Mathf.Sin(Mathf.PI * 2f * f * t) * Mathf.Exp(-14f * t) * 0.35f;
        }
        return MakeClip("Fall", d);
    }

    private static AudioClip BuildObstacle()
    {
        int n = (int)(0.18f * SR);
        float[] d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t   = (float)i / SR;
            float env = Mathf.Exp(-11f * t);
            d[i] = ((Random.value * 2f - 1f) * 0.38f
                  + Mathf.Sin(Mathf.PI * 2f * 200f * t) * 0.38f) * env * 0.62f;
        }
        return MakeClip("Obstacle", d);
    }

    private static AudioClip BuildLevelUp()
        => Sequence(new[] { 261.6f, 329.6f, 392f, 523.2f, 659.2f },
                    new[] { 0.10f,  0.10f, 0.10f, 0.12f,  0.30f  }, SlowEnv);

    private static AudioClip BuildGameOver()
        => Sequence(new[] { 392f, 349.2f, 311.1f, 261.6f },
                    new[] { 0.14f, 0.14f, 0.14f,  0.36f  }, SlowEnv);

    // ── Core helpers ─────────────────────────────────────────
    private delegate float EnvFn(float norm);
    private static float DecayEnv(float n) => Mathf.Exp(-6f * n);
    private static float SlowEnv (float n) => Mathf.Exp(-2.5f * n);

    private static AudioClip Sequence(float[] freqs, float[] durs, EnvFn env)
    {
        int total = 0;
        foreach (float d in durs) total += (int)(d * SR);
        float[] data = new float[total];
        int pos = 0;
        for (int i = 0; i < freqs.Length; i++)
        {
            int seg = (int)(durs[i] * SR);
            for (int j = 0; j < seg; j++)
            {
                float t = (float)j / SR;
                data[pos + j] = Mathf.Sin(Mathf.PI * 2f * freqs[i] * t)
                                * env((float)j / seg) * 0.55f;
            }
            pos += seg;
        }
        return MakeClip("Seq", data);
    }

    private static AudioClip MakeClip(string name, float[] samples)
    {
        var c = AudioClip.Create(name, samples.Length, 1, SR, false);
        c.SetData(samples, 0);
        return c;
    }
}

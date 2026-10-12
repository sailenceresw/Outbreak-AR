using System;
using UnityEngine;

// Sound effects synthesized at runtime, so they need no imported audio files.
public static class GameAudio
{
    const int SampleRate = 44100;
    const float TwoPi = Mathf.PI * 2f;

    static AudioSource source;
    static readonly System.Random noiseRandom = new System.Random(1234);

    static AudioClip heartbeat, hitMarker, killMarker, dryFire, pickup, powerUp, siren, fanfare, impact;

    public static AudioClip Heartbeat => heartbeat != null ? heartbeat : heartbeat = Build("Heartbeat", 0.55f, t =>
        Thump(t) + 0.7f * Thump(t - 0.2f));

    public static AudioClip HitMarker => hitMarker != null ? hitMarker : hitMarker = Build("HitMarker", 0.07f, t =>
        Mathf.Sin(TwoPi * 1900f * t) * Mathf.Exp(-t * 70f) + Noise() * 0.2f * Mathf.Exp(-t * 120f));

    public static AudioClip KillMarker => killMarker != null ? killMarker : killMarker = Build("KillMarker", 0.18f, t =>
        Mathf.Sin(TwoPi * 1400f * t) * Mathf.Exp(-t * 25f) + Mathf.Sin(TwoPi * 2100f * t) * Mathf.Exp(-t * 30f) * 0.6f);

    public static AudioClip DryFire => dryFire != null ? dryFire : dryFire = Build("DryFire", 0.06f, t =>
        Noise() * Mathf.Exp(-t * 130f) + Mathf.Sin(TwoPi * 850f * t) * Mathf.Exp(-t * 90f) * 0.5f);

    public static AudioClip Pickup => pickup != null ? pickup : pickup = Build("Pickup", 0.4f, t =>
        Note(t, 0f, 880f, 0.15f) + Note(t, 0.12f, 1320f, 0.28f));

    public static AudioClip PowerUp => powerUp != null ? powerUp : powerUp = Build("PowerUp", 0.6f, t =>
    {
        // rising sweep from 300Hz to 1500Hz
        float phase = TwoPi * (300f * t + (1200f / 1.2f) * t * t);
        return (Mathf.Sin(phase) * 0.7f + Mathf.Sign(Mathf.Sin(phase)) * 0.15f) * Mathf.Clamp01((0.6f - t) * 6f);
    });

    public static AudioClip Siren => siren != null ? siren : siren = Build("Siren", 2.4f, t =>
    {
        // wailing pitch sweep, like an air-raid siren
        float phase = TwoPi * (700f * t - (200f / (TwoPi * 1.2f)) * Mathf.Cos(TwoPi * 1.2f * t));
        float envelope = Mathf.Clamp01(t * 3f) * Mathf.Clamp01((2.4f - t) * 2f);
        return (Mathf.Sin(phase) * 0.6f + Mathf.Sign(Mathf.Sin(phase)) * 0.2f) * envelope;
    });

    public static AudioClip Fanfare => fanfare != null ? fanfare : fanfare = Build("Fanfare", 1.1f, t =>
        Note(t, 0f, 523.25f, 0.18f) + Note(t, 0.15f, 659.25f, 0.18f) + Note(t, 0.3f, 783.99f, 0.18f) + Note(t, 0.45f, 1046.5f, 0.65f));

    // deep cinematic hit for level intros and boss arrivals
    public static AudioClip Impact => impact != null ? impact : impact = Build("Impact", 2f, t =>
        Mathf.Sin(TwoPi * 42f * t) * Mathf.Exp(-t * 1.8f)
        + Mathf.Sin(TwoPi * 63f * t) * Mathf.Exp(-t * 2.5f) * 0.5f
        + Noise() * Mathf.Exp(-t * 7f) * 0.35f);

    public static void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;
        if (source == null)
        {
            GameObject holder = new GameObject("GameAudio");
            source = holder.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }
        source.pitch = pitch;
        source.PlayOneShot(clip, volume);
    }

    static float Thump(float t)
    {
        if (t < 0f) return 0f;
        return Mathf.Sin(TwoPi * 55f * t) * Mathf.Exp(-t * 22f) + Mathf.Sin(TwoPi * 35f * t) * Mathf.Exp(-t * 16f) * 0.5f;
    }

    static float Note(float t, float start, float frequency, float length)
    {
        float local = t - start;
        if (local < 0f || local > length) return 0f;
        float envelope = Mathf.Clamp01(local * 80f) * Mathf.Clamp01((length - local) * 12f);
        return (Mathf.Sin(TwoPi * frequency * local) + Mathf.Sin(TwoPi * frequency * 2f * local) * 0.25f) * envelope * 0.6f;
    }

    static float Noise()
    {
        return (float)(noiseRandom.NextDouble() * 2.0 - 1.0);
    }

    static AudioClip Build(string name, float duration, Func<float, float> wave)
    {
        int count = Mathf.CeilToInt(duration * SampleRate);
        float[] samples = new float[count];
        float peak = 0.0001f;
        for (int i = 0; i < count; i++)
        {
            samples[i] = wave(i / (float)SampleRate);
            peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
        }

        // normalize so every effect has a similar loudness
        float gain = 0.9f / peak;
        for (int i = 0; i < count; i++) samples[i] *= gain;

        AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}

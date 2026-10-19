using System;
using UnityEngine;

// Sound effects synthesized at runtime, so they need no imported audio files.
public static class GameAudio
{
    const int SampleRate = 44100;
    const float TwoPi = Mathf.PI * 2f;

    static AudioSource source;
    static readonly System.Random noiseRandom = new System.Random(1234);

    static AudioClip heartbeat, hitMarker, killMarker, dryFire, pickup, powerUp, siren, fanfare, impact, explosion, throwWhoosh;
    static AudioClip footstep, ambience, spit, acidSplash;

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

    // sharp crack followed by a low rumble
    public static AudioClip Explosion => explosion != null ? explosion : explosion = Build("Explosion", 1.8f, t =>
        Noise() * Mathf.Exp(-t * 9f)
        + Noise() * Mathf.Exp(-t * 2.2f) * 0.45f
        + Mathf.Sin(TwoPi * (38f - 12f * t) * t) * Mathf.Exp(-t * 2.5f) * 0.9f);

    // pin click, then air rushing past
    public static AudioClip Throw => throwWhoosh != null ? throwWhoosh : throwWhoosh = Build("Throw", 0.45f, t =>
        Mathf.Sin(TwoPi * 2400f * t) * Mathf.Exp(-t * 160f) * 0.6f
        + Noise() * Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - 0.05f) / 0.4f)) * 0.5f);

    // a dragging, scuffed step on a hard floor
    public static AudioClip Footstep => footstep != null ? footstep : footstep = Build("Footstep", 0.22f, t =>
        Mathf.Sin(TwoPi * 70f * t) * Mathf.Exp(-t * 35f) * 0.8f
        + Noise() * Mathf.Exp(-t * 22f) * 0.6f * Mathf.Clamp01(t * 60f));

    // eight-second loop: low drone, wind and a distant metallic moan, quiet enough to sit under the music
    public static AudioClip Ambience => ambience != null ? ambience : ambience = Build("Ambience", 8f, t =>
    {
        float drone = Mathf.Sin(TwoPi * 41f * t) * 0.5f + Mathf.Sin(TwoPi * 61.5f * t + Mathf.Sin(TwoPi * 0.25f * t)) * 0.3f;
        float wind = Noise() * (0.25f + 0.2f * Mathf.Sin(TwoPi * 0.125f * t));
        float moanEnvelope = Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - 4.5f) / 2.5f)));
        float moan = Mathf.Sin(TwoPi * (220f - 30f * (t - 4.5f)) * t) * moanEnvelope * 0.25f;
        // fade the ends so the loop has no click
        float edge = Mathf.Clamp01(t * 4f) * Mathf.Clamp01((8f - t) * 4f);
        return (drone + wind * 0.6f + moan) * edge;
    });

    // wet hawk and spit
    public static AudioClip Spit => spit != null ? spit : spit = Build("Spit", 0.35f, t =>
        Noise() * Mathf.Exp(-t * 14f) * (0.6f + 0.4f * Mathf.Sin(TwoPi * 30f * t))
        + Mathf.Sin(TwoPi * (300f - 400f * t) * t) * Mathf.Exp(-t * 10f) * 0.5f);

    // acid sizzle on impact
    public static AudioClip AcidSplash => acidSplash != null ? acidSplash : acidSplash = Build("AcidSplash", 0.7f, t =>
        Noise() * Mathf.Exp(-t * 5f) * (0.5f + 0.5f * Mathf.Abs(Mathf.Sin(TwoPi * 90f * t)))
        + Mathf.Sin(TwoPi * 180f * t) * Mathf.Exp(-t * 18f) * 0.4f);

    // a positional sound at a point in the world, for things the player should locate by ear
    public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;
        GameObject holder = new GameObject("Sound " + clip.name);
        holder.transform.position = position;
        AudioSource source3D = holder.AddComponent<AudioSource>();
        Make3D(source3D);
        source3D.clip = clip;
        source3D.volume = volume;
        source3D.pitch = pitch;
        source3D.Play();
        UnityEngine.Object.Destroy(holder, clip.length / Mathf.Max(pitch, 0.1f) + 0.1f);
    }

    // full 3D: pans to the side the sound comes from and fades with distance
    public static void Make3D(AudioSource source3D)
    {
        source3D.spatialBlend = 1f;
        source3D.rolloffMode = AudioRolloffMode.Logarithmic;
        source3D.minDistance = 1f;
        source3D.maxDistance = 18f;
        source3D.dopplerLevel = 0f;
        source3D.spread = 40f;
    }

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

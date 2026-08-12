using UnityEngine;

/// <summary>
/// Generates simple runtime AudioClips so the game has music/SFX without imported assets.
/// Replace with real clips on AudioManager when available.
/// </summary>
public static class ProceduralAudio
{
    public static AudioClip Tone(string name, float frequency, float duration, float volume = 0.4f, int sampleRate = 22050)
    {
        int samples = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = 1f - (t / duration);
            data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * volume * envelope;
        }

        AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    public static AudioClip NoiseBurst(string name, float duration, float volume = 0.35f, int sampleRate = 22050)
    {
        int samples = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = Mathf.Exp(-t * 8f);
            data[i] = (Random.value * 2f - 1f) * volume * envelope;
        }

        AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    public static AudioClip Sweep(string name, float startFreq, float endFreq, float duration, float volume = 0.3f, int sampleRate = 22050)
    {
        int samples = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[samples];
        double phase = 0;
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)(samples - 1);
            float freq = Mathf.Lerp(startFreq, endFreq, t);
            phase += 2.0 * Mathf.PI * freq / sampleRate;
            float envelope = 1f - t;
            data[i] = (float)System.Math.Sin(phase) * volume * envelope;
        }

        AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    public static AudioClip CombatLoop(float intensity = 1f)
    {
        int sampleRate = 22050;
        float duration = 2f;
        int samples = Mathf.CeilToInt(duration * sampleRate);
        float[] data = new float[samples];
        float bass = 55f * intensity;
        float lead = 110f * intensity;
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sampleRate;
            float beat = (Mathf.Sin(2f * Mathf.PI * 2f * t) > 0f) ? 1f : 0.35f;
            float wave = Mathf.Sin(2f * Mathf.PI * bass * t) * 0.22f
                + Mathf.Sin(2f * Mathf.PI * lead * t) * 0.12f * beat
                + Mathf.Sin(2f * Mathf.PI * (lead * 1.5f) * t) * 0.05f;
            data[i] = wave;
        }

        AudioClip clip = AudioClip.Create("CombatLoop", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}

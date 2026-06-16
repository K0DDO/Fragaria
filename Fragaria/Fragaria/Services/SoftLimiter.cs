using NAudio.Wave;

namespace Fragaria.Services;

/// <summary>
/// Soft-knee limiter / brickwall volume cap for stream safety.
/// </summary>
public sealed class SoftLimiter
{
    private readonly float _threshold;
    private float _gain = 1f;

    public SoftLimiter(float threshold = 0.95f) => _threshold = threshold;

    public void Process(ReadOnlySpan<float> input, Span<float> output)
    {
        var peak = 0f;
        for (int i = 0; i < input.Length; i++)
            peak = Math.Max(peak, Math.Abs(input[i]));

        if (peak > _threshold)
        {
            var target = _threshold / peak;
            _gain = _gain * 0.9f + target * 0.1f;
        }
        else
        {
            _gain = Math.Min(1f, _gain * 1.01f);
        }

        for (int i = 0; i < input.Length; i++)
            output[i] = input[i] * _gain;
    }

    public static float ApplyCap(float sample, float limit) =>
        Math.Clamp(sample, -limit, limit);
}

public static class AudioMath
{
    public static float ComputePeak(ReadOnlySpan<float> samples)
    {
        var peak = 0f;
        for (int i = 0; i < samples.Length; i++)
            peak = Math.Max(peak, Math.Abs(samples[i]));
        return peak;
    }

    public static float Db(float linear) =>
        linear <= 0 ? -60f : 20f * MathF.Log10(linear);

    public static void MixAdd(Span<float> target, ReadOnlySpan<float> source, float gain)
    {
        var n = Math.Min(target.Length, source.Length);
        for (int i = 0; i < n; i++)
            target[i] += source[i] * gain;
    }

    public static byte[] FloatToPcm16(ReadOnlySpan<float> samples)
    {
        var bytes = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            var v = (short)(Math.Clamp(samples[i], -1f, 1f) * short.MaxValue);
            bytes[i * 2] = (byte)(v & 0xFF);
            bytes[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
        }
        return bytes;
    }
}

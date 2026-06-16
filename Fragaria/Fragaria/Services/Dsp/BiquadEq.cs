using Fragaria.Models;

namespace Fragaria.Services.Dsp;

/// <summary>Single biquad filter (RBJ cookbook).</summary>
public sealed class BiquadFilter
{
    private float _b0 = 1, _b1, _b2, _a1, _a2;
    private float _z1, _z2;
    private readonly float _sampleRate;

    public BiquadFilter(float sampleRate = 48000f) => _sampleRate = sampleRate;

    public void SetLowShelf(float freq, float gainDb, float q = 0.707f) =>
        Set(BiquadType.LowShelf, freq, gainDb, q);

    public void SetPeaking(float freq, float gainDb, float q = 1f) =>
        Set(BiquadType.Peaking, freq, gainDb, q);

    public void SetHighShelf(float freq, float gainDb, float q = 0.707f) =>
        Set(BiquadType.HighShelf, freq, gainDb, q);

    private enum BiquadType { LowShelf, Peaking, HighShelf }

    private void Set(BiquadType type, float freq, float gainDb, float q)
    {
        var a = MathF.Pow(10, gainDb / 40f);
        var w0 = 2f * MathF.PI * freq / _sampleRate;
        var cos = MathF.Cos(w0);
        var sin = MathF.Sin(w0);
        var alpha = sin / (2f * q);

        float b0, b1, b2, a0, a1, a2;
        switch (type)
        {
            case BiquadType.LowShelf:
            {
                var ap1 = a + 1;
                var am1 = a - 1;
                var twoSqrtAalpha = 2 * MathF.Sqrt(a) * alpha;
                b0 = a * (ap1 - am1 * cos + twoSqrtAalpha);
                b1 = 2 * a * (am1 - ap1 * cos);
                b2 = a * (ap1 - am1 * cos - twoSqrtAalpha);
                a0 = ap1 + am1 * cos + twoSqrtAalpha;
                a1 = -2 * (am1 + ap1 * cos);
                a2 = ap1 + am1 * cos - twoSqrtAalpha;
                break;
            }
            case BiquadType.Peaking:
            {
                b0 = 1 + alpha * a;
                b1 = -2 * cos;
                b2 = 1 - alpha * a;
                a0 = 1 + alpha / a;
                a1 = -2 * cos;
                a2 = 1 - alpha / a;
                break;
            }
            default:
            {
                var ap1 = a + 1;
                var am1 = a - 1;
                var twoSqrtAalpha = 2 * MathF.Sqrt(a) * alpha;
                b0 = a * (ap1 + am1 * cos + twoSqrtAalpha);
                b1 = -2 * a * (am1 + ap1 * cos);
                b2 = a * (ap1 + am1 * cos - twoSqrtAalpha);
                a0 = ap1 - am1 * cos + twoSqrtAalpha;
                a1 = 2 * (am1 - ap1 * cos);
                a2 = ap1 - am1 * cos - twoSqrtAalpha;
                break;
            }
        }

        _b0 = b0 / a0; _b1 = b1 / a0; _b2 = b2 / a0;
        _a1 = a1 / a0; _a2 = a2 / a0;
    }

    public float Process(float x)
    {
        var y = _b0 * x + _z1;
        _z1 = _b1 * x - _a1 * y + _z2;
        _z2 = _b2 * x - _a2 * y;
        return y;
    }

    public void Process(Span<float> samples)
    {
        for (int i = 0; i < samples.Length; i++)
            samples[i] = Process(samples[i]);
    }
}

public sealed class ThreeBandEq
{
    private readonly BiquadFilter _low = new();
    private readonly BiquadFilter _mid = new();
    private readonly BiquadFilter _high = new();
    private EqSettings _last = new();

    public void Update(EqSettings eq)
    {
        if (eq.LowDb == _last.LowDb && eq.MidDb == _last.MidDb && eq.HighDb == _last.HighDb)
            return;
        _low.SetLowShelf(120f, eq.LowDb);
        _mid.SetPeaking(1000f, eq.MidDb);
        _high.SetHighShelf(6000f, eq.HighDb);
        _last = new EqSettings { LowDb = eq.LowDb, MidDb = eq.MidDb, HighDb = eq.HighDb };
    }

    public void Process(Span<float> samples, EqSettings eq)
    {
        Update(eq);
        _low.Process(samples);
        _mid.Process(samples);
        _high.Process(samples);
    }
}

public sealed class StripDspChain
{
    private readonly ThreeBandEq _eq = new();
    private DynamicsCompressor _compressor = new(new CompressorSettings());
    private CompressorSettings _lastComp = new();

    public void Process(Span<float> samples, AudioStrip strip)
    {
        _eq.Process(samples, strip.Eq);
        if (!ReferenceEquals(strip.Compressor, _lastComp) &&
            strip.Compressor.ThresholdDb != _lastComp.ThresholdDb)
        {
            _compressor = new DynamicsCompressor(strip.Compressor);
            _lastComp = strip.Compressor;
        }
        _compressor.Process(samples);
    }
}

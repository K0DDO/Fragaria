using Fragaria.Models;

namespace Fragaria.Services.Dsp;

public sealed class NoiseGate
{
    private readonly NoiseGateSettings _s;
    private float _envelope;
    private readonly float _sampleRate;

    public NoiseGate(NoiseGateSettings settings, float sampleRate = 48000f)
    {
        _s = settings;
        _sampleRate = sampleRate;
    }

    public void Process(Span<float> samples)
    {
        if (!_s.Enabled) return;

        var attack = MathF.Exp(-1f / (_s.AttackMs * 0.001f * _sampleRate));
        var release = MathF.Exp(-1f / (_s.ReleaseMs * 0.001f * _sampleRate));

        for (int i = 0; i < samples.Length; i++)
        {
            var amp = MathF.Abs(samples[i]);
            var coeff = amp > _s.Threshold ? attack : release;
            _envelope = coeff * _envelope + (1 - coeff) * (amp > _s.Threshold ? 1f : 0f);
            var gain = _envelope > 0.01f ? 1f : _s.Floor;
            samples[i] *= gain;
        }
    }
}

public sealed class DynamicsCompressor
{
    private readonly CompressorSettings _s;
    private float _env;
    private readonly float _sampleRate;

    public DynamicsCompressor(CompressorSettings settings, float sampleRate = 48000f)
    {
        _s = settings;
        _sampleRate = sampleRate;
    }

    public void Process(Span<float> samples)
    {
        if (!_s.Enabled) return;

        var attack = MathF.Exp(-1f / (_s.AttackMs * 0.001f * _sampleRate));
        var release = MathF.Exp(-1f / (_s.ReleaseMs * 0.001f * _sampleRate));
        var makeup = MathF.Pow(10f, _s.MakeupDb / 20f);

        for (int i = 0; i < samples.Length; i++)
        {
            var x = samples[i];
            var abs = MathF.Abs(x);
            var coeff = abs > _env ? attack : release;
            _env = coeff * _env + (1 - coeff) * abs;

            var envDb = 20f * MathF.Log10(Math.Max(_env, 1e-9f));
            var gainDb = 0f;
            if (envDb > _s.ThresholdDb)
                gainDb = (_s.ThresholdDb - envDb) * (1f - 1f / _s.Ratio);

            var gain = MathF.Pow(10f, gainDb / 20f) * makeup;
            samples[i] = x * gain;
        }
    }
}

public sealed class DuckingProcessor
{
    private readonly DuckingSettings _s;
    private float _duckGain = 1f;
    private readonly float _sampleRate;

    public DuckingProcessor(DuckingSettings settings, float sampleRate = 48000f)
    {
        _s = settings;
        _sampleRate = sampleRate;
    }

    public float CurrentGain => _duckGain;

    public void UpdateFromMicPeak(float micPeak)
    {
        if (!_s.Enabled) { _duckGain = 1f; return; }

        var attack = MathF.Exp(-1f / (_s.AttackMs * 0.001f * _sampleRate));
        var release = MathF.Exp(-1f / (_s.ReleaseMs * 0.001f * _sampleRate));
        var target = micPeak > _s.Threshold ? (1f - _s.Amount) : 1f;
        var coeff = target < _duckGain ? attack : release;
        _duckGain = coeff * _duckGain + (1 - coeff) * target;
    }
}

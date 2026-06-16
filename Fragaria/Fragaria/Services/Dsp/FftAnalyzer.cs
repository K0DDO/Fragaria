namespace Fragaria.Services.Dsp;

/// <summary>32-band magnitude spectrum for UI meters.</summary>
public sealed class FftAnalyzer
{
    private readonly int _fftSize;
    private readonly float[] _window;
    private readonly float[] _real;
    private readonly float[] _imag;
    public float[] Bands { get; }

    public FftAnalyzer(int fftSize = 512, int bands = 32)
    {
        _fftSize = fftSize;
        Bands = new float[bands];
        _real = new float[fftSize];
        _imag = new float[fftSize];
        _window = new float[fftSize];
        for (int i = 0; i < fftSize; i++)
            _window[i] = 0.5f * (1 - MathF.Cos(2 * MathF.PI * i / fftSize));
    }

    public void Analyze(ReadOnlySpan<float> samples)
    {
        Array.Clear(_real); Array.Clear(_imag);
        var n = Math.Min(samples.Length, _fftSize);
        for (int i = 0; i < n; i++) _real[i] = samples[i] * _window[i];

        FftInPlace(_real, _imag);

        var bandCount = Bands.Length;
        var binSize = (_fftSize / 2) / bandCount;
        for (int b = 0; b < bandCount; b++)
        {
            float sum = 0;
            for (int k = 1; k <= binSize; k++)
            {
                var idx = b * binSize + k;
                if (idx >= _fftSize / 2) break;
                var mag = MathF.Sqrt(_real[idx] * _real[idx] + _imag[idx] * _imag[idx]);
                sum += mag;
            }
            Bands[b] = Bands[b] * 0.7f + (sum / binSize) * 0.3f;
        }
    }

    private static void FftInPlace(float[] real, float[] imag)
    {
        var n = real.Length;
        int j = 0;
        for (int i = 0; i < n; i++)
        {
            if (i < j) { (real[i], real[j]) = (real[j], real[i]); (imag[i], imag[j]) = (imag[j], imag[i]); }
            var m = n >> 1;
            while (m >= 1 && j >= m) { j -= m; m >>= 1; }
            j += m;
        }

        for (int s = 1; s < n; s <<= 1)
        {
            var wR = MathF.Cos(MathF.PI / s);
            var wI = -MathF.Sin(MathF.PI / s);
            for (int k = 0; k < n; k += s << 1)
            {
                var curR = 1f; var curI = 0f;
                for (int i = 0; i < s; i++)
                {
                    var tR = curR * real[k + i + s] - curI * imag[k + i + s];
                    var tI = curR * imag[k + i + s] + curI * real[k + i + s];
                    real[k + i + s] = real[k + i] - tR;
                    imag[k + i + s] = imag[k + i] - tI;
                    real[k + i] += tR;
                    imag[k + i] += tI;
                    var nR = curR * wR - curI * wI;
                    curI = curR * wI + curI * wR;
                    curR = nR;
                }
            }
        }
    }
}

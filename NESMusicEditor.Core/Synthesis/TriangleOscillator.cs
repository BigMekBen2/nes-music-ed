namespace NESMusicEditor.Synthesis;

public class TriangleOscillator
{
    private double _phase = 0;

    public float Next(float frequency, float amplitude, int sampleRate)
    {
        _phase += frequency / sampleRate;
        if (_phase >= 1.0) _phase -= 1.0;
        float t = (float)_phase;
        float tri = t < 0.5f ? 4f * t - 1f : 3f - 4f * t;
        return tri * amplitude;
    }

    public void Reset() => _phase = 0;
}

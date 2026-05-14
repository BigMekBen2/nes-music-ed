namespace NESMusicEditor.Synthesis;

public class SquareOscillator
{
    private double _phase = 0;

    public float Next(float frequency, byte dutyCycle, float amplitude, int sampleRate)
    {
        _phase += frequency / sampleRate;
        if (_phase >= 1.0) _phase -= 1.0;
        double duty = dutyCycle switch { 0 => 0.125, 1 => 0.25, 3 => 0.75, _ => 0.5 };
        return _phase < duty ? amplitude : -amplitude;
    }

    public void Reset() => _phase = 0;
}

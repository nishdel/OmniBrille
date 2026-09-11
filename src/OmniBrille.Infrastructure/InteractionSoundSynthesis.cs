using OmniBrille.Core;

namespace OmniBrille.Infrastructure;

/// <summary>Deterministic, bounded PCM cues with no files, device, or background playback.</summary>
public static class InteractionSoundSynthesis
{
    public const int SampleRate = 22_050;

    public static byte[] CreatePcm16Mono(InteractionSoundCue cue)
    {
        var duration = cue switch
        {
            InteractionSoundCue.Hover => 0.085,
            InteractionSoundCue.Select => 0.100,
            InteractionSoundCue.Navigate => 0.240,
            InteractionSoundCue.FolderEnter => 0.440,
            InteractionSoundCue.FileOpen => 0.160,
            _ => throw new ArgumentOutOfRangeException(nameof(cue)),
        };
        var sampleCount = (int)(SampleRate * duration);
        var bytes = new byte[sampleCount * sizeof(short)];
        uint noiseState = 0x9E3779B9;
        double filteredNoise = 0;
        for (var index = 0; index < sampleCount; index++)
        {
            var time = index / (double)SampleRate;
            var progress = index / (double)(sampleCount - 1);
            noiseState ^= noiseState << 13;
            noiseState ^= noiseState >> 17;
            noiseState ^= noiseState << 5;
            var noise = (noiseState / (double)uint.MaxValue * 2) - 1;
            filteredNoise += 0.16 * (noise - filteredNoise);

            var signal = cue switch
            {
                // Air crossing a scanner, with just a trace of an electronic sweep.
                InteractionSoundCue.Hover =>
                    (filteredNoise * 1.5) + (Chirp(time, duration, 1_300, 680) * 0.08),
                // Two compact latch/data ticks instead of a musical notification.
                InteractionSoundCue.Select =>
                    Burst(time, 0.004, 0.018, 1_650, 0.55) +
                    Burst(time, 0.033, 0.022, 920, 0.34) +
                    (noise * Math.Exp(-time * 80) * 0.12),
                InteractionSoundCue.Navigate =>
                    (Chirp(time, duration, 680, 170) * 0.22) +
                    (filteredNoise * Math.Sin(Math.PI * progress) * 1.5) +
                    Burst(time, 0.008, 0.025, 430, 0.35),
                // Vault catches release, a low door thud, then a receding air pull.
                InteractionSoundCue.FolderEnter =>
                    Burst(time, 0.006, 0.028, 1_850, 0.65) +
                    Burst(time, 0.045, 0.040, 710, 0.55) +
                    Burst(time, 0.092, 0.170, 78, 0.70) +
                    (Chirp(time, duration, 430, 62) * progress * 0.28) +
                    (filteredNoise * Math.Sin(Math.PI * progress) * 1.6),
                InteractionSoundCue.FileOpen =>
                    Burst(time, 0.006, 0.034, 780, 0.42) +
                    Burst(time, 0.055, 0.047, 1_560, 0.35) +
                    (filteredNoise * 0.38),
                _ => 0,
            };
            // A smooth envelope and conservative ceiling avoid harsh onset and clipping.
            var envelope = Math.Min(1, time / 0.004) * Math.Pow(Math.Sin(Math.PI * progress), 0.6);
            var sample = (short)(Math.Tanh(signal) * envelope * 0.24 * short.MaxValue);
            bytes[index * 2] = (byte)(sample & 0xff);
            bytes[(index * 2) + 1] = (byte)((sample >> 8) & 0xff);
        }

        return bytes;
    }

    private static double Chirp(double time, double duration, double from, double to) =>
        Math.Sin(2 * Math.PI * ((from * time) + ((to - from) * time * time / (2 * duration))));

    private static double Burst(double time, double start, double duration, double frequency, double amplitude)
    {
        var elapsed = time - start;
        if (elapsed <= 0 || elapsed >= duration)
        {
            return 0;
        }

        return Math.Sin(2 * Math.PI * frequency * elapsed) *
               Math.Sin(Math.PI * elapsed / duration) * amplitude;
    }
}

using System.Buffers.Binary;
using OmniBrille.Core;
using OmniBrille.Infrastructure;

namespace OmniBrille.Tests;

public sealed class InteractionSoundSynthesisTests
{
    [Theory]
    [InlineData(InteractionSoundCue.Hover)]
    [InlineData(InteractionSoundCue.Select)]
    [InlineData(InteractionSoundCue.Navigate)]
    [InlineData(InteractionSoundCue.FolderEnter)]
    [InlineData(InteractionSoundCue.FileOpen)]
    public void Cues_FitPlaybackBufferAndHaveConservativeLevelWithQuietEndpoints(InteractionSoundCue cue)
    {
        var clip = InteractionSoundSynthesis.CreatePcm16Mono(cue);
        var samples = new short[clip.Length / sizeof(short)];
        for (var index = 0; index < samples.Length; index++)
        {
            samples[index] = BinaryPrimitives.ReadInt16LittleEndian(clip.AsSpan(index * sizeof(short)));
        }

        Assert.InRange(samples.Length, InteractionSoundSynthesis.SampleRate / 25,
            InteractionSoundSynthesis.SampleRate / 2);
        Assert.Equal(0, samples[0]);
        Assert.Equal(0, samples[^1]);
        Assert.InRange(samples.Max(sample => Math.Abs((int)sample)), 100, (int)(short.MaxValue * 0.24));
        Assert.Equal(clip, InteractionSoundSynthesis.CreatePcm16Mono(cue));
    }

    [Fact]
    public void FolderEntry_HasDistinctLongerVaultAndAirTail()
    {
        var folder = InteractionSoundSynthesis.CreatePcm16Mono(InteractionSoundCue.FolderEnter);
        foreach (var cue in Enum.GetValues<InteractionSoundCue>().Where(cue => cue != InteractionSoundCue.FolderEnter))
        {
            Assert.True(folder.Length > InteractionSoundSynthesis.CreatePcm16Mono(cue).Length);
        }

        var tailStart = (int)(InteractionSoundSynthesis.SampleRate * 0.3) * sizeof(short);
        Assert.Contains(folder[tailStart..], sample => sample != 0);
    }
}

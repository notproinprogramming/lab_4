using System;
using Xunit;
using Moq;
using NAudio.Wave;
using AudioSynthApp;

[Collection("NonParallelTests")]
public class AudioSynthTests
{
    [Fact]
    public void Start_ShouldSetIsRunningToTrue()
    {
        var mockOutput = new Mock<IAudioOutput>();
        using var synth = new AudioSynth(mockOutput.Object);

        synth.Start();
        Assert.True(synth.IsRunning);
        synth.Stop();
    }

    [Fact]
    public void Stop_ShouldSetIsRunningToFalse()
    {
        var mockOutput = new Mock<IAudioOutput>();
        using var synth = new AudioSynth(mockOutput.Object);
        synth.Start();

        synth.Stop();
        Assert.False(synth.IsRunning);
    }

    [Fact]
    public void SetFrequency_ShouldChangeFrequency()
    {
        var mockOutput = new Mock<IAudioOutput>();
        using var synth = new AudioSynth(mockOutput.Object);

        synth.SetFrequency(600.0);
        Assert.Equal(600.0, synth.CurrentFrequency);
    }

    [Fact]
    public void GenerateAudioFrame_ShouldAddSamplesToBuffer()
    {
        var mockOutput = new Mock<IAudioOutput>();
        using var synth = new AudioSynth(mockOutput.Object);
        int initialBufferedSamples = synth.BufferedSamples;

        synth.GenerateAudioFrame();
        Assert.True(synth.BufferedSamples > initialBufferedSamples);
    }

    [Fact]
    public void Constructor_ShouldInitializeWithDefaultFrequency()
    {
        var mockOutput = new Mock<IAudioOutput>();
        using var synth = new AudioSynth(mockOutput.Object);

        Assert.Equal(440.0, synth.CurrentFrequency);
    }

    [Fact]
    public void Dispose_ShouldStopAndDisposeOutput()
    {
        var mockOutput = new Mock<IAudioOutput>();
        var synth = new AudioSynth(mockOutput.Object);
        synth.Start();

        synth.Dispose();

        mockOutput.Verify(x => x.Stop(), Times.Once);
        mockOutput.Verify(x => x.Dispose(), Times.Once);
        Assert.False(synth.IsRunning);
    }
}

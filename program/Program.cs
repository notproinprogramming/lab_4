using System;
using System.Threading;
using Microsoft.VisualBasic;
using NAudio.Wave;

namespace AudioSynthApp
{
    public interface IAudioOutput
    {
        void Play();
        void Stop();
        void Init(IWaveProvider waveProvider);
        void Dispose();
    }

    public class NAudioWrapper : IAudioOutput, IDisposable
    {
        private readonly IWavePlayer _waveOut;

        public NAudioWrapper(IWavePlayer waveOut) => _waveOut = waveOut;
        public void Play() => _waveOut.Play();
        public void Stop() => _waveOut.Stop();
        public void Init(IWaveProvider waveProvider) => _waveOut.Init(waveProvider);
        public void Dispose() => _waveOut.Dispose();
    }

    public class AudioSynth : IDisposable
    {
        private const int TargetFps = 60;
        private const double TargetFrameTime = 1000.0 / TargetFps;

        private bool _isRunning;
        private Thread _audioThread;
        private readonly IAudioOutput _waveOut;
        private readonly BufferedWaveProvider _waveProvider;
        private double _phase;
        private double _frequency = 440.0;
        private readonly int _sampleRate = 44100;
        private readonly int _bufferSize;
        private readonly Func<double> _timeProvider;

        public double CurrentFrequency => _frequency;
        public bool IsRunning => _isRunning;
        public int BufferedSamples => _waveProvider.BufferedBytes / 2;

        public AudioSynth() : this(null, null) { }

        public AudioSynth(IAudioOutput waveOut = null, Func<double> timeProvider = null)
        {
            _waveProvider = new BufferedWaveProvider(new WaveFormat(_sampleRate, 16, 1))
            {
                BufferDuration = TimeSpan.FromMilliseconds(500)
            };

            _waveOut = waveOut ?? CreateDefaultAudioOutput();
            _waveOut.Init(_waveProvider);

            _bufferSize = _sampleRate / TargetFps;
            _timeProvider = timeProvider ?? (() => System.Diagnostics.Stopwatch.StartNew().Elapsed.TotalMilliseconds);
        }

        private IAudioOutput CreateDefaultAudioOutput()
        {
            IWavePlayer waveOut;
            try
            {
                waveOut = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, 100);
            }
            catch
            {
                try
                {
                    waveOut = new DirectSoundOut();
                }
                catch
                {
                    waveOut = new WaveOutEvent();
                }
            }
            return new NAudioWrapper(waveOut);
        }

        public void Start()
        {
            if (_isRunning) return;

            _isRunning = true;
            _waveOut.Play();
            _audioThread = new Thread(RunAudioLoop)
            {
                Priority = ThreadPriority.Highest
            };
            _audioThread.Start();
        }

        public void Stop()
        {
            _isRunning = false;
            _audioThread?.Join();
            _waveOut.Stop();
        }

        public void SetFrequency(double freq)
        {
            _frequency = freq;
        }

        private void RunAudioLoop()
        {
            double previousTime = _timeProvider();

            while (_isRunning)
            {
                double currentTime = _timeProvider();
                double elapsedTime = currentTime - previousTime;
                previousTime = currentTime;

                if (_waveProvider.BufferedDuration.TotalMilliseconds < 300)
                {
                    GenerateAudioFrame();
                }

                double frameTime = _timeProvider() - currentTime;
                if (frameTime < TargetFrameTime)
                {
                    int sleepTime = (int)(TargetFrameTime - frameTime);
                    Thread.Sleep(sleepTime);
                }
            }
        }

        public void GenerateAudioFrame()
        {
            byte[] buffer = new byte[_bufferSize * 2];

            for (int i = 0; i < _bufferSize; i++)
            {
                double sample = Math.Sin(_phase) * 0.9;
                short pcm = (short)(sample * short.MaxValue);
                buffer[i * 2] = (byte)(pcm & 0xFF);
                buffer[i * 2 + 1] = (byte)((pcm >> 8) & 0xFF);

                _phase += 2 * Math.PI * _frequency / _sampleRate;
                if (_phase > 2 * Math.PI) _phase -= 2 * Math.PI;
            }

            _waveProvider.AddSamples(buffer, 0, buffer.Length);
        }

        public void Dispose()
        {
            Stop();
            _waveOut.Dispose();
        }
    }

    class Program
    {
        static void Main()
        {
            Console.WriteLine("Q - 400 hz | W - 500 hz | E - exit");

            using (var synth = new AudioSynth())
            {
                synth.Start();

                while (true)
                {
                    var key = Console.ReadKey(true);
                    switch (key.Key)
                    {
                        case ConsoleKey.Q:
                            synth.SetFrequency(400.0);
                            Console.WriteLine("frequency: 400 hz");
                            break;
                        case ConsoleKey.W:
                            synth.SetFrequency(500.0);
                            Console.WriteLine("frequency: 500 hz");
                            break;
                        case ConsoleKey.E:
                            return;
                    }
                }
            }
        }
    }
}

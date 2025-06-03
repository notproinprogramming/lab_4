using System;
using System.Threading;
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


    public class FpsBasedLoop : IDisposable
    {
        private const int DefaultTargetFps = 60;

        private readonly int _targetFps;
        private readonly Action _callback;
        private Thread _thread;
        private bool _isRunning;
        private readonly Func<double> _timeProvider;

        public FpsBasedLoop(Action callback, int targetFps = DefaultTargetFps,
                          Func<double> timeProvider = null)
        {
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
            _targetFps = targetFps;
            _timeProvider = timeProvider ?? (() =>
                System.Diagnostics.Stopwatch.StartNew().Elapsed.TotalMilliseconds);
        }

        public void Start()
        {
            if (_isRunning) return;

            _isRunning = true;
            _thread = new Thread(RunLoop)
            {
                Priority = ThreadPriority.Highest
            };
            _thread.Start();
        }

        private void RunLoop()
        {
            double targetFrameTime = 1000.0 / _targetFps;
            double previousTime = _timeProvider();

            while (_isRunning)
            {
                double currentTime = _timeProvider();
                double elapsedTime = currentTime - previousTime;
                previousTime = currentTime;

                _callback();

                double frameTime = _timeProvider() - currentTime;
                if (frameTime < targetFrameTime)
                {
                    int sleepTime = (int)(targetFrameTime - frameTime);
                    Thread.Sleep(sleepTime);
                }
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _thread?.Join();
        }

        public void Dispose() => Stop();
    }


    public class AudioSynthesizer : IDisposable
    {
        private readonly IAudioOutput _audioOutput;
        private readonly BufferedWaveProvider _waveProvider;
        private readonly int _bufferSize;

        private double _phase;
        private double _frequency = 440.0;
        private readonly int _sampleRate = 44100;

        public double CurrentFrequency => _frequency;
        public bool IsRunning { get; private set; }
        public int BufferedSamples => _waveProvider.BufferedBytes / 2;

        public AudioSynthesizer(IAudioOutput audioOutput = null, int bufferSize = 735)
        {
            _audioOutput = audioOutput ?? CreateDefaultAudioOutput();
            _bufferSize = bufferSize;

            _waveProvider = new BufferedWaveProvider(new WaveFormat(_sampleRate, 16, 1))
            {
                BufferDuration = TimeSpan.FromMilliseconds(500)
            };

            _audioOutput.Init(_waveProvider);
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
            if (IsRunning) return;

            IsRunning = true;
            _audioOutput.Play();
        }

        public void Stop()
        {
            IsRunning = false;
            _audioOutput.Stop();
        }

        public void SetFrequency(double freq) => _frequency = freq;

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
            _audioOutput.Dispose();
        }
    }


    class Program
    {
        static void Main()
        {
            Console.WriteLine("Q - 400 hz | W - 500 hz | E - exit");

            using (var synthesizer = new AudioSynthesizer())
            using (var loop = new FpsBasedLoop(() =>
            {
                if (synthesizer.IsRunning)
                    synthesizer.GenerateAudioFrame();
            }))
            {
                synthesizer.Start();
                loop.Start();

                while (true)
                {
                    var key = Console.ReadKey(true);
                    switch (key.Key)
                    {
                        case ConsoleKey.Q:
                            synthesizer.SetFrequency(400.0);
                            Console.WriteLine("frequency: 400 hz");
                            break;
                        case ConsoleKey.W:
                            synthesizer.SetFrequency(500.0);
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

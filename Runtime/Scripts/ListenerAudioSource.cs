using System;
using LiveKit;
using UnityEngine;

namespace com.virnect.livekit.collab
{
    /// <summary>
    /// An audio source which captures the mixed output from a Unity <see cref="AudioListener"/>.
    /// </summary>
    public sealed class ListenerAudioSource : RtcAudioSource
    {
        private const int FRAME_DURATION_MS = 10;
        private const int BUFFERED_FRAME_COUNT = 8;
        private const float OUTPUT_HEADROOM_GAIN = 0.85f;

        private readonly AudioListener listener;

        public override event Action<float[], int, int> AudioRead;

        private ListenerAudioProbe probe;
        private bool disposed = false;
        private bool started = false;

        private int currentChannels;
        private int currentSampleRate;
        private int frameSampleCount;
        private float[] ringBuffer = Array.Empty<float>();
        private float[] frameBuffer = Array.Empty<float>();
        private int readIndex;
        private int writeIndex;
        private int bufferedSamples;

        /// <summary>
        /// Creates a new listener audio source for the given <see cref="AudioListener"/>.
        /// </summary>
        public ListenerAudioSource(AudioListener listener, int channels = 2, RtcAudioSourceType sourceType = RtcAudioSourceType.AudioSourceCustom) : base(channels, sourceType)
        {
            this.listener = listener;
        }

        private void OnAudioRead(float[] data, int channels, int sampleRate)
        {
            if (channels <= 0 || sampleRate <= 0 || data == null || data.Length == 0)
                return;

            if (channels != currentChannels || sampleRate != currentSampleRate)
            {
                currentChannels = channels;
                currentSampleRate = sampleRate;

                int samplesPerChannel = Math.Max(1, sampleRate * FRAME_DURATION_MS / 1000);
                frameSampleCount = samplesPerChannel * channels;

                int ringBufferSize = Math.Max(frameSampleCount * BUFFERED_FRAME_COUNT, data.Length * 4);
                ringBuffer = new float[ringBufferSize];
                frameBuffer = new float[frameSampleCount];
                readIndex = 0;
                writeIndex = 0;
                bufferedSamples = 0;
            }

            if (data.Length > ringBuffer.Length)
            {
                ringBuffer = new float[data.Length * 2];
                readIndex = 0;
                writeIndex = 0;
                bufferedSamples = 0;
            }

            int ringLength = ringBuffer.Length;
            for (int i = 0; i < data.Length; i++)
            {
                float sample = Mathf.Clamp(data[i] * OUTPUT_HEADROOM_GAIN, -0.98f, 0.98f);

                if (bufferedSamples == ringLength)
                {
                    readIndex = (readIndex + 1) % ringLength;
                    bufferedSamples--;
                }

                ringBuffer[writeIndex] = sample;
                writeIndex = (writeIndex + 1) % ringLength;
                bufferedSamples++;
            }

            while (bufferedSamples >= frameSampleCount)
            {
                for (int i = 0; i < frameSampleCount; i++)
                {
                    frameBuffer[i] = ringBuffer[readIndex];
                    readIndex = (readIndex + 1) % ringLength;
                }

                bufferedSamples -= frameSampleCount;
                AudioRead?.Invoke(frameBuffer, channels, sampleRate);
            }
        }

        public override void Start()
        {
            if (listener == null)
                throw new InvalidOperationException("AudioListener is null");

            base.Start();
            if (started) return;

            probe = listener.gameObject.AddComponent<ListenerAudioProbe>();
            probe.AudioRead += OnAudioRead;

            started = true;
        }

        public override void Stop()
        {
            base.Stop();
            if (!started) return;

            if (probe != null)
            {
                probe.AudioRead -= OnAudioRead;
                UnityEngine.Object.Destroy(probe);
                probe = null;
            }

            readIndex = 0;
            writeIndex = 0;
            bufferedSamples = 0;

            started = false;
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposed && disposing) Stop();
            disposed = true;
            base.Dispose(disposing);
        }

        ~ListenerAudioSource()
        {
            Dispose(false);
        }

        private sealed class ListenerAudioProbe : MonoBehaviour
        {
            public event Action<float[], int, int> AudioRead;

            private int sampleRate;

            private void OnEnable()
            {
                OnAudioConfigurationChanged(false);
                AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
            }

            private void OnDisable()
            {
                AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
            }

            private void OnAudioConfigurationChanged(bool deviceWasChanged)
            {
                sampleRate = AudioSettings.outputSampleRate;
            }

            private void OnAudioFilterRead(float[] data, int channels)
            {
                AudioRead?.Invoke(data, channels, sampleRate);
            }

            private void OnDestroy()
            {
                AudioRead = null;
            }
        }
    }
}

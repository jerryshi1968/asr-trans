using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace SystemSubtitleTranslator;

public sealed class WasapiSystemAudioSource : IDisposable
{
    const int TargetSampleRate = 16000;
    readonly WasapiLoopbackCapture capture;
    readonly List<float> sourceBuffer = [];
    readonly object sync = new();
    double sourcePosition;
    bool disposed;

    public event Action<float[]>? SamplesAvailable;

    public string DeviceName { get; }

    public WasapiSystemAudioSource()
    {
        capture = new WasapiLoopbackCapture();
        DeviceName = GetDefaultRenderDeviceName();
        capture.DataAvailable += OnDataAvailable;
        capture.RecordingStopped += (_, e) => {
            if (e.Exception != null)
                throw e.Exception;
        };
    }

    public void Start()
    {
        capture.StartRecording();
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        capture.DataAvailable -= OnDataAvailable;
        if (capture.CaptureState == CaptureState.Capturing)
            capture.StopRecording();
        capture.Dispose();
    }

    void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        float[] mono = DecodeToMono(e.Buffer, e.BytesRecorded, capture.WaveFormat);
        if (mono.Length == 0)
            return;

        float[] samples = ResampleTo16k(mono, capture.WaveFormat.SampleRate);
        if (samples.Length > 0)
            SamplesAvailable?.Invoke(samples);
    }

    float[] DecodeToMono(byte[] buffer, int bytesRecorded, WaveFormat format)
    {
        int channels = Math.Max(1, format.Channels);
        int bytesPerSample = Math.Max(1, format.BitsPerSample / 8);
        int bytesPerFrame = bytesPerSample * channels;
        int frameCount = bytesRecorded / bytesPerFrame;
        if (frameCount <= 0)
            return [];

        float[] mono = new float[frameCount];
        bool isFloat = format.Encoding == WaveFormatEncoding.IeeeFloat;

        for (int frame = 0; frame < frameCount; frame++)
        {
            float sum = 0;
            int frameOffset = frame * bytesPerFrame;
            for (int channel = 0; channel < channels; channel++)
            {
                int offset = frameOffset + channel * bytesPerSample;
                sum += isFloat
                    ? BitConverter.ToSingle(buffer, offset)
                    : DecodePcmSample(buffer, offset, bytesPerSample);
            }
            mono[frame] = Math.Clamp(sum / channels, -1f, 1f);
        }

        return mono;
    }

    static float DecodePcmSample(byte[] buffer, int offset, int bytesPerSample)
    {
        return bytesPerSample switch {
            2 => BitConverter.ToInt16(buffer, offset) / 32768f,
            3 => Decode24BitPcm(buffer, offset) / 8388608f,
            4 => BitConverter.ToInt32(buffer, offset) / 2147483648f,
            _ => 0
        };
    }

    static int Decode24BitPcm(byte[] buffer, int offset)
    {
        int value = buffer[offset] | (buffer[offset + 1] << 8) | (buffer[offset + 2] << 16);
        if ((value & 0x800000) != 0)
            value |= unchecked((int)0xFF000000);
        return value;
    }

    float[] ResampleTo16k(float[] mono, int sourceSampleRate)
    {
        if (sourceSampleRate <= 0)
            return [];

        lock (sync)
        {
            sourceBuffer.AddRange(mono);
            double step = (double)sourceSampleRate / TargetSampleRate;
            int capacity = Math.Max(0, (int)((sourceBuffer.Count - sourcePosition) / step));
            if (capacity == 0)
                return [];

            List<float> output = new(capacity);
            while (sourcePosition + 1 < sourceBuffer.Count)
            {
                int index = (int)sourcePosition;
                double fraction = sourcePosition - index;
                float sample = (float)(sourceBuffer[index] + (sourceBuffer[index + 1] - sourceBuffer[index]) * fraction);
                output.Add(sample);
                sourcePosition += step;
            }

            int consumed = Math.Min((int)sourcePosition, sourceBuffer.Count - 1);
            if (consumed > 0)
            {
                sourceBuffer.RemoveRange(0, consumed);
                sourcePosition -= consumed;
            }

            return output.ToArray();
        }
    }

    static string GetDefaultRenderDeviceName()
    {
        using MMDeviceEnumerator enumerator = new();
        return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).FriendlyName;
    }
}

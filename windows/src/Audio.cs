using NAudio.CoreAudioApi;
using NAudio.Wave;
namespace SpaceWall;

/// Captures what the default speakers play (WASAPI loopback) and converts it to 44100 Hz mono s16le,
/// the exact format index.html expects. Restarts itself when the default output device changes.
class AudioFeed : IDisposable
{
    WasapiLoopbackCapture cap;
    MMDevice dev;
    readonly object lk = new();
    readonly List<short> q = new();
    public string DeviceName = "(none)", Format = "", LastError = "";
    string devId = "";
    public long Packets, RealSamples; public float Peak1s; DateTime peakT = DateTime.Now; DateTime lastData = DateTime.MinValue, lastDrain = DateTime.Now;
    public bool Running => cap != null;
    // resampler state
    int ch = 2, bits = 32; bool isFloat = true; double step = 1, pos; float prev; long idx;

    public void Start()
    {
        Stop();
        try
        {
            using var en = new MMDeviceEnumerator();
            dev = en.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            devId = dev.ID; DeviceName = dev.FriendlyName;
            cap = new WasapiLoopbackCapture(dev);
            var wf = cap.WaveFormat;
            ch = wf.Channels; bits = wf.BitsPerSample; step = wf.SampleRate / 44100.0;
            isFloat = wf.Encoding == WaveFormatEncoding.IeeeFloat ||
                      (wf is WaveFormatExtensible x && x.SubFormat == new Guid("00000003-0000-0010-8000-00aa00389b71"));
            Format = $"{wf.SampleRate} Hz, {ch} ch, {bits} bit {(isFloat ? "float" : "pcm")}";
            pos = 0; prev = 0; idx = 0;
            cap.DataAvailable += OnData;
            cap.RecordingStopped += (s, e) => { LastError = e.Exception?.Message ?? ""; Log.W("capture stopped " + LastError); cap = null; };
            cap.StartRecording();
            Log.W($"audio capture started: {DeviceName} ({Format})");
        }
        catch (Exception e) { LastError = e.Message; Log.W("audio start failed: " + e); cap = null; }
    }

    public void Stop()
    {
        var c = cap; cap = null;
        if (c != null) { try { c.DataAvailable -= OnData; c.StopRecording(); c.Dispose(); } catch { } }
        lock (lk) q.Clear();
    }

    /// call every few seconds: restart if stopped or the default output changed
    public void Watchdog()
    {
        try
        {
            using var en = new MMDeviceEnumerator();
            string id = "";
            try { id = en.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia).ID; } catch { }
            if (cap == null || (id != "" && id != devId)) { Log.W("audio (re)start, default device now " + id); Start(); }
        }
        catch (Exception e) { Log.W("audio watchdog: " + e.Message); }
    }

    void OnData(object s, WaveInEventArgs a)
    {
        int bytesPer = bits / 8, frame = bytesPer * ch, n = a.BytesRecorded / frame;
        if (n <= 0) return;
        var buf = a.Buffer; var outp = new List<short>(n);
        float mx = 0;
        for (int i = 0; i < n; i++)
        {
            float sum = 0;
            for (int c = 0; c < ch; c++)
            {
                int o = i * frame + c * bytesPer; float v;
                if (isFloat && bits == 32) v = BitConverter.ToSingle(buf, o);
                else if (bits == 16) v = BitConverter.ToInt16(buf, o) / 32768f;
                else if (bits == 24) v = ((buf[o] << 8) | (buf[o + 1] << 16) | (buf[o + 2] << 24)) / 2147483648f;
                else if (bits == 32) v = BitConverter.ToInt32(buf, o) / 2147483648f;
                else v = 0;
                sum += v;
            }
            float x = sum / ch;
            if (Math.Abs(x) > mx) mx = Math.Abs(x);
            // linear-interpolating resample to 44100
            while (pos <= idx)
            {
                float y = prev + (x - prev) * (float)(pos - (idx - 1));
                y = Math.Clamp(y, -1f, 1f);
                outp.Add((short)(y * 32767));
                pos += step;
            }
            prev = x; idx++;
        }
        Packets++; RealSamples += n; lastData = DateTime.Now;
        var now = DateTime.Now;
        if ((now - peakT).TotalSeconds > 1) { Peak1s = 0; peakT = now; }
        if (mx > Peak1s) Peak1s = mx;
        lock (lk) { q.AddRange(outp); if (q.Count > 44100) q.RemoveRange(0, q.Count - 8820); }
    }

    /// bytes (s16le mono 44.1k) captured since the last call. WASAPI loopback sends nothing during silence,
    /// so silence is filled in to keep the page's "live" detection and decay logic running like with parec.
    public byte[] Drain()
    {
        short[] arr;
        var now = DateTime.Now;
        lock (lk) { arr = q.ToArray(); q.Clear(); }
        if (arr.Length == 0)
        {
            if (cap != null && (now - lastData).TotalMilliseconds > 80)
                arr = new short[Math.Min(4410, (int)((now - lastDrain).TotalSeconds * 44100))];
        }
        lastDrain = now;
        if (arr.Length == 0) return null;
        var b = new byte[arr.Length * 2];
        Buffer.BlockCopy(arr, 0, b, 0, b.Length);
        return b;
    }

    public void Dispose() => Stop();
}

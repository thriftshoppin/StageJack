using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave;

// ---------- Device list (same shape the UI already uses) ----------
// Id is the index into the list returned by Inputs()/Outputs(). EndpointId is the Windows device id used by WASAPI.
public sealed class Device {
    public int Id;
    public string Name;
    public string EndpointId;
    public override string ToString() { return Name; }
}

// Kept as "Mme" so StageJackApp.cs does not need edits. Internally this is WASAPI device discovery.
public static class Mme {
    public static List<Device> Inputs() { return WasapiDevices.List(true); }
    public static List<Device> Outputs() { return WasapiDevices.List(false); }
}

// ---------- WASAPI COM interop (no third-party libraries) ----------
static class WasapiNative {
    public const int DEVICE_STATE_ACTIVE = 0x1;
    public const int eRender = 0, eCapture = 1;
    public const int CLSCTX_ALL = 23;
    public const int AUDCLNT_SHAREMODE_SHARED = 0;
    public const int AUDCLNT_STREAMFLAGS_EVENTCALLBACK = 0x00040000;
    public const int AUDCLNT_STREAMFLAGS_NOPERSIST = 0x00080000;
    public const int AUDCLNT_STREAMFLAGS_LOOPBACK = 0x00020000;
    public const int AUDCLNT_BUFFERFLAGS_SILENT = 0x2;
    public const int STGM_READ = 0;
    public static readonly Guid IID_IAudioClient = new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
    public static readonly Guid IID_IAudioCaptureClient = new Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317");
    public static readonly Guid IID_IAudioRenderClient = new Guid("F294ACFC-3146-4483-A7BF-ADDCA43E81D0");
    public static readonly Guid IID_IMMDeviceEnumerator = new Guid("A95664D2-9614-4F35-A746-DE8DB63617E6");
    public static readonly Guid CLSID_MMDeviceEnumerator = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
    public static readonly Guid PKEY_Device_FriendlyName = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0");

    [StructLayout(LayoutKind.Sequential)]
    public struct PROPERTYKEY { public Guid fmtid; public int pid; }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public struct WAVEFORMATEX {
        public ushort wFormatTag, nChannels;
        public uint nSamplesPerSec, nAvgBytesPerSec;
        public ushort nBlockAlign, wBitsPerSample, cbSize;
    }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMDeviceEnumerator {
        int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IMMDeviceCollection devices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice device);
        int RegisterEndpointNotificationCallback(IntPtr pClient);
        int UnregisterEndpointNotificationCallback(IntPtr pClient);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMDeviceCollection {
        int GetCount(out int devices);
        int Item(int device, out IMMDevice endpoint);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMDevice {
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        int OpenPropertyStore(int stgmAccess, out IPropertyStore props);
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
        int GetState(out int pdwState);
    }

    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPropertyStore {
        int GetCount(out int cProps);
        int GetAt(int iProp, out PROPERTYKEY pkey);
        int GetValue(ref PROPERTYKEY key, out PropVariant pv);
        int SetValue(ref PROPERTYKEY key, ref PropVariant pv);
        int Commit();
    }

    // Native PROPVARIANT is 16 bytes on x64 (vt + 6 pad + pointer/value).
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct PropVariant {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr pointerValue;
    }

    [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioClient {
        int Initialize(int shareMode, int streamFlags, long hnsBufferDuration, long hnsPeriodicity, IntPtr pFormat, [In] ref Guid audioSessionGuid);
        int GetBufferSize(out int pNumBufferFrames);
        int GetStreamLatency(out long phnsLatency);
        int GetCurrentPadding(out int pNumPaddingFrames);
        int IsFormatSupported(int shareMode, IntPtr pFormat, out IntPtr ppClosestMatch);
        int GetMixFormat(out IntPtr ppDeviceFormat);
        int GetDevicePeriod(out long phnsDefaultDevicePeriod, out long phnsMinimumDevicePeriod);
        int Start();
        int Stop();
        int Reset();
        int SetEventHandle(IntPtr eventHandle);
        int GetService(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
    }

    [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioCaptureClient {
        int GetBuffer(out IntPtr ppData, out int pNumFramesToRead, out int pdwFlags, out long pu64DevicePosition, out long pu64QPCPosition);
        int ReleaseBuffer(int numFramesRead);
        int GetNextPacketSize(out int pNumFramesInNextPacket);
    }

    [ComImport, Guid("F294ACFC-3146-4483-A7BF-ADDCA43E81D0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioRenderClient {
        int GetBuffer(int numFramesRequested, out IntPtr ppData);
        int ReleaseBuffer(int numFramesWritten, int dwFlags);
    }

    [DllImport("ole32.dll")] public static extern int CoCreateInstance([In] ref Guid clsid, IntPtr punkOuter, int dwClsCtx, [In] ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
    [DllImport("ole32.dll")] public static extern void CoTaskMemFree(IntPtr pv);
    [DllImport("ole32.dll")] public static extern int PropVariantClear(ref PropVariant pvar);

    public static IMMDeviceEnumerator CreateEnumerator() {
        Guid clsid = CLSID_MMDeviceEnumerator, iid = IID_IMMDeviceEnumerator;
        object o;
        int hr = CoCreateInstance(ref clsid, IntPtr.Zero, CLSCTX_ALL, ref iid, out o);
        if (hr != 0 || o == null) throw new Exception("Could not reach Windows audio (error " + hr + ").");
        return (IMMDeviceEnumerator)o;
    }

    public static string FriendlyName(IMMDevice dev) {
        try {
            IPropertyStore store;
            if (dev.OpenPropertyStore(STGM_READ, out store) != 0 || store == null) return null;
            PROPERTYKEY key = new PROPERTYKEY { fmtid = PKEY_Device_FriendlyName, pid = 14 };
            PropVariant pv;
            if (store.GetValue(ref key, out pv) != 0) return null;
            string s = null;
            if (pv.vt == 31 && pv.pointerValue != IntPtr.Zero) s = Marshal.PtrToStringUni(pv.pointerValue); // VT_LPWSTR
            PropVariantClear(ref pv);
            return s;
        } catch { return null; }
    }
}

static class WasapiDevices {
    public static List<Device> List(bool capture) {
        var list = new List<Device>();
        WasapiNative.IMMDeviceEnumerator en = null;
        try {
            en = WasapiNative.CreateEnumerator();
            WasapiNative.IMMDeviceCollection col;
            int flow = capture ? WasapiNative.eCapture : WasapiNative.eRender;
            if (en.EnumAudioEndpoints(flow, WasapiNative.DEVICE_STATE_ACTIVE, out col) != 0 || col == null) return list;
            int count;
            col.GetCount(out count);
            for (int i = 0; i < count; i++) {
                WasapiNative.IMMDevice dev;
                if (col.Item(i, out dev) != 0 || dev == null) continue;
                string id;
                dev.GetId(out id);
                string name = WasapiNative.FriendlyName(dev);
                if (string.IsNullOrEmpty(name)) name = id ?? ("Device " + i);
                list.Add(new Device { Id = list.Count, Name = name, EndpointId = id });
            }
        } catch { }
        return list;
    }

    public static string EndpointIdAt(bool capture, int index) {
        List<Device> l = List(capture);
        if (index < 0 || index >= l.Count) return null;
        return l[index].EndpointId;
    }
}


// ---------- Signal processing ----------
sealed class Biquad {
    float b0 = 1, b1, b2, a1, a2, z1L, z2L, z1R, z2R;
    public static float F(float v) { return (v < 1e-15f && v > -1e-15f) ? 0f : v; }
    public void Peak(float fs, float f0, float dB, float q) {
        double A = Math.Pow(10, dB / 40.0), w = 2 * Math.PI * f0 / fs, al = Math.Sin(w) / (2 * q), cs = Math.Cos(w), a0 = 1 + al / A;
        b0 = (float)((1 + al * A) / a0); b1 = (float)(-2 * cs / a0); b2 = (float)((1 - al * A) / a0); a1 = (float)(-2 * cs / a0); a2 = (float)((1 - al / A) / a0);
    }
    public void LowShelf(float fs, float f0, float dB) {
        double A = Math.Pow(10, dB / 40.0), w = 2 * Math.PI * f0 / fs, cs = Math.Cos(w), al = Math.Sin(w) / Math.Sqrt(2.0), sa = 2 * Math.Sqrt(A) * al;
        double a0 = (A + 1) + (A - 1) * cs + sa;
        b0 = (float)(A * ((A + 1) - (A - 1) * cs + sa) / a0); b1 = (float)(2 * A * ((A - 1) - (A + 1) * cs) / a0); b2 = (float)(A * ((A + 1) - (A - 1) * cs - sa) / a0);
        a1 = (float)(-2 * ((A - 1) + (A + 1) * cs) / a0); a2 = (float)(((A + 1) + (A - 1) * cs - sa) / a0);
    }
    public void HighShelf(float fs, float f0, float dB) {
        double A = Math.Pow(10, dB / 40.0), w = 2 * Math.PI * f0 / fs, cs = Math.Cos(w), al = Math.Sin(w) / Math.Sqrt(2.0), sa = 2 * Math.Sqrt(A) * al;
        double a0 = (A + 1) - (A - 1) * cs + sa;
        b0 = (float)(A * ((A + 1) + (A - 1) * cs + sa) / a0); b1 = (float)(-2 * A * ((A - 1) + (A + 1) * cs) / a0); b2 = (float)(A * ((A + 1) + (A - 1) * cs - sa) / a0);
        a1 = (float)(2 * ((A - 1) - (A + 1) * cs) / a0); a2 = (float)(((A + 1) - (A - 1) * cs - sa) / a0);
    }
    // Magnitude response in dB at frequency f (used to draw the EQ curve)
    public double MagDb(double f, double fs) {
        double w = 2 * Math.PI * f / fs, c1 = Math.Cos(w), s1 = -Math.Sin(w), c2 = Math.Cos(2 * w), s2 = -Math.Sin(2 * w);
        double nr = b0 + b1 * c1 + b2 * c2, ni = b1 * s1 + b2 * s2, dr = 1 + a1 * c1 + a2 * c2, di = a1 * s1 + a2 * s2;
        return 10 * Math.Log10((nr * nr + ni * ni) / (dr * dr + di * di) + 1e-30);
    }
    public float L(float x) { float y = b0 * x + z1L; z1L = F(b1 * x - a1 * y + z2L); z2L = F(b2 * x - a2 * y); return y; }
    public float R(float x) { float y = b0 * x + z1R; z1R = F(b1 * x - a1 * y + z2R); z2R = F(b2 * x - a2 * y); return y; }
}

// Universe-style reverb building blocks (feedback delay network)
sealed class Ap {
    readonly float[] buf; int i;
    public Ap(int len) { buf = new float[Math.Max(1, len)]; }
    public float P(float x) { float b = buf[i]; float v = x - 0.6f * b; buf[i] = Biquad.F(v); float o = b + 0.6f * v; if (++i >= buf.Length) i = 0; return o; }
}

sealed class FdnLine {
    readonly float[] buf; int w; float ph, lp;
    public readonly int Len; public float Gain, ModDepth, ModInc;
    public FdnLine(int len, float modDepth, float modHz, float rate, float startPhase) {
        Len = len; buf = new float[len + (int)modDepth + 8]; ModDepth = modDepth; ModInc = modHz / rate; ph = startPhase;
    }
    public float Read() {
        ph += ModInc; if (ph >= 1f) ph -= 1f;
        float d = Len + ModDepth * (float)Math.Sin(6.2831853 * ph);
        float rr = w - d + buf.Length; int r0 = (int)rr; float fr = rr - r0;
        int i0 = r0 % buf.Length, i1 = i0 + 1; if (i1 >= buf.Length) i1 = 0;
        return buf[i0] * (1f - fr) + buf[i1] * fr;
    }
    public void Write(float mixed, float input, float damp) {
        float y = mixed * Gain; lp = Biquad.F((1f - damp) * y + damp * lp);
        buf[w] = Biquad.F(lp + input); if (++w >= buf.Length) w = 0;
    }
}

// Shared EQ definition: the simple Low/Mid/High filters and the curve the UI draws
public static class EqModel {
    public const float LowHz = 150f, MidHz = 1000f, MidQ = 0.7f, HighHz = 5000f, HeightHz = 7500f, HeightQ = 1.2f;
    public static float HeightDb(float param) { return (param - 50f) / 50f * 6f; }
    public static float FreqAt(int i, int n) { return (float)(20.0 * Math.Pow(1000.0, i / (double)(n - 1))); }
    // Combined response (dB) of every active filter at n log-spaced points from 20 Hz to 20 kHz
    public static float[] Curve(int n, float fs, float low, float mid, float high, float heightParam, bool advanced, float[] bands) {
        List<Biquad> fl = new List<Biquad>();
        if (Math.Abs(low) > 0.05f) { Biquad b = new Biquad(); b.LowShelf(fs, LowHz, low); fl.Add(b); }
        if (Math.Abs(mid) > 0.05f) { Biquad b = new Biquad(); b.Peak(fs, MidHz, mid, MidQ); fl.Add(b); }
        if (Math.Abs(high) > 0.05f) { Biquad b = new Biquad(); b.HighShelf(fs, HighHz, high); fl.Add(b); }
        if (advanced && bands != null) for (int i = 0; i < 10 && i < bands.Length; i++) if (Math.Abs(bands[i]) > 0.05f) { Biquad b = new Biquad(); b.Peak(fs, Dsp.Freqs[i], bands[i], 1.0f); fl.Add(b); }
        float hg = HeightDb(heightParam); if (Math.Abs(hg) > 0.05f) { Biquad b = new Biquad(); b.Peak(fs, HeightHz, hg, HeightQ); fl.Add(b); }
        float[] r = new float[n];
        for (int i = 0; i < n; i++) { double f = FreqAt(i, n), sum = 0; for (int k = 0; k < fl.Count; k++) sum += fl[k].MagDb(f, fs); r[i] = (float)sum; }
        return r;
    }
}

// Master limiter: 1.5 ms look-ahead, stereo-linked, smooth release. Keeps output under -1 dBFS without crunchy clipping.
sealed class Limiter {
    const int N = 72;
    readonly float[] dl = new float[N], dr = new float[N], req = new float[N], bx = new float[N];
    int i; double sum = N; float gPrev = 1f; readonly float ceil = 0.891f, relA;
    public Limiter(float rate) {
        relA = (float)(1 - Math.Exp(-1.0 / (0.150 * rate)));
        for (int k = 0; k < N; k++) { req[k] = 1f; bx[k] = 1f; }
    }
    public void P(ref float l, ref float r) {
        float pk = Math.Max(Math.Abs(l), Math.Abs(r));
        dl[i] = l; dr[i] = r; req[i] = pk > ceil ? ceil / pk : 1f;
        int o = i + 1; if (o >= N) o = 0;
        float m = 1f; for (int k = 0; k < N; k++) if (req[k] < m) m = req[k];
        float rl = gPrev + (1f - gPrev) * relA; if (m > rl) m = rl;
        gPrev = m;
        sum += m - bx[i]; bx[i] = m; float gain = (float)(sum / N);
        float oL = dl[o] * gain, oR = dr[o] * gain;
        if (++i >= N) i = 0;
        l = oL; r = oR;
    }
}

public sealed class Dsp {
    public static readonly float[] Freqs = { 31.25f, 62.5f, 125f, 250f, 500f, 1000f, 2000f, 4000f, 8000f, 16000f };
    public volatile bool Bypass;
    readonly float fs; readonly object sync = new object(); volatile int version; int seen = -1;
    float pRev = 20, pWidth = 50, pDepth = 0, pHeight = 50, pLevel = 0; readonly float[] pEq = new float[10];
    readonly Biquad[] eq = new Biquad[10]; readonly bool[] eqOn = new bool[10]; readonly Biquad hb = new Biquad(); bool hOn;
    float widthGain = 1f, dryGain = 1f, wetGain = 0f, levelLin = 1f, lpA = 1f; bool lpOn; int preSamp; float lpL, lpR;
    readonly float[] pre = new float[4096]; int pp;
    // ---- Universe reverb tuning (edit these to taste) ----
    const float UniLowCutHz = 140f;   // keeps the tail from getting muddy
    readonly FdnLine[] fl = new FdnLine[8]; readonly Ap[] dif = new Ap[4]; readonly float[] fo = new float[8];
    static readonly int[] FdnLen = { 1031, 1327, 1583, 1913, 2251, 2647, 3001, 3469 };
    static readonly float[] FdnModDepth = { 7f, 8.5f, 6.5f, 9f, 7.5f, 8f, 6f, 9.5f };
    static readonly float[] FdnModHz = { 0.13f, 0.17f, 0.21f, 0.11f, 0.19f, 0.15f, 0.23f, 0.09f };
    static readonly int[] DifLen = { 142, 107, 379, 277 };
    static readonly float[] SgnL = { 1, -1, 1, -1, 1, -1, 1, -1 }, SgnR = { 1, 1, -1, -1, 1, 1, -1, -1 };
    readonly Biquad bLo = new Biquad(), bMi = new Biquad(), bHi = new Biquad(); bool loOn, miOn, hiOn;
    float pLow, pMid, pHigh; bool pAdv;
    readonly float[] tapL = new float[4096], tapR = new float[4096]; volatile int tapW;
    readonly Limiter lim;
    float hpA, hpL, hpR;
    float pSize = 75, pTone = 60, pPre = 30, pClar = 50, pRWidth = 100, pSmooth = 60;
    float tWidth = 1f, tDry = 1f, tWet = 0f, tLevel = 1f, tWetWidth = 1f, preCur, duckCur = 1f, duckSlew = 0.01f, parA;
    static float Sm(float cur, float tgt, float a) { cur += a * (tgt - cur); return Math.Abs(cur - tgt) < 1e-5f ? tgt : cur; }
    float damp = 0.27f, duckAmt, wetWidth = 1f, env, atkA, relA;

    public Dsp(int rate) {
        fs = rate; float k = rate / 48000f; lim = new Limiter(rate);
        for (int i = 0; i < 10; i++) eq[i] = new Biquad();
        for (int i = 0; i < 8; i++) {
            fl[i] = new FdnLine((int)(FdnLen[i] * k), FdnModDepth[i] * k, FdnModHz[i], rate, i / 8f);
            fl[i].Gain = 0.97f;
        }
        for (int i = 0; i < 4; i++) dif[i] = new Ap((int)(DifLen[i] * k));
        hpA = (float)(1 - Math.Exp(-2 * Math.PI * UniLowCutHz / rate));
        parA = (float)(1 - Math.Exp(-1.0 / (0.020 * rate)));
    }

    public void Set(float reverb, float width, float depth, float height, float levelDb, float size, float tone, float preMs, float clarity, float rWidth, float smooth, float lowDb, float midDb, float highDb, bool advanced, float[] eqDb) {
        lock (sync) {
            pLow = lowDb; pMid = midDb; pHigh = highDb; pAdv = advanced; pSmooth = smooth; pSize = size; pTone = tone; pPre = preMs; pClar = clarity; pRWidth = rWidth;
            pRev = reverb; pWidth = width; pDepth = depth; pHeight = height; pLevel = levelDb;
            for (int i = 0; i < 10 && i < eqDb.Length; i++) pEq[i] = eqDb[i];
            version++;
        }
    }

    void Recompute() {
        for (int i = 0; i < 10; i++) { eqOn[i] = pAdv && Math.Abs(pEq[i]) > 0.05f; if (eqOn[i]) eq[i].Peak(fs, Freqs[i], pEq[i], 1.0f); }
        loOn = Math.Abs(pLow) > 0.05f; if (loOn) bLo.LowShelf(fs, EqModel.LowHz, pLow);
        miOn = Math.Abs(pMid) > 0.05f; if (miOn) bMi.Peak(fs, EqModel.MidHz, pMid, EqModel.MidQ);
        hiOn = Math.Abs(pHigh) > 0.05f; if (hiOn) bHi.HighShelf(fs, EqModel.HighHz, pHigh);
        float hg = (pHeight - 50f) / 50f * 6f; hOn = Math.Abs(hg) > 0.05f; if (hOn) hb.Peak(fs, 7500f, hg, 1.2f);
        tWidth = pWidth / 50f;
        float d = pDepth / 100f, r = pRev / 100f;
        preSamp = Math.Min(4000, (int)((pPre * 0.001f + d * 0.04f) * fs));
        double rt = 0.3 * Math.Pow(24.0, pSize / 100.0);   // 0.3 s .. 7.2 s (RT60)
        for (int i = 0; i < 8; i++) fl[i].Gain = (float)Math.Pow(10.0, -3.0 * fl[i].Len / (fs * rt));
        damp = 0.6f - 0.55f * pTone / 100f;
        float sm = pSmooth / 100f;
        atkA = (float)(1 - Math.Exp(-1.0 / ((0.004 + 0.046 * sm) * fs)));
        relA = (float)(1 - Math.Exp(-1.0 / ((0.18 + 0.62 * sm) * fs)));
        duckSlew = (float)(1 - Math.Exp(-1.0 / ((0.005 + 0.095 * sm) * fs)));
        duckAmt = 0.9f * pClar / 100f; tWetWidth = pRWidth / 100f; lpOn = d >= 0.01f;
        lpA = lpOn ? (float)(1 - Math.Exp(-2 * Math.PI * (20000.0 - 11000.0 * d) / fs)) : 1f;
        tDry = (1f - 0.25f * d) * (1f - 0.35f * r);
        tWet = r * 1.3f * (1f + 0.5f * d);
        float boost = Math.Max(0f, hg); boost = Math.Max(boost, Math.Max(pLow, Math.Max(pMid, pHigh))); if (pAdv) for (int i = 0; i < 10; i++) if (pEq[i] > boost) boost = pEq[i];
        tLevel = (float)Math.Pow(10, (pLevel - 0.5f * boost) / 20.0);
    }

    // Copies the most recent output samples (mono) for the spectrum display
    public void CopyRecent(float[] dst) {
        int n = dst.Length, w = tapW;
        for (int i = 0; i < n; i++) { int at = (w - n + i) & 4095; dst[i] = (tapL[at] + tapR[at]) * 0.5f; }
    }
    public void CopyRecent(float[] left, float[] right) {
        int n = Math.Min(left.Length, right.Length), w = tapW;
        for (int i = 0; i < n; i++) { int at = (w - n + i) & 4095; left[i] = tapL[at]; right[i] = tapR[at]; }
    }

    static short ToS(float x) {
        float a = Math.Abs(x);
        if (a > 0.9f) { float t = 0.9f + 0.1f * (float)Math.Tanh((a - 0.9f) / 0.1f); x = x < 0 ? -t : t; }
        return (short)(x * 32767f);
    }

    // buf = interleaved 16-bit stereo, processed in place
    public void Process(short[] b, int frames) {
        if (version != seen) { lock (sync) { Recompute(); seen = version; } }
        if (Bypass) { for (int n = 0; n < frames; n++) { tapL[tapW] = b[2 * n] / 32768f; tapR[tapW] = b[2 * n + 1] / 32768f; tapW = (tapW + 1) & 4095; } return; }
        const float inv = 1f / 32768f;
        float tPre = preSamp;
        for (int n = 0; n < frames; n++) {
            widthGain = Sm(widthGain, tWidth, parA); dryGain = Sm(dryGain, tDry, parA); wetGain = Sm(wetGain, tWet, parA); levelLin = Sm(levelLin, tLevel, parA); wetWidth = Sm(wetWidth, tWetWidth, parA);
            preCur += 0.0008f * (tPre - preCur);
            float l = b[2 * n] * inv, r = b[2 * n + 1] * inv;
            if (loOn) { l = bLo.L(l); r = bLo.R(r); }
            if (miOn) { l = bMi.L(l); r = bMi.R(r); }
            if (hiOn) { l = bHi.L(l); r = bHi.R(r); }
            for (int i = 0; i < 10; i++) if (eqOn[i]) { l = eq[i].L(l); r = eq[i].R(r); }
            if (hOn) { l = hb.L(l); r = hb.R(r); }
            if (widthGain != 1f) { float m = (l + r) * 0.5f, s = (l - r) * 0.5f * widthGain; l = m + s; r = m - s; }
            { float e = Math.Abs(l) + Math.Abs(r); env = Biquad.F(env + (e > env ? atkA : relA) * (e - env)); }
            if (lpOn) { lpL = Biquad.F(lpL + lpA * (l - lpL)); lpR = Biquad.F(lpR + lpA * (r - lpR)); l = lpL; r = lpR; }
            pre[pp] = (l + r) * 0.5f; float rd = pp - preCur; int ri = (int)Math.Floor(rd); float rf = rd - ri;
            float x = pre[ri & 4095] * (1f - rf) + pre[(ri + 1) & 4095] * rf; pp = (pp + 1) & 4095;
            for (int a = 0; a < 4; a++) x = dif[a].P(x);
            float wl = 0, wr = 0;
            for (int c = 0; c < 8; c++) { float o = fl[c].Read(); fo[c] = o; wl += SgnL[c] * o; wr += SgnR[c] * o; }
            wl *= 0.35355339f; wr *= 0.35355339f;
            // 8x8 Hadamard feedback matrix (energy preserving)
            float s0 = fo[0] + fo[1], d0 = fo[0] - fo[1], s1 = fo[2] + fo[3], d1 = fo[2] - fo[3], s2 = fo[4] + fo[5], d2 = fo[4] - fo[5], s3 = fo[6] + fo[7], d3 = fo[6] - fo[7];
            float a0 = s0 + s1, a1 = d0 + d1, a2 = s0 - s1, a3 = d0 - d1, a4 = s2 + s3, a5 = d2 + d3, a6 = s2 - s3, a7 = d2 - d3;
            const float hn = 0.35355339f;
            fl[0].Write((a0 + a4) * hn, x * 0.5f, damp); fl[1].Write((a1 + a5) * hn, x * 0.5f, damp);
            fl[2].Write((a2 + a6) * hn, x * 0.5f, damp); fl[3].Write((a3 + a7) * hn, x * 0.5f, damp);
            fl[4].Write((a0 - a4) * hn, x * 0.5f, damp); fl[5].Write((a1 - a5) * hn, x * 0.5f, damp);
            fl[6].Write((a2 - a6) * hn, x * 0.5f, damp); fl[7].Write((a3 - a7) * hn, x * 0.5f, damp);
            hpL = Biquad.F(hpL + hpA * (wl - hpL)); wl -= hpL; hpR = Biquad.F(hpR + hpA * (wr - hpR)); wr -= hpR;
            float duckT = 1f - duckAmt * Math.Min(1f, env * 4f); duckCur += duckSlew * (duckT - duckCur); wl *= duckCur; wr *= duckCur;
            if (wetWidth != 1f) { float wm = (wl + wr) * 0.5f, ws = (wl - wr) * 0.5f * wetWidth; wl = wm + ws; wr = wm - ws; }
            float outL = (l * dryGain + wl * wetGain) * levelLin, outR = (r * dryGain + wr * wetGain) * levelLin;
            lim.P(ref outL, ref outR);
            tapL[tapW] = outL; tapR[tapW] = outR; tapW = (tapW + 1) & 4095;
            b[2 * n] = ToS(outL);
            b[2 * n + 1] = ToS(outR);
        }
    }
}


// ---------- Capture -> process -> playback (WASAPI shared mode) ----------
// Same public surface as before so the window code does not need changes:
//   AudioEngine.Rate, .Dsp, .Error, .Running, .Start(inId, outId), .Stop()
// Target latency: about 15-30 ms (was 60-90 ms with MME).
// Virtual devices (Sunshine, some cable drivers) can invalidate mid-stream; the engine
// reconnects in-thread instead of dying on the first glitch.
public sealed class AudioEngine {
    public const int Rate = 48000;
    const int InputTapLength = 8192;
    // Requested buffer duration in 100-nanosecond units (10 ms). Shared mode rounds to the device period.
    const long HnsBuffer = 100000 * 10;
    // Common transient WASAPI failures on virtual endpoints
    const int AUDCLNT_E_DEVICE_INVALIDATED = unchecked((int)0x88890004);
    const int AUDCLNT_E_SERVICE_NOT_RUNNING = unchecked((int)0x88890010);
    const int AUDCLNT_E_NOT_INITIALIZED = unchecked((int)0x88890001);
    const int AUDCLNT_E_ENDPOINT_CREATE_FAILED = unchecked((int)0x8889000F);

    public readonly Dsp Dsp = new Dsp(Rate);
    // A monitor tap written from decoded WASAPI capture packets before DSP changes them.
    // The EQ visualizer reads this ring independently of the playback/render pipeline.
    readonly float[] inputTapL = new float[InputTapLength];
    readonly float[] inputTapR = new float[InputTapLength];
    readonly float[] outputTapL = new float[InputTapLength];
    readonly float[] outputTapR = new float[InputTapLength];
    readonly object outputTapLock = new object();
    volatile int inputTapWrite;
    volatile int outputTapWrite;
    volatile int outputTapLastTick;
    volatile bool outputHasData;
    int inputTapCount;
    int outputTapCount;
    WasapiLoopbackCapture systemLoopback;
    MMDeviceEnumerator analysisEnumerator;
    MMDevice systemLoopbackDevice;
    string analysisEndpointId;
    bool analysisConfigured;
    public volatile string Error;
    public volatile string MonitorError;
    Thread th; volatile bool run;
    string inEndpoint, outEndpoint;
    public AudioEngine() {
        // Create NAudio's COM enumerator before the app's legacy WASAPI wrappers.
        // Both interop layers target the same COM identity; keeping one NAudio
        // enumerator avoids a Runtime Callable Wrapper type collision.
        try { analysisEnumerator = new MMDeviceEnumerator(); }
        catch (Exception e) { MonitorError = e.Message; }
    }
    public bool Running { get { return th != null && th.IsAlive; } }

    public void CopyRecentInput(float[] left, float[] right) {
        CopyRecent(inputTapL, inputTapR, inputTapWrite, inputTapCount, left, right);
    }

    public void CopyRecentOutput(float[] left, float[] right) {
        if (left == null || right == null || left.Length != right.Length) return;
        lock (outputTapLock) {
            int age = unchecked(Environment.TickCount - outputTapLastTick);
            if (!outputHasData || age > 500) {
                Array.Clear(left, 0, left.Length); Array.Clear(right, 0, right.Length);
                outputTapCount = 0; outputHasData = false;
                return;
            }
            CopyRecent(outputTapL, outputTapR, outputTapWrite, outputTapCount, left, right);
        }
    }

    static void CopyRecent(float[] ringL, float[] ringR, int write, int available, float[] left, float[] right) {
        if (left == null || right == null || left.Length != right.Length) return;
        available = Math.Min(available, InputTapLength);
        int count = Math.Min(left.Length, available);
        int pad = left.Length - count;
        for (int i = 0; i < pad; i++) { left[i] = 0f; right[i] = 0f; }
        int start = (write - count + InputTapLength) & (InputTapLength - 1);
        for (int i = 0; i < count; i++) {
            int at = (start + i) & (InputTapLength - 1);
            left[pad + i] = ringL[at];
            right[pad + i] = ringR[at];
        }
    }

    void TapInput(short[] stereo, int frames) {
        int w = inputTapWrite;
        for (int i = 0; i < frames; i++) {
            inputTapL[w] = stereo[i * 2] / 32768f;
            inputTapR[w] = stereo[i * 2 + 1] / 32768f;
            w = (w + 1) & (InputTapLength - 1);
        }
        inputTapCount = Math.Min(InputTapLength, inputTapCount + frames);
        inputTapWrite = w;
    }

    void TapSystemOutput(object sender, WaveInEventArgs e) {
        WasapiLoopbackCapture capture = sender as WasapiLoopbackCapture;
        if (capture == null || e.BytesRecorded <= 0) return;
        WaveFormat format = capture.WaveFormat;
        int block = format.BlockAlign;
        int channels = Math.Max(1, format.Channels);
        int sampleBytes = Math.Max(1, format.BitsPerSample / 8);
        if (block <= 0 || e.BytesRecorded < block) return;
        bool isFloat = format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32;
        int frames = e.BytesRecorded / block;
        lock (outputTapLock) {
            int w = outputTapWrite;
            for (int i = 0; i < frames; i++) {
                int frame = i * block;
                float l = ReadLoopbackSample(e.Buffer, frame, format.BitsPerSample, isFloat);
                float r = channels > 1 ? ReadLoopbackSample(e.Buffer, frame + sampleBytes, format.BitsPerSample, isFloat) : l;
                outputTapL[w] = ClampSample(l); outputTapR[w] = ClampSample(r);
                w = (w + 1) & (InputTapLength - 1);
            }
            outputTapCount = Math.Min(InputTapLength, outputTapCount + frames);
            outputTapWrite = w;
            outputTapLastTick = Environment.TickCount;
            outputHasData = true;
        }
    }

    static float ReadLoopbackSample(byte[] data, int offset, int bits, bool isFloat) {
        if (isFloat) return BitConverter.ToSingle(data, offset);
        if (bits == 8) return (data[offset] - 128) / 128f;
        if (bits == 16) return BitConverter.ToInt16(data, offset) / 32768f;
        if (bits == 24) {
            int raw = data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16);
            if ((raw & 0x800000) != 0) raw |= unchecked((int)0xff000000);
            return raw / 8388608f;
        }
        if (bits == 32) return BitConverter.ToInt32(data, offset) / 2147483648f;
        return 0f;
    }

    static float ClampSample(float value) {
        if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
        return value > 1f ? 1f : (value < -1f ? -1f : value);
    }

    void StartSystemLoopback(string endpointId) {
        StopSystemLoopback();
        lock (outputTapLock) {
            outputTapCount = 0; outputTapWrite = 0; outputHasData = false; outputTapLastTick = 0;
            Array.Clear(outputTapL, 0, outputTapL.Length); Array.Clear(outputTapR, 0, outputTapR.Length);
        }
        MMDevice device = null;
        WasapiLoopbackCapture capture = null;
        try {
            if (analysisEnumerator == null) analysisEnumerator = new MMDeviceEnumerator();
            device = string.IsNullOrEmpty(endpointId)
                ? analysisEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
                : analysisEnumerator.GetDevice(endpointId);
            capture = new WasapiLoopbackCapture(device);
            capture.DataAvailable += TapSystemOutput;
            capture.RecordingStopped += delegate(object sender, StoppedEventArgs e) {
                if (e.Exception != null) MonitorError = e.Exception.Message;
            };
            systemLoopbackDevice = device;
            systemLoopback = capture;
            analysisEndpointId = endpointId;
            analysisConfigured = true;
            capture.StartRecording();
            MonitorError = null;
        } catch (Exception e) {
            MonitorError = e.Message;
            try { if (capture != null) capture.Dispose(); } catch { }
            try { if (device != null) device.Dispose(); } catch { }
            systemLoopback = null; systemLoopbackDevice = null;
            analysisEndpointId = endpointId;
            analysisConfigured = true;
        }
    }

    void StopSystemLoopback() {
        WasapiLoopbackCapture capture = systemLoopback;
        MMDevice device = systemLoopbackDevice;
        systemLoopback = null;
        systemLoopbackDevice = null;
        if (capture != null) {
            capture.DataAvailable -= TapSystemOutput;
            try { capture.StopRecording(); } catch { }
            try { capture.Dispose(); } catch { }
        }
        try { if (device != null) device.Dispose(); } catch { }
    }

    public void Start(int inId, int outId) {
        Start(inId, outId, null);
    }
    public void Start(int inId, int outId, string analysisEndpointId) {
        Stop();
        Error = null;
        inEndpoint = WasapiDevices.EndpointIdAt(true, inId);
        outEndpoint = WasapiDevices.EndpointIdAt(false, outId);
        if (string.IsNullOrEmpty(inEndpoint)) { Error = "Could not find that input device. Press Refresh and try again."; return; }
        if (string.IsNullOrEmpty(outEndpoint)) { Error = "Could not find that output device. Press Refresh and try again."; return; }
        SetAnalysisEndpoint(analysisEndpointId);
        run = true;
        th = new Thread(Run); th.IsBackground = true; th.Priority = ThreadPriority.Highest; th.Name = "StageJack audio"; th.Start();
    }
    public void SetAnalysisEndpoint(string endpointId) {
        if (analysisConfigured && string.Equals(this.analysisEndpointId, endpointId, StringComparison.OrdinalIgnoreCase)
            && systemLoopback != null) return;
        StartSystemLoopback(endpointId);
    }
    public void Stop() { run = false; if (th != null) { th.Join(3000); th = null; } }
    public void Shutdown() { Stop(); StopSystemLoopback(); try { if (analysisEnumerator != null) analysisEnumerator.Dispose(); } catch { } analysisEnumerator = null; analysisConfigured = false; analysisEndpointId = null; }

    static int FloatBits(float f) { return BitConverter.ToInt32(BitConverter.GetBytes(f), 0); }
    static bool Transient(int hr) {
        return hr == AUDCLNT_E_DEVICE_INVALIDATED || hr == AUDCLNT_E_SERVICE_NOT_RUNNING
            || hr == AUDCLNT_E_NOT_INITIALIZED || hr == AUDCLNT_E_ENDPOINT_CREATE_FAILED
            || hr == unchecked((int)0x8007001F) // ERROR_GEN_FAILURE – common on virtual pins
            || hr == unchecked((int)0x80070057); // E_INVALIDARG from some cable drivers on re-init
    }
    static void Hr(int hr, string what) {
        if (hr < 0) throw new Exception("Could not " + what + " (Windows audio error 0x" + hr.ToString("X8") + ").");
    }
    static string FriendlyFail(Exception e) {
        string m = e.Message ?? "";
        if (m.IndexOf("0x88890004", StringComparison.OrdinalIgnoreCase) >= 0)
            return "The audio device was unplugged or reset (common with virtual cables / Sunshine). StageJack will keep trying.";
        return m;
    }

    // Opens one WASAPI client. Tries event-driven first; falls back to pull mode for drivers that hate EVENTCALLBACK.
    static WasapiNative.IAudioClient OpenClient(string endpointId, bool capture, AutoResetEvent frameEvent, bool useEvent, out IntPtr mixFormat, out int bufferFrames, out int frameBytes, out int channels, out int sampleBits, out int validBits, out bool float32, out bool eventArmed, bool loopback = false) {
        eventArmed = false;
        WasapiNative.IMMDeviceEnumerator en = WasapiNative.CreateEnumerator();
        WasapiNative.IMMDevice dev;
        Hr(en.GetDevice(endpointId, out dev), capture ? "open the input device" : "open the headset or speakers");
        object o;
        Guid iid = WasapiNative.IID_IAudioClient;
        Hr(dev.Activate(ref iid, WasapiNative.CLSCTX_ALL, IntPtr.Zero, out o), "activate audio on that device");
        WasapiNative.IAudioClient client = (WasapiNative.IAudioClient)o;
        Hr(client.GetMixFormat(out mixFormat), "read the device format");
        WasapiNative.WAVEFORMATEX fmt = (WasapiNative.WAVEFORMATEX)Marshal.PtrToStructure(mixFormat, typeof(WasapiNative.WAVEFORMATEX));
        channels = fmt.nChannels < 1 ? 1 : fmt.nChannels;
        sampleBits = fmt.wBitsPerSample;
        validBits = sampleBits;
        ushort formatTag = fmt.wFormatTag;
        if (formatTag == 0xFFFE && fmt.cbSize >= 22) {
            validBits = unchecked((ushort)Marshal.ReadInt16(mixFormat, 18));
            Guid subFormat = (Guid)Marshal.PtrToStructure(IntPtr.Add(mixFormat, 24), typeof(Guid));
            if (subFormat == new Guid("00000003-0000-0010-8000-00aa00389b71")) formatTag = 3;
            else if (subFormat == new Guid("00000001-0000-0010-8000-00aa00389b71")) formatTag = 1;
            else formatTag = 0;
        }
        float32 = formatTag == 3 && sampleBits == 32;
        if (sampleBits == 0) sampleBits = 16;
        if (validBits <= 0 || validBits > sampleBits) validBits = sampleBits;
        frameBytes = fmt.nBlockAlign > 0 ? fmt.nBlockAlign : (sampleBits / 8) * channels;
        if (frameBytes <= 0) frameBytes = 4 * channels;

        Guid session = Guid.Empty;
        int flags = WasapiNative.AUDCLNT_STREAMFLAGS_NOPERSIST;
        if (useEvent) flags |= WasapiNative.AUDCLNT_STREAMFLAGS_EVENTCALLBACK;
        if (loopback) flags |= WasapiNative.AUDCLNT_STREAMFLAGS_LOOPBACK;
        int hr = client.Initialize(WasapiNative.AUDCLNT_SHAREMODE_SHARED, flags, HnsBuffer, 0, mixFormat, ref session);
        if (hr < 0 && useEvent) {
            // Driver rejected event mode – rebuild client in pull mode
            WasapiNative.CoTaskMemFree(mixFormat); mixFormat = IntPtr.Zero;
            Hr(dev.Activate(ref iid, WasapiNative.CLSCTX_ALL, IntPtr.Zero, out o), "activate audio on that device");
            client = (WasapiNative.IAudioClient)o;
            Hr(client.GetMixFormat(out mixFormat), "read the device format");
            fmt = (WasapiNative.WAVEFORMATEX)Marshal.PtrToStructure(mixFormat, typeof(WasapiNative.WAVEFORMATEX));
            channels = fmt.nChannels < 1 ? 1 : fmt.nChannels;
            sampleBits = fmt.wBitsPerSample;
            validBits = sampleBits;
            formatTag = fmt.wFormatTag;
            if (formatTag == 0xFFFE && fmt.cbSize >= 22) {
                validBits = unchecked((ushort)Marshal.ReadInt16(mixFormat, 18));
                Guid subFormat = (Guid)Marshal.PtrToStructure(IntPtr.Add(mixFormat, 24), typeof(Guid));
                if (subFormat == new Guid("00000003-0000-0010-8000-00aa00389b71")) formatTag = 3;
                else if (subFormat == new Guid("00000001-0000-0010-8000-00aa00389b71")) formatTag = 1;
                else formatTag = 0;
            }
            float32 = formatTag == 3 && sampleBits == 32;
            if (sampleBits == 0) sampleBits = 16;
            if (validBits <= 0 || validBits > sampleBits) validBits = sampleBits;
            frameBytes = fmt.nBlockAlign > 0 ? fmt.nBlockAlign : (sampleBits / 8) * channels;
            if (frameBytes <= 0) frameBytes = 4 * channels;
            session = Guid.Empty;
            flags = WasapiNative.AUDCLNT_STREAMFLAGS_NOPERSIST;
            hr = client.Initialize(WasapiNative.AUDCLNT_SHAREMODE_SHARED, flags, HnsBuffer, 0, mixFormat, ref session);
        }
        Hr(hr, capture ? "start listening on the input" : "start playing on the output");
        if (useEvent && (flags & WasapiNative.AUDCLNT_STREAMFLAGS_EVENTCALLBACK) != 0) {
            hr = client.SetEventHandle(frameEvent.SafeWaitHandle.DangerousGetHandle());
            if (hr >= 0) eventArmed = true;
        }
        Hr(client.GetBufferSize(out bufferFrames), "read the buffer size");
        return client;
    }

    static void CloseSession(ref WasapiNative.IAudioClient inClient, ref WasapiNative.IAudioClient outClient, ref IntPtr inFmt, ref IntPtr outFmt) {
        try { if (inClient != null) inClient.Stop(); } catch { }
        try { if (outClient != null) outClient.Stop(); } catch { }
        if (inFmt != IntPtr.Zero) { WasapiNative.CoTaskMemFree(inFmt); inFmt = IntPtr.Zero; }
        if (outFmt != IntPtr.Zero) { WasapiNative.CoTaskMemFree(outFmt); outFmt = IntPtr.Zero; }
        inClient = null; outClient = null;
    }

    void Run() {
        AutoResetEvent inEv = new AutoResetEvent(false);
        AutoResetEvent outEv = new AutoResetEvent(false);
        int openFails = 0;
        try {
            while (run) {
                WasapiNative.IAudioClient inClient = null, outClient = null;
                WasapiNative.IAudioCaptureClient capture = null;
                WasapiNative.IAudioRenderClient render = null;
                IntPtr inFmt = IntPtr.Zero, outFmt = IntPtr.Zero;
                try {
                    int inBufFrames, outBufFrames, inFrameBytes, outFrameBytes, inCh, outCh, inSampleBits, inValidBits, outSampleBits, outValidBits;
                    bool inFloat, outFloat, inEvent, outEvent;
                    inClient = OpenClient(inEndpoint, true, inEv, true, out inFmt, out inBufFrames, out inFrameBytes, out inCh, out inSampleBits, out inValidBits, out inFloat, out inEvent);
                    outClient = OpenClient(outEndpoint, false, outEv, true, out outFmt, out outBufFrames, out outFrameBytes, out outCh, out outSampleBits, out outValidBits, out outFloat, out outEvent);

                    object svc;
                    Guid iidCap = WasapiNative.IID_IAudioCaptureClient, iidRen = WasapiNative.IID_IAudioRenderClient;
                    Hr(inClient.GetService(ref iidCap, out svc), "reach the capture service"); capture = (WasapiNative.IAudioCaptureClient)svc;
                    Hr(outClient.GetService(ref iidRen, out svc), "reach the playback service"); render = (WasapiNative.IAudioRenderClient)svc;

                    int maxFrames = Math.Max(inBufFrames, outBufFrames) + 32;
                    short[] dspBuf = new short[maxFrames * 2];
                    float[] fTmp = new float[maxFrames * Math.Max(Math.Max(inCh, outCh), 2)];

                    int pad;
                    outClient.GetCurrentPadding(out pad);
                    int prime = outBufFrames - pad;
                    if (prime > 0) {
                        IntPtr pSilent;
                        if (render.GetBuffer(prime, out pSilent) == 0 && pSilent != IntPtr.Zero) {
                            int bytes = prime * outFrameBytes;
                            for (int i = 0; i < bytes; i++) Marshal.WriteByte(pSilent, i, 0);
                            render.ReleaseBuffer(prime, WasapiNative.AUDCLNT_BUFFERFLAGS_SILENT);
                        }
                    }

                    Hr(inClient.Start(), "start capture");
                    Hr(outClient.Start(), "start playback");
                    openFails = 0;
                    Error = null; // clear any earlier transient message once we are live

                    WaitHandle[] waits = new WaitHandle[] { inEv, outEv };
                    bool useWait = inEvent || outEvent;
                    int idleTicks = 0;

                    while (run) {
                        if (useWait) WaitHandle.WaitAny(waits, 15);
                        else Thread.Sleep(5);

                        bool gotData = false;
                        while (run) {
                            int packet;
                            int hrPkt = capture.GetNextPacketSize(out packet);
                            if (hrPkt < 0) {
                                if (Transient(hrPkt)) throw new Exception("Device reset while listening (0x" + hrPkt.ToString("X8") + ").");
                                break;
                            }
                            if (packet == 0) break;

                            IntPtr data;
                            int frames, flags;
                            long pos, qpc;
                            int hr = capture.GetBuffer(out data, out frames, out flags, out pos, out qpc);
                            if (hr < 0) {
                                if (Transient(hr)) throw new Exception("Device reset while listening (0x" + hr.ToString("X8") + ").");
                                break;
                            }
                            if (frames <= 0) { capture.ReleaseBuffer(0); break; }
                            if (frames > maxFrames) frames = maxFrames;
                            bool silent = (flags & WasapiNative.AUDCLNT_BUFFERFLAGS_SILENT) != 0;
                            gotData = true;

                            if (silent || data == IntPtr.Zero) {
                                for (int i = 0; i < frames * 2; i++) dspBuf[i] = 0;
                            } else if (inFloat) {
                                int n = frames * inCh;
                                Marshal.Copy(data, fTmp, 0, n);
                                for (int i = 0; i < frames; i++) {
                                    float L = fTmp[i * inCh];
                                    float R = inCh > 1 ? fTmp[i * inCh + 1] : L;
                                    if (L > 1f) L = 1f; else if (L < -1f) L = -1f;
                                    if (R > 1f) R = 1f; else if (R < -1f) R = -1f;
                                    dspBuf[i * 2] = (short)(L * 32767f);
                                    dspBuf[i * 2 + 1] = (short)(R * 32767f);
                                }
                            } else {
                                int bytesPerSamp = inSampleBits / 8;
                                if (inSampleBits == 8 || inSampleBits == 16 || inSampleBits == 24 || inSampleBits == 32) {
                                    for (int i = 0; i < frames; i++) {
                                        int frameOffset = i * inFrameBytes;
                                        float l = ReadPcmSample(data, frameOffset, inSampleBits, inValidBits);
                                        float r = inCh > 1 ? ReadPcmSample(data, frameOffset + bytesPerSamp, inSampleBits, inValidBits) : l;
                                        dspBuf[i * 2] = ToShort(l);
                                        dspBuf[i * 2 + 1] = ToShort(r);
                                    }
                                } else {
                                    for (int i = 0; i < frames * 2; i++) dspBuf[i] = 0;
                                }
                            }
                            capture.ReleaseBuffer(frames);

                            TapInput(dspBuf, frames);
                            Dsp.Process(dspBuf, frames);

                            int outPad;
                            int hrPad = outClient.GetCurrentPadding(out outPad);
                            if (hrPad < 0) {
                                if (Transient(hrPad)) throw new Exception("Device reset while playing (0x" + hrPad.ToString("X8") + ").");
                                continue;
                            }
                            int room = outBufFrames - outPad;
                            if (room <= 0) continue;
                            int write = frames < room ? frames : room;
                            IntPtr outPtr;
                            int hrGet = render.GetBuffer(write, out outPtr);
                            if (hrGet < 0 || outPtr == IntPtr.Zero) {
                                if (Transient(hrGet)) throw new Exception("Device reset while playing (0x" + hrGet.ToString("X8") + ").");
                                continue;
                            }

                            if (outFloat) {
                                for (int i = 0; i < write; i++) {
                                    float L = dspBuf[i * 2] / 32768f;
                                    float R = dspBuf[i * 2 + 1] / 32768f;
                                    for (int c = 0; c < outCh; c++) {
                                        float sv = (c == 0) ? L : (c == 1 ? R : 0f);
                                        Marshal.WriteInt32(outPtr, (i * outCh + c) * 4, FloatBits(sv));
                                    }
                                }
                            } else {
                                for (int i = 0; i < write; i++) {
                                    short L = dspBuf[i * 2], R = dspBuf[i * 2 + 1];
                                    for (int c = 0; c < outCh; c++) {
                                        short sv = (c == 0) ? L : (c == 1 ? R : (short)0);
                                        Marshal.WriteInt16(outPtr, i * outFrameBytes + c * 2, sv);
                                    }
                                }
                            }
                            int hrRel = render.ReleaseBuffer(write, 0);
                            if (hrRel < 0 && Transient(hrRel))
                                throw new Exception("Device reset while playing (0x" + hrRel.ToString("X8") + ").");
                        }

                        if (gotData) idleTicks = 0;
                        else {
                            idleTicks++;
                            // No capture packets for a long stretch is normal when silence is playing;
                            // only bail if the client itself starts failing (handled above).
                            if (idleTicks > 2000) idleTicks = 0;
                        }
                    }
                }
                catch (Exception e) {
                    CloseSession(ref inClient, ref outClient, ref inFmt, ref outFmt);
                    if (!run) break;
                    openFails++;
                    Error = FriendlyFail(e);
                    // Stay in-thread and retry: virtual devices (Sunshine jack, some cables) flap.
                    // After many failures, surface the error but keep trying until the user hits STOP.
                    int delay = openFails < 3 ? 200 : (openFails < 10 ? 500 : 1500);
                    int waited = 0;
                    while (run && waited < delay) { Thread.Sleep(50); waited += 50; }
                    continue;
                }
                finally {
                    CloseSession(ref inClient, ref outClient, ref inFmt, ref outFmt);
                }
            }
        }
        finally {
            inEv.Close();
            outEv.Close();
        }
    }

    static float ReadPcmSample(IntPtr data, int offset, int containerBits, int validBits) {
        long value;
        if (containerBits == 8) return (Marshal.ReadByte(data, offset) - 128) / 128f;
        if (containerBits == 16) value = Marshal.ReadInt16(data, offset);
        else if (containerBits == 24) {
            int raw = Marshal.ReadByte(data, offset) | (Marshal.ReadByte(data, offset + 1) << 8) | (Marshal.ReadByte(data, offset + 2) << 16);
            value = (raw & 0x800000) != 0 ? raw | unchecked((int)0xff000000) : raw;
        } else value = Marshal.ReadInt32(data, offset);
        if (validBits > 0 && validBits < containerBits) value >>= containerBits - validBits;
        int scaleBits = validBits > 0 && validBits <= containerBits ? validBits : containerBits;
        return (float)(value / (double)(1L << (scaleBits - 1)));
    }

    static short ToShort(float value) {
        if (value >= 1f) return short.MaxValue;
        if (value <= -1f) return short.MinValue;
        return (short)(value * 32768f);
    }
}

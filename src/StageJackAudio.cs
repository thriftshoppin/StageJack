using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

// ---------- Windows MME (winmm) interop: works on every Windows version, no extra libraries ----------
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct WaveFormat { public ushort wFormatTag, nChannels; public uint nSamplesPerSec, nAvgBytesPerSec; public ushort nBlockAlign, wBitsPerSample, cbSize; }

[StructLayout(LayoutKind.Sequential)]
public struct WaveHdr { public IntPtr lpData; public int dwBufferLength; public int dwBytesRecorded; public IntPtr dwUser; public int dwFlags; public int dwLoops; public IntPtr lpNext; public IntPtr reserved; }

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct WaveInCaps { public ushort Mid, Pid; public uint Ver; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name; public uint Formats; public ushort Channels, Reserved; }

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct WaveOutCaps { public ushort Mid, Pid; public uint Ver; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name; public uint Formats; public ushort Channels, Reserved; public uint Support; }

public sealed class Device { public int Id; public string Name; public override string ToString() { return Name; } }

public static class Mme {
    [DllImport("winmm.dll")] public static extern int waveInGetNumDevs();
    [DllImport("winmm.dll")] public static extern int waveOutGetNumDevs();
    [DllImport("winmm.dll", EntryPoint = "waveInGetDevCapsW", CharSet = CharSet.Unicode)] public static extern int waveInGetDevCaps(IntPtr id, ref WaveInCaps c, int size);
    [DllImport("winmm.dll", EntryPoint = "waveOutGetDevCapsW", CharSet = CharSet.Unicode)] public static extern int waveOutGetDevCaps(IntPtr id, ref WaveOutCaps c, int size);
    [DllImport("winmm.dll")] public static extern int waveInOpen(out IntPtr h, int dev, ref WaveFormat f, IntPtr cb, IntPtr inst, int flags);
    [DllImport("winmm.dll")] public static extern int waveInClose(IntPtr h);
    [DllImport("winmm.dll")] public static extern int waveInPrepareHeader(IntPtr h, IntPtr hdr, int size);
    [DllImport("winmm.dll")] public static extern int waveInUnprepareHeader(IntPtr h, IntPtr hdr, int size);
    [DllImport("winmm.dll")] public static extern int waveInAddBuffer(IntPtr h, IntPtr hdr, int size);
    [DllImport("winmm.dll")] public static extern int waveInStart(IntPtr h);
    [DllImport("winmm.dll")] public static extern int waveInStop(IntPtr h);
    [DllImport("winmm.dll")] public static extern int waveInReset(IntPtr h);
    [DllImport("winmm.dll")] public static extern int waveOutOpen(out IntPtr h, int dev, ref WaveFormat f, IntPtr cb, IntPtr inst, int flags);
    [DllImport("winmm.dll")] public static extern int waveOutClose(IntPtr h);
    [DllImport("winmm.dll")] public static extern int waveOutPrepareHeader(IntPtr h, IntPtr hdr, int size);
    [DllImport("winmm.dll")] public static extern int waveOutUnprepareHeader(IntPtr h, IntPtr hdr, int size);
    [DllImport("winmm.dll")] public static extern int waveOutWrite(IntPtr h, IntPtr hdr, int size);
    [DllImport("winmm.dll")] public static extern int waveOutReset(IntPtr h);

    public static List<Device> Inputs() {
        var l = new List<Device>(); int n = waveInGetNumDevs();
        for (int i = 0; i < n; i++) { var c = new WaveInCaps(); if (waveInGetDevCaps((IntPtr)i, ref c, Marshal.SizeOf(typeof(WaveInCaps))) == 0) l.Add(new Device { Id = i, Name = c.Name }); }
        return l;
    }
    public static List<Device> Outputs() {
        var l = new List<Device>(); int n = waveOutGetNumDevs();
        for (int i = 0; i < n; i++) { var c = new WaveOutCaps(); if (waveOutGetDevCaps((IntPtr)i, ref c, Marshal.SizeOf(typeof(WaveOutCaps))) == 0) l.Add(new Device { Id = i, Name = c.Name }); }
        return l;
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
    readonly float[] tap = new float[4096]; volatile int tapW;
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
        double rt = 0.6 * Math.Pow(12.0, pSize / 100.0);
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
    public void CopyRecent(float[] dst) { int n = dst.Length, w = tapW; for (int i = 0; i < n; i++) dst[i] = tap[(w - n + i) & 4095]; }

    static short ToS(float x) {
        float a = Math.Abs(x);
        if (a > 0.9f) { float t = 0.9f + 0.1f * (float)Math.Tanh((a - 0.9f) / 0.1f); x = x < 0 ? -t : t; }
        return (short)(x * 32767f);
    }

    // buf = interleaved 16-bit stereo, processed in place
    public void Process(short[] b, int frames) {
        if (version != seen) { lock (sync) { Recompute(); seen = version; } }
        if (Bypass) { for (int n = 0; n < frames; n++) { tap[tapW] = (b[2 * n] + b[2 * n + 1]) * (0.5f / 32768f); tapW = (tapW + 1) & 4095; } return; }
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
            tap[tapW] = (outL + outR) * 0.5f; tapW = (tapW + 1) & 4095;
            b[2 * n] = ToS(outL);
            b[2 * n + 1] = ToS(outR);
        }
    }
}

// ---------- Capture -> process -> playback ----------
public sealed class AudioEngine {
    public const int Rate = 48000, FramesPer = 480, InBufs = 4, OutBufs = 6;
    const int CALLBACK_EVENT = 0x50000, WHDR_DONE = 1, WHDR_INQUEUE = 0x10;
    static readonly int OffLen = (int)Marshal.OffsetOf(typeof(WaveHdr), "dwBufferLength");
    static readonly int OffRec = (int)Marshal.OffsetOf(typeof(WaveHdr), "dwBytesRecorded");
    static readonly int OffFlags = (int)Marshal.OffsetOf(typeof(WaveHdr), "dwFlags");
    public readonly Dsp Dsp = new Dsp(Rate);
    public volatile string Error;
    Thread th; volatile bool run; int inDev, outDev;
    public bool Running { get { return th != null && th.IsAlive; } }

    public void Start(int inId, int outId) {
        Stop(); Error = null; inDev = inId; outDev = outId; run = true;
        th = new Thread(Run); th.IsBackground = true; th.Priority = ThreadPriority.Highest; th.Name = "StageJack audio"; th.Start();
    }
    public void Stop() { run = false; if (th != null) { th.Join(2000); th = null; } }

    static void Check(int r, string what) {
        if (r != 0) throw new Exception("Could not " + what + " (Windows audio error " + r + (r == 32 ? ": device rejected 48 kHz 16-bit stereo" : r == 4 ? ": device is busy or unavailable" : "") + ").");
    }
    static void Alloc(out IntPtr hdr, out IntPtr data, int bytes, int hs) {
        hdr = Marshal.AllocHGlobal(hs); data = Marshal.AllocHGlobal(bytes);
        for (int i = 0; i < hs; i++) Marshal.WriteByte(hdr, i, 0);
        for (int i = 0; i < bytes; i++) Marshal.WriteByte(data, i, 0);
        Marshal.WriteIntPtr(hdr, 0, data); Marshal.WriteInt32(hdr, OffLen, bytes);
    }

    void Run() {
        IntPtr hin = IntPtr.Zero, hout = IntPtr.Zero; bool started = false;
        IntPtr[] ih = new IntPtr[InBufs], idat = new IntPtr[InBufs], oh = new IntPtr[OutBufs], odat = new IntPtr[OutBufs];
        int hs = Marshal.SizeOf(typeof(WaveHdr)), bytes = FramesPer * 4;
        var ev = new AutoResetEvent(false); short[] tmp = new short[FramesPer * 2];
        try {
            var f = new WaveFormat { wFormatTag = 1, nChannels = 2, nSamplesPerSec = Rate, nAvgBytesPerSec = Rate * 4, nBlockAlign = 4, wBitsPerSample = 16, cbSize = 0 };
            Check(Mme.waveInOpen(out hin, inDev, ref f, ev.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, CALLBACK_EVENT), "open the input device");
            Check(Mme.waveOutOpen(out hout, outDev, ref f, IntPtr.Zero, IntPtr.Zero, 0), "open the output device");
            for (int i = 0; i < InBufs; i++) { Alloc(out ih[i], out idat[i], bytes, hs); Check(Mme.waveInPrepareHeader(hin, ih[i], hs), "prepare input"); Check(Mme.waveInAddBuffer(hin, ih[i], hs), "queue input"); }
            for (int i = 0; i < OutBufs; i++) { Alloc(out oh[i], out odat[i], bytes, hs); Check(Mme.waveOutPrepareHeader(hout, oh[i], hs), "prepare output"); }
            for (int i = 0; i < 3; i++) Check(Mme.waveOutWrite(hout, oh[i], hs), "start output");
            Check(Mme.waveInStart(hin), "start capture"); started = true;
            int next = 0;
            while (run) {
                ev.WaitOne(30);
                while (run) {
                    if ((Marshal.ReadInt32(ih[next], OffFlags) & WHDR_DONE) == 0) break;
                    int frames = Marshal.ReadInt32(ih[next], OffRec) / 4;
                    if (frames > 0) {
                        int o = -1;
                        for (int k = 0; k < OutBufs; k++) if ((Marshal.ReadInt32(oh[k], OffFlags) & WHDR_INQUEUE) == 0) { o = k; break; }
                        if (o >= 0) {
                            Marshal.Copy(idat[next], tmp, 0, frames * 2);
                            Dsp.Process(tmp, frames);
                            Marshal.Copy(tmp, 0, odat[o], frames * 2);
                            Marshal.WriteInt32(oh[o], OffLen, frames * 4);
                            if (Mme.waveOutWrite(hout, oh[o], hs) != 0) throw new Exception("Output device disconnected.");
                        }
                    }
                    if (Mme.waveInAddBuffer(hin, ih[next], hs) != 0) throw new Exception("Input device disconnected.");
                    next = (next + 1) % InBufs;
                }
            }
        }
        catch (Exception e) { Error = e.Message; }
        finally {
            if (hin != IntPtr.Zero) { if (started) Mme.waveInStop(hin); Mme.waveInReset(hin); for (int i = 0; i < InBufs; i++) if (ih[i] != IntPtr.Zero) Mme.waveInUnprepareHeader(hin, ih[i], hs); Mme.waveInClose(hin); }
            if (hout != IntPtr.Zero) { Mme.waveOutReset(hout); for (int i = 0; i < OutBufs; i++) if (oh[i] != IntPtr.Zero) Mme.waveOutUnprepareHeader(hout, oh[i], hs); Mme.waveOutClose(hout); }
            for (int i = 0; i < InBufs; i++) { if (ih[i] != IntPtr.Zero) Marshal.FreeHGlobal(ih[i]); if (idat[i] != IntPtr.Zero) Marshal.FreeHGlobal(idat[i]); }
            for (int i = 0; i < OutBufs; i++) { if (oh[i] != IntPtr.Zero) Marshal.FreeHGlobal(oh[i]); if (odat[i] != IntPtr.Zero) Marshal.FreeHGlobal(odat[i]); }
            ev.Close();
        }
    }
}

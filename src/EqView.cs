using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ---------- Spectrum analyzer: FFT of the real output, mapped to log-spaced columns ----------
public sealed class SpectrumAnalyzer {
    public const int Size = 2048;
    public float FloorDb = -96f, CeilDb = -22f;     // display range (after the +3 dB/octave tilt); open enough to show real program material
    public readonly float[] Level;                  // 0..1 per column, smoothed
    readonly int cols; readonly float fs;
    readonly float[] win = new float[Size], re = new float[Size], im = new float[Size], db = new float[Size / 2 + 1];
    readonly float[] cosT = new float[Size / 2], sinT = new float[Size / 2]; readonly int[] rev = new int[Size];

    public SpectrumAnalyzer(int columns, float rate) {
        cols = columns; fs = rate; Level = new float[columns];
        for (int i = 0; i < Size; i++) win[i] = (float)(0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (Size - 1)));
        for (int i = 0; i < Size / 2; i++) { cosT[i] = (float)Math.Cos(2 * Math.PI * i / Size); sinT[i] = (float)Math.Sin(2 * Math.PI * i / Size); }
        int bits = 0; while ((1 << bits) < Size) bits++;
        for (int i = 0; i < Size; i++) { int r = 0; for (int b = 0; b < bits; b++) if ((i & (1 << b)) != 0) r |= 1 << (bits - 1 - b); rev[i] = r; }
    }

    void Fft() {
        for (int i = 0; i < Size; i++) { int j = rev[i]; if (j > i) { float t = re[i]; re[i] = re[j]; re[j] = t; t = im[i]; im[i] = im[j]; im[j] = t; } }
        for (int len = 2; len <= Size; len <<= 1) {
            int half = len >> 1, step = Size / len;
            for (int i = 0; i < Size; i += len) {
                for (int k = 0; k < half; k++) {
                    float wr = cosT[k * step], wi = -sinT[k * step]; int a = i + k, b = a + half;
                    float tr = re[b] * wr - im[b] * wi, ti = re[b] * wi + im[b] * wr;
                    re[b] = re[a] - tr; im[b] = im[a] - ti; re[a] += tr; im[a] += ti;
                }
            }
        }
    }

    // samples: Size mono samples (-1..1). silent = true lets the display fall to zero.
    public void Analyze(float[] samples, bool silent) {
        int half = Size / 2;
        if (!silent) {
            for (int i = 0; i < Size; i++) { re[i] = samples[i] * win[i]; im[i] = 0f; }
            Fft();
            for (int i = 0; i <= half; i++) {
                float mag = (float)Math.Sqrt(re[i] * re[i] + im[i] * im[i]) * (4f / Size);
                float v = 20f * (float)Math.Log10(mag + 1e-9f);
                if (i > 0) { double f = i * fs / Size; v += 3f * (float)(Math.Log(f / 1000.0) / Math.Log(2.0)); }   // pink-noise tilt
                db[i] = v;
            }
        }
        for (int c = 0; c < cols; c++) {
            float n = 0f;
            if (!silent) {
                double fLo = 20.0 * Math.Pow(1000.0, c / (double)cols), fHi = 20.0 * Math.Pow(1000.0, (c + 1) / (double)cols);
                double bLo = fLo * Size / fs, bHi = fHi * Size / fs; float v;
                if (bHi - bLo < 1.0) {
                    double bc = (bLo + bHi) * 0.5; int i0 = Math.Min((int)bc, half - 1); float fr = (float)(bc - i0);
                    v = db[i0] * (1f - fr) + db[i0 + 1] * fr;
                } else {
                    int i0 = (int)Math.Ceiling(bLo), i1 = Math.Min((int)Math.Floor(bHi), half); v = -200f;
                    for (int i = i0; i <= i1; i++) if (db[i] > v) v = db[i];
                }
                n = (v - FloorDb) / (CeilDb - FloorDb); if (n < 0f) n = 0f; if (n > 1f) n = 1f;
            }
            Level[c] += (n > Level[c] ? 0.60f : 0.14f) * (n - Level[c]);
            if (Level[c] < 0.001f) Level[c] = 0f;
        }
    }
}

// ---------- Visual EQ: live spectrum, EQ curve, draggable nodes ----------
public sealed class EqView : Control {
    public event Action<string, int> NodeDrag;      // (slider name, new dB value)
    const float DbRange = 15f;                      // vertical half-range of the curve
    float[] spec, curve; string[] names; float[] hz, nodeDb; bool[] major;
    int drag = -1, hover = -1;

    public EqView() {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(14, 17, 17); TabStop = false;
    }

    public void SetSpectrum(float[] levels) { spec = levels; Invalidate(); }
    public void SetCurve(float[] c) { curve = c; Invalidate(); }
    public void SetNodes(string[] n, float[] f, float[] d, bool[] m) { names = n; hz = f; nodeDb = d; major = m; Invalidate(); }

    const int Pad = 8;
    float XOf(float f) { double t = Math.Log(f / 20.0) / Math.Log(1000.0); return Pad + (float)(t * (Width - 2 * Pad)); }
    float YOf(float d) { float h = Height * 0.5f - Pad; return Height * 0.5f - d / DbRange * h; }
    float DbOf(float y) { float h = Height * 0.5f - Pad; return (Height * 0.5f - y) / h * DbRange; }

    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e); Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        Color acc = Theme.Accent; int w = Width, h = Height;
        using (SolidBrush bgb = new SolidBrush(BackColor)) g.FillRectangle(bgb, 0, 0, w, h);

        // grid
        using (Pen grid = new Pen(Color.FromArgb(34, 40, 39), 1f))
        using (Pen zero = new Pen(Color.FromArgb(70, 80, 76), 1f))
        using (SolidBrush txt = new SolidBrush(Color.FromArgb(120, 130, 126))) {
            float[] vf = { 50f, 100f, 200f, 500f, 1000f, 2000f, 5000f, 10000f };
            for (int i = 0; i < vf.Length; i++) g.DrawLine(grid, XOf(vf[i]), 0, XOf(vf[i]), h);
            float[] hd = { -12f, -6f, 6f, 12f };
            for (int i = 0; i < hd.Length; i++) g.DrawLine(grid, 0, YOf(hd[i]), w, YOf(hd[i]));
            g.DrawLine(zero, 0, YOf(0f), w, YOf(0f));
            Font f = Font;
            g.DrawString("100", f, txt, XOf(100f) + 2, h - f.Height - 1);
            g.DrawString("1k", f, txt, XOf(1000f) + 2, h - f.Height - 1);
            g.DrawString("10k", f, txt, XOf(10000f) + 2, h - f.Height - 1);
            g.DrawString("+12", f, txt, 2, YOf(12f) - f.Height * 0.5f);
            g.DrawString("-12", f, txt, 2, YOf(-12f) - f.Height * 0.5f);
        }

        // live output spectrum
        if (spec != null && spec.Length > 1) {
            int n = spec.Length; float bottom = h - 1, top = 4;
            PointF[] pts = new PointF[n + 2];
            pts[0] = new PointF(XOf(20f), bottom);
            for (int i = 0; i < n; i++) {
                float f = (float)(20.0 * Math.Pow(1000.0, (i + 0.5) / n));
                pts[i + 1] = new PointF(XOf(f), bottom - spec[i] * (bottom - top));
            }
            pts[n + 1] = new PointF(XOf(20000f), bottom);
            using (LinearGradientBrush fill = new LinearGradientBrush(new Rectangle(0, 0, w, h), Color.FromArgb(110, acc), Color.FromArgb(10, acc), 90f))
                g.FillPolygon(fill, pts);
            using (Pen edge = new Pen(Color.FromArgb(150, acc), 1.2f)) {
                PointF[] line = new PointF[n]; for (int i = 0; i < n; i++) line[i] = pts[i + 1];
                g.DrawLines(edge, line);
            }
        }

        // EQ curve
        if (curve != null && curve.Length > 1) {
            int n = curve.Length; PointF[] line = new PointF[n];
            for (int i = 0; i < n; i++) {
                float d = Math.Max(-DbRange, Math.Min(DbRange, curve[i]));
                line[i] = new PointF(XOf(EqModel.FreqAt(i, n)), YOf(d));
            }
            using (Pen p = new Pen(Color.FromArgb(240, 246, 244), 2.2f)) { p.LineJoin = LineJoin.Round; g.DrawLines(p, line); }
        }

        // nodes
        if (names != null) {
            for (int i = 0; i < names.Length; i++) {
                float x = XOf(hz[i]), y = YOf(Math.Max(-DbRange, Math.Min(DbRange, nodeDb[i])));
                float r = major[i] ? 7f : 4.5f; bool active = (i == drag || i == hover);
                if (active) using (SolidBrush halo = new SolidBrush(Color.FromArgb(60, acc))) g.FillEllipse(halo, x - r - 6, y - r - 6, 2 * (r + 6), 2 * (r + 6));
                using (SolidBrush dot = new SolidBrush(major[i] ? acc : Color.FromArgb(200, acc))) g.FillEllipse(dot, x - r, y - r, 2 * r, 2 * r);
                using (SolidBrush core = new SolidBrush(Color.FromArgb(15, 17, 17))) g.FillEllipse(core, x - r * 0.4f, y - r * 0.4f, r * 0.8f, r * 0.8f);
                if (active) {
                    string t = (nodeDb[i] > 0 ? "+" : "") + nodeDb[i].ToString("0") + " dB";
                    using (SolidBrush tb = new SolidBrush(Color.FromArgb(238, 242, 240))) g.DrawString(t, Font, tb, Math.Min(x + 10, w - 52), Math.Max(2, y - 22));
                }
            }
        }
        using (Pen border = new Pen(Color.FromArgb(48, 56, 54), 1f)) g.DrawRectangle(border, 0, 0, w - 1, h - 1);
    }

    int Hit(int mx, int my) {
        if (names == null) return -1;
        int best = -1; float bestD = 18f * 18f;
        for (int i = 0; i < names.Length; i++) {
            float dx = XOf(hz[i]) - mx, dy = YOf(Math.Max(-DbRange, Math.Min(DbRange, nodeDb[i]))) - my, d = dx * dx + dy * dy;
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }
    void Emit(int i, int y) {
        int v = (int)Math.Round(DbOf(y)); if (v > 12) v = 12; if (v < -12) v = -12;
        if (NodeDrag != null) NodeDrag(names[i], v);
    }
    protected override void OnMouseDown(MouseEventArgs e) {
        base.OnMouseDown(e); int i = Hit(e.X, e.Y);
        if (i >= 0 && e.Button == MouseButtons.Left) { drag = i; Capture = true; Emit(i, e.Y); }
    }
    protected override void OnMouseMove(MouseEventArgs e) {
        base.OnMouseMove(e);
        if (drag >= 0) { Emit(drag, e.Y); return; }
        int i = Hit(e.X, e.Y); if (i != hover) { hover = i; Cursor = i >= 0 ? Cursors.SizeNS : Cursors.Default; Invalidate(); }
    }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); drag = -1; Capture = false; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); if (hover != -1) { hover = -1; Invalidate(); } }
    protected override void OnMouseDoubleClick(MouseEventArgs e) {
        base.OnMouseDoubleClick(e); int i = Hit(e.X, e.Y);
        if (i >= 0 && NodeDrag != null) NodeDrag(names[i], 0);   // double-click a node to reset it
    }
}

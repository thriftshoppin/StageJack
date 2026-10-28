using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public static class Theme {
    public static readonly Color Accent = Color.FromArgb(0, 190, 255);
    public static readonly Color AccentDim = Color.FromArgb(40, 0, 190, 255);
}

public class StageSlider : Control {
    int minimum = 0, maximum = 100, value = 50; bool dragging = false;
    public event EventHandler ValueChanged;
    public int Minimum { get { return minimum; } set { minimum = value; Invalidate(); } }
    public int Maximum { get { return maximum; } set { maximum = value; Invalidate(); } }
    public int Value {
        get { return value; }
        set {
            int n = Math.Max(minimum, Math.Min(maximum, value));
            if (n != this.value) { this.value = n; Invalidate(); if (ValueChanged != null) ValueChanged(this, EventArgs.Empty); }
        }
    }
    public StageSlider() {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Height = 38; Cursor = Cursors.Hand; BackColor = Color.FromArgb(27, 30, 30);
    }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        int left = 14, right = Width - 14, y = Height / 2;
        float t = maximum == minimum ? 0 : (float)(value - minimum) / (maximum - minimum);
        int x = left + (int)((right - left) * t);
        using (var track = new Pen(Color.FromArgb(66, 76, 72), 5)) { track.StartCap = track.EndCap = LineCap.Round; e.Graphics.DrawLine(track, left, y, right, y); }
        using (var fill = new Pen(Theme.Accent, 5)) { fill.StartCap = fill.EndCap = LineCap.Round; e.Graphics.DrawLine(fill, left, y, x, y); }
        using (var shadow = new SolidBrush(Theme.AccentDim)) e.Graphics.FillEllipse(shadow, x - 12, y - 12, 24, 24);
        using (var knob = new SolidBrush(Theme.Accent)) e.Graphics.FillEllipse(knob, x - 7, y - 7, 14, 14);
        using (var core = new SolidBrush(Color.FromArgb(15, 17, 17))) e.Graphics.FillEllipse(core, x - 3, y - 3, 6, 6);
    }
    int Position(int x) {
        int l = 14, r = Width - 14;
        float t = Math.Max(0, Math.Min(1, (float)(x - l) / (r - l)));
        return minimum + (int)Math.Round(t * (maximum - minimum));
    }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); dragging = true; Capture = true; Value = Position(e.X); }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (dragging) Value = Position(e.X); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); dragging = false; Capture = false; }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Focus(); }
    protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); Value += Math.Sign(e.Delta); }
}

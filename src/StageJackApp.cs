using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

static class Program {
    [STAThread] static void Main() {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        try { Application.Run(new MainWindow()); }
        catch (Exception e) { MessageBox.Show(e.Message, "StageJack", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}

// Named reverb spaces. Values come from measured behavior of the reference reverb (decay, tone, width, level, pre-delay), not from copied audio.
// EDIT THIS TABLE to tune a space. T30 = seconds for the tail to fall 30 dB; Tone 0 (dark) - 100 (bright); Width 0-100; Wet = Reverb amount 0-100; Pre = pre-delay ms (keep 0-5).
sealed class ReverbSpace {
    public readonly string Name; public readonly float T30; public readonly int Tone, Width, Wet, Pre;
    public ReverbSpace(string name, float t30, int tone, int width, int wet, int pre) { Name = name; T30 = t30; Tone = tone; Width = width; Wet = wet; Pre = pre; }
    public static readonly ReverbSpace[] All = {
        new ReverbSpace("Campus",     0.55f, 80,  70, 25, 2),   // brightest
        new ReverbSpace("Rhythm",     0.35f, 60,  60, 15, 0),   // quieter
        new ReverbSpace("Classroom",  0.18f, 55,  50, 15, 0),   // shortest, quieter
        new ReverbSpace("Dorm",       0.45f, 25,  80, 35, 3),   // dark, louder
        new ReverbSpace("Union",      0.90f, 20,  90, 40, 4),   // longest, dark, louder
        new ReverbSpace("Innovation", 0.70f, 45,  85, 38, 2)    // louder
    };
    // Engine tail length is RT60 = 0.3 * 24^(size/100) seconds, and RT60 is about 2 x T30
    public static int SizeFor(float t30) {
        double s = 100.0 * Math.Log(2.0 * t30 / 0.3) / Math.Log(24.0);
        return (int)Math.Max(0, Math.Min(100, Math.Round(s)));
    }
}

sealed class MainWindow : Form {
    readonly Color accent = Theme.Accent, bg = Color.FromArgb(11, 13, 13), card = Color.FromArgb(23, 27, 26), muted = Color.FromArgb(160, 170, 166), white = Color.FromArgb(238, 242, 240);
    readonly Dictionary<string, StageSlider> sliders = new Dictionary<string, StageSlider>();
    static readonly string[] bands = { "31 Hz", "62 Hz", "125 Hz", "250 Hz", "500 Hz", "1 kHz", "2 kHz", "4 kHz", "8 kHz", "16 kHz" };
    readonly AudioEngine engine = new AudioEngine();
    readonly string savePath;
    readonly Timer saveTimer, pollTimer, pingTimer;
    readonly Label status;
    readonly PrivateFontCollection fonts = new PrivateFontCollection();
    Font regular, strong, display, small;
    ComboBox inBox, outBox, spaceBox; bool applyingSpace; Button startBtn, bypassBtn;
    static readonly string[] spaceSliders = { "Reverb", "Reverb size", "Reverb tone", "Reverb pre-delay", "Reverb width" };
    EqView eqView; CheckBox advChk; Panel eqSection, bandPanel, vp; Timer viewTimer;
    readonly SpectrumAnalyzer analyzer = new SpectrumAnalyzer(128, AudioEngine.Rate); readonly float[] fftBuf = new float[SpectrumAnalyzer.Size];
    bool ready, wantRun, waiting; int tries, autoFails; DateTime lastStart = DateTime.MinValue; string wantIn, wantOut;

    public MainWindow() {
        Text = "StageJack"; StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(620, 900); MinimumSize = new Size(560, 740);
        BackColor = bg; ForeColor = white; FormBorderStyle = FormBorderStyle.Sizable; Font = new Font("Segoe UI", 9);
        string dir = AppDomain.CurrentDomain.BaseDirectory; savePath = Path.Combine(dir, "settings.ini");
        try {
            fonts.AddFontFile(Path.Combine(dir, "Rajdhani-Regular.ttf")); fonts.AddFontFile(Path.Combine(dir, "Rajdhani-SemiBold.ttf"));
            regular = new Font(fonts.Families[0], 14); strong = new Font(fonts.Families[1], 14, FontStyle.Bold); display = new Font(fonts.Families[1], 24, FontStyle.Bold); small = new Font(fonts.Families[0], 10);
        } catch {
            regular = new Font("Segoe UI", 11); strong = new Font("Segoe UI Semibold", 11, FontStyle.Bold); display = new Font("Segoe UI Semibold", 19, FontStyle.Bold); small = new Font("Segoe UI", 8);
        }

        Panel header = new Panel { Dock = DockStyle.Top, Height = 100, BackColor = bg };
        header.Controls.Add(new Label { Text = "STAGEJACK", ForeColor = accent, Font = display, AutoSize = true, Location = new Point(22, 12) });
        status = new Label { Text = "●  STOPPED", ForeColor = muted, Font = small, AutoSize = true, Location = new Point(25, 55) }; header.Controls.Add(status);
        Button reset = MakeButton("RESET", 90, 34); reset.Location = new Point(500, 28); reset.Anchor = AnchorStyles.Top | AnchorStyles.Right; reset.Click += delegate { ResetAll(); }; header.Controls.Add(reset);
        Panel viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = bg };
        vp = viewport;
        Controls.Add(header); Controls.Add(viewport); header.BringToFront();

        saveTimer = new Timer { Interval = 500 }; saveTimer.Tick += delegate { saveTimer.Stop(); Save(); };
        pollTimer = new Timer { Interval = 500 }; pollTimer.Tick += delegate { Poll(); }; pollTimer.Start();
        viewTimer = new Timer { Interval = 33 }; viewTimer.Tick += delegate { UpdateSpectrum(); }; viewTimer.Start();
        pingTimer = new Timer { Interval = 3000 }; pingTimer.Tick += delegate { Ping(); };

        Panel route = Section(viewport, "ROUTING", 250);
        route.Controls.Add(MakeLabel("INPUT  /  your virtual cable", 18, 42));
        inBox = MakeCombo(66); route.Controls.Add(inBox);
        route.Controls.Add(MakeLabel("OUTPUT  /  your headset", 18, 110));
        outBox = MakeCombo(134); route.Controls.Add(outBox);
        startBtn = MakeButton("START", 150, 40); startBtn.Location = new Point(18, 192); startBtn.Click += delegate { Toggle(); }; route.Controls.Add(startBtn);
        bypassBtn = MakeButton("BYPASS: OFF", 150, 40); bypassBtn.Location = new Point(178, 192); bypassBtn.Click += delegate { engine.Dsp.Bypass = !engine.Dsp.Bypass; bypassBtn.Text = engine.Dsp.Bypass ? "BYPASS: ON" : "BYPASS: OFF"; }; route.Controls.Add(bypassBtn);
        Button refresh = MakeButton("REFRESH", 150, 40); refresh.Location = new Point(338, 192); refresh.Click += delegate { Populate(wantIn ?? SelName(inBox), wantOut ?? SelName(outBox)); Resolve(); }; route.Controls.Add(refresh);

        Panel stage = Section(viewport, "SOUND STAGE", 450);
        AddSlider(stage, "Reverb", 42, 0, 100, 20, "DRY", "WET", "%");
        AddSlider(stage, "Stage width", 120, 0, 100, 50, "NARROW", "WIDE", "");
        AddSlider(stage, "Stage depth", 199, 0, 100, 0, "CLOSE", "DEEP", "");
        AddSlider(stage, "Stage height", 278, 0, 100, 50, "LOW", "TALL", "");
        AddSlider(stage, "Output level", 357, -12, 6, 0, "QUIETER", "LOUDER", " dB");

        Panel rev = Section(viewport, "REVERB  /  SEPARATION", 608);
        rev.Controls.Add(MakeLabel("SPACE  /  preset", 18, 42));
        spaceBox = MakeCombo(66); spaceBox.Items.Add("Custom");
        foreach (ReverbSpace sp in ReverbSpace.All) spaceBox.Items.Add(sp.Name);
        spaceBox.SelectedIndex = 0; rev.Controls.Add(spaceBox);
        AddSlider(rev, "Reverb size", 120, 0, 100, 75, "SHORT", "HUGE", "");
        AddSlider(rev, "Reverb tone", 198, 0, 100, 60, "DARK", "BRIGHT", "");
        AddSlider(rev, "Reverb pre-delay", 277, 0, 60, 30, "0 MS", "60 MS", " ms");
        AddSlider(rev, "Reverb clarity", 356, 0, 100, 50, "OFF", "MAX", "");
        AddSlider(rev, "Reverb width", 435, 0, 100, 100, "CENTER", "WIDE", "");
        AddSlider(rev, "Smoothness", 514, 0, 100, 60, "TIGHT", "SMOOTH", "");
        spaceBox.SelectedIndexChanged += delegate { if (!applyingSpace && ready && spaceBox.SelectedIndex > 0) { ApplySpace(ReverbSpace.All[spaceBox.SelectedIndex - 1]); saveTimer.Stop(); saveTimer.Start(); } };

        eqSection = Section(viewport, "EQUALIZER", 500);
        advChk = new CheckBox { Text = "ADVANCED  /  10-BAND", ForeColor = accent, BackColor = card, Font = strong, AutoSize = true, FlatStyle = FlatStyle.Flat, TabStop = false, Cursor = Cursors.Hand, Location = new Point(318, 8) };
        eqSection.Controls.Add(advChk);
        eqView = new EqView { Location = new Point(18, 44), Size = new Size(494, 190), Font = small };
        eqView.NodeDrag += delegate(string name, int v) { StageSlider sl; if (sliders.TryGetValue(name, out sl)) sl.Value = v; };
        eqSection.Controls.Add(eqView);
        AddSlider(eqSection, "Low", 252, -12, 12, 0, "BASS  /  below 150 Hz", "", " dB");
        AddSlider(eqSection, "Mid", 331, -12, 12, 0, "VOICE  /  around 1 kHz", "", " dB");
        AddSlider(eqSection, "High", 410, -12, 12, 0, "TREBLE  /  above 5 kHz", "", " dB");
        bandPanel = new Panel { Location = new Point(0, 500), Size = new Size(530, 660), BackColor = card, Visible = false };
        bandPanel.Controls.Add(new Label { Text = "10-BAND  /  fine control", ForeColor = accent, Font = strong, AutoSize = true, Location = new Point(15, 4) });
        for (int i = 0; i < bands.Length; i++) AddSlider(bandPanel, bands[i], 36 + i * 62, -12, 12, 0, "", "", " dB");
        eqSection.Controls.Add(bandPanel);
        advChk.CheckedChanged += delegate { ApplyAdvanced(); if (ready) { PushParams(); saveTimer.Stop(); saveTimer.Start(); } };
        ApplyAdvanced();

        Shown += delegate { ActiveControl = null; viewport.AutoScrollPosition = Point.Empty; };

        string savedIn = null, savedOut = null;
        LoadSaved(out savedIn, out savedOut);
        Populate(savedIn, savedOut);
        wantIn = savedIn ?? SelName(inBox); wantOut = savedOut ?? SelName(outBox);
        inBox.SelectedIndexChanged += delegate { if (ready) { wantIn = SelName(inBox); Restart(); Resolve(); saveTimer.Stop(); saveTimer.Start(); } };
        outBox.SelectedIndexChanged += delegate { if (ready) { wantOut = SelName(outBox); Restart(); Resolve(); saveTimer.Stop(); saveTimer.Start(); } };
        ready = true;
        PushParams();
        if (Missing()) BeginWait(); else if (wantRun) Start();

        FormClosing += delegate { viewTimer.Stop(); pingTimer.Stop(); engine.Stop(); Save(); };
    }

    Button MakeButton(string text, int w, int h) {
        Button b = new Button { Text = text, FlatStyle = FlatStyle.Flat, ForeColor = accent, BackColor = card, Font = strong, Size = new Size(w, h), TabStop = false };
        b.FlatAppearance.BorderColor = accent; return b;
    }
    Label MakeLabel(string text, int x, int y) { return new Label { Text = text, ForeColor = white, Font = regular, AutoSize = true, Location = new Point(x, y) }; }
    ComboBox MakeCombo(int y) {
        return new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(35, 40, 39), ForeColor = white, Font = regular, Width = 490, Location = new Point(18, y), TabStop = false };
    }
    Panel Section(Panel parent, string title, int height) {
        int y = parent.Controls.Count == 0 ? 12 : parent.Controls[parent.Controls.Count - 1].Bottom + 13;
        Panel p = new Panel { Width = 530, Height = height, BackColor = card, Location = new Point(16, y) };
        p.Controls.Add(new Label { Text = title, ForeColor = accent, Font = strong, AutoSize = true, Location = new Point(15, 12) });
        parent.Controls.Add(p); return p;
    }
    void AddSlider(Panel parent, string name, int y, int min, int max, int value, string left, string right, string unit) {
        parent.Controls.Add(MakeLabel(name, 18, y));
        Label output = new Label { Text = value + unit, ForeColor = accent, Font = strong, Width = 72, Height = 25, TextAlign = ContentAlignment.MiddleRight, Location = new Point(412, y - 2) }; parent.Controls.Add(output);
        StageSlider slider = new StageSlider { Minimum = min, Maximum = max, Value = value, Width = 490, Height = 36, Location = new Point(12, y + 22), BackColor = card, Name = name, TabStop = false };
        parent.Controls.Add(slider); sliders[name] = slider;
        if (!String.IsNullOrEmpty(left)) parent.Controls.Add(new Label { Text = left, ForeColor = muted, Font = small, AutoSize = true, Location = new Point(18, y + 61) });
        if (!String.IsNullOrEmpty(right)) parent.Controls.Add(new Label { Text = right, ForeColor = muted, Font = small, AutoSize = true, Location = new Point(435, y + 61) });
        slider.ValueChanged += delegate { output.Text = slider.Value + unit; if (!applyingSpace && spaceBox != null && spaceBox.SelectedIndex != 0 && Array.IndexOf(spaceSliders, name) >= 0) spaceBox.SelectedIndex = 0; if (ready) { PushParams(); saveTimer.Stop(); saveTimer.Start(); } };
    }

    void PushParams() {
        float[] eq = new float[bands.Length];
        for (int i = 0; i < eq.Length; i++) eq[i] = sliders[bands[i]].Value;
        engine.Dsp.Set(sliders["Reverb"].Value, sliders["Stage width"].Value, sliders["Stage depth"].Value, sliders["Stage height"].Value, sliders["Output level"].Value, sliders["Reverb size"].Value, sliders["Reverb tone"].Value, sliders["Reverb pre-delay"].Value, sliders["Reverb clarity"].Value, sliders["Reverb width"].Value, sliders["Smoothness"].Value, sliders["Low"].Value, sliders["Mid"].Value, sliders["High"].Value, advChk.Checked, eq);
        RefreshEq();
    }
    void ApplyAdvanced() {
        bool on = advChk.Checked; bandPanel.Visible = on; eqSection.Height = on ? 1160 : 500;
        vp.AutoScrollMinSize = new Size(0, eqSection.Bottom + 16);
    }
    // redraw the EQ curve and nodes from the current slider values
    void RefreshEq() {
        if (eqView == null) return;
        float[] bv = new float[bands.Length];
        for (int i = 0; i < bv.Length; i++) bv[i] = sliders[bands[i]].Value;
        bool adv = advChk.Checked;
        eqView.SetCurve(EqModel.Curve(160, AudioEngine.Rate, sliders["Low"].Value, sliders["Mid"].Value, sliders["High"].Value, sliders["Stage height"].Value, adv, bv));
        int n = adv ? 3 + bands.Length : 3;
        string[] names = new string[n]; float[] hz = new float[n], db = new float[n]; bool[] major = new bool[n];
        names[0] = "Low"; hz[0] = EqModel.LowHz; names[1] = "Mid"; hz[1] = EqModel.MidHz; names[2] = "High"; hz[2] = EqModel.HighHz;
        for (int i = 0; i < 3; i++) { db[i] = sliders[names[i]].Value; major[i] = true; }
        if (adv) for (int i = 0; i < bands.Length; i++) { names[3 + i] = bands[i]; hz[3 + i] = Dsp.Freqs[i]; db[3 + i] = bv[i]; }
        eqView.SetNodes(names, hz, db, major);
    }
    void UpdateSpectrum() {
        if (eqView == null || !eqView.Visible || WindowState == FormWindowState.Minimized) return;
        bool live = engine.Running;
        if (live) engine.Dsp.CopyRecent(fftBuf);
        analyzer.Analyze(fftBuf, !live);
        eqView.SetSpectrum(analyzer.Level);
    }
    void ApplySpace(ReverbSpace sp) {
        applyingSpace = true;
        try {
            sliders["Reverb"].Value = sp.Wet; sliders["Reverb size"].Value = ReverbSpace.SizeFor(sp.T30); sliders["Reverb tone"].Value = sp.Tone;
            sliders["Reverb width"].Value = sp.Width; sliders["Reverb pre-delay"].Value = sp.Pre;
        } finally { applyingSpace = false; }
        PushParams();
    }
    void ResetAll() {
        applyingSpace = true;
        sliders["Reverb"].Value = 20; sliders["Stage width"].Value = 50; sliders["Stage depth"].Value = 0; sliders["Stage height"].Value = 50; sliders["Output level"].Value = 0;
        sliders["Reverb size"].Value = 75; sliders["Reverb tone"].Value = 60; sliders["Reverb pre-delay"].Value = 30; sliders["Reverb clarity"].Value = 50; sliders["Reverb width"].Value = 100; sliders["Smoothness"].Value = 60;
        sliders["Low"].Value = 0; sliders["Mid"].Value = 0; sliders["High"].Value = 0;
        foreach (string b in bands) sliders[b].Value = 0;
        applyingSpace = false; spaceBox.SelectedIndex = 0;
    }

    // ----- devices -----
    string SelName(ComboBox b) { Device d = b.SelectedItem as Device; return d == null ? null : d.Name; }
    static bool Has(string s, string part) { return s.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0; }
    static bool IsVirtual(string n) { return Has(n, "cable") || Has(n, "virtual") || Has(n, "voicemeeter") || Has(n, "vb-audio"); }
    static string Key(string n) { int a = n.IndexOf('('); return a < 0 ? "" : n.Substring(a).ToLowerInvariant(); }

    void Populate(string wantIn, string wantOut) {
        bool was = ready; ready = false;
        inBox.Items.Clear(); outBox.Items.Clear();
        foreach (Device d in Mme.Inputs()) inBox.Items.Add(d);
        foreach (Device d in Mme.Outputs()) outBox.Items.Add(d);
        inBox.SelectedIndex = Pick(inBox, wantIn, true);
        outBox.SelectedIndex = Pick(outBox, wantOut, false);
        ready = was;
    }
    int Pick(ComboBox box, string want, bool virt) {
        if (box.Items.Count == 0) return -1;
        if (want != null) for (int i = 0; i < box.Items.Count; i++) if (((Device)box.Items[i]).Name == want) return i;
        for (int i = 0; i < box.Items.Count; i++) { string n = ((Device)box.Items[i]).Name; if (virt ? Has(n, "cable output") : !IsVirtual(n)) return i; }
        for (int i = 0; i < box.Items.Count; i++) { string n = ((Device)box.Items[i]).Name; if (virt ? IsVirtual(n) : true) return i; }
        return 0;
    }

    // ----- engine control -----
    void Toggle() {
        if (engine.Running || waiting) { wantRun = false; waiting = false; pingTimer.Stop(); engine.Stop(); }
        else { wantRun = true; autoFails = 0; if (Missing()) BeginWait(); else Start(); }
        saveTimer.Stop(); saveTimer.Start(); Poll();
    }

    // ----- device "pings": recheck every few seconds (not continuously) until the saved devices show up -----
    static bool Present(ComboBox b, string name) { if (name == null) return false; foreach (object o in b.Items) if (((Device)o).Name == name) return true; return false; }
    static bool HasName(List<Device> l, string name) { if (name == null) return false; foreach (Device d in l) if (d.Name == name) return true; return false; }
    bool Missing() { return !Present(inBox, wantIn) || !Present(outBox, wantOut); }
    void BeginWait() { waiting = true; tries = 0; pingTimer.Interval = 3000; pingTimer.Start(); }
    void Resolve() { if (waiting && !Missing()) { waiting = false; pingTimer.Stop(); if (wantRun) Start(); } }
    void Ping() {
        if (!waiting) { pingTimer.Stop(); return; }
        tries++;
        if (HasName(Mme.Inputs(), wantIn) && HasName(Mme.Outputs(), wantOut)) {
            Populate(wantIn, wantOut); Resolve();
            return;
        }
        if (tries == 20) pingTimer.Interval = 10000;   // after ~1 minute, ping less often
    }
    void Restart() { if (engine.Running) Start(); }
    void Start() {
        Device di = inBox.SelectedItem as Device, dout = outBox.SelectedItem as Device;
        if (di == null || dout == null) { SetStatus("●  PICK AN INPUT AND OUTPUT", Color.OrangeRed); wantRun = false; return; }
        string k = Key(di.Name);
        if (di.Name == dout.Name || (k.Length > 0 && k == Key(dout.Name) && (Has(k, "virtual") || Has(k, "cable")))) { SetStatus("●  INPUT AND OUTPUT CAN'T BE THE SAME CABLE", Color.OrangeRed); wantRun = false; return; }
        lastStart = DateTime.UtcNow; engine.Start(di.Id, dout.Id);
    }
    void SetStatus(string text, Color c) { status.Text = text; status.ForeColor = c; }
    void Poll() {
        string err = engine.Error;
        if (engine.Running) {
            SetStatus(engine.Dsp.Bypass ? "●  LIVE  /  BYPASSED" : "●  LIVE  /  PROCESSING", accent); startBtn.Text = "STOP";
            if ((DateTime.UtcNow - lastStart).TotalSeconds > 10) autoFails = 0;
        }
        else if (waiting) {
            startBtn.Text = "STOP";
            SetStatus("●  WAITING FOR  " + (wantOut ?? "OUTPUT").ToUpperInvariant() + "  /  RECHECKING", Color.FromArgb(255, 176, 0));
        }
        else {
            startBtn.Text = "START";
            if (err != null) {
                if (wantRun && autoFails < 5) { autoFails++; engine.Stop(); BeginWait(); return; }
                wantRun = false; SetStatus("●  " + err.ToUpperInvariant(), Color.OrangeRed);
            }
            else if (status.ForeColor != Color.OrangeRed) SetStatus("●  STOPPED", muted);
        }
    }

    // ----- settings -----
    void LoadSaved(out string inName, out string outName) {
        inName = null; outName = null;
        if (!File.Exists(savePath)) return;
        applyingSpace = true;
        try {
            foreach (string line in File.ReadAllLines(savePath)) {
                int e = line.IndexOf('='); if (e <= 0) continue;
                string k = line.Substring(0, e), v = line.Substring(e + 1);
                if (k == "Input") inName = v; else if (k == "Output") outName = v; else if (k == "Running") wantRun = v == "1";
                else if (k == "Advanced") advChk.Checked = v == "1";
                else if (k == "Space") { int si; if (int.TryParse(v, out si) && si >= 0 && si < spaceBox.Items.Count) spaceBox.SelectedIndex = si; }
                else if (sliders.ContainsKey(k)) { int n; if (int.TryParse(v, out n)) sliders[k].Value = n; }
            }
        } catch { }
        applyingSpace = false;
    }
    void Save() {
        try {
            List<string> lines = new List<string>();
            foreach (KeyValuePair<string, StageSlider> kv in sliders) lines.Add(kv.Key + "=" + kv.Value.Value);
            if ((wantIn ?? SelName(inBox)) != null) lines.Add("Input=" + (wantIn ?? SelName(inBox)));
            if ((wantOut ?? SelName(outBox)) != null) lines.Add("Output=" + (wantOut ?? SelName(outBox)));
            lines.Add("Advanced=" + (advChk.Checked ? "1" : "0"));
            lines.Add("Space=" + spaceBox.SelectedIndex);
            lines.Add("Running=" + (engine.Running || wantRun || waiting ? "1" : "0"));
            File.WriteAllLines(savePath, lines.ToArray());
        } catch { }
    }
}

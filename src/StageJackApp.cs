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
    ComboBox inBox, outBox; Button startBtn, bypassBtn;
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
        Controls.Add(header); Controls.Add(viewport); header.BringToFront();

        saveTimer = new Timer { Interval = 500 }; saveTimer.Tick += delegate { saveTimer.Stop(); Save(); };
        pollTimer = new Timer { Interval = 500 }; pollTimer.Tick += delegate { Poll(); }; pollTimer.Start();
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

        Panel rev = Section(viewport, "REVERB  /  SEPARATION", 530);
        AddSlider(rev, "Reverb size", 42, 0, 100, 75, "SHORT", "HUGE", "");
        AddSlider(rev, "Reverb tone", 120, 0, 100, 60, "DARK", "BRIGHT", "");
        AddSlider(rev, "Reverb pre-delay", 199, 0, 60, 30, "0 MS", "60 MS", " ms");
        AddSlider(rev, "Reverb clarity", 278, 0, 100, 50, "OFF", "MAX", "");
        AddSlider(rev, "Reverb width", 357, 0, 100, 100, "CENTER", "WIDE", "");
        AddSlider(rev, "Smoothness", 436, 0, 100, 60, "TIGHT", "SMOOTH", "");

        Panel eq = Section(viewport, "EQUALIZER  /  10-BAND", 680);
        for (int i = 0; i < bands.Length; i++) AddSlider(eq, bands[i], 40 + i * 62, -12, 12, 0, "", "", " dB");
        viewport.AutoScrollMinSize = new Size(0, eq.Bottom + 16);

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

        FormClosing += delegate { pingTimer.Stop(); engine.Stop(); Save(); };
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
        slider.ValueChanged += delegate { output.Text = slider.Value + unit; if (ready) { PushParams(); saveTimer.Stop(); saveTimer.Start(); } };
    }

    void PushParams() {
        float[] eq = new float[bands.Length];
        for (int i = 0; i < eq.Length; i++) eq[i] = sliders[bands[i]].Value;
        engine.Dsp.Set(sliders["Reverb"].Value, sliders["Stage width"].Value, sliders["Stage depth"].Value, sliders["Stage height"].Value, sliders["Output level"].Value, sliders["Reverb size"].Value, sliders["Reverb tone"].Value, sliders["Reverb pre-delay"].Value, sliders["Reverb clarity"].Value, sliders["Reverb width"].Value, sliders["Smoothness"].Value, eq);
    }
    void ResetAll() {
        sliders["Reverb"].Value = 20; sliders["Stage width"].Value = 50; sliders["Stage depth"].Value = 0; sliders["Stage height"].Value = 50; sliders["Output level"].Value = 0;
        sliders["Reverb size"].Value = 75; sliders["Reverb tone"].Value = 60; sliders["Reverb pre-delay"].Value = 30; sliders["Reverb clarity"].Value = 50; sliders["Reverb width"].Value = 100; sliders["Smoothness"].Value = 60;
        foreach (string b in bands) sliders[b].Value = 0;
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
        try {
            foreach (string line in File.ReadAllLines(savePath)) {
                int e = line.IndexOf('='); if (e <= 0) continue;
                string k = line.Substring(0, e), v = line.Substring(e + 1);
                if (k == "Input") inName = v; else if (k == "Output") outName = v; else if (k == "Running") wantRun = v == "1";
                else if (sliders.ContainsKey(k)) { int n; if (int.TryParse(v, out n)) sliders[k].Value = n; }
            }
        } catch { }
    }
    void Save() {
        try {
            List<string> lines = new List<string>();
            foreach (KeyValuePair<string, StageSlider> kv in sliders) lines.Add(kv.Key + "=" + kv.Value.Value);
            if ((wantIn ?? SelName(inBox)) != null) lines.Add("Input=" + (wantIn ?? SelName(inBox)));
            if ((wantOut ?? SelName(outBox)) != null) lines.Add("Output=" + (wantOut ?? SelName(outBox)));
            lines.Add("Running=" + (engine.Running || wantRun || waiting ? "1" : "0"));
            File.WriteAllLines(savePath, lines.ToArray());
        } catch { }
    }
}

using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
namespace SpaceWall;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.ThreadException += (s, e) => { Log.W("UI exception: " + e.Exception); };
        AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.W("FATAL: " + e.ExceptionObject);
        using var mutex = new Mutex(true, "SpaceWallShips_single_instance", out bool first);
        if (!first) { MessageBox.Show("SpaceWall is already running (look for its icon in the tray, near the clock).", "SpaceWall"); return; }
        Application.EnableVisualStyles();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Log.W("==== start v0.1.0 args=" + string.Join(' ', args) + " ====");
        Application.Run(new Wall(args));
    }
}

class Wall : Form
{
    readonly Settings S = Settings.Load();
    readonly string[] args;
    readonly WebView2 web = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.FromArgb(5, 3, 15), AllowExternalDrop = false };
    readonly AudioFeed audio = new();
    readonly NotifyIcon tray = new();
    readonly System.Windows.Forms.Timer tFlush = new() { Interval = 45 }, tWatch = new() { Interval = 1500 };
    string activeMode = "?", attachNote = "";
    IntPtr curParent = IntPtr.Zero;
    bool fsPaused, testing, webReady, webFailed; string webError = "";
    int lastAudioCheck, lastTrace;
    async Task Trace()
    {
        var r = await JsEval("typeof S!=='undefined'?[S.live,S.bass.toFixed(2),S.mid.toFixed(2),S.high.toFixed(2),beatN,moveN,rollN,swapN].join(' '):'n/a'");
        Log.W($"trace paused={fsPaused} peak={audio.Peak1s:F3} pkts={audio.Packets} page[live bass mid high beats jumps flips swaps]={r}");
    }
    string AppDir => Path.Combine(AppContext.BaseDirectory, "app");

    public Wall(string[] a)
    {
        args = a;
        Text = "SpaceWall";
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(5, 3, 15); BackgroundImageLayout = ImageLayout.Stretch;
        try { BackgroundImage = Image.FromFile(Path.Combine(AppDir, "assets", "wallpaper-off.png")); } catch (Exception e) { Log.W("off image: " + e.Message); }
        Controls.Add(web);
        _ = Handle;
        Icon = MakeIcon();
        if (a.Contains("--window")) S.Mode = "window";
        if (a.Contains("--demo")) S.Demo = true;
        BuildTray();
        tFlush.Tick += (s, e) => Flush();
        tWatch.Tick += (s, e) => Watch();
        Load += async (s, e) => await Boot();
        FormClosing += (s, e) => { if (activeMode == "window" && e.CloseReason == CloseReason.UserClosing) Quit(); };
    }

    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= (int)(N.WS_EX_TOOLWINDOW | N.WS_EX_NOACTIVATE); return p; } }
    protected override void SetVisibleCore(bool v) { base.SetVisibleCore(true); }

    // ---------------------------------------------------------------- startup
    async Task Boot()
    {
        try
        {
            if (args.Contains("--autostart")) await Task.Delay(4000);   // let Explorer finish creating the desktop
            Bounds = Target();
            ApplyMode();
            await InitWeb();
            if (S.On) TurnOn(); else TurnOff();
            tFlush.Start(); tWatch.Start();
            if (args.Contains("--alltests")) { await Task.Delay(8000); await RunAllTests(true); }
            else _ = Task.Run(async () => { await Task.Delay(15000); BeginInvoke(async () => { if (!testing) await RunBasicReport(); }); });
        }
        catch (Exception e) { Log.W("boot failed: " + e); MessageBox.Show("SpaceWall failed to start:\n" + e.Message + "\n\nSee " + Log.Path_, "SpaceWall"); }
    }

    async Task InitWeb()
    {
        try
        {
            var opts = new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required --ignore-gpu-blocklist --enable-gpu-rasterization");
            var env = await CoreWebView2Environment.CreateAsync(null, Dir.F("webview"), opts);
            await web.EnsureCoreWebView2Async(env);
            var c = web.CoreWebView2;
            c.Settings.AreDefaultContextMenusEnabled = false; c.Settings.AreDevToolsEnabled = true; c.Settings.IsStatusBarEnabled = false;
            c.Settings.IsZoomControlEnabled = false; c.Settings.IsBuiltInErrorPageEnabled = false; c.Settings.AreDefaultScriptDialogsEnabled = false;
            c.NavigationCompleted += (s, e) => { Log.W($"navigation completed ok={e.IsSuccess} err={e.WebErrorStatus}"); ApplyPause(); };
            c.ProcessFailed += (s, e) => { Log.W("WebView2 process failed: " + e.ProcessFailedKind); if (S.On) BeginInvoke(() => Navigate()); };
            webReady = true;
            Log.W("WebView2 ready, runtime " + env.BrowserVersionString);
        }
        catch (Exception e)
        {
            webFailed = true; webError = e.Message; Log.W("WebView2 init failed: " + e);
            MessageBox.Show("SpaceWall needs the Microsoft Edge WebView2 Runtime, which could not start.\n\nInstall it from:\nhttps://developer.microsoft.com/microsoft-edge/webview2/ (Evergreen Standalone Installer)\n\nThen start SpaceWall again.\n\nDetails: " + e.Message, "SpaceWall");
        }
    }

    string Url()
    {
        var q = new List<string>();
        q.Add(S.Quality switch { "low" => "fps=24&bgscale=0.5&nebscale=0.5", "high" => "fps=60", _ => "fps=30" });
        if (S.Sens != 1.0) q.Add("sens=" + S.Sens.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (S.Demo) q.Add("demo=1");
        if (S.Debug) q.Add("debug=1");
        return new Uri(Path.Combine(AppDir, "index.html")).AbsoluteUri + "?" + string.Join('&', q);
    }

    void Navigate() { if (webReady) { Log.W("navigate " + Url()); web.CoreWebView2.Navigate(Url()); } }
    void Js(string code) { try { if (webReady) web.CoreWebView2.ExecuteScriptAsync(code); } catch { } }

    void TurnOn()
    {
        S.On = true; S.Save();
        web.Visible = true;
        if (!S.Demo) audio.Start();
        Navigate();
        BuildTray();
    }
    void TurnOff()
    {
        S.On = false; S.Save();
        audio.Stop();
        if (webReady) web.CoreWebView2.Navigate("about:blank");
        web.Visible = false;
        BuildTray();
    }

    // ---------------------------------------------------------------- audio -> page, pause logic
    void Flush()
    {
        if (!S.On || !webReady || S.Demo) return;
        if (Environment.TickCount - lastAudioCheck > 3000) { lastAudioCheck = Environment.TickCount; audio.Watchdog(); }
        if (Environment.TickCount - lastTrace > 2000) { lastTrace = Environment.TickCount; _ = Trace(); }
        var b = audio.Drain();
        if (b == null || (fsPaused && !testing)) return;
        Js("window.A&&A('" + Convert.ToBase64String(b) + "')");
    }

    void ApplyPause() { Js("window.P&&P(" + ((fsPaused && !testing) ? "true" : "false") + ")"); }

    void Watch()
    {
        try
        {
            // pause while a fullscreen game / video / presentation owns the screen
            if (N.SHQueryUserNotificationState(out int st) == 0 && !testing)
            {
                bool fs = st == 2 || st == 3 || st == 4;
                if (fs != fsPaused) { fsPaused = fs; Log.W("fullscreen pause=" + fs + " (state " + st + ")"); ApplyPause(); }
            }
            // keep the wallpaper window attached (Explorer restart, Win+D, display change)
            if (!testing && activeMode != "window" && activeMode != "bottom" && activeMode != "?")
            {
                bool bad = !N.IsWindow(curParent) || N.GetParent(Handle) != curParent || !N.IsWindowVisible(Handle);
                if (bad) { Log.W("watchdog: window lost its parent/visibility, re-attaching"); ApplyMode(); }
            }
        }
        catch (Exception e) { Log.W("watch: " + e.Message); }
    }

    // ---------------------------------------------------------------- placing the window
    Rectangle Target() => S.Span ? SystemInformation.VirtualScreen : Screen.PrimaryScreen.Bounds;

    void Detach()
    {
        if (N.GetParent(Handle) != IntPtr.Zero)
        {
            N.SetStyle(Handle, (N.Style(Handle) & ~N.WS_CHILD) | N.WS_POPUP);
            N.SetParent(Handle, IntPtr.Zero);
        }
        curParent = IntPtr.Zero;
    }

    void Place(IntPtr parent, IntPtr after, Rectangle r)
    {
        var pt = new POINT { X = r.X, Y = r.Y };
        if (parent != IntPtr.Zero) N.MapWindowPoints(IntPtr.Zero, parent, ref pt, 1);
        N.SetWindowPos(Handle, after, pt.X, pt.Y, r.Width, r.Height, N.SWP_NOACTIVATE | N.SWP_SHOWWINDOW | N.SWP_FRAMECHANGED);
    }

    IntPtr FindLegacyWorkerW()
    {
        IntPtr worker = IntPtr.Zero;
        N.EnumWindows((top, _) =>
        {
            if (N.FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
            { var w = N.FindWindowEx(IntPtr.Zero, top, "WorkerW", null); if (w != IntPtr.Zero) worker = w; }
            return true;
        }, IntPtr.Zero);
        return worker;
    }

    /// mode: auto workerw progman progman-bottom bottom window. Returns true when the window sits where intended.
    public bool ApplyMode(string mode = null)
    {
        mode ??= S.Mode;
        try
        {
            Detach();
            var r = Target();
            var progman = N.FindWindow("Progman", null);
            string how;
            if (mode == "window")
            {
                N.SetStyle(Handle, N.WS_OVERLAPPEDWINDOW | N.WS_VISIBLE);
                N.SetExStyle(Handle, (N.ExStyle(Handle) & ~(N.WS_EX_TOOLWINDOW | N.WS_EX_NOACTIVATE)) | N.WS_EX_APPWINDOW);
                N.SetWindowPos(Handle, IntPtr.Zero, 80, 80, 1280, 720, N.SWP_FRAMECHANGED | N.SWP_SHOWWINDOW | N.SWP_NOZORDER);
                activeMode = "window"; attachNote = "normal test window"; Log.W("mode window"); return true;
            }
            N.SetExStyle(Handle, (N.ExStyle(Handle) | N.WS_EX_TOOLWINDOW | N.WS_EX_NOACTIVATE) & ~N.WS_EX_APPWINDOW);
            if (mode == "bottom")
            {
                N.SetStyle(Handle, N.WS_POPUP | N.WS_VISIBLE);
                Place(IntPtr.Zero, N.HWND_BOTTOM, r);
                activeMode = "bottom"; attachNote = "borderless window at the very bottom (covers desktop icons)"; Log.W("mode bottom"); return true;
            }
            N.SendMessageTimeout(progman, 0x052C, (IntPtr)0xD, (IntPtr)1, 0, 1000, out _);
            Thread.Sleep(250);
            var worker = FindLegacyWorkerW();
            var defInProgman = N.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            Log.W($"progman={progman.ToInt64():X} legacyWorkerW={worker.ToInt64():X} defViewInProgman={defInProgman.ToInt64():X}");
            if (mode == "auto") mode = worker != IntPtr.Zero ? "workerw" : (defInProgman != IntPtr.Zero ? "progman" : "bottom");
            if (mode == "workerw" && worker != IntPtr.Zero)
            { N.SetParent(Handle, worker); curParent = worker; N.SetStyle(Handle, (N.Style(Handle) & ~N.WS_POPUP) | N.WS_CHILD | N.WS_VISIBLE); Place(worker, IntPtr.Zero, r); how = "child of WorkerW (classic, Win10/early Win11)"; }
            else if (mode == "progman" && progman != IntPtr.Zero)
            { N.SetParent(Handle, progman); curParent = progman; N.SetStyle(Handle, (N.Style(Handle) & ~N.WS_POPUP) | N.WS_CHILD | N.WS_VISIBLE); Place(progman, defInProgman != IntPtr.Zero ? defInProgman : N.HWND_BOTTOM, r); how = "child of Progman, just below the icon layer"; }
            else if (mode == "progman-bottom" && progman != IntPtr.Zero)
            { N.SetParent(Handle, progman); curParent = progman; N.SetStyle(Handle, (N.Style(Handle) & ~N.WS_POPUP) | N.WS_CHILD | N.WS_VISIBLE); Place(progman, N.HWND_BOTTOM, r); how = "child of Progman, bottom of its z-order"; }
            else
            {
                Log.W($"mode {mode} not possible, falling back to bottom window");
                N.SetStyle(Handle, N.WS_POPUP | N.WS_VISIBLE); Place(IntPtr.Zero, N.HWND_BOTTOM, r);
                activeMode = "bottom"; attachNote = $"FALLBACK (wanted {mode}): borderless window at the bottom"; return false;
            }
            activeMode = mode; attachNote = how;
            bool ok = N.GetParent(Handle) == curParent && N.IsWindowVisible(Handle);
            Log.W($"mode {mode}: {how}; parentOk={ok}; rect={N.Rc(Handle)}");
            return ok;
        }
        catch (Exception e) { Log.W("ApplyMode failed: " + e); return false; }
    }

    // ---------------------------------------------------------------- tests / diagnostics
    string EnvInfo()
    {
        var sb = new StringBuilder();
        string win = "?";
        try
        {
            using var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            win = $"{k.GetValue("ProductName")} {k.GetValue("DisplayVersion")} build {k.GetValue("CurrentBuild")}.{k.GetValue("UBR")}";
        }
        catch { }
        sb.AppendLine($"SpaceWall 0.1.0 | {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("Windows: " + win);
        sb.AppendLine("Screens: " + string.Join("; ", Screen.AllScreens.Select(s => $"{s.Bounds.Width}x{s.Bounds.Height}@{s.Bounds.X},{s.Bounds.Y}{(s.Primary ? " primary" : "")}")));
        string wv = webFailed ? "FAILED: " + webError : (webReady ? web.CoreWebView2.Environment.BrowserVersionString : "not ready");
        sb.AppendLine("WebView2 runtime: " + wv);
        sb.AppendLine($"Settings: on={S.On} mode={S.Mode} quality={S.Quality} demo={S.Demo} span={S.Span} autostart={AutoStart.Get()}");
        sb.AppendLine($"Active mode: {activeMode} ({attachNote}); parent={curParent.ToInt64():X}; window rect {N.Rc(Handle)}");
        sb.AppendLine($"Audio: device='{audio.DeviceName}' format={audio.Format} running={audio.Running} packets={audio.Packets} peak(last s)={audio.Peak1s:F3} err='{audio.LastError}'");
        return sb.ToString();
    }

    async Task<string> JsEval(string code)
    {
        try { return webReady ? (await web.CoreWebView2.ExecuteScriptAsync(code)) : "null"; } catch (Exception e) { return "error " + e.Message; }
    }

    async Task<string> PageInfo()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Page webgl: " + await JsEval("(()=>{try{const c=document.createElement('canvas');const g=c.getContext('webgl2');if(!g)return 'NO WEBGL2';const e=g.getExtension('WEBGL_debug_renderer_info');return e?g.getParameter(e.UNMASKED_RENDERER_WEBGL):'webgl2 ok'}catch(x){return 'err '+x}})()"));
        sb.AppendLine("Page fps: " + await JsEval("typeof fpsV!=='undefined'?fpsV:-1"));
        sb.AppendLine("Page audio live/ bass/ energy: " + await JsEval("typeof S!=='undefined'?[S.live,S.bass.toFixed(2),S.energy.toFixed(2)].join(' / '):'n/a'"));
        return sb.ToString();
    }

    async Task<Bitmap> WebShot()
    {
        using var ms = new MemoryStream();
        await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, ms);
        ms.Position = 0; using var tmp = new Bitmap(ms); return new Bitmap(tmp);
    }

    static Bitmap ScreenShot(Rectangle r)
    {
        var b = new Bitmap(r.Width, r.Height);
        using var g = Graphics.FromImage(b);
        g.CopyFromScreen(r.Location, Point.Empty, r.Size);
        return b;
    }

    static double Luma(Color c) => 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;

    /// correlation of brightness pattern between what the web view draws and what is really on screen
    static double Match(Bitmap a, Bitmap b)
    {
        var xs = new List<double>(); var ys = new List<double>();
        for (int j = 0; j < 18; j++) for (int i = 0; i < 32; i++)
            {
                double fx = (i + .5) / 32, fy = (j + .5) / 18;
                xs.Add(Luma(a.GetPixel((int)(fx * (a.Width - 1)), (int)(fy * (a.Height - 1)))));
                ys.Add(Luma(b.GetPixel((int)(fx * (b.Width - 1)), (int)(fy * (b.Height - 1)))));
            }
        double mx = xs.Average(), my = ys.Average(), sxy = 0, sxx = 0, syy = 0;
        for (int k = 0; k < xs.Count; k++) { sxy += (xs[k] - mx) * (ys[k] - my); sxx += (xs[k] - mx) * (xs[k] - mx); syy += (ys[k] - my) * (ys[k] - my); }
        if (sxx < 1 || syy < 1) return double.NaN;
        return sxy / Math.Sqrt(sxx * syy);
    }

    static void SaveShot(Bitmap b, string name)
    {
        try
        {
            Directory.CreateDirectory(Dir.F("shots"));
            using var s = new Bitmap(b, new Size(960, Math.Max(1, b.Height * 960 / b.Width)));
            s.Save(Dir.F("shots\\" + name + ".png"), System.Drawing.Imaging.ImageFormat.Png);
        }
        catch (Exception e) { Log.W("shot: " + e.Message); }
    }

    async Task<string> AudioToneTest()
    {
        if (S.Demo) return "AUDIO TEST skipped (demo mode)";
        if (!audio.Running) audio.Start();
        await Task.Delay(300);
        float before = audio.Peak1s; long p0 = audio.RealSamples; float maxPeak = 0;
        try
        {
            var gen = new SignalGenerator(44100, 1) { Frequency = 330, Gain = 0.15, Type = SignalGeneratorType.Sin };
            using var o = new WaveOutEvent();
            o.Init(gen.ToWaveProvider16()); o.Play();
            for (int i = 0; i < 12; i++) { await Task.Delay(100); maxPeak = Math.Max(maxPeak, audio.Peak1s); }
            o.Stop();
        }
        catch (Exception e) { return "AUDIO TEST FAIL: could not play test tone: " + e.Message; }
        long got = audio.RealSamples - p0;
        bool ok = maxPeak > 0.02 && got > 20000;
        return $"AUDIO TEST {(ok ? "PASS" : "FAIL")}: played a short quiet tone; captured {got} samples, peak {maxPeak:F3} (device '{audio.DeviceName}')";
    }

    async Task RunBasicReport()
    {
        var sb = new StringBuilder(EnvInfo());
        sb.AppendLine(await PageInfo());
        sb.AppendLine("--- desktop window tree ---"); sb.Append(N.DesktopTree());
        File.WriteAllText(Dir.F("diagnostics.txt"), sb.ToString());
        Log.W("basic diagnostics written");
    }

    public async Task RunAllTests(bool fromArg)
    {
        if (testing) return;
        testing = true; tray.ShowBalloonTip(3000, "SpaceWall tests", "Running tests (about 40 s). Don't touch the mouse; a short quiet beep will play.", ToolTipIcon.Info);
        var sb = new StringBuilder(); var results = new List<string>();
        string savedMode = S.Mode; bool savedOn = S.On;
        object shell = null;
        try
        {
            if (!S.On) TurnOn();
            sb.AppendLine("=== SPACEWALL FULL TEST ===").Append(EnvInfo()).AppendLine();
            results.Add(webReady ? "PASS WebView2 starts" : "FAIL WebView2 did not start");
            await Task.Delay(1500);
            var pi = await PageInfo(); sb.AppendLine(pi);
            results.Add(pi.Contains("NO WEBGL2") ? "FAIL WebGL2 unavailable in WebView2 (wallpaper can't render)" : "PASS WebGL2 works");
            double fps = double.TryParse((await JsEval("typeof fpsV!=='undefined'?fpsV:-1")).Trim('"'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : -1;
            results.Add(fps >= 10 ? $"PASS animation runs ({fps:F0} fps in the page)" : $"FAIL animation too slow or not running ({fps:F0} fps)");

            var at = await AudioToneTest(); results.Add(at); sb.AppendLine(at);
            await Task.Delay(500);
            sb.AppendLine("Page after tone: " + await JsEval("typeof S!=='undefined'?[S.live,S.bass.toFixed(2),S.energy.toFixed(2)].join(' / '):'n/a'"));

            try { shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")); shell.GetType().InvokeMember("MinimizeAll", System.Reflection.BindingFlags.InvokeMethod, null, shell, null); } catch (Exception e) { Log.W("minimize: " + e.Message); }
            await Task.Delay(1500);
            sb.AppendLine("--- desktop window tree before attach tests ---").Append(N.DesktopTree());

            foreach (var m in new[] { "workerw", "progman", "progman-bottom", "bottom" })
            {
                bool placed = ApplyMode(m);
                await Task.Delay(4500);
                string line;
                try
                {
                    using var w = await WebShot(); using var sc = ScreenShot(Target());
                    double r = Match(w, sc); SaveShot(sc, "screen-" + m);
                    bool shown = !double.IsNaN(r) && r > 0.6;
                    line = $"{(placed && activeMode == m ? (shown ? "PASS" : "FAIL") : "NOT POSSIBLE")} mode {m}: placed={placed} active={activeMode} screen-match={(double.IsNaN(r) ? "n/a" : r.ToString("F2"))} -> wallpaper {(shown ? "VISIBLE on screen" : "NOT visible on screen")}";
                }
                catch (Exception e) { line = $"ERROR mode {m}: {e.Message}"; }
                results.Add(line); Log.W(line);
            }
            sb.AppendLine("--- desktop window tree after tests ---").Append(N.DesktopTree());
        }
        catch (Exception e) { results.Add("ERROR during tests: " + e); }
        finally
        {
            try { shell?.GetType().InvokeMember("UndoMinimizeALL", System.Reflection.BindingFlags.InvokeMethod, null, shell, null); } catch { }
            testing = false; ApplyPause();
            ApplyMode(savedMode); if (!savedOn) TurnOff();
        }
        // which mode should be the default?
        var best = new[] { "workerw", "progman", "progman-bottom", "bottom" }.FirstOrDefault(m => results.Any(x => x.StartsWith("PASS mode " + m)));
        string verdict = best == null ? "No attach mode showed the wallpaper behind the icons. Use 'Mode > Bottom window' or run-in-window.bat. Send diagnostics.txt + shots folder." : $"Best mode on this PC: {best}";
        if (best != null && S.Mode == "auto") { S.Mode = best; S.Save(); ApplyMode(); verdict += " (now set as your mode)"; }
        var rep = new StringBuilder("=== RESULTS ===\n").AppendJoin("\n", results).AppendLine().AppendLine(verdict).AppendLine().Append(sb);
        File.WriteAllText(Dir.F("diagnostics.txt"), rep.ToString());
        try { Clipboard.SetText(rep.ToString()); } catch { }
        BuildTray();
        MessageBox.Show(string.Join("\n", results) + "\n\n" + verdict + "\n\nFull report (already copied to clipboard):\n" + Dir.F("diagnostics.txt"), "SpaceWall test results");
    }

    // ---------------------------------------------------------------- tray menu
    static Icon MakeIcon()
    {
        using var b = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(b))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.Transparent);
            using var br = new LinearGradientBrush(new Rectangle(0, 0, 32, 32), Color.FromArgb(140, 80, 255), Color.FromArgb(30, 10, 90), 60f);
            g.FillEllipse(br, 1, 1, 30, 30);
            using var p = new Pen(Color.White, 2.5f); g.DrawLines(p, new[] { new PointF(8, 22), new PointF(16, 8), new PointF(24, 22) });
        }
        return Icon.FromHandle(b.GetHicon());
    }

    void BuildTray()
    {
        var m = new ContextMenuStrip();
        void Add(ToolStripItemCollection to, string text, Action act, bool check = false)
        { var it = new ToolStripMenuItem(text) { Checked = check }; it.Click += (s, e) => { try { act(); } catch (Exception ex) { Log.W("menu: " + ex); } }; to.Add(it); }
        Add(m.Items, S.On ? "Turn OFF (stop audio, show still picture)" : "Turn ON", () => { if (S.On) TurnOff(); else TurnOn(); });
        var mode = new ToolStripMenuItem("Mode (where the wallpaper window sits)");
        foreach (var (k, t) in new[] { ("auto", "Auto"), ("workerw", "A: WorkerW (classic)"), ("progman", "B: Progman, under icons"), ("progman-bottom", "C: Progman, bottom"), ("bottom", "D: Bottom window (hides icons)"), ("window", "E: Normal test window") })
        { var k2 = k; Add(mode.DropDownItems, t, () => { S.Mode = k2; S.Save(); ApplyMode(); BuildTray(); }, S.Mode == k); }
        m.Items.Add(mode);
        var q = new ToolStripMenuItem("Quality");
        foreach (var k in new[] { "low", "normal", "high" }) { var k2 = k; Add(q.DropDownItems, k == "low" ? "Low (24 fps, half resolution)" : k == "normal" ? "Normal (30 fps)" : "High (60 fps)", () => { S.Quality = k2; S.Save(); if (S.On) Navigate(); BuildTray(); }, S.Quality == k); }
        m.Items.Add(q);
        var se = new ToolStripMenuItem("Sensitivity");
        foreach (var v in new[] { 1.0, 1.5, 2.0, 3.0 }) { var v2 = v; Add(se.DropDownItems, v + "x", () => { S.Sens = v2; S.Save(); if (S.On) Navigate(); BuildTray(); }, S.Sens == v); }
        m.Items.Add(se);
        Add(m.Items, "Demo mode (synthetic beat, no audio capture)", () => { S.Demo = !S.Demo; S.Save(); if (S.On) { if (S.Demo) audio.Stop(); else audio.Start(); Navigate(); } BuildTray(); }, S.Demo);
        Add(m.Items, "Debug overlay", () => { S.Debug = !S.Debug; S.Save(); if (S.On) Navigate(); BuildTray(); }, S.Debug);
        Add(m.Items, "Span all screens (default: main screen only)", () => { S.Span = !S.Span; S.Save(); ApplyMode(); BuildTray(); }, S.Span);
        Add(m.Items, "Start with Windows", () => { AutoStart.Set(!AutoStart.Get()); BuildTray(); }, AutoStart.Get());
        m.Items.Add(new ToolStripSeparator());
        Add(m.Items, "Run ALL tests (about 40 s)", async () => await RunAllTests(false));
        Add(m.Items, "Diagnostics (copy to clipboard)", async () =>
        {
            var t = EnvInfo() + await PageInfo() + "--- desktop window tree ---\n" + N.DesktopTree();
            File.WriteAllText(Dir.F("diagnostics.txt"), t); try { Clipboard.SetText(t); } catch { }
            MessageBox.Show(t.Split("--- desktop")[0] + "\n(copied to clipboard, saved to " + Dir.F("diagnostics.txt") + ")", "SpaceWall diagnostics");
        });
        Add(m.Items, "Open log / data folder", () => Process.Start(new ProcessStartInfo("explorer.exe", "\"" + Dir.Data + "\"") { UseShellExecute = true }));
        m.Items.Add(new ToolStripSeparator());
        Add(m.Items, "Quit", Quit);
        tray.ContextMenuStrip?.Dispose();
        tray.ContextMenuStrip = m; tray.Icon = Icon; tray.Text = "SpaceWall Ships" + (S.On ? "" : " (off)"); tray.Visible = true;
        tray.MouseClick -= TrayClick; tray.MouseClick += TrayClick;
    }

    void TrayClick(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) { if (S.On) TurnOff(); else TurnOn(); } }

    void Quit()
    {
        Log.W("quit");
        tray.Visible = false; audio.Dispose();
        try { Hide(); N.RedrawWindow(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0x1 | 0x4 | 0x80 | 0x100); } catch { }
        Application.Exit();
    }
}

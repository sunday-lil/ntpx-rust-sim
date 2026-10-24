using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace NTPXSim
{
    // ─────────────────────────────────────────────────────────────────────────────
    //  NTPX//RUST  ·  Distributed Stress Console  —  SIMULATION BUILD 4.0
    //
    //  THIS IS A NON-FUNCTIONAL VISUAL MOCK.
    //  It generates NO network traffic, opens NO sockets, resolves NO hostnames,
    //  and spawns NO processes. Every value on screen is procedurally generated
    //  from System.Random for demonstration / cinema use.
    //
    //  SimAudit.Run() proves the above against the compiled assembly at startup;
    //  the INTEGRITY AUDIT card renders the result instead of hiding it in a doc.
    // ─────────────────────────────────────────────────────────────────────────────
    public partial class MainWindow : Window
    {
        private const string Version = "4.0.0";
        private const int GRAPH_POINTS = 120;

        private enum Phase { Standby, Arming, Armed, Engaging, Engaged, Aborting, Idle }
        private Phase _phase = Phase.Standby;

        private readonly DispatcherTimer _tick = new DispatcherTimer(DispatcherPriority.Render);
        private readonly Random _rng = new Random(0xC0FFEE);
        private readonly List<double> _graph = new List<double>();
        private readonly List<FleetNode> _nodes = new List<FleetNode>();

        private readonly SolidColorBrush _green, _cyan, _red, _amber, _purple, _dim, _txt;
        private readonly SolidColorBrush _cellOn = new SolidColorBrush(Color.FromRgb(0x0E, 0x16, 0x12));
        private readonly SolidColorBrush _cellOff = new SolidColorBrush(Color.FromRgb(0x07, 0x0A, 0x0F));
        private readonly SolidColorBrush _ledOff = new SolidColorBrush(Color.FromRgb(0x26, 0x33, 0x3F));
        private readonly Dictionary<Phase, TextBlock> _phaseRows = new Dictionary<Phase, TextBlock>();
        private readonly Dictionary<Phase, Ellipse> _phaseLeds = new Dictionary<Phase, Ellipse>();

        private double _egressGbps, _reflectedGbps, _pps, _amp, _peak, _scale = 10;
        private double _progress;
        private TimeSpan _uptime;

        private readonly string _snapshotPath;
        private readonly string _snapshotState;
        private readonly string _selfTestPath;
        private DispatcherTimer _splashTimer;

        public MainWindow() : this(null, "engaged", null) { }

        public MainWindow(string snapshotPath, string snapshotState, string selfTestPath)
        {
            _snapshotPath = string.IsNullOrWhiteSpace(snapshotPath) ? null : snapshotPath;
            _snapshotState = (snapshotState ?? "engaged").Trim().ToLowerInvariant();
            _selfTestPath = string.IsNullOrWhiteSpace(selfTestPath) ? null : selfTestPath;

            InitializeComponent();

            _green  = (SolidColorBrush)FindResource("Green");
            _cyan   = (SolidColorBrush)FindResource("Cyan");
            _red    = (SolidColorBrush)FindResource("Red");
            _amber  = (SolidColorBrush)FindResource("Amber");
            _purple = (SolidColorBrush)FindResource("Purple");
            _dim    = (SolidColorBrush)FindResource("Dim");
            _txt    = (SolidColorBrush)FindResource("Txt");

            SimAudit.Run();
            RenderAudit();

            BuildFleet();
            BuildPhaseTrack();
            InitGraph();
            BootLog();

            _tick.Interval = TimeSpan.FromMilliseconds(70);
            _tick.Tick += Tick;
            _tick.Start();

            BtnInit.Click   += (s, e) => BeginArm();
            BtnEngage.Click += (s, e) => BeginEngage();
            BtnAbort.Click  += (s, e) => BeginAbort();
            BtnEnter.Click  += (s, e) => DismissSplash();
            PreviewKeyDown  += OnPreviewKeyDown;

            Closing += (s, e) => _tick.Stop();

            if (_selfTestPath != null)
            {
                ConfigureOffscreen();
                SplashLayer.Visibility = Visibility.Collapsed;
                Loaded += (s, e) => RunSelfTest(_selfTestPath);
            }
            else if (_snapshotPath != null)
            {
                ConfigureOffscreen();
                if (_snapshotState == "splash") ShowSplash();
                else SplashLayer.Visibility = Visibility.Collapsed;
                Loaded += OnSnapshotLoaded;
            }
            else
            {
                FitToWorkArea();
                Loaded += (s, e) => ShowSplash();
            }
        }

        /// <summary>Shrink the console so it never hangs off a small or heavily scaled desktop.</summary>
        private void FitToWorkArea()
        {
            Rect wa = SystemParameters.WorkArea;
            double maxW = Math.Max(MinWidth, wa.Width - 32);
            double maxH = Math.Max(MinHeight, wa.Height - 32);
            if (Width > maxW) Width = maxW;
            if (Height > maxH) Height = maxH;
        }

        /// <summary>Park the window off-screen with no chrome so renders are reproducible.</summary>
        private void ConfigureOffscreen()
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = -12000;
            Top = -12000;
            ShowInTaskbar = false;
            Width = 1480;
            Height = 820;
        }

        // ── SPLASH · the honest half of the prop ─────────────────────────────────
        private void ShowSplash()
        {
            int left = 4;
            SplashCountdown.Text = $"auto-continue in {left}s";
            _splashTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _splashTimer.Tick += (s, e) =>
            {
                left--;
                if (left <= 0) DismissSplash();
                else SplashCountdown.Text = $"auto-continue in {left}s";
            };
            _splashTimer.Start();
            SplashLayer.MouseLeftButtonDown += (s, e) => DismissSplash();
        }

        private void DismissSplash()
        {
            if (SplashLayer.Visibility != Visibility.Visible) return;
            _splashTimer?.Stop();
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(380))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            fade.Completed += (s, e) => SplashLayer.Visibility = Visibility.Collapsed;
            SplashLayer.BeginAnimation(OpacityProperty, fade);
        }

        // ── KEYBOARD ──────────────────────────────────────────────────────────────
        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F5: BeginArm(); e.Handled = true; break;
                case Key.F6: BeginEngage(); e.Handled = true; break;
                case Key.F7: BeginAbort(); e.Handled = true; break;
                case Key.F12: ExportFrame(); e.Handled = true; break;
                case Key.Escape: DismissSplash(); break;
            }
        }

        // ── FLEET MATRIX ──────────────────────────────────────────────────────────
        private void BuildFleet()
        {
            string[] patNames = { "NTP-AMP", "SYNACK", "H2-SET", "H2-PNG" };
            Brush[] patCol = { _green, _cyan, _amber, _purple };

            for (int i = 0; i < 50; i++)
            {
                int p = i % 4;
                var node = new FleetNode
                {
                    Index = i,
                    Pattern = p,
                    PatternName = patNames[p],
                    PatternBrush = patCol[p],
                    Alive = false,
                    Pps = 0
                };
                _nodes.Add(node);

                var cell = new Border
                {
                    Margin = new Thickness(2),
                    Padding = new Thickness(3, 2, 3, 2),
                    Background = _cellOff,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x16, 0x1D, 0x28)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(2)
                };
                var sp = new StackPanel();
                var head = new TextBlock
                {
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 9,
                    Foreground = _dim,
                    Text = $"G{i + 1:D2} {patNames[p]}"
                };
                var led = new Ellipse { Width = 6, Height = 6, Fill = _dim, Margin = new Thickness(0, 1, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
                var val = new TextBlock
                {
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 9,
                    Foreground = patCol[p],
                    Text = "0"
                };
                sp.Children.Add(head);
                sp.Children.Add(led);
                sp.Children.Add(val);
                cell.Child = sp;

                node.Cell = cell;
                node.Led = led;
                node.ValText = val;
                FleetGrid.Children.Add(cell);
            }
        }

        // ── PHASE TRACK ───────────────────────────────────────────────────────────
        private void BuildPhaseTrack()
        {
            var phases = new (Phase P, string Label)[]
            {
                (Phase.Standby,  "STANDBY"),
                (Phase.Arming,   "ARMING FLEET"),
                (Phase.Armed,    "ARMED · STANDBY"),
                (Phase.Engaging, "ENGAGING"),
                (Phase.Engaged,  "ENGAGED"),
                (Phase.Aborting, "ABORTING"),
                (Phase.Idle,     "IDLE"),
            };

            foreach (var (p, label) in phases)
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 3) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(13) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var led = new Ellipse { Width = 5, Height = 5, Fill = _ledOff, VerticalAlignment = VerticalAlignment.Center };
                var txt = new TextBlock
                {
                    Text = label,
                    FontSize = 9.5,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = _dim
                };

                Grid.SetColumn(led, 0);
                Grid.SetColumn(txt, 1);
                row.Children.Add(led);
                row.Children.Add(txt);
                PhaseTrack.Children.Add(row);

                _phaseLeds[p] = led;
                _phaseRows[p] = txt;
            }

            UpdatePhaseTrack();
        }

        private void UpdatePhaseTrack()
        {
            foreach (var kv in _phaseRows)
            {
                bool active = kv.Key == _phase;
                kv.Value.Foreground = active ? _green : _dim;
                kv.Value.FontWeight = active ? FontWeights.Bold : FontWeights.Normal;
                _phaseLeds[kv.Key].Fill = active ? _green : _ledOff;
            }
        }

        // ── GRAPH ─────────────────────────────────────────────────────────────────
        private void InitGraph()
        {
            for (int i = 0; i < GRAPH_POINTS; i++) _graph.Add(0);
            GraphHost.SizeChanged += (s, e) => RedrawGraph();
            RedrawGraph();
        }

        private void RedrawGraph()
        {
            double w = Math.Max(1, GraphHost.ActualWidth);
            double h = Math.Max(1, GraphHost.ActualHeight);

            // Smoothly chase the peak so the curve does not snap when a spike lands.
            double target = Math.Max(10, _peak * 1.15);
            _scale += (target - _scale) * 0.10;
            if (_scale < 10) _scale = 10;

            SetLine(GridLine1, w, h * 0.25, h * 0.25);
            SetLine(GridLine2, w, h * 0.5, h * 0.5);
            SetLine(GridLine3, w, h * 0.75, h * 0.75);

            var pts = new PointCollection();
            var fillPts = new PointCollection();
            for (int i = 0; i < _graph.Count; i++)
            {
                double x = w * i / (_graph.Count - 1);
                double y = h - (h * _graph[i] / _scale);
                y = Math.Max(0, Math.Min(h, y));
                pts.Add(new Point(x, y));
                fillPts.Add(new Point(x, y));
            }
            fillPts.Insert(0, new Point(0, h));
            fillPts.Add(new Point(w, h));
            TrafficLine.Points = pts;
            TrafficFill.Points = fillPts;

            PeakLabel.Text = $"peak {_peak:0.0} Gbps  ·  scale {_scale:0.0}";
        }

        private static void SetLine(Polyline ln, double w, double y1, double y2)
        {
            ln.Points = new PointCollection { new Point(0, y1), new Point(w, y2) };
        }

        // ── LOG ───────────────────────────────────────────────────────────────────
        private void Log(string text) => LogColored(text, _txt);
        private void LogSys(string text) => LogColored(text, _dim);
        private void LogOk(string text) => LogColored(text, _green);
        private void LogWarn(string text) => LogColored(text, _amber);
        private void LogBad(string text) => LogColored(text, _red);
        private void Log(Brush c, string s) => LogColored(s, c);

        private void LogColored(string text, Brush color)
        {
            LogBlock.Inlines.Add(new Run($"[{DateTime.Now:HH:mm:ss.fff}] ") { Foreground = _dim });
            LogBlock.Inlines.Add(new Run(text) { Foreground = color });
            LogBlock.Inlines.Add(new LineBreak());

            // Keep roughly the last 240 lines: 3 inlines per line.
            while (LogBlock.Inlines.Count > 240 * 3)
                LogBlock.Inlines.Remove(LogBlock.Inlines.FirstInline);

            LogScroll.ScrollToEnd();
        }

        private void BootLog()
        {
            LogOk($"NTPX//RUST console v{Version} (sim build) — no real traffic generated.");
            LogSys("loading runtime config · wg0 tunnel · kali dispatch shell");
            LogSys("patterns registered: A=NTP MONLIST AMP · B=SYN+ACK REFLECT · C=H2 SETTINGS · D=H2 PING+DATA(0)");
            LogSys("fleet matrix empty — press INITIALIZE FLEET to bring nodes online.");
            LogSys($"self-audit: {SimAudit.AssemblyReferences} assembly refs · {SimAudit.NetworkReferences} network refs · verdict {SimAudit.Verdict}");
            KaliIp.Text = RandIp(0x6);
            BinHash.Text = "sha256:" + RandHex(64);
            sHops.Text = "--";
            sFrag.Text = "off";
        }

        private void RenderAudit()
        {
            AuditVerdict.Text  = SimAudit.Verdict;
            AuditVerdict.Foreground = SimAudit.Clean ? _green : _red;
            AuditNetRefs.Text  = SimAudit.NetworkReferences.ToString();
            AuditNetTypes.Text = SimAudit.NetworkTypes.ToString();
            AuditCounters.Text = $"{SimAudit.PacketsEmitted} · {SimAudit.SocketsOpened} · {SimAudit.ProcessesSpawned}";
            AuditNote.Text = $"static scan of {SimAudit.AssemblyReferences} referenced assemblies · no send path exists";
            TxCount.Text  = $"{SimAudit.PacketsEmitted} B";
            SockCount.Text = SimAudit.SocketsOpened.ToString();
            ProcCount.Text = SimAudit.ProcessesSpawned.ToString();
        }

        // ── PHASE TRANSITIONS ─────────────────────────────────────────────────────
        private void BeginArm()
        {
            if (_phase == Phase.Engaged || _phase == Phase.Engaging) return;

            _phase = Phase.Arming;
            _uptime = TimeSpan.Zero;
            _peak = 0;
            _scale = 10;
            _progress = 0;
            Prog.Value = 0;
            for (int i = 0; i < _graph.Count; i++) _graph[i] = 0;

            PhaseLabel.Text = "ARMING FLEET";
            PhaseLabel.Foreground = _amber;
            LogOk($"binding WireGuard wg0 → kali@{KaliIp.Text}  (peer ghost-0x7f)");
            LogSys($"dispatch --fleet 50 --nodes 1000 --thr/node {SafeThr()} --target {SafeHost()}:{SafePort()}");
            LogSys("syncing custom ntpd binary (random-padding patch) to fleet… [simulated]");
            BinHash.Text = "sha256:" + RandHex(64);
        }

        private void BeginEngage()
        {
            if (_phase == Phase.Standby || _phase == Phase.Idle) BeginArm();

            _phase = Phase.Engaging;
            if (_amp <= 0) _amp = 8.6 + _rng.NextDouble() * 2;
            PhaseLabel.Text = "ENGAGING";
            PhaseLabel.Foreground = _green;
            LogBad($"TARGET LOCK :: {SafeHost()}:{SafePort()}  — DISPATCH LIVE [SIMULATED]");
            LogWarn("IP fragmentation enabled (MF=1) · cross-border multi-hop egress");
            sFrag.Text = "on · MF=1";
            sHops.Text = $"{_rng.Next(3, 6)} transit";
            Route.Text = BuildRoute();
        }

        private void BeginAbort()
        {
            if (_phase == Phase.Standby) return;
            _phase = Phase.Aborting;
            PhaseLabel.Text = "ABORTING";
            PhaseLabel.Foreground = _red;
            LogBad("ABORT received — draining queues · winding down fleet…");
        }

        // ── MAIN TICK ─────────────────────────────────────────────────────────────
        private void Tick(object sender, EventArgs e)
        {
            _uptime += _tick.Interval;
            Uptime.Text = _uptime.ToString(@"hh\:mm\:ss");
            LogoSpin.Angle = (LogoSpin.Angle + 6) % 360;

            switch (_phase)
            {
                case Phase.Arming:   TickArming();   break;
                case Phase.Engaging: TickEngaging(); break;
                case Phase.Engaged:  TickEngaged();  break;
                case Phase.Aborting: TickAborting(); break;
                default: TickIdle(); break;
            }

            if (_phase == Phase.Engaged || _phase == Phase.Engaging)
            {
                // Mean-reverting curve: ramps are driven by the phase tick, then the
                // value settles into a band instead of random-walking off the chart.
                double target = _phase == Phase.Engaged ? 78.0 : _egressGbps;
                _egressGbps = Math.Max(0, _egressGbps + (target - _egressGbps) * 0.04 + (_rng.NextDouble() - 0.48) * 2.2);
                _reflectedGbps = _egressGbps * _amp;
                _pps = _egressGbps * 1e9 / 8.0 / 64; // cosmetic: derived from 64-byte frames
                _peak = Math.Max(_peak, _egressGbps);
            }
            else if (_phase == Phase.Aborting)
            {
                _egressGbps *= 0.90; _reflectedGbps *= 0.90; _pps *= 0.90;
                if (_egressGbps < 0.05)
                {
                    _egressGbps = 0; _reflectedGbps = 0; _pps = 0; _amp = 0;
                    _phase = Phase.Idle;
                    PhaseLabel.Text = "IDLE"; PhaseLabel.Foreground = _dim;
                    LogOk("fleet drained · idle.");
                }
            }
            else
            {
                _egressGbps = 0; _reflectedGbps = 0; _pps = 0;
            }

            UpdateStats();
            UpdateGraph();
            UpdateFleet();
            UpdatePhaseTrack();
        }

        private void TickIdle()
        {
            if (_rng.NextDouble() < 0.02)
                LogSys($"wg0 keepalive peer ghost-{RandHex(2)} ok · rtt {_rng.Next(40, 180)}ms");
        }

        private void TickArming()
        {
            int aliveCount = _nodes.Count(n => n.Alive);
            int bring = _rng.Next(20, 70);
            for (int k = 0; k < bring && aliveCount < _nodes.Count; k++)
            {
                var n = _nodes.First(x => !x.Alive);
                n.Alive = true;
                n.Pps = _rng.Next(2000, 9000);
                aliveCount++;
                if (_rng.NextDouble() < 0.25)
                    LogOk($"node G{n.Index + 1:D2}/N{_rng.Next(1, 11):D3} online · ntpd_custom@{RandHex(8)} patched [simulated]");
            }

            _progress = (double)aliveCount / _nodes.Count * 100;
            Prog.Value = _progress;

            if (aliveCount >= _nodes.Count)
            {
                _phase = Phase.Armed;
                PhaseLabel.Text = "ARMED · STANDBY";
                PhaseLabel.Foreground = _green;
                LogOk($"fleet ready :: 50 groups · 1000 nodes · {SafeThr()}/node threads");
                LogOk($"target vector: {SafeHost()}:{SafePort()}  duration {SafeDur()}");
                _progress = 0; Prog.Value = 0;
            }
        }

        private void TickEngaging()
        {
            _egressGbps += _rng.NextDouble() * 6 + 3;
            _amp = 8.6 + _rng.NextDouble() * 2;
            foreach (var n in _nodes) if (n.Alive) n.Pps = _rng.Next(8000, 40000);
            _progress = Math.Min(99, _progress + _rng.NextDouble() * 4);
            Prog.Value = _progress;

            MaybeEmitPatternLog();
            if (_egressGbps > 40)
            {
                _phase = Phase.Engaged; PhaseLabel.Text = "ENGAGED";
                PhaseLabel.Foreground = _green; _progress = 100; Prog.Value = 100;
                LogOk("engagement nominal — sustained egress [simulated].");
            }
        }

        private void TickEngaged()
        {
            foreach (var n in _nodes) if (n.Alive) n.Pps = Math.Max(3000, n.Pps + (_rng.NextDouble() - 0.5) * 8000);
            if (_rng.NextDouble() < 0.6) MaybeEmitPatternLog();
            if (_rng.NextDouble() < 0.05)
            {
                LogWarn($"route rotate :: {BuildRoute()}");
                Route.Text = BuildRoute();
            }
            if (_rng.NextDouble() < 0.03)
            {
                var n = _nodes[_rng.Next(_nodes.Count)];
                LogWarn($"node G{n.Index + 1:D2} backpressure · throttle 12%");
            }
        }

        private void TickAborting()
        {
            foreach (var n in _nodes) if (n.Alive && _rng.NextDouble() < 0.12) { n.Alive = false; n.Pps = 0; }
            int alive = _nodes.Count(n => n.Alive);
            _progress = Math.Max(0, _progress - 3);
            Prog.Value = _progress;
            if (_rng.NextDouble() < 0.3) LogSys($"draining G{_rng.Next(1, 51):D2} :: {alive * 10} nodes");
        }

        // ── FAKE PATTERN LOGS ─────────────────────────────────────────────────────
        private void MaybeEmitPatternLog()
        {
            int p = _rng.Next(4);
            int g = _rng.Next(1, 51);
            int node = _rng.Next(1, 11);
            uint ack = (uint)(_rng.Next(1 << 16) << 16 | _rng.Next(1 << 16));
            switch (p)
            {
                case 0:
                    Log(_green, $"[G{g:D2}/N{node:D3}] NTP MONLIST → {SafeHost()}:{_rng.Next(123, 52000)} | req 64B resp {_rng.Next(4400, 4600)}B amp {_amp:0.0}x | {_rng.Next(8000, 60000):N0} pps");
                    break;
                case 1:
                    Log(_cyan, $"[G{g:D2}/N{node:D3}] SYN+ACK reflect src-spoof {RandIp(_rng.Next(1, 224))} → :{SafePort()} | {_rng.Next(8000, 60000):N0} pps");
                    break;
                case 2:
                    Log(_amber, $"[G{g:D2}/N{node:D3}] H2 SETTINGS flood stream={_rng.Next(1, 1000):X4} frame={_rng.Next(9, 30)}B | {_rng.Next(20000, 90000):N0} fps");
                    break;
                case 3:
                    Log(_purple, $"[G{g:D2}/N{node:D3}] H2 PING+DATA(0) frame={_rng.Next(8, 22)}B ack={ack:X8} | {_rng.Next(20000, 90000):N0} pps");
                    break;
            }
            if (_rng.NextDouble() < 0.12)
                LogSys($"frag offset {_rng.Next(0, 8192)} MF=1 → {RandIp(_rng.Next(1, 224))} (transit {PickCc()})");
        }

        // ── UI UPDATES ────────────────────────────────────────────────────────────
        private void UpdateStats()
        {
            sEgress.Text    = $"{_egressGbps:0.0} Gbps";
            sReflected.Text = $"{_reflectedGbps:0.0} Gbps";
            sPps.Text       = $"{(long)_pps:N0}";
            sAmp.Text       = _amp > 0.05 ? $"{_amp:0.0}x" : "—";
            GbpsLabel.Text  = $"{_egressGbps:0.0} Gbps";
            PpsLabel.Text   = $"{(long)_pps:N0} pps";
        }

        private void UpdateGraph()
        {
            _graph.RemoveAt(0);
            _graph.Add(_egressGbps);
            RedrawGraph();
        }

        private void UpdateFleet()
        {
            int alive = 0;
            foreach (var n in _nodes)
            {
                if (n.Alive) alive++;
                n.Led.Fill = n.Alive ? n.PatternBrush : _dim;
                n.ValText.Text = n.Alive ? $"{n.Pps / 1000.0:0.0}k" : "0";
                n.Cell.Background = n.Alive ? _cellOn : _cellOff;
            }
            AliveNodes.Text = $"{alive * 1000 / _nodes.Count}/1000";
        }

        // ── FRAME CAPTURE ─────────────────────────────────────────────────────────
        private void ExportFrame()
        {
            string name = $"ntpx-frame-{DateTime.Now:yyyyMMdd-HHmmss}.png";
            string path = System.IO.Path.Combine(Directory.GetCurrentDirectory(), name);
            try
            {
                CaptureTo(path);
                LogOk($"frame exported → {name}");
            }
            catch (Exception ex)
            {
                LogBad($"frame export failed :: {ex.GetType().Name}");
            }
        }

        private void CaptureTo(string path)
        {
            var root = (FrameworkElement)Content;
            root.UpdateLayout();

            // Render at 1 device-independent pixel per image pixel so the exported
            // frame is identical on every DPI setting (docs / CI screenshots).
            int w = Math.Max(1, (int)Math.Round(root.ActualWidth));
            int h = Math.Max(1, (int)Math.Round(root.ActualHeight));

            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(root);

            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(rtb));
            string dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            using (var fs = File.Create(path)) png.Save(fs);
        }

        /// <summary>
        /// Scripted smoke test of the phase machine. Drives the simulation loop
        /// synchronously (no wall-clock waiting), checks every transition, writes a
        /// report, and exits with 0 (pass) or 1 (fail) so CI can gate on it.
        /// </summary>
        private void RunSelfTest(string reportPath)
        {
            var sb = new StringBuilder();
            bool ok = true;

            void Check(string name, bool cond)
            {
                ok &= cond;
                sb.AppendLine($"{(cond ? "PASS" : "FAIL")}  {name}");
            }

            Check("static audit clean (no network surface)", SimAudit.Clean);
            Check("starts in STANDBY", _phase == Phase.Standby);
            Check("fleet starts offline", _nodes.All(n => !n.Alive));

            BeginArm();
            for (int i = 0; i < 800 && _phase != Phase.Armed; i++) Tick(null, EventArgs.Empty);
            Check("INITIALIZE -> ARMED", _phase == Phase.Armed);
            Check("fleet fully online", _nodes.All(n => n.Alive));
            Check("alive counter reads 1000/1000", AliveNodes.Text == "1000/1000");

            BeginEngage();
            for (int i = 0; i < 400 && _phase != Phase.Engaged; i++) Tick(null, EventArgs.Empty);
            Check("ENGAGE -> ENGAGED", _phase == Phase.Engaged);
            Check($"egress above 40 Gbps (actual {_egressGbps:0.0})", _egressGbps > 40);
            Check("progress bar at 100", Math.Abs(Prog.Value - 100) < 0.001);
            Check("amp factor in plausible band", _amp > 8 && _amp < 11);

            BeginAbort();
            for (int i = 0; i < 1200 && _phase != Phase.Idle; i++) Tick(null, EventArgs.Empty);
            Check("ABORT -> IDLE", _phase == Phase.Idle);
            Check("egress drained to zero", _egressGbps == 0);

            Check("packets emitted == 0", SimAudit.PacketsEmitted == 0);
            Check("sockets opened == 0", SimAudit.SocketsOpened == 0);
            Check("processes spawned == 0", SimAudit.ProcessesSpawned == 0);

            sb.AppendLine(ok ? "RESULT: PASS" : "RESULT: FAIL");

            try
            {
                string dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(reportPath));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(reportPath, sb.ToString());
            }
            catch { /* report is best-effort; the exit code is the contract */ }

            Application.Current.Shutdown(ok ? 0 : 1);
        }

        private void OnSnapshotLoaded(object sender, RoutedEventArgs e)
        {
            PrepareSnapshotState();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                try { CaptureTo(_snapshotPath); }
                catch (Exception ex) { Console.Error.WriteLine("snapshot failed: " + ex); }
                finally { Application.Current.Shutdown(); }
            };
            timer.Start();
        }

        /// <summary>Deterministic, visually rich state so doc screenshots are stable.</summary>
        private void PrepareSnapshotState()
        {
            if (_snapshotState == "splash") return;

            _uptime = TimeSpan.FromSeconds(6443); // 01:47:23
            Uptime.Text = _uptime.ToString(@"hh\:mm\:ss");
            KaliIp.Text = RandIp(0x6);
            BinHash.Text = "sha256:" + RandHex(64);

            if (_snapshotState == "idle")
            {
                PhaseLabel.Text = "STANDBY";
                return;
            }

            foreach (var n in _nodes)
            {
                n.Alive = true;
                n.Pps = _rng.Next(8000, 40000);
            }
            UpdateFleet();

            if (_snapshotState == "armed")
            {
                _phase = Phase.Armed;
                PhaseLabel.Text = "ARMED · STANDBY";
                PhaseLabel.Foreground = _green;
                Prog.Value = 0;
                return;
            }

            _phase = Phase.Engaged;
            PhaseLabel.Text = "ENGAGED";
            PhaseLabel.Foreground = _green;
            _amp = 9.7;
            _egressGbps = 79.6;
            _reflectedGbps = _egressGbps * _amp;
            _pps = _egressGbps * 1e9 / 8.0 / 64;
            _progress = 100;
            Prog.Value = 100;
            sFrag.Text = "on · MF=1";
            sHops.Text = "4 transit";
            Route.Text = BuildRoute();

            double v = 0;
            for (int i = 0; i < GRAPH_POINTS; i++)
            {
                double target = i < 28 ? 3 : Math.Min(82, (i - 28) * 2.55);
                v += (target - v) * 0.22 + (_rng.NextDouble() - 0.5) * 2.6;
                v = Math.Max(0, v);
                _graph[i] = v;
            }
            _peak = _graph.Max();
            _scale = Math.Max(10, _peak * 1.15);

            string[] lines =
            {
                "TARGET LOCK :: index.com:443  — DISPATCH LIVE [SIMULATED]",
                "IP fragmentation enabled (MF=1) · cross-border multi-hop egress",
                "[G04/N003] NTP MONLIST → index.com:20442 | req 64B resp 4508B amp 9.7x | 51,204 pps",
                "[G12/N007] SYN+ACK reflect src-spoof 188.61.22.104 → :443 | 42,881 pps",
                "[G21/N001] H2 SETTINGS flood stream=04CB frame=18B | 77,392 fps",
                "[G33/N009] H2 PING+DATA(0) frame=12B ack=34AF0E9C | 61,837 pps",
                "[G08/N002] NTP MONLIST → index.com:11023 | req 64B resp 4462B amp 9.7x | 38,110 pps",
                "[G45/N004] H2 PING+DATA(0) frame=9B ack=7C21B4DE | 88,005 pps",
                "route rotate :: 95.19.142.47 (UA) → 171.31.9.106 (NL) → 151.17.208.10 (VN)",
                "[G27/N006] SYN+ACK reflect src-spoof 203.68.160.13 → :443 | 49,949 pps",
                "[G19/N010] H2 SETTINGS flood stream=0295 frame=21B | 88,650 fps",
                "[G36/N005] NTP MONLIST → index.com:15812 | req 64B resp 4408B amp 9.7x | 59,020 pps",
                "engagement nominal — sustained egress [simulated]."
            };
            foreach (string line in lines) LogSys(line);

            LogOk("fleet ready :: 50 groups · 1000 nodes · 20/node threads");
        }

        // ── HELPERS ────────────────────────────────────────────────────────────────
        private string SafeHost() => string.IsNullOrWhiteSpace(TgtHost.Text) ? "index.com" : TgtHost.Text.Trim();
        private string SafePort() => string.IsNullOrWhiteSpace(TgtPort.Text) ? "443" : TgtPort.Text.Trim();
        private string SafeDur()  => string.IsNullOrWhiteSpace(Dur.Text) ? "00:30:00" : Dur.Text.Trim();
        private string SafeThr()  => int.TryParse(ThrNode.Text, out var t) && t > 0 ? t.ToString() : "20";

        private string RandIp(int seed)
        {
            int a = (seed == 0 ? _rng.Next(1, 224) : seed);
            return $"{a}.{_rng.Next(0, 256)}.{_rng.Next(0, 256)}.{_rng.Next(1, 255)}";
        }

        private string RandHex(int len)
        {
            const string h = "0123456789abcdef";
            var sb = new StringBuilder(len);
            for (int i = 0; i < len; i++) sb.Append(h[_rng.Next(16)]);
            return sb.ToString();
        }

        private static readonly string[] CC = { "RU", "NL", "DE", "RO", "UA", "MD", "BG", "VN", "ID", "BR", "PA", "SG" };
        private string PickCc() => CC[_rng.Next(CC.Length)];

        private string BuildRoute()
        {
            var hops = new List<string>();
            int n = _rng.Next(3, 6);
            for (int i = 0; i < n; i++) hops.Add($"{RandIp(_rng.Next(1, 224))} ({PickCc()})");
            return string.Join(" → ", hops);
        }
    }

    internal class FleetNode
    {
        public int Index;
        public int Pattern;
        public string PatternName;
        public Brush PatternBrush;
        public bool Alive;
        public double Pps;
        public Border Cell;
        public Ellipse Led;
        public TextBlock ValText;
    }
}

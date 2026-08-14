using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace NTPXSim
{
    // ─────────────────────────────────────────────────────────────────────────────
    //  NTPX//RUST  ·  Distributed Stress Console  —  SIMULATION BUILD
    //
    //  THIS IS A NON-FUNCTIONAL VISUAL MOCK.
    //  It generates NO network traffic, opens NO sockets, sends NO packets.
    //  Every value on screen is procedurally faked for demonstration / cinema use.
    // ─────────────────────────────────────────────────────────────────────────────
    public partial class MainWindow : Window
    {
        private enum Phase { Standby, Arming, Armed, Engaging, Engaged, Aborting, Idle }
        private Phase _phase = Phase.Standby;

        private readonly DispatcherTimer _tick = new DispatcherTimer(DispatcherPriority.Render);
        private readonly Random _rng = new Random(0xC0FFEE);
        private readonly List<double> _graph = new List<double>();
        private const int GRAPH_POINTS = 120;

        private readonly List<FleetNode> _nodes = new List<FleetNode>();
        private readonly SolidColorBrush _green, _cyan, _red, _amber, _purple, _dim, _txt;

        private double _egressGbps, _reflectedGbps, _pps, _amp, _peak;
        private double _progress;
        private DateTime _start;
        private TimeSpan _uptime;

        public MainWindow()
        {
            InitializeComponent();
            _green  = (SolidColorBrush)FindResource("Green");
            _cyan   = (SolidColorBrush)FindResource("Cyan");
            _red    = (SolidColorBrush)FindResource("Red");
            _amber  = (SolidColorBrush)FindResource("Amber");
            _purple = (SolidColorBrush)FindResource("Purple");
            _dim    = (SolidColorBrush)FindResource("Dim");
            _txt    = (SolidColorBrush)FindResource("Txt");

            BuildFleet();
            InitGraph();
            BootLog();

            _tick.Interval = TimeSpan.FromMilliseconds(70);
            _tick.Tick += Tick;
            _tick.Start();

            BtnInit.Click   += (s, e) => BeginArm();
            BtnEngage.Click += (s, e) => BeginEngage();
            BtnAbort.Click  += (s, e) => BeginAbort();

            Closing += (s, e) => _tick.Stop();
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
                    Background = new SolidColorBrush(Color.FromRgb(0x07, 0x0A, 0x0F)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x16, 0x1D, 0x28)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(2)
                };
                var sp = new StackPanel();
                var head = new TextBlock
                {
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 9.5,
                    Foreground = _dim,
                    Text = $"G{i + 1:D2} {patNames[p]}"
                };
                var led = new Ellipse { Width = 7, Height = 7, Fill = _dim, Margin = new Thickness(0, 1, 0, 0) };
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
            double maxV = Math.Max(10, _peak * 1.2);

            SetLine(GridLine1, w, h * 0.25, h * 0.25);
            SetLine(GridLine2, w, h * 0.5, h * 0.5);
            SetLine(GridLine3, w, h * 0.75, h * 0.75);

            var pts = new PointCollection();
            var fillPts = new PointCollection();
            for (int i = 0; i < _graph.Count; i++)
            {
                double x = w * i / (_graph.Count - 1);
                double y = h - (h * _graph[i] / maxV);
                y = Math.Max(0, Math.Min(h, y));
                pts.Add(new Point(x, y));
                fillPts.Add(new Point(x, y));
            }
            fillPts.Insert(0, new Point(0, h));
            fillPts.Add(new Point(w, h));
            TrafficLine.Points = pts;
            TrafficFill.Points = fillPts;

            Canvas.SetLeft(PeakLabel, 8);
            Canvas.SetTop(PeakLabel, 4);
            PeakLabel.Text = $"peak {_peak:0.0} Gbps  ·  max {maxV:0.0}";
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

        private void LogColored(string text, Brush color)
        {
            var ts = new Run($"[{DateTime.Now:HH:mm:ss.fff}] ") { Foreground = _dim };
            var msg = new Run(text) { Foreground = color };
            LogBlock.Inlines.Add(ts);
            LogBlock.Inlines.Add(msg);
            LogBlock.Inlines.Add(new LineBreak());

            // keep last ~240 lines (each line ≈ 2 inlines + 1 LineBreak = 3 inlines)
            while (LogBlock.Inlines.Count > 240 * 3)
                LogBlock.Inlines.Remove(LogBlock.Inlines.FirstInline);

            LogScroll.ScrollToEnd();
        }

        private void BootLog()
        {
            LogOk("NTPX//RUST console v3.7.1 (sim build) — no real traffic generated.");
            LogSys("loading runtime config · wg0 tunnel · kali dispatch shell");
            LogSys("patterns registered: A=NTP MONLIST AMP · B=SYN+ACK REFLECT · C=H2 SETTINGS · D=H2 PING+DATA(0)");
            LogSys("fleet matrix empty — press INITIALIZE FLEET to bring nodes online.");
            KaliIp.Text = RandIp(0x6);
            BinHash.Text = "sha256:" + RandHex(64);
            sHops.Text = "--";
            sFrag.Text = "off";
        }

        // ── PHASE TRANSITIONS ─────────────────────────────────────────────────────
        private void BeginArm()
        {
            if (_phase == Phase.Engaged || _phase == Phase.Engaging) return;
            _phase = Phase.Arming;
            _start = DateTime.Now;
            _uptime = TimeSpan.Zero;
            PhaseLabel.Text = "ARMING FLEET";
            PhaseLabel.Foreground = _amber;
            LogOk($"binding WireGuard wg0 → kali@{KaliIp.Text}  (peer ghost-0x7f)");
            LogSys($"dispatch --fleet 50 --nodes 1000 --thr/node {SafeThr()} --target {SafeHost()}:{SafePort()}");
            LogSys("syncing custom ntpd binary (random-padding patch) to fleet…");
            BinHash.Text = "sha256:" + RandHex(64);
        }

        private void BeginEngage()
        {
            if (_phase != Phase.Armed && _phase != Phase.Engaging && _phase != Phase.Engaged)
            {
                BeginArm();
            }
            _phase = Phase.Engaging;
            PhaseLabel.Text = "ENGAGING";
            PhaseLabel.Foreground = _green;
            LogBad($"TARGET LOCK :: {SafeHost()}:{SafePort()}  — DISPATCH LIVE");
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
                case Phase.Engaged:   TickEngaged();  break;
                case Phase.Aborting: TickAborting(); break;
                default: TickIdle(); break;
            }

            // numeric jitter on displayed stats (cosmetic)
            if (_phase == Phase.Engaged || _phase == Phase.Engaging)
            {
                _egressGbps    = Math.Max(0, _egressGbps    + (_rng.NextDouble() - 0.45) * _egressGbps * 0.08 + 0.3);
                _reflectedGbps = _egressGbps * _amp;
                _pps = _egressGbps * 1e9 / 8.0 / 64; // cosmetic pps from 64B-ish packets
                _peak = Math.Max(_peak, _egressGbps);
            }
            else if (_phase == Phase.Aborting)
            {
                _egressGbps *= 0.90; _reflectedGbps *= 0.90; _pps *= 0.90;
                if (_egressGbps < 0.05)
                {
                    _egressGbps = 0; _phase = Phase.Idle;
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
        }

        private void TickIdle()
        {
            if (_rng.NextDouble() < 0.08)
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
                    LogOk($"node G{n.Index + 1:D2}/N{_rng.Next(1, 11):D3} online · ntpd_custom@{RandHex(8)} patched");
            }
            _progress = (double)aliveCount / _nodes.Count * 100;
            Prog.Value = _progress;
            AliveNodes.Text = $"{aliveCount * 10}/1000";

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
                LogOk("engagement nominal — sustained egress.");
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
            AliveNodes.Text = $"{alive * 10}/1000";
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

        private void Log(Brush c, string s) => LogColored(s, c);

        // ── UI UPDATES ────────────────────────────────────────────────────────────
        private void UpdateStats()
        {
            sEgress.Text    = $"{_egressGbps:0.0} Gbps";
            sReflected.Text = $"{_reflectedGbps:0.0} Gbps";
            sPps.Text       = $"{(long)_pps:N0}";
            sAmp.Text       = $"{_amp:0.0}x";
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
            foreach (var n in _nodes)
            {
                n.Led.Fill = n.Alive ? n.PatternBrush : _dim;
                n.ValText.Text = n.Alive ? $"{n.Pps / 1000.0:0.0}k" : "0";
                var bg = (SolidColorBrush)n.Cell.Background;
                if (n.Alive && bg.Color.R != 0x0E) n.Cell.Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x16, 0x12));
                if (!n.Alive && bg.Color.R != 0x07) n.Cell.Background = new SolidColorBrush(Color.FromRgb(0x07, 0x0A, 0x0F));
            }
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

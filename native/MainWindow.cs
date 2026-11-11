using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Windows.Threading;

namespace VoiceBox
{
    public class MainWindow : Window
    {
        // ---- state ----
        public string CurrentTrack = "v1";
        public int SelectedId = -1;
        public double Snap = 0.25;

        double _playBeat;
        bool _playing;
        bool _rendering;
        bool _loop;
        bool _metro = true;
        double _ppb = 46;
        string _paramMode = "vibrato";
        string _voice = "Hayden 2";
        double _vibDepth = 0.62;
        double _vibRate = 0.42;
        double _tension = 18;
        double _breath = 42;
        double _gender = -6;
        bool _needRender = true;
        bool _syncing;
        bool _updatingUi;
        int _takeIndex;
        readonly List<string> _takeSubs = new List<string>();
        readonly List<string> _takeTags = new List<string>();

        // ---- visuals ----
        Grid _root;
        ArrangementView _arr;
        PianoRollView _pr;
        KeysView _keys;
        ParamView _param;
        ScrollViewer _arrScroll;
        ScrollViewer _prScroll;
        Canvas _toastLayer;
        Grid _helpLayer;
        bool _helpBuilt;
        Border _helpChip;
        Border _helpHint;
        DispatcherTimer _hintTimer;
        StackPanel _trackHeads;
        StackPanel _retakePanel;
        StackPanel _libPanel;
        TextBlock _clock;
        TextBlock _clockSub;
        TextBlock _statusVoice;
        TextBlock _engineStatus;
        TextBlock _selInfo;
        TextBlock _latency;
        TextBlock _fadeStat;
        double _trimDb;
        TextBlock _trimVal;
        Fader _trimFader;
        Fader _trackGainF;
        Fader _trackPanF;
        TextBlock _trackGainVal;
        TextBlock _trackPanVal;
        TextBlock _voiceName;
        TextBlock _voiceAv;
        TextBlock _voiceModeName;
        TextBlock _exprTarget;
        TextBlock _tensionVal;
        TextBlock _breathVal;
        TextBlock _genderVal;
        TextBlock _libNote;
        Border _vibKnob;
        Border _playBorder;
        Border _loopChip;
        Border _metroChip;
        Border _engineMeterFill;
        FrameworkElement _shimmerHost;
        Border _shimmerFill;
        TranslateTransform _shimmerShift;
        double _shimmerPhase;
        TextBox _lyricBox;
        TextBox _libSearch;
        WrapPanel _libChips;
        string _libEngine = "";
        readonly List<Border> _paramTabs = new List<Border>();

        Recorder _recorder;
        bool _recording;
        float[] _takeSamples;
        double _takeStartBeat;
        double _takeEndBeat;
        Border _recordBtn;
        Border _recMeterFill;
        Ellipse _recordDot;
        TextBlock _recInfo;
        TextBlock _recTimer;
        int _recTick;

        TextBox _lyricsBox;
        TextBlock _lyricsCount;
        TextBlock _styleInfo;
        Border _styleBadge;
        TextBlock _eqLowVal;
        TextBlock _eqMidVal;
        TextBlock _eqHighVal;
        TextBox _styleName;
        StackPanel _styleList;
        Border _styleSaveBtn;
        TextBlock _styleSaveLabel;
        string _renameFrom;
        EqCurve _eqCurve;
        Fader _eqLowF;
        Fader _eqMidF;
        Fader _eqHighF;

        // ---- audio ----
        string _wav;
        string _wavBase;
        string _wavAlt;
        bool _liveRendering;
        bool _livePending;
        DispatcherTimer _liveTimer;
        MediaPlayer _media;
        MediaPlayer _mediaNext;
        DispatcherTimer _xfade;
        string _xfadeWav;
        bool _xfadePending;
        const double MasterVolume = 0.75;
        const int XfadeIntervalMs = 8;
        double _xfadeMs = 48;
        Stopwatch _xfadeClock = new Stopwatch();
        readonly Dictionary<string, Meter> _meters = new Dictionary<string, Meter>();
        readonly Dictionary<string, TextBlock> _levelTexts = new Dictionary<string, TextBlock>();
        TextBlock _xfadeVal;
        Border _xfadeCurveChip;
        Fader _xfadeFader;
        TextBox _mixName;
        StackPanel _mixList;
        Border _eqChip;
        Border _fadeFloat;
        Canvas _floatLayer;
        StackPanel _fadeBody;
        StackPanel _fadeDockHost;
        TextBlock _fadeDockHint;
        Border _fadeFloatBtn;
        Border _fadeMinBtn;
        bool _fadeDetached;
        bool _fadeCollapsed;
        Point _fadeDragOrigin;
        double _fadeDragLeft;
        double _fadeDragTop;
        bool _xfadeLinear;
        Stopwatch _sw = new Stopwatch();
        DispatcherTimer _timer;
        double _startBeat;
        DateTime _lastSpace = DateTime.MinValue;

        public MainWindow()
        {
            Title = "Voice Box — Neural Vocal Studio";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Rect wa = SystemParameters.WorkArea;
            Width = Math.Min(1460, wa.Width - 40);
            Height = Math.Min(920, wa.Height - 40);
            // At 150% scaling the work area in DIPs can be smaller than the design minimum, so the
            // minimum is clamped to it: otherwise the window, and the shortcut legend inside it,
            // would hang off a small laptop screen.
            MinWidth = Math.Min(1080, Math.Max(720, wa.Width - 20));
            MinHeight = Math.Min(700, Math.Max(480, wa.Height - 20));

            WindowChrome chrome = new WindowChrome();
            chrome.CaptionHeight = 0;
            chrome.ResizeBorderThickness = new Thickness(5);
            chrome.CornerRadius = new CornerRadius(12);
            chrome.GlassFrameThickness = new Thickness(0);
            WindowChrome.SetWindowChrome(this, chrome);

            _takeSubs.Add("Original render · timing");
            _takeTags.Add("BASE");
            _takeSubs.Add("Pitch realigned · 0.4 st");
            _takeTags.Add("AI");
            _takeSubs.Add("Timbre alt · warm");
            _takeTags.Add("AI");

            _wav = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "voicebox_take.wav");
            _wavBase = _wav;
            _wavAlt = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "voicebox_take_b.wav");
            _media = new MediaPlayer();
            _media.Volume = MasterVolume;
            _mediaNext = new MediaPlayer();
            _mediaNext.Volume = 0;
            _mediaNext.MediaOpened += NextTakeOpened;
            _mediaNext.MediaFailed += NextTakeFailed;
            _xfade = new DispatcherTimer();
            _xfade.Interval = TimeSpan.FromMilliseconds(XfadeIntervalMs);
            _xfade.Tick += XfadeTick;
            AppSettings.Load();
            _xfadeMs = AppSettings.FadeMs;
            _xfadeLinear = AppSettings.FadeLinear;
            ApplySavedMix();
            _recorder = new Recorder();
            if (Application.Current != null)
            {
                Style sbStyle = ScrollTheme.Dark();
                if (sbStyle != null) Application.Current.Resources[typeof(ScrollBar)] = sbStyle;
            }

            BuildUi();

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(33);
            _timer.Tick += Tick;
            _timer.Start();

            Loaded += delegate
            {
                SelectTrack("v1");
                UpdateStatusVoice();
            };
            Closing += delegate { if (_recorder != null) _recorder.Stop(); };
            PreviewKeyDown += OnKey;
        }

        // =========================================================
        //  UI construction
        // =========================================================
        void BuildUi()
        {
            _root = new Grid();
            LinearGradientBrush bg = new LinearGradientBrush();
            bg.StartPoint = new Point(0.1, 0);
            bg.EndPoint = new Point(0.9, 1);
            bg.GradientStops.Add(new GradientStop(T.C("#0a1c38"), 0));
            bg.GradientStops.Add(new GradientStop(T.C("#061021"), 0.42));
            bg.GradientStops.Add(new GradientStop(T.C("#030814"), 1));
            _root.Background = bg;

            // ambient light behind the glass
            Canvas glow = new Canvas();
            glow.IsHitTestVisible = false;
            glow.ClipToBounds = true;
            _root.Children.Add(glow);
            glow.Children.Add(Blob("#2f7bff", 120, 980, 980, -180, -360));
            glow.Children.Add(Blob("#4fd4ff", 80, 720, 720, 760, -260));
            glow.Children.Add(Blob("#5263ff", 100, 1100, 760, 380, 520));

            Grid content = new Grid();
            content.Margin = new Thickness(10);
            content.RowDefinitions.Add(Finite(58));
            content.RowDefinitions.Add(Finite(10));
            content.RowDefinitions.Add(Star());
            content.RowDefinitions.Add(Finite(10));
            content.RowDefinitions.Add(Finite(30));
            content.Children.Add(BuildTopBar());

            Grid workspace = new Grid();
            workspace.ColumnDefinitions.Add(ColStar());
            workspace.ColumnDefinitions.Add(ColFix(10));
            workspace.ColumnDefinitions.Add(ColFix(346));
            Grid.SetRow(workspace, 2);
            content.Children.Add(workspace);

            Grid center = new Grid();
            center.RowDefinitions.Add(Finite(248));
            center.RowDefinitions.Add(Finite(10));
            center.RowDefinitions.Add(Star());
            center.Children.Add(BuildArranger());
            Grid pr = BuildPianoRoll();
            Grid.SetRow(pr, 2);
            center.Children.Add(pr);
            workspace.Children.Add(center);

            UIElement side = BuildSidebar();
            Grid.SetColumn(side, 2);
            workspace.Children.Add(side);

            Grid status = BuildStatusBar();
            Grid.SetRow(status, 4);
            content.Children.Add(status);

            _root.Children.Add(content);

            _toastLayer = new Canvas();
            _toastLayer.IsHitTestVisible = false;
            _root.Children.Add(_toastLayer);

            _floatLayer = new Canvas();
            _root.Children.Add(_floatLayer);

            _helpLayer = new Grid();
            _helpLayer.Visibility = Visibility.Collapsed;
            _root.Children.Add(_helpLayer);

            Content = _root;

            // New users get one quiet nudge towards the shortcut legend, once, ever.
            Loaded += delegate { ShowHelpHintOnce(); };
        }

        static RowDefinition Finite(double h)
        {
            RowDefinition r = new RowDefinition();
            r.Height = new GridLength(h);
            return r;
        }

        static RowDefinition Star()
        {
            RowDefinition r = new RowDefinition();
            r.Height = new GridLength(1, GridUnitType.Star);
            return r;
        }

        static ColumnDefinition ColStar()
        {
            ColumnDefinition c = new ColumnDefinition();
            c.Width = new GridLength(1, GridUnitType.Star);
            return c;
        }

        static ColumnDefinition ColFix(double w)
        {
            ColumnDefinition c = new ColumnDefinition();
            c.Width = new GridLength(w);
            return c;
        }

        static UIElement Blob(string hex, byte alpha, double w, double h, double left, double top)
        {
            Ellipse e = new Ellipse();
            e.Width = w;
            e.Height = h;
            RadialGradientBrush b = new RadialGradientBrush();
            Color c = T.C(hex);
            b.GradientStops.Add(new GradientStop(Color.FromArgb(alpha, c.R, c.G, c.B), 0));
            b.GradientStops.Add(new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1));
            e.Fill = b;
            Canvas.SetLeft(e, left);
            Canvas.SetTop(e, top);
            return e;
        }

        // ---------------------------------------------------------
        Grid BuildTopBar()
        {
            Grid bar = new Grid();
            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            sp.VerticalAlignment = VerticalAlignment.Center;
            sp.Margin = new Thickness(14, 0, 14, 0);

            // brand
            Grid brand = new Grid();
            brand.ColumnDefinitions.Add(ColFix(34));
            brand.ColumnDefinitions.Add(ColFix(8));
            brand.ColumnDefinitions.Add(ColStar());
            Border logo = new Border();
            logo.Width = 32;
            logo.Height = 32;
            logo.CornerRadius = new CornerRadius(10);
            logo.Background = new LinearGradientBrush(T.C("#5ab6ff"), T.C("#4a4fd6"), 140);
            logo.Effect = T.Glow(T.C("#46a0ff"), 18, 0.75);
            StackPanel bars = new StackPanel();
            bars.Orientation = Orientation.Horizontal;
            bars.HorizontalAlignment = HorizontalAlignment.Center;
            bars.VerticalAlignment = VerticalAlignment.Center;
            double[] heights = { 8, 13, 20, 13, 8 };
            for (int i = 0; i < heights.Length; i++)
            {
                Border b = new Border();
                b.Width = 2.2;
                b.Height = heights[i];
                b.CornerRadius = new CornerRadius(1.1);
                b.Background = T.S(Colors.White);
                b.Margin = new Thickness(0, 0, 1.6, 0);
                bars.Children.Add(b);
            }
            logo.Child = bars;
            brand.Children.Add(logo);

            StackPanel names = new StackPanel();
            names.VerticalAlignment = VerticalAlignment.Center;
            TextBlock nm = new TextBlock();
            nm.FontSize = 14;
            nm.FontWeight = FontWeights.Bold;
            nm.FontFamily = T.font;
            nm.Inlines.Add(new System.Windows.Documents.Run("VOICE "));
            System.Windows.Documents.Run boxRun = new System.Windows.Documents.Run("BOX");
            boxRun.Foreground = new LinearGradientBrush(T.C("#9fe4ff"), T.C("#5aa9ff"), 0);
            boxRun.FontWeight = FontWeights.Black;
            nm.Inlines.Add(boxRun);
            nm.Foreground = T.S(T.C("#eef7ff"));
            names.Children.Add(nm);
            TextBlock sub = T.Txt("NEURAL VOCAL STUDIO", 8, T.TextFaint, FontWeights.SemiBold);
            names.Children.Add(sub);
            Grid.SetColumn(names, 2);
            brand.Children.Add(names);
            sp.Children.Add(brand);

            sp.Children.Add(Spacer(16));

            // transport
            StackPanel tr = new StackPanel();
            tr.Orientation = Orientation.Horizontal;
            tr.VerticalAlignment = VerticalAlignment.Center;
            tr.Children.Add(TransportBtn("M 0,0 L 2,0 L 2,14 L 0,14 Z M 13,0 L 15,0 L 15,14 L 13,14 Z M 3,7 L 12,1 L 12,13 Z", false, delegate { SeekBeat(0); }));
            tr.Children.Add(Spacer(5));
            tr.Children.Add(TransportBtn("M 12,0 L 12,14 L 2,7 Z M 0,0 L 2,0 L 2,14 L 0,14 Z", false, delegate { SeekBeat(Math.Max(0, Math.Floor(_playBeat / 4) * 4 - 4)); }));
            tr.Children.Add(Spacer(5));
            _playBorder = PlayButton();
            tr.Children.Add(_playBorder);
            tr.Children.Add(Spacer(5));
            tr.Children.Add(TransportBtn("M 0,0 L 11,0 L 11,11 L 0,11 Z", false, delegate { StopPlayback(); }));
            tr.Children.Add(Spacer(5));
            _recordBtn = RecordButton();
            tr.Children.Add(_recordBtn);
            tr.Children.Add(Spacer(5));
            tr.Children.Add(TransportBtn("M 0,0 L 2,0 L 2,14 L 0,14 Z M 3,7 L 12,1 L 12,13 Z", false, delegate { SeekBeat(Math.Min(60, Math.Floor(_playBeat / 4) * 4 + 4)); }));
            tr.Children.Add(Spacer(8));

            _loopChip = T.Chip("LOOP", false, delegate
            {
                _loop = !_loop;
                T.SetChipState(_loopChip, _loop);
                Toast("LOOP " + (_loop ? "ON" : "OFF"), _loop ? "Playback cycles the 32-beat phrase." : "Free running playback.");
            });
            tr.Children.Add(_loopChip);
            tr.Children.Add(Spacer(4));
            _metroChip = T.Chip("CLICK", true, delegate
            {
                _metro = !_metro;
                T.SetChipState(_metroChip, _metro);
                _needRender = true;
            });
            tr.Children.Add(_metroChip);

            // Reachable discovery for the shortcut legend: F1 still works, but nothing on
            // screen told you about it.
            tr.Children.Add(Spacer(4));
            Border helpChip = T.Chip("?", false, delegate { ToggleHelp(); });
            helpChip.ToolTip = "Keyboard shortcuts (F1)";
            _helpChip = helpChip;
            tr.Children.Add(helpChip);
            sp.Children.Add(tr);

            // clock
            sp.Children.Add(Spacer(16));
            Border clockBox = new Border();
            clockBox.CornerRadius = new CornerRadius(11);
            clockBox.Background = new LinearGradientBrush(Color.FromArgb(190, 4, 16, 34), Color.FromArgb(170, 6, 22, 46), 90);
            clockBox.BorderBrush = T.SA("#78beff", 46);
            clockBox.BorderThickness = new Thickness(1);
            clockBox.Padding = new Thickness(14, 4, 14, 4);
            StackPanel cb = new StackPanel();
            _clock = new TextBlock();
            _clock.Text = "00:00.000";
            _clock.FontSize = 21;
            _clock.FontFamily = T.font;
            _clock.FontWeight = FontWeights.SemiBold;
            _clock.Foreground = T.S(T.C("#d8f0ff"));
            _clock.HorizontalAlignment = HorizontalAlignment.Right;
            cb.Children.Add(_clock);
            _clockSub = T.Txt("BAR 1 · BEAT 1 · 000", 8, T.TextFaint, FontWeights.SemiBold);
            _clockSub.HorizontalAlignment = HorizontalAlignment.Right;
            cb.Children.Add(_clockSub);
            clockBox.Child = cb;
            sp.Children.Add(clockBox);

            // tempo / grid / key
            sp.Children.Add(Spacer(14));
            sp.Children.Add(Field("TEMPO", TempoControl()));
            sp.Children.Add(Spacer(10));
            sp.Children.Add(Field("GRID", T.Select(new string[] { "1/4", "1/8", "1/16", "1/32" }, 2, delegate(int i)
            {
                Snap = i == 0 ? 1 : (i == 1 ? 0.5 : (i == 2 ? 0.25 : 0.125));
            })));
            sp.Children.Add(Spacer(10));
            StackPanel keyF = new StackPanel();
            keyF.Children.Add(T.Label("KEY"));
            keyF.Children.Add(T.Txt("C# minor", 11.5, T.TextMain, FontWeights.SemiBold));
            sp.Children.Add(keyF);

            // right side
            sp.Children.Add(Spacer(14));
            StackPanel fmt = new StackPanel();
            fmt.Orientation = Orientation.Horizontal;
            string[] formats = { "VST3", "AU", "AAX", "ARA" };
            for (int i = 0; i < formats.Length; i++)
            {
                Border f = new Border();
                f.CornerRadius = new CornerRadius(5);
                f.Padding = new Thickness(5, 3, 5, 3);
                f.Margin = new Thickness(0, 0, 4, 0);
                f.Background = T.SA("#468ce6", 34);
                f.BorderBrush = T.SA("#82c3ff", 46);
                f.BorderThickness = new Thickness(1);
                f.Child = T.Txt(formats[i], 7.5, T.C("#a9cffa"), FontWeights.SemiBold);
                fmt.Children.Add(f);
            }
            sp.Children.Add(fmt);

            sp.Children.Add(Spacer(12));
            StackPanel eng = new StackPanel();
            eng.Children.Add(T.Label("GPU · NEURAL"));
            Border meter = new Border();
            meter.Width = 74;
            meter.Height = 5;
            meter.CornerRadius = new CornerRadius(3);
            meter.Background = T.SA("#061121", 220);
            meter.BorderBrush = T.SA("#78b4ff", 36);
            meter.BorderThickness = new Thickness(1);
            _engineMeterFill = new Border();
            _engineMeterFill.HorizontalAlignment = HorizontalAlignment.Left;
            _engineMeterFill.Width = 20;
            _engineMeterFill.CornerRadius = new CornerRadius(3);
            _engineMeterFill.Background = T.HGrad(T.C("#3fa9ff"), T.C("#8ce6ff"));
            meter.Child = _engineMeterFill;
            eng.Children.Add(meter);
            sp.Children.Add(eng);

            sp.Children.Add(Spacer(10));
            sp.Children.Add(WindowBtn("–", delegate { WindowState = WindowState.Minimized; }, false));
            sp.Children.Add(Spacer(3));
            sp.Children.Add(WindowBtn("□", delegate
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }, false));
            sp.Children.Add(Spacer(3));
            sp.Children.Add(WindowBtn("✕", delegate { Close(); }, true));

            bar.Children.Add(sp);
            bar.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                if (!e.Handled) { try { DragMove(); } catch { } }
            };

            Grid g = T.Glass(16, bar, new Thickness(0), true);
            g.Height = 58;
            return g;
        }

        Border WindowBtn(string glyph, Action click, bool close)
        {
            Border b = new Border();
            b.Width = 25;
            b.Height = 25;
            b.CornerRadius = new CornerRadius(7);
            b.Background = T.SA("#3269b9", 34);
            b.BorderBrush = T.SA("#84bdff", 40);
            b.BorderThickness = new Thickness(1);
            b.Cursor = Cursors.Hand;
            TextBlock t = T.Txt(glyph, 10, T.C("#a9c7e6"), FontWeights.SemiBold);
            t.HorizontalAlignment = HorizontalAlignment.Center;
            b.Child = t;
            b.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { e.Handled = true; };
            b.MouseLeftButtonUp += delegate { if (click != null) click(); };
            b.MouseEnter += delegate
            {
                b.Background = close ? T.SA("#e0455f", 150) : T.SA("#4f8fdc", 90);
                t.Foreground = T.S(Colors.White);
            };
            b.MouseLeave += delegate
            {
                b.Background = T.SA("#3269b9", 34);
                t.Foreground = T.S(T.C("#a9c7e6"));
            };
            return b;
        }

        static UIElement Spacer(double w)
        {
            Border b = new Border();
            b.Width = w;
            return b;
        }

        static StackPanel Field(string label, UIElement control)
        {
            StackPanel s = new StackPanel();
            s.Children.Add(T.Label(label));
            s.Children.Add(control);
            return s;
        }

        UIElement TempoControl()
        {
            Border b = new Border();
            b.CornerRadius = new CornerRadius(7);
            b.Background = T.SA("#06142a", 170);
            b.BorderBrush = T.SA("#82b9ff", 46);
            b.BorderThickness = new Thickness(1);
            b.Padding = new Thickness(7, 3, 7, 3);
            b.Cursor = Cursors.Hand;
            StackPanel s = new StackPanel();
            s.Orientation = Orientation.Horizontal;
            TextBlock val = T.Txt("96", 12, T.TextMain, FontWeights.SemiBold);
            s.Children.Add(val);
            TextBlock unit = T.Txt(" BPM", 8, T.TextFaint, FontWeights.SemiBold);
            s.Children.Add(unit);
            b.Child = s;
            b.MouseLeftButtonUp += delegate
            {
                ContextMenu m = new ContextMenu();
                double[] tempos = { 72, 84, 96, 108, 120, 128 };
                foreach (double t in tempos)
                {
                    double tv = t;
                    MenuItem mi = new MenuItem();
                    mi.Header = t.ToString("0") + " BPM";
                    mi.Click += delegate
                    {
                        Project.Bpm = tv;
                        val.Text = tv.ToString("0");
                        _needRender = true;
                        UpdateClock(_playBeat);
                        Toast("TEMPO", "Project tempo set to " + tv.ToString("0") + " BPM.");
                    };
                    m.Items.Add(mi);
                }
                T.StyleMenu(m);
                m.PlacementTarget = b;
                m.Placement = PlacementMode.Bottom;
                m.IsOpen = true;
            };
            return b;
        }

        Border PlayButton()
        {
            Border b = new Border();
            b.Width = 42;
            b.Height = 32;
            b.CornerRadius = new CornerRadius(10);
            b.Background = new LinearGradientBrush(T.C("#58b2ff"), T.C("#246ad6"), 90);
            b.BorderBrush = T.SA("#96d7ff", 130);
            b.BorderThickness = new Thickness(1);
            b.Cursor = Cursors.Hand;
            b.Effect = T.Glow(T.C("#40a0ff"), 16, 0.6);
            b.Child = PlayIcon(false);
            b.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { e.Handled = true; };
            b.MouseLeftButtonUp += delegate
            {
                if (_playing) PausePlayback(); else Play();
            };
            b.MouseEnter += delegate { b.Opacity = 0.9; };
            b.MouseLeave += delegate { b.Opacity = 1; };
            return b;
        }

        static UIElement PlayIcon(bool playing)
        {
            Canvas c = new Canvas();
            c.Width = 15;
            c.Height = 15;
            if (playing)
            {
                Rectangle r1 = new Rectangle();
                r1.Width = 4;
                r1.Height = 13;
                r1.Fill = T.S(Colors.White);
                r1.RadiusX = 1.2;
                r1.RadiusY = 1.2;
                Canvas.SetLeft(r1, 2);
                Canvas.SetTop(r1, 1);
                Rectangle r2 = new Rectangle();
                r2.Width = 4;
                r2.Height = 13;
                r2.Fill = T.S(Colors.White);
                r2.RadiusX = 1.2;
                r2.RadiusY = 1.2;
                Canvas.SetLeft(r2, 9);
                Canvas.SetTop(r2, 1);
                c.Children.Add(r1);
                c.Children.Add(r2);
            }
            else
            {
                Polygon p = new Polygon();
                p.Points = new PointCollection(new Point[] { new Point(3, 1), new Point(14, 7.5), new Point(3, 14) });
                p.Fill = T.S(Colors.White);
                c.Children.Add(p);
            }
            return c;
        }

        Border TransportBtn(string geometry, bool primary, Action click)
        {
            Border b = new Border();
            b.Width = 29;
            b.Height = 29;
            b.CornerRadius = new CornerRadius(8);
            b.Background = T.SA("#3269b9", 40);
            b.BorderBrush = T.SA("#84bdff", 46);
            b.BorderThickness = new Thickness(1);
            b.Cursor = Cursors.Hand;
            System.Windows.Shapes.Path p = new System.Windows.Shapes.Path();
            p.Data = Geometry.Parse(geometry);
            p.Fill = T.S(Colors.White);
            p.Stretch = Stretch.Uniform;
            p.Width = 11;
            p.Height = 11;
            p.Opacity = 0.75;
            b.Child = p;
            b.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { e.Handled = true; };
            b.MouseLeftButtonUp += delegate { if (click != null) click(); };
            b.MouseEnter += delegate { b.Background = T.SA("#4f8fdc", 80); p.Opacity = 1; };
            b.MouseLeave += delegate { b.Background = T.SA("#3269b9", 40); p.Opacity = 0.75; };
            return b;
        }

        // ---------------------------------------------------------
        Grid BuildArranger()
        {
            Grid panel = new Grid();
            panel.RowDefinitions.Add(Finite(32));
            panel.RowDefinitions.Add(Star());

            Grid headRight = new Grid();
            StackPanel hb = new StackPanel();
            hb.Orientation = Orientation.Horizontal;
            hb.HorizontalAlignment = HorizontalAlignment.Right;
            Border zo = T.Btn("−", delegate { SetZoom(-1); }, false);
            zo.Padding = new Thickness(9, 3, 9, 3);
            Border zi = T.Btn("+", delegate { SetZoom(1); }, false);
            zi.Padding = new Thickness(9, 3, 9, 3);
            hb.Children.Add(zo);
            hb.Children.Add(Spacer(4));
            hb.Children.Add(zi);
            hb.Children.Add(Spacer(8));
            Border render = T.Btn("RENDER TAKES", delegate { SimulateRender(); }, false);
            hb.Children.Add(render);
            hb.Children.Add(Spacer(6));
            Border export = T.Btn("EXPORT WAV", delegate { ExportTake(true); }, false);
            hb.Children.Add(export);
            hb.Children.Add(Spacer(6));
            Border exportRaw = T.Btn("EXPORT RAW", delegate { ExportTake(false); }, false);
            hb.Children.Add(exportRaw);
            hb.Children.Add(Spacer(6));
            Border exportStems = T.Btn("EXPORT STEMS", delegate { ExportStems(); }, false);
            hb.Children.Add(exportStems);
            headRight.Children.Add(hb);

            Grid head = T.PanelHead("Arrangement", "3 TRACKS", true, headRight);

            Grid body = new Grid();
            body.ColumnDefinitions.Add(ColFix(172));
            body.ColumnDefinitions.Add(ColStar());

            Grid heads = new Grid();
            heads.RowDefinitions.Add(Finite(24));
            heads.RowDefinitions.Add(Star());
            Border spacer = new Border();
            spacer.BorderBrush = T.SA("#82b9ff", 22);
            spacer.BorderThickness = new Thickness(0, 0, 1, 1);
            TextBlock sp = T.Label("TRACK");
            sp.Margin = new Thickness(10, 0, 0, 0);
            Grid spacerGrid = new Grid();
            spacerGrid.Children.Add(spacer);
            spacerGrid.Children.Add(sp);
            heads.Children.Add(spacerGrid);

            _trackHeads = new StackPanel();
            ScrollViewer thScroll = new ScrollViewer();
            thScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            thScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            thScroll.Content = _trackHeads;
            Grid.SetRow(thScroll, 1);
            heads.Children.Add(thScroll);

            Border headBg = new Border();
            headBg.Background = new LinearGradientBrush(Color.FromArgb(70, 18, 42, 80), Color.FromArgb(20, 10, 24, 48), 90);
            headBg.BorderBrush = T.SA("#82b9ff", 26);
            headBg.BorderThickness = new Thickness(0, 0, 1, 0);
            heads.Children.Insert(0, headBg);

            body.Children.Add(heads);

            _arr = new ArrangementView(this);
            _arrScroll = new ScrollViewer();
            _arrScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            _arrScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            _arrScroll.Background = Brushes.Transparent;
            _arrScroll.Content = _arr;
            _arrScroll.ScrollChanged += delegate(object s, ScrollChangedEventArgs e)
            {
                if (e.HorizontalChange == 0 || _syncing || _prScroll == null) return;
                _syncing = true;
                _prScroll.ScrollToHorizontalOffset(_arrScroll.HorizontalOffset);
                _syncing = false;
                SyncParam();
            };
            Grid.SetColumn(_arrScroll, 1);
            body.Children.Add(_arrScroll);

            Grid.SetRow(body, 1);
            panel.Children.Add(head);
            panel.Children.Add(body);

            BuildTrackHeads();
            return T.Glass(14, panel, new Thickness(0), true);
        }

        void BuildTrackHeads()
        {
            _trackHeads.Children.Clear();
            foreach (Track tr in Project.Tracks)
            {
                Track track = tr;
                Border head = new Border();
                head.Height = 58;
                head.Background = tr.Id == CurrentTrack ? T.SA("#4a96f5", 34) : Brushes.Transparent;
                head.BorderBrush = T.SA("#82b9ff", 22);
                head.BorderThickness = new Thickness(0, 0, 0, 1);
                head.Cursor = Cursors.Hand;
                head.MouseLeftButtonUp += delegate { SelectTrack(track.Id); };

                Grid g = new Grid();
                g.ColumnDefinitions.Add(ColFix(3));
                g.ColumnDefinitions.Add(ColStar());
                g.Margin = new Thickness(8, 4, 8, 4);

                Border accent = new Border();
                accent.Width = 2.5;
                accent.CornerRadius = new CornerRadius(2);
                accent.Background = T.S(T.C(tr.Color));
                accent.VerticalAlignment = VerticalAlignment.Stretch;
                accent.Margin = new Thickness(0, 4, 0, 4);
                accent.Effect = T.Glow(T.C(tr.Color), 10, 0.8);
                g.Children.Add(accent);

                StackPanel info = new StackPanel();
                info.Margin = new Thickness(8, 0, 0, 0);
                info.VerticalAlignment = VerticalAlignment.Center;
                StackPanel nameRow = new StackPanel();
                nameRow.Orientation = Orientation.Horizontal;
                Border dot = new Border();
                dot.Width = 6;
                dot.Height = 6;
                dot.CornerRadius = new CornerRadius(3);
                dot.Background = T.S(T.C(tr.Color));
                dot.Margin = new Thickness(0, 0, 6, 0);
                dot.VerticalAlignment = VerticalAlignment.Center;
                nameRow.Children.Add(dot);
                nameRow.Children.Add(T.Txt(tr.Name, 11, T.C("#cfe3fb"), FontWeights.SemiBold));
                info.Children.Add(nameRow);
                TextBlock sub = T.Txt(tr.Voice, 9, T.TextFaint);
                sub.Margin = new Thickness(0, 1, 0, 3);
                info.Children.Add(sub);

                Grid levelRow = new Grid();
                levelRow.ColumnDefinitions.Add(ColStar());
                levelRow.ColumnDefinitions.Add(ColFix(44));
                Meter meter = new Meter();
                meter.VerticalAlignment = VerticalAlignment.Center;
                meter.Margin = new Thickness(0, 0, 6, 2);
                levelRow.Children.Add(meter);
                TextBlock levelText = T.Txt("—", 8, T.TextFaint, FontWeights.SemiBold);
                levelText.HorizontalAlignment = HorizontalAlignment.Right;
                levelText.VerticalAlignment = VerticalAlignment.Center;
                levelText.Margin = new Thickness(0, 0, 0, 2);
                Grid.SetColumn(levelText, 1);
                levelRow.Children.Add(levelText);
                info.Children.Add(levelRow);
                _meters[tr.Id] = meter;
                _levelTexts[tr.Id] = levelText;

                StackPanel ctl = new StackPanel();
                ctl.Orientation = Orientation.Horizontal;
                Border m = MiniBtn("M", tr.Muted, delegate
                {
                    tr.Muted = !tr.Muted;
                    BuildTrackHeads();
                    PersistMix();
                    MarkAudioDirty();
                });
                Border s = MiniBtn("S", tr.Solo, delegate
                {
                    tr.Solo = !tr.Solo;
                    BuildTrackHeads();
                    PersistMix();
                    MarkAudioDirty();
                });
                ctl.Children.Add(m);
                ctl.Children.Add(Spacer(3));
                ctl.Children.Add(s);
                info.Children.Add(ctl);

                Grid.SetColumn(info, 1);
                g.Children.Add(info);
                head.Child = g;
                _trackHeads.Children.Add(head);
            }

            UpdateTrackMeters();
        }

        static Border MiniBtn(string text, bool on, Action click)
        {
            Border b = new Border();
            b.Width = 18;
            b.Height = 15;
            b.CornerRadius = new CornerRadius(4);
            b.Background = on ? T.SA("#50beff", 90) : T.SA("#1e4682", 70);
            b.BorderBrush = on ? T.SA("#8ce6ff", 150) : T.SA("#82b9ff", 40);
            b.BorderThickness = new Thickness(1);
            b.Cursor = Cursors.Hand;
            b.Child = T.Txt(text, 7.5, on ? T.C("#eafaff") : T.TextFaint, FontWeights.Bold);
            b.HorizontalAlignment = HorizontalAlignment.Left;
            b.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { e.Handled = true; };
            b.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                if (click != null) click();
            };
            return b;
        }

        // ---------------------------------------------------------
        Grid BuildPianoRoll()
        {
            Grid panel = new Grid();
            panel.RowDefinitions.Add(Finite(34));
            panel.RowDefinitions.Add(Star());
            panel.RowDefinitions.Add(Finite(56));
            panel.RowDefinitions.Add(Finite(62));

            // toolbar
            Grid tb = new Grid();
            tb.Margin = new Thickness(10, 0, 10, 0);
            StackPanel left = new StackPanel();
            left.Orientation = Orientation.Horizontal;
            left.VerticalAlignment = VerticalAlignment.Center;

            Border grp = new Border();
            grp.CornerRadius = new CornerRadius(8);
            grp.Background = T.SA("#06142a", 130);
            grp.BorderBrush = T.SA("#82b9ff", 30);
            grp.BorderThickness = new Thickness(1);
            grp.Padding = new Thickness(3);
            StackPanel tools = new StackPanel();
            tools.Orientation = Orientation.Horizontal;
            Border t1 = ToolBtn("M 2,1 L 12,8 L 8,9 L 6,14 Z", true);
            Border t2 = ToolBtn("M 1,9 L 9,1 L 12,4 L 4,12 L 1,12 Z", false);
            Border t3 = ToolBtn("M 0,8 C 3,8 4,4 7,4 C 10,4 11,8 14,8", false);
            tools.Children.Add(t1);
            tools.Children.Add(t2);
            tools.Children.Add(t3);
            grp.Child = tools;
            left.Children.Add(grp);
            left.Children.Add(Spacer(9));

            left.Children.Add(T.Label("SNAP"));
            left.Children.Add(Spacer(5));
            left.Children.Add(T.Select(new string[] { "1/4", "1/8", "1/16", "1/32" }, 2, delegate(int i)
            {
                Snap = i == 0 ? 1 : (i == 1 ? 0.5 : (i == 2 ? 0.25 : 0.125));
            }));
            left.Children.Add(Spacer(9));
            left.Children.Add(T.Label("PITCH"));
            left.Children.Add(Spacer(5));
            left.Children.Add(T.Select(new string[] { "Vibrato", "Pitch Deviation", "Phoneme Timing" }, 0, delegate(int i)
            {
                if (i == 2) SetParamMode("phoneme");
                else SetParamMode("vibrato");
                if (i == 1) Toast("PITCH DEVIATION", "Generated pitch shown over the target pitch line.");
            }));
            tb.Children.Add(left);

            StackPanel right = new StackPanel();
            right.Orientation = Orientation.Horizontal;
            right.HorizontalAlignment = HorizontalAlignment.Right;
            right.VerticalAlignment = VerticalAlignment.Center;
            _selInfo = T.Txt("NO NOTE SELECTED", 8.5, T.TextFaint, FontWeights.SemiBold);
            _selInfo.Margin = new Thickness(0, 0, 10, 0);
            right.Children.Add(_selInfo);
            right.Children.Add(T.Btn("AI TUNE", delegate { Humanize(); }, false));
            tb.Children.Add(right);
            panel.Children.Add(tb);

            // body
            Grid body = new Grid();
            body.ColumnDefinitions.Add(ColFix(62));
            body.ColumnDefinitions.Add(ColStar());

            _keys = new KeysView();
            _keys.VerticalAlignment = VerticalAlignment.Top;
            _keys.RenderTransform = new TranslateTransform(0, 0);
            Border keysClip = new Border();
            keysClip.ClipToBounds = true;
            keysClip.Child = _keys;
            body.Children.Add(keysClip);

            _pr = new PianoRollView(this);
            _prScroll = new ScrollViewer();
            _prScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            _prScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            _prScroll.CanContentScroll = false;
            _prScroll.Background = Brushes.Transparent;
            _prScroll.Content = _pr;
            _prScroll.ScrollChanged += delegate(object s, ScrollChangedEventArgs e)
            {
                // The keyboard is pinned beside the roll, so it has to follow the vertical
                // scroll or the key labels drift away from the rows they belong to.
                if (e.VerticalChange != 0 && _keys.RenderTransform is TranslateTransform)
                    ((TranslateTransform)_keys.RenderTransform).Y = -_prScroll.VerticalOffset;

                // The velocity lane is a separate surface, so it follows the roll by hand.
                if (_velShift != null) _velShift.X = -_prScroll.HorizontalOffset;
                if (_velLane != null)
                {
                    double want = Project.TotalBeats * _pr.PpB;
                    if (Math.Abs(_velLane.Width - want) > 0.5) _velLane.Width = want;
                }

                if (e.HorizontalChange == 0 || _syncing) return;
                _syncing = true;
                _arrScroll.ScrollToHorizontalOffset(_prScroll.HorizontalOffset);
                _syncing = false;
                SyncParam();
            };
            Grid.SetColumn(_prScroll, 1);
            body.Children.Add(_prScroll);
            Grid.SetRow(body, 1);
            panel.Children.Add(body);

            // velocity lane — one bar per note, drag to draw the dynamics
            Grid velRow = new Grid();
            velRow.Background = new LinearGradientBrush(Color.FromArgb(150, 6, 18, 36), Color.FromArgb(120, 4, 12, 26), 90);
            velRow.ColumnDefinitions.Add(ColFix(62));
            velRow.ColumnDefinitions.Add(ColStar());

            TextBlock velLabel = T.Txt("VELOCITY", 7.2, T.TextFaint, FontWeights.Bold);
            velLabel.VerticalAlignment = VerticalAlignment.Center;
            velLabel.HorizontalAlignment = HorizontalAlignment.Left;
            velLabel.Margin = new Thickness(6, 0, 0, 0);
            velRow.Children.Add(velLabel);

            Border velClip = new Border();
            velClip.ClipToBounds = true;
            velClip.Background = Brushes.Transparent;
            _velLane = new VelocityLane();
            _velLane.Width = Project.TotalBeats * _pr.PpB;
            _velLane.HorizontalAlignment = HorizontalAlignment.Left;
            _velLane.TrackOf = delegate { return _pr.Track; };
            _velLane.Edited = delegate(Note n) { MarkAudioDirty(); };
            _velShift = new TranslateTransform(0, 0);
            _velLane.RenderTransform = _velShift;
            velClip.Child = _velLane;
            Grid.SetColumn(velClip, 1);
            velRow.Children.Add(velClip);

            Grid.SetRow(velRow, 2);
            panel.Children.Add(velRow);

            // parameter strip
            Grid strip = new Grid();
            strip.Background = new LinearGradientBrush(Color.FromArgb(160, 8, 22, 44), Color.FromArgb(140, 4, 12, 26), 90);
            strip.ColumnDefinitions.Add(ColFix(62));
            strip.ColumnDefinitions.Add(ColStar());

            StackPanel tabs = new StackPanel();
            tabs.VerticalAlignment = VerticalAlignment.Center;
            tabs.Margin = new Thickness(4, 0, 6, 0);
            tabs.Children.Add(ParamTab("Vibrato", "vibrato"));
            tabs.Children.Add(ParamTab("Phoneme", "phoneme"));
            tabs.Children.Add(ParamTab("Loudness", "loudness"));
            strip.Children.Add(tabs);

            Border paramHost = new Border();
            paramHost.ClipToBounds = true;
            paramHost.Background = Brushes.Transparent;
            _param = new ParamView();
            _param.HorizontalAlignment = HorizontalAlignment.Left;
            _param.VerticalAlignment = VerticalAlignment.Top;
            paramHost.Child = _param;
            Grid.SetColumn(paramHost, 1);
            strip.Children.Add(paramHost);

            Grid.SetRow(strip, 3);
            panel.Children.Add(strip);

            return T.Glass(14, panel, new Thickness(0), true);
        }

        static Border ToolBtn(string geometry, bool on)
        {
            Border b = new Border();
            b.Width = 24;
            b.Height = 22;
            b.CornerRadius = new CornerRadius(6);
            b.Background = on ? (Brush)new LinearGradientBrush(T.C("#5ab4ff"), T.C("#286ed2"), 90) : Brushes.Transparent;
            b.Cursor = Cursors.Hand;
            System.Windows.Shapes.Path p = new System.Windows.Shapes.Path();
            p.Data = Geometry.Parse(geometry);
            p.Stroke = on ? T.S(Colors.White) : T.S(T.TextDim);
            p.StrokeThickness = 1.4;
            p.StrokeLineJoin = PenLineJoin.Round;
            p.Width = 13;
            p.Height = 13;
            p.Stretch = Stretch.Uniform;
            b.Child = p;
            b.MouseEnter += delegate { b.Background = T.SA("#4f8fdc", 70); };
            b.MouseLeave += delegate
            {
                b.Background = on ? (Brush)new LinearGradientBrush(T.C("#5ab4ff"), T.C("#286ed2"), 90) : Brushes.Transparent;
            };
            return b;
        }

        Border ParamTab(string text, string mode)
        {
            Border b = new Border();
            b.CornerRadius = new CornerRadius(4);
            b.Padding = new Thickness(5, 2, 5, 2);
            b.Cursor = Cursors.Hand;
            b.Margin = new Thickness(0, 1, 0, 1);
            bool on = _paramMode == mode;
            b.Background = on ? T.SA("#46aaff", 50) : Brushes.Transparent;
            b.Child = T.Txt(text, 7.6, on ? T.C("#bfeaff") : T.TextFaint, FontWeights.SemiBold);
            b.Tag = mode;
            b.MouseLeftButtonUp += delegate { SetParamMode(mode); };
            _paramTabs.Add(b);
            return b;
        }

        void SetParamMode(string mode)
        {
            _paramMode = mode;
            _param.Mode = mode;
            _param.InvalidateVisual();
            RefreshParamTabs();
        }

        void RefreshParamTabs()
        {
            foreach (Border tab in _paramTabs)
            {
                bool on = (tab.Tag as string) == _paramMode;
                tab.Background = on ? T.SA("#46aaff", 50) : Brushes.Transparent;
                TextBlock t = tab.Child as TextBlock;
                if (t != null) t.Foreground = on ? T.S(T.C("#bfeaff")) : T.S(T.TextFaint);
            }
        }

        // ---------------------------------------------------------
        UIElement BuildSidebar()
        {
            ScrollViewer sv = new ScrollViewer();
            sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            sv.Background = Brushes.Transparent;
            Grid.SetColumn(sv, 2);

            StackPanel stack = new StackPanel();
            stack.Children.Add(PanelWrap(BuildVoicePanel()));
            stack.Children.Add(PanelWrap(BuildExpressionPanel()));
            stack.Children.Add(PanelWrap(BuildMixPanel()));
            stack.Children.Add(PanelWrap(BuildLyricsPanel()));
            stack.Children.Add(PanelWrap(BuildStylePanel()));
            stack.Children.Add(PanelWrap(BuildEqPanel()));
            stack.Children.Add(PanelWrap(BuildRecordPanel()));
            stack.Children.Add(PanelWrap(BuildRetakesPanel()));
            stack.Children.Add(PanelWrap(BuildLibraryPanel()));
            sv.Content = stack;
            return sv;
        }

        static UIElement PanelWrap(UIElement inner)
        {
            Border m = new Border();
            m.Margin = new Thickness(0, 0, 0, 10);
            m.Child = inner;
            return m;
        }

        UIElement BuildVoicePanel()
        {
            StackPanel body = new StackPanel();
            body.Margin = new Thickness(11, 9, 11, 10);

            Border card = new Border();
            card.CornerRadius = new CornerRadius(11);
            card.Padding = new Thickness(10, 8, 10, 8);
            card.Cursor = Cursors.Hand;
            LinearGradientBrush cardBg = new LinearGradientBrush(Color.FromArgb(70, 70, 150, 245), Color.FromArgb(30, 8, 22, 44), 45);
            card.Background = cardBg;
            card.BorderBrush = T.SA("#82c3ff", 60);
            card.BorderThickness = new Thickness(1);
            card.MouseLeftButtonUp += delegate { ShowVoiceMenu(card); };

            Grid cg = new Grid();
            cg.ColumnDefinitions.Add(ColFix(40));
            cg.ColumnDefinitions.Add(ColStar());
            Border av = new Border();
            av.Width = 38;
            av.Height = 38;
            av.CornerRadius = new CornerRadius(10);
            av.Background = new LinearGradientBrush(T.C("#b8f2ff"), T.C("#4a86ff"), 150);
            av.Effect = T.Glow(T.C("#5ac8ff"), 16, 0.6);
            _voiceAv = T.Txt("H2", 13, T.C("#04203a"), FontWeights.Black);
            _voiceAv.HorizontalAlignment = HorizontalAlignment.Center;
            av.Child = _voiceAv;
            cg.Children.Add(av);

            StackPanel vi = new StackPanel();
            vi.Margin = new Thickness(10, 0, 0, 0);
            vi.VerticalAlignment = VerticalAlignment.Center;
            StackPanel nameRow = new StackPanel();
            nameRow.Orientation = Orientation.Horizontal;
            _voiceName = T.Txt("Hayden 2", 13, T.C("#eef8ff"), FontWeights.Bold);
            nameRow.Children.Add(_voiceName);
            TextBlock caret = T.Txt("▾", 9, T.C("#8fbde8"));
            caret.Margin = new Thickness(6, 0, 0, 0);
            nameRow.Children.Add(caret);
            vi.Children.Add(nameRow);
            StackPanel modeRow = new StackPanel();
            modeRow.Orientation = Orientation.Horizontal;
            modeRow.Children.Add(T.Txt("Vocal mode · ", 9, T.TextDim));
            _voiceModeName = T.Txt("Clear", 9, T.C("#bfeaff"), FontWeights.SemiBold);
            modeRow.Children.Add(_voiceModeName);
            vi.Children.Add(modeRow);
            StackPanel langs = new StackPanel();
            langs.Orientation = Orientation.Horizontal;
            langs.Margin = new Thickness(0, 4, 0, 0);
            string[] ls = { "EN", "JA", "ZH", "KO", "ES" };
            for (int i = 0; i < ls.Length; i++)
            {
                Border lb = new Border();
                lb.CornerRadius = new CornerRadius(3);
                lb.Background = T.SA("#0a2850", 150);
                lb.BorderBrush = T.SA("#8ccdff", 60);
                lb.BorderThickness = new Thickness(1);
                lb.Padding = new Thickness(4, 1, 4, 1);
                lb.Margin = new Thickness(0, 0, 3, 0);
                lb.Child = T.Txt(ls[i], 7, T.C("#c7e8ff"), FontWeights.SemiBold);
                langs.Children.Add(lb);
            }
            vi.Children.Add(langs);
            Grid.SetColumn(vi, 1);
            cg.Children.Add(vi);
            card.Child = cg;
            body.Children.Add(card);
            body.Children.Add(SpacerY(8));

            body.Children.Add(T.Row("Version", T.Select(new string[] { "200", "110", "100" }, 0, null), 64));
            body.Children.Add(T.Row("Preset", T.Select(new string[]
            {
                "Default*", "Airy Ballad", "Bright Pop", "Soft Whisper", "Power Belt", "Vocaloid-ish"
            }, 0, null), 64));
            body.Children.Add(T.Row("Language", T.Select(Langs.Labels, 0, delegate(int i)
            {
                SetLanguage(Langs.Keys[i]);
            }), 64));

            Grid modeGrid = new Grid();
            modeGrid.ColumnDefinitions.Add(ColFix(64));
            modeGrid.ColumnDefinitions.Add(ColStar());
            TextBlock ml = T.Label("MODE");
            modeGrid.Children.Add(ml);
            Grid seg = new Grid();
            seg.ColumnDefinitions.Add(ColStar());
            seg.ColumnDefinitions.Add(ColStar());
            Border sing = SegmentBtn("SING", true);
            Border rap = SegmentBtn("RAP", false);
            seg.Children.Add(sing);
            Grid.SetColumn(rap, 1);
            seg.Children.Add(rap);
            Grid.SetColumn(seg, 1);
            modeGrid.Children.Add(seg);
            modeGrid.Margin = new Thickness(0, 3, 0, 3);
            body.Children.Add(modeGrid);

            UniformGrid modes = new UniformGrid();
            modes.Columns = 4;
            modes.Margin = new Thickness(0, 5, 0, 0);
            string[] modeNames = { "Clear", "Tension", "Falsetto", "Blend" };
            string[] modeSubs = { "natural", "pressed", "airy", "mixed" };
            List<Border> chips = new List<Border>();
            for (int i = 0; i < modeNames.Length; i++)
            {
                int idx = i;
                Border chip = new Border();
                chip.CornerRadius = new CornerRadius(7);
                chip.Padding = new Thickness(2, 4, 2, 4);
                chip.Margin = new Thickness(i == 0 ? 0 : 3, 0, 0, 0);
                chip.Cursor = Cursors.Hand;
                bool on = i == 0;
                chip.Background = on ? T.SA("#46afff", 72) : T.SA("#14304f", 110);
                chip.BorderBrush = on ? T.SA("#8ce0ff", 150) : T.SA("#82b9ff", 40);
                chip.BorderThickness = new Thickness(1);
                StackPanel cs = new StackPanel();
                TextBlock nmx = T.Txt(modeNames[i], 7.8, on ? T.C("#eafaff") : T.TextDim, FontWeights.SemiBold);
                nmx.HorizontalAlignment = HorizontalAlignment.Center;
                TextBlock subx = T.Txt(modeSubs[i], 6.6, T.TextFaint);
                subx.HorizontalAlignment = HorizontalAlignment.Center;
                cs.Children.Add(nmx);
                cs.Children.Add(subx);
                chip.ClipToBounds = true;
                chip.Child = cs;
                chips.Add(chip);
                chip.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { e.Handled = true; };
                chip.MouseLeftButtonUp += delegate
                {
                    for (int k = 0; k < chips.Count; k++)
                    {
                        bool active = k == idx;
                        chips[k].Background = active ? T.SA("#46afff", 72) : T.SA("#14304f", 110);
                        chips[k].BorderBrush = active ? T.SA("#8ce0ff", 150) : T.SA("#82b9ff", 40);
                        StackPanel spp = chips[k].Child as StackPanel;
                        if (spp != null)
                        {
                            TextBlock t0 = spp.Children[0] as TextBlock;
                            if (t0 != null) t0.Foreground = active ? T.S(T.C("#eafaff")) : T.S(T.TextDim);
                        }
                    }
                    _voiceModeName.Text = modeNames[idx];
                    _tension = modeNames[idx] == "Tension" ? 62 : (modeNames[idx] == "Falsetto" ? 12 : (modeNames[idx] == "Blend" ? 34 : 18));
                    _needRender = true;
                    Toast("VOCAL MODE", modeNames[idx] + " — timbre model switched for " + _voice + ".");
                };
                modes.Children.Add(chip);
            }
            body.Children.Add(modes);

            Grid head = T.PanelHead("Voice", "SYNTHESIS v2.4", true, null);
            Border wrap = new Border();
            wrap.Child = body;
            StackPanel root = new StackPanel();
            root.Children.Add(head);
            root.Children.Add(wrap);
            return T.Glass(14, root, new Thickness(0), true);
        }

        static Border SegmentBtn(string text, bool on)
        {
            Border b = new Border();
            b.Height = 24;
            b.CornerRadius = new CornerRadius(6);
            b.Background = on ? (Brush)new LinearGradientBrush(Color.FromArgb(150, 90, 180, 255), Color.FromArgb(150, 40, 110, 210), 90) : T.SA("#05121f", 120);
            b.Child = T.Txt(text, 9, on ? T.C("#f2fbff") : T.TextFaint, FontWeights.SemiBold);
            return b;
        }

        static UIElement SpacerY(double h)
        {
            Border b = new Border();
            b.Height = h;
            return b;
        }

        UIElement BuildExpressionPanel()
        {
            StackPanel body = new StackPanel();
            body.Margin = new Thickness(11, 9, 11, 10);

            Grid padGrid = new Grid();
            padGrid.ColumnDefinitions.Add(ColFix(132));
            padGrid.ColumnDefinitions.Add(ColFix(10));
            padGrid.ColumnDefinitions.Add(ColStar());

            Grid padHost = new Grid();
            Border pad = new Border();
            pad.CornerRadius = new CornerRadius(10);
            pad.Height = 98;
            RadialGradientBrush pb = new RadialGradientBrush();
            pb.GradientStops.Add(new GradientStop(Color.FromArgb(52, 90, 180, 255), 0));
            pb.GradientStops.Add(new GradientStop(Color.FromArgb(0, 90, 180, 255), 0.75));
            pad.Background = pb;
            pad.BorderBrush = T.SA("#82c3ff", 52);
            pad.BorderThickness = new Thickness(1);
            pad.Cursor = Cursors.Cross;

            Canvas pc = new Canvas();
            Ellipse ring1 = new Ellipse();
            ring1.Width = 78;
            ring1.Height = 78;
            ring1.Stroke = T.SA("#8ed4ff", 44);
            ring1.StrokeThickness = 1;
            Canvas.SetLeft(ring1, 26);
            Canvas.SetTop(ring1, 9);
            pc.Children.Add(ring1);
            Ellipse ring2 = new Ellipse();
            ring2.Width = 40;
            ring2.Height = 40;
            ring2.Stroke = T.SA("#8ed4ff", 36);
            ring2.StrokeThickness = 1;
            Canvas.SetLeft(ring2, 45);
            Canvas.SetTop(ring2, 28);
            pc.Children.Add(ring2);
            for (int i = 0; i < 2; i++)
            {
                Border line = new Border();
                line.Background = T.SA("#8ed4ff", 30);
                if (i == 0) { line.Width = 130; line.Height = 1; Canvas.SetLeft(line, 1); Canvas.SetTop(line, 48); }
                else { line.Width = 1; line.Height = 96; Canvas.SetLeft(line, 65); Canvas.SetTop(line, 1); }
                pc.Children.Add(line);
            }
            _vibKnob = new Border();
            _vibKnob.Width = 13;
            _vibKnob.Height = 13;
            _vibKnob.CornerRadius = new CornerRadius(6.5);
            _vibKnob.Background = new RadialGradientBrush(T.C("#e8fbff"), T.C("#2e8bf0"));
            _vibKnob.Effect = T.Glow(T.C("#78dcff"), 14, 0.9);
            Canvas.SetLeft(_vibKnob, 62);
            Canvas.SetTop(_vibKnob, 20);
            pc.Children.Add(_vibKnob);
            pad.Child = pc;

            pad.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                pad.CaptureMouse();
                MoveVibPad(pad, e);
                e.Handled = true;
            };
            pad.MouseMove += delegate(object s, MouseEventArgs e)
            {
                if (pad.IsMouseCaptured) MoveVibPad(pad, e);
            };
            pad.MouseLeftButtonUp += delegate { pad.ReleaseMouseCapture(); };
            padHost.Children.Add(pad);
            padGrid.Children.Add(padHost);

            StackPanel sliders = new StackPanel();
            sliders.Margin = new Thickness(0, 2, 0, 0);
            sliders.Children.Add(SliderRow("TENSION", 0, 100, _tension, delegate(double v)
            {
                _tension = v;
                _tensionVal.Text = ((int)v).ToString();
                MarkAudioDirty();
            }, out _tensionVal));
            sliders.Children.Add(SliderRow("BREATH", 0, 100, _breath, delegate(double v)
            {
                _breath = v;
                _breathVal.Text = ((int)v).ToString();
                MarkAudioDirty();
            }, out _breathVal));
            sliders.Children.Add(SliderRow("GENDER", -50, 50, _gender, delegate(double v)
            {
                _gender = v;
                _genderVal.Text = ((int)v).ToString();
                MarkAudioDirty();
            }, out _genderVal));

            Track ct = FindTrack(CurrentTrack);
            double ctGain = ct != null ? ct.GainDb : 0;
            double ctPan = ct != null ? ct.Pan * 100 : 0;
            sliders.Children.Add(SliderRow("TRACK LEVEL", -24, 12, ctGain, delegate(double v)
            {
                Track t = FindTrack(CurrentTrack);
                if (t == null) return;
                t.GainDb = Math.Round(v * 2) / 2;
                if (_trackGainVal != null) _trackGainVal.Text = (t.GainDb > 0 ? "+" : "") + t.GainDb.ToString("0.#");
                PersistMix();
                MarkAudioDirty();
            }, out _trackGainVal, out _trackGainF));
            sliders.Children.Add(SliderRow("TRACK PAN", -100, 100, ctPan, delegate(double v)
            {
                Track t = FindTrack(CurrentTrack);
                if (t == null) return;
                t.Pan = Math.Round(v) / 100.0;
                if (_trackPanVal != null) _trackPanVal.Text = PanLabel(t.Pan);
                PersistMix();
                MarkAudioDirty();
            }, out _trackPanVal, out _trackPanF));
            if (_trackGainVal != null) _trackGainVal.Text = (ctGain > 0 ? "+" : "") + ctGain.ToString("0.#");
            if (_trackPanVal != null) _trackPanVal.Text = PanLabel(ctPan / 100.0);
            Grid.SetColumn(sliders, 2);
            padGrid.Children.Add(sliders);
            body.Children.Add(padGrid);
            body.Children.Add(SpacerY(8));

            Grid lyr = new Grid();
            lyr.ColumnDefinitions.Add(ColFix(64));
            lyr.ColumnDefinitions.Add(ColStar());
            lyr.Children.Add(T.Label("LYRIC"));
            _lyricBox = T.Input("Select a note");
            _lyricBox.TextChanged += delegate
            {
                if (_updatingUi) return;
                Note n = Project.Find(SelectedId);
                if (n == null) return;
                n.Word = string.IsNullOrEmpty(_lyricBox.Text) ? "la" : _lyricBox.Text;
                _pr.InvalidateVisual();
                _needRender = true;
            };
            Grid.SetColumn(_lyricBox, 1);
            lyr.Children.Add(_lyricBox);
            body.Children.Add(lyr);

            StackPanel root = new StackPanel();
            Grid head = T.PanelHead("Expression", null, true, null);
            _exprTarget = T.Txt(_voice.ToUpperInvariant(), 8.5, T.C("#9ec8f5"), FontWeights.SemiBold);
            _exprTarget.HorizontalAlignment = HorizontalAlignment.Right;
            _exprTarget.Margin = new Thickness(0, 0, 12, 0);
            Grid headGrid = new Grid();
            headGrid.Children.Add(head);
            headGrid.Children.Add(_exprTarget);
            root.Children.Add(headGrid);
            root.Children.Add(body);
            return T.Glass(14, root, new Thickness(0), true);
        }

        UIElement SliderRow(string label, double min, double max, double value, Action<double> onChange, out TextBlock valueText)
        {
            Fader ignored;
            return SliderRow(label, min, max, value, onChange, out valueText, out ignored);
        }

        UIElement SliderRow(string label, double min, double max, double value, Action<double> onChange, out TextBlock valueText, out Fader fader)
        {
            Grid g = new Grid();
            g.Margin = new Thickness(0, 1, 0, 4);
            g.ColumnDefinitions.Add(ColStar());
            g.ColumnDefinitions.Add(ColFix(26));
            TextBlock l = T.Label(label);
            g.Children.Add(l);
            TextBlock v = T.Txt(((int)value).ToString(), 9, T.C("#cfe9ff"), FontWeights.SemiBold);
            v.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(v, 1);
            g.Children.Add(v);
            valueText = v;

            Fader s = new Fader();
            s.Min = min;
            s.Max = max;
            s.Value = value;
            s.Changed = onChange;
            fader = s;
            StackPanel sp = new StackPanel();
            sp.Children.Add(g);
            sp.Children.Add(s);
            return sp;
        }

        void MoveVibPad(Border pad, MouseEventArgs e)
        {
            Point p = e.GetPosition(pad);
            double w = Math.Max(1, pad.ActualWidth);
            double h = Math.Max(1, pad.ActualHeight);
            _vibDepth = Math.Max(0, Math.Min(1, p.X / w));
            _vibRate = Math.Max(0, Math.Min(1, p.Y / h));
            Canvas.SetLeft(_vibKnob, Math.Max(0, Math.Min(w - 13, p.X - 6.5)));
            Canvas.SetTop(_vibKnob, Math.Max(0, Math.Min(h - 13, p.Y - 6.5)));

            List<Note> targets = SelectedId >= 0
                ? Project.Notes.Where(n => n.Id == SelectedId).ToList()
                : Project.VisibleNotes(CurrentTrack);
            foreach (Note n in targets)
            {
                n.Vib = 0.1 + _vibDepth * 0.9;
                n.Rate = 4 + _vibRate * 4;
            }
            _pr.InvalidateVisual();
            _param.InvalidateVisual();
            _needRender = true;
        }

        UIElement BuildRetakesPanel()
        {
            StackPanel root = new StackPanel();
            root.Children.Add(T.PanelHead("AI Retakes", "3 TAKES", true, null));
            _retakePanel = new StackPanel();
            _retakePanel.Margin = new Thickness(11, 9, 11, 6);
            root.Children.Add(_retakePanel);

            StackPanel trim = new StackPanel();
            trim.Margin = new Thickness(11, 0, 11, 8);
            trim.Children.Add(SliderRow("EXPORT TRIM DB", -24, 12, _trimDb, delegate(double v)
            {
                _trimDb = Math.Round(v * 2) / 2;
                if (_trimVal != null) _trimVal.Text = (_trimDb > 0 ? "+" : "") + _trimDb.ToString("0.#");
            }, out _trimVal, out _trimFader));
            root.Children.Add(trim);
            if (_trimVal != null) _trimVal.Text = _trimDb.ToString("0.#");

            Border gen = T.Btn("✦  GENERATE TAKES", delegate { GenerateTake(); }, true);
            gen.Margin = new Thickness(11, 0, 11, 11);
            gen.HorizontalAlignment = HorizontalAlignment.Stretch;
            gen.MouseLeftButtonUp += delegate { gen.IsEnabled = true; };
            root.Children.Add(gen);

            RenderRetakes();
            return T.Glass(14, root, new Thickness(0), true);
        }

        void RenderRetakes()
        {
            if (_retakePanel == null) return;
            _retakePanel.Children.Clear();
            for (int i = 0; i < _takeSubs.Count; i++)
            {
                int idx = i;
                bool on = idx == _takeIndex;
                Border row = new Border();
                row.CornerRadius = new CornerRadius(8);
                row.Padding = new Thickness(8, 5, 8, 5);
                row.Margin = new Thickness(0, 0, 0, 5);
                row.Cursor = Cursors.Hand;
                row.Background = on ? T.SA("#2c8ae0", 110) : T.SA("#0a1c36", 150);
                row.BorderBrush = on ? T.SA("#8ce0ff", 150) : T.SA("#82b9ff", 36);
                row.BorderThickness = new Thickness(1);
                row.MouseLeftButtonUp += delegate { ApplyTake(idx); };

                Grid g = new Grid();
                g.ColumnDefinitions.Add(ColStar());
                g.ColumnDefinitions.Add(ColFix(44));
                StackPanel txt = new StackPanel();
                txt.Children.Add(T.Txt("Take " + (i + 1), 10, on ? T.C("#eafaff") : T.C("#dbeeff"), FontWeights.SemiBold));
                txt.Children.Add(T.Txt(_takeSubs[i], 8.5, T.TextFaint));
                g.Children.Add(txt);
                Border tag = new Border();
                tag.CornerRadius = new CornerRadius(4);
                tag.Padding = new Thickness(5, 2, 5, 2);
                tag.Background = T.SA("#3caaf0", 50);
                tag.BorderBrush = T.SA("#8ce0ff", 60);
                tag.BorderThickness = new Thickness(1);
                tag.Child = T.Txt(_takeTags[i], 7, T.C("#9fe9ff"), FontWeights.Bold);
                tag.HorizontalAlignment = HorizontalAlignment.Right;
                tag.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(tag, 1);
                g.Children.Add(tag);
                row.Child = g;
                _retakePanel.Children.Add(row);
            }
        }

        UIElement BuildLibraryPanel()
        {
            StackPanel root = new StackPanel();
            StackPanel right = new StackPanel();
            Border badge = new Border();
            badge.CornerRadius = new CornerRadius(5);
            badge.Padding = new Thickness(6, 2, 6, 2);
            badge.Background = T.SA("#46c8f0", 36);
            badge.BorderBrush = T.SA("#8ce0ff", 50);
            badge.BorderThickness = new Thickness(1);
            badge.Child = T.Txt(Library.FreeCount() + " FREE", 8, T.C("#9ec8f5"), FontWeights.SemiBold);
            right.Children.Add(badge);
            root.Children.Add(T.PanelHead("Voice Library", null, true, right));

            StackPanel body = new StackPanel();
            body.Margin = new Thickness(11, 9, 11, 10);
            _libSearch = T.Input("Search free voice models…");
            _libSearch.TextChanged += delegate { RenderLibrary(_libSearch.Text); };
            body.Children.Add(_libSearch);
            body.Children.Add(SpacerY(6));

            _libChips = new WrapPanel();
            _libChips.Margin = new Thickness(0, 0, 0, 2);
            foreach (string eng in Library.Engines)
            {
                string key = eng;
                Border chip = T.Chip(key.Length == 0 ? "ALL" : key.ToUpperInvariant(), key == _libEngine,
                    delegate { SetLibEngine(key); });
                chip.Tag = key;
                chip.Margin = new Thickness(0, 0, 4, 4);
                _libChips.Children.Add(chip);
            }
            body.Children.Add(_libChips);
            body.Children.Add(SpacerY(2));

            _libPanel = new StackPanel();
            body.Children.Add(_libPanel);
            _libNote = T.Txt("Free models only · downloads run inside the app, into its Voices folder.", 8.5, T.C("#5f7897"));
            _libNote.TextWrapping = TextWrapping.Wrap;
            _libNote.Margin = new Thickness(0, 6, 0, 0);
            body.Children.Add(_libNote);

            Border openFolder = T.Btn("OPEN VOICES FOLDER", delegate
            {
                try { Process.Start(new ProcessStartInfo(Downloader.Root) { UseShellExecute = true }); }
                catch { Toast("FOLDER", "Could not open " + Downloader.Root); }
            }, false);
            openFolder.Margin = new Thickness(0, 6, 0, 0);
            body.Children.Add(openFolder);
            root.Children.Add(body);

            RenderLibrary("");
            return T.Glass(14, root, new Thickness(0), true);
        }

        void SetLibEngine(string engine)
        {
            _libEngine = engine == null ? "" : engine;
            if (_libChips != null)
            {
                foreach (UIElement e in _libChips.Children)
                {
                    Border b = e as Border;
                    if (b == null) continue;
                    T.SetChipState(b, (string)b.Tag == _libEngine);
                }
            }
            RenderLibrary(_libSearch == null ? "" : _libSearch.Text);
        }

        void RenderLibrary(string filter)
        {
            if (_libPanel == null) return;
            _libPanel.Children.Clear();
            string q = (filter ?? "").Trim().ToLowerInvariant();
            string eng = _libEngine;
            List<VoiceModel> list = Library.Voices.Where(v =>
                (eng.Length == 0 || v.Engine == eng) &&
                (q.Length == 0 ||
                 v.Name.ToLowerInvariant().Contains(q) ||
                 v.Langs.ToLowerInvariant().Contains(q) ||
                 v.Engine.ToLowerInvariant().Contains(q))).ToList();

            if (list.Count == 0)
            {
                _libPanel.Children.Add(T.Txt("No models match that search.", 9, T.TextFaint));
                return;
            }

            foreach (VoiceModel v in list)
            {
                VoiceModel vm = v;
                Border item = new Border();
                item.CornerRadius = new CornerRadius(9);
                item.Padding = new Thickness(8, 6, 8, 6);
                item.Margin = new Thickness(0, 0, 0, 5);
                item.Background = vm.Installed ? T.SA("#103c46", 70) : T.SA("#091830", 130);
                item.BorderBrush = vm.Installed ? T.SA("#6ee1c8", 60) : T.SA("#82b9ff", 30);
                item.BorderThickness = new Thickness(1);

                Grid g = new Grid();
                g.ColumnDefinitions.Add(ColFix(30));
                g.ColumnDefinitions.Add(ColStar());
                g.ColumnDefinitions.Add(ColFix(68));

                Border av = new Border();
                av.Width = 30;
                av.Height = 30;
                av.CornerRadius = new CornerRadius(8);
                av.Background = new LinearGradientBrush(T.C("#a8ecff"), T.C("#4a83ff"), 150);
                TextBlock avt = T.Txt(vm.Ini, 9, T.C("#062038"), FontWeights.Black);
                avt.HorizontalAlignment = HorizontalAlignment.Center;
                avt.VerticalAlignment = VerticalAlignment.Center;
                av.Child = avt;
                g.Children.Add(av);

                StackPanel info = new StackPanel();
                info.Margin = new Thickness(8, 0, 4, 0);
                StackPanel nameRow = new StackPanel();
                nameRow.Orientation = Orientation.Horizontal;
                nameRow.Children.Add(T.Txt(vm.Name, 10, T.C("#e2f1ff"), FontWeights.SemiBold));
                Border free = new Border();
                free.CornerRadius = new CornerRadius(3);
                free.Padding = new Thickness(4, 1, 4, 1);
                free.Margin = new Thickness(5, 0, 0, 0);
                free.Background = T.SA("#32d2aa", 34);
                free.BorderBrush = T.SA("#5aebc8", 60);
                free.BorderThickness = new Thickness(1);
                free.Child = T.Txt("FREE", 6.5, T.C("#8ff0d4"), FontWeights.Bold);
                nameRow.Children.Add(free);
                info.Children.Add(nameRow);
                string meta = vm.Engine;
                if (vm.Size != null && vm.Size.Length > 0) meta += " · " + vm.Size;
                meta += " · " + vm.Langs;
                info.Children.Add(T.Txt(meta, 8.2, T.C("#7e9ab9")));
                Grid.SetColumn(info, 1);
                g.Children.Add(info);

                string label = vm.Bundled ? "BUILT-IN"
                    : (vm.Installed ? "USE"
                    : (vm.Downloading ? (vm.DownloadPct < 0 ? "…" : vm.DownloadPct + "%") : "DOWNLOAD"));
                Border act = new Border();
                act.CornerRadius = new CornerRadius(6);
                act.Padding = new Thickness(6, 3, 6, 3);
                act.Background = T.SA("#3272d6", 70);
                act.BorderBrush = T.SA("#8ccdff", 80);
                act.BorderThickness = new Thickness(1);
                act.Cursor = vm.Bundled ? Cursors.Arrow : Cursors.Hand;
                act.VerticalAlignment = VerticalAlignment.Center;
                act.HorizontalAlignment = HorizontalAlignment.Right;
                TextBlock actLabel = T.Txt(label, 7.2, T.C("#d5eeff"), FontWeights.Bold);
                act.Child = actLabel;
                if (!vm.Bundled)
                {
                    act.MouseLeftButtonUp += delegate
                    {
                        if (vm.Installed) { SetVoice(vm.Name); return; }
                        if (vm.Downloading) return;
                        actLabel.Text = "…";
                        Downloader.Start(vm,
                            delegate(int pct) { actLabel.Text = pct < 0 ? "…" : pct + "%"; },
                            delegate(string error)
                            {
                                if (error == null)
                                {
                                    vm.Installed = true;
                                    Toast("MODEL READY", vm.Name + " downloaded into the app · " + vm.Langs);
                                }
                                else Toast("DOWNLOAD", vm.Name + ": " + error);
                                RenderLibrary(_libSearch.Text);
                            });
                    };
                }
                Grid.SetColumn(act, 2);
                g.Children.Add(act);

                item.Child = g;
                _libPanel.Children.Add(item);
            }
        }

        Border _warmChip;
        TextBlock _warmLabel;
        DispatcherTimer _warmTimer;

        // The status bar states whether the piano tone tables are already built, so a first play
        // that would otherwise stall on table building is announced instead of surprising anyone.
        void StartWarmWatch()
        {
            if (VocalSynth.PianoTablesWarm)
            {
                ShowTablesWarm();
                return;
            }

            _warmTimer = new DispatcherTimer();
            _warmTimer.Interval = TimeSpan.FromMilliseconds(150);
            _warmTimer.Tick += delegate { WarmTick(); };
            _warmTimer.Start();
        }

        void WarmTick()
        {
            if (!VocalSynth.PianoTablesWarm) return;
            if (_warmTimer != null)
            {
                _warmTimer.Stop();
                _warmTimer = null;
            }
            ShowTablesWarm();
        }

        void ShowTablesWarm()
        {
            if (_warmChip == null) return;
            _warmChip.Background = T.SA("#4fe0b0", 34);
            _warmChip.BorderBrush = T.SA("#6ff0c4", 120);
            _warmChip.ToolTip = "Piano tone tables are built — the first play won't wait on them.";
            if (_warmLabel != null)
            {
                _warmLabel.Text = "TABLES READY";
                _warmLabel.Foreground = T.S(T.C("#9df3d5"));
            }
        }

        Grid BuildStatusBar()
        {
            Grid bar = new Grid();
            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            sp.VerticalAlignment = VerticalAlignment.Center;
            sp.Margin = new Thickness(12, 0, 12, 0);

            Border dot = new Border();
            dot.Width = 5;
            dot.Height = 5;
            dot.CornerRadius = new CornerRadius(2.5);
            dot.Background = T.S(T.C("#4fe0b0"));
            dot.Effect = T.Glow(T.C("#4fe0b0"), 8, 0.9);
            dot.Margin = new Thickness(0, 0, 6, 0);
            sp.Children.Add(dot);
            _engineStatus = T.Txt("ENGINE READY", 9, T.TextFaint, FontWeights.SemiBold);
            sp.Children.Add(_engineStatus);

            // Sweeping highlight that only appears while a take is being rendered or faded in.
            Grid shimmer = new Grid();
            shimmer.Width = 64;
            shimmer.Height = 5;
            shimmer.Margin = new Thickness(9, 0, 0, 0);
            shimmer.VerticalAlignment = VerticalAlignment.Center;
            shimmer.Clip = new RectangleGeometry(new Rect(0, 0, 64, 5));
            Border shimmerTrack = new Border();
            shimmerTrack.CornerRadius = new CornerRadius(2.5);
            shimmerTrack.Background = T.SA("#0a1e36", 210);
            shimmerTrack.BorderBrush = T.SA("#78b4ff", 45);
            shimmerTrack.BorderThickness = new Thickness(1);
            shimmer.Children.Add(shimmerTrack);
            _shimmerFill = new Border();
            _shimmerFill.Width = 26;
            _shimmerFill.Height = 5;
            _shimmerFill.CornerRadius = new CornerRadius(2.5);
            _shimmerFill.HorizontalAlignment = HorizontalAlignment.Left;
            _shimmerFill.Background = T.HGrad(T.C("#2f86e6"), T.C("#e6faff"));
            _shimmerShift = new TranslateTransform(-26, 0);
            _shimmerFill.RenderTransform = _shimmerShift;
            shimmer.Children.Add(_shimmerFill);
            shimmer.Visibility = Visibility.Collapsed;
            _shimmerHost = shimmer;
            sp.Children.Add(shimmer);
            sp.Children.Add(T.Sep());
            sp.Children.Add(T.Txt("SAMPLE RATE ", 9, T.TextFaint));
            sp.Children.Add(T.Txt(SampleRateLabel(), 9, T.C("#a9c7e6"), FontWeights.SemiBold));
            sp.Children.Add(T.Sep());
            sp.Children.Add(T.Txt("LATENCY ", 9, T.TextFaint));
            _latency = T.Txt("6.2 ms", 9, T.C("#a9c7e6"), FontWeights.SemiBold);
            sp.Children.Add(_latency);
            sp.Children.Add(T.Sep());
            sp.Children.Add(T.Txt("FADE ", 9, T.TextFaint));
            _fadeStat = T.Txt("—", 9, T.C("#a9c7e6"), FontWeights.SemiBold);
            sp.Children.Add(_fadeStat);
            sp.Children.Add(T.Sep());
            sp.Children.Add(T.Txt("VOICE ", 9, T.TextFaint));
            _statusVoice = T.Txt("HAYDEN 2 · EN", 9, T.C("#a9c7e6"), FontWeights.SemiBold);
            sp.Children.Add(_statusVoice);
            sp.Children.Add(T.Sep());

            _warmChip = new Border();
            _warmChip.CornerRadius = new CornerRadius(7);
            _warmChip.Padding = new Thickness(6, 1, 6, 1);
            _warmChip.VerticalAlignment = VerticalAlignment.Center;
            _warmChip.Background = T.SA("#ffb85c", 38);
            _warmChip.BorderBrush = T.SA("#ffc879", 110);
            _warmChip.BorderThickness = new Thickness(1);
            _warmChip.ToolTip = "Building the piano tone tables — the first play may wait a moment.";
            _warmLabel = T.Txt("WARMING TABLES", 7.6, T.C("#ffcf8a"), FontWeights.Bold);
            _warmChip.Child = _warmLabel;
            sp.Children.Add(_warmChip);

            StackPanel rightS = new StackPanel();
            rightS.Orientation = Orientation.Horizontal;
            rightS.HorizontalAlignment = HorizontalAlignment.Right;
            rightS.VerticalAlignment = VerticalAlignment.Center;
            rightS.Margin = new Thickness(0, 0, 12, 0);
            rightS.Children.Add(T.Txt("STANDALONE", 9, T.TextFaint, FontWeights.SemiBold));
            rightS.Children.Add(T.Sep());
            rightS.Children.Add(T.Txt("VOICEBOX 2.4.1 · NEURAL DIFFUSION", 9, T.TextFaint, FontWeights.SemiBold));
            bar.Children.Add(sp);
            bar.Children.Add(rightS);

            StartWarmWatch();

            Grid g = T.Glass(10, bar, new Thickness(0), true);
            return g;
        }

        // =========================================================
        //  voice / language / tracks
        // =========================================================
        void ShowVoiceMenu(Border anchor)
        {
            ContextMenu m = new ContextMenu();
            foreach (VoiceModel v in Library.Installed())
            {
                VoiceModel vm = v;
                MenuItem mi = new MenuItem();
                mi.Header = vm.Name + (vm.Name == _voice ? "  ✓" : "");
                mi.Click += delegate { SetVoice(vm.Name); };
                m.Items.Add(mi);
            }
            T.StyleMenu(m);
            m.PlacementTarget = anchor;
            m.Placement = PlacementMode.Bottom;
            m.IsOpen = true;
        }

        void SetVoice(string name)
        {
            VoiceModel v = Library.Find(name);
            if (v == null) return;
            _voice = name;
            Project.VoiceName = name;
            Project.Tracks[0].Voice = name;
            _voiceName.Text = name;
            _voiceAv.Text = Initials(name);
            _exprTarget.Text = name.ToUpperInvariant();
            if (_arr != null) _arr.InvalidateVisual();
            UpdateStatusVoice();
            Toast("VOICE LOADED", name + " · " + v.Langs);
        }

        static string Initials(string name)
        {
            string letters = "";
            foreach (string part in name.Split(' '))
                if (part.Length > 0 && char.IsLetter(part[0])) letters += char.ToUpperInvariant(part[0]);
            if (letters.Length == 0) letters = "VB";
            return letters.Length > 2 ? letters.Substring(0, 2) : letters;
        }

        void SetLanguage(string key)
        {
            Project.Language = key;
            if (_param != null) { _param.Lang = key; _param.InvalidateVisual(); }
            UpdateStatusVoice();
            if (_pr != null) _pr.InvalidateVisual();
            Toast("LANGUAGE → " + Langs.Label(key).ToUpperInvariant(),
                "Lyrics re-phonemized natively — no foreign-accent approximation.");
        }

        void UpdateStatusVoice()
        {
            if (_statusVoice == null) return;
            _statusVoice.Text = _voice.ToUpperInvariant() + " · " + Langs.Short(Project.Language);
        }

        VelocityLane _velLane;
        TranslateTransform _velShift;

        void SelectTrack(string id)
        {
            CurrentTrack = id;
            if (_pr != null) { _pr.Track = id; _pr.InvalidateVisual(); }
            if (_velLane != null) _velLane.InvalidateVisual();
            SelectedId = -1;
            SelectNote(null);
            BuildTrackHeads();
            SyncTrackMix();
        }

        Track FindTrack(string id)
        {
            foreach (Track t in Project.Tracks) if (t.Id == id) return t;
            return null;
        }

        static string PanLabel(double pan)
        {
            int v = (int)Math.Round(pan * 100);
            if (v == 0) return "C";
            return (v < 0 ? "L" : "R") + Math.Abs(v);
        }

        // The track mix rows follow the selected track rather than keeping their own value.
        void SyncTrackMix()
        {
            Track t = FindTrack(CurrentTrack);
            if (t == null) return;
            if (_trackGainF != null) { _trackGainF.Value = t.GainDb; _trackGainF.InvalidateVisual(); }
            if (_trackPanF != null) { _trackPanF.Value = t.Pan * 100; _trackPanF.InvalidateVisual(); }
            if (_trackGainVal != null) _trackGainVal.Text = (t.GainDb > 0 ? "+" : "") + t.GainDb.ToString("0.#");
            if (_trackPanVal != null) _trackPanVal.Text = PanLabel(t.Pan);
        }

        // =========================================================
        //  note selection / editing
        // =========================================================
        public void SelectNote(Note n)
        {
            SelectedId = n == null ? -1 : n.Id;
            if (_pr != null) _pr.InvalidateVisual();
            if (_selInfo != null)
            {
                if (n == null) _selInfo.Text = "NO NOTE SELECTED";
                else _selInfo.Text = Geo.NoteName(n.Midi) + " · " + n.Dur.ToString("0.00") + " BEAT · " + n.Word.ToUpperInvariant();
            }
            if (_lyricBox != null)
            {
                _updatingUi = true;
                _lyricBox.Text = n == null ? "" : n.Word;
                _updatingUi = false;
            }
        }

        public void OnNoteDragged(Note n)
        {
            if (_selInfo != null)
                _selInfo.Text = Geo.NoteName(n.Midi) + " · " + n.Dur.ToString("0.00") + " BEAT · " + n.Word.ToUpperInvariant();
            _needRender = true;
        }

        void Humanize()
        {
            List<Note> targets = SelectedId >= 0
                ? Project.Notes.Where(n => n.Id == SelectedId).ToList()
                : Project.VisibleNotes(CurrentTrack);
            for (int i = 0; i < targets.Count; i++)
            {
                Note n = targets[i];
                n.Vib = Math.Max(0.08, Math.Min(1, n.Vib + (i % 2 == 0 ? -0.04 : 0.06)));
                n.Drift = ((i % 5) - 2) * 0.9;
            }
            _pr.InvalidateVisual();
            _param.InvalidateVisual();
            _needRender = true;
            Toast("AI TUNE", "Pitch drift, scoops and vibrato humanized on " + targets.Count + " notes.");
        }

        void ApplyTake(int idx)
        {
            _takeIndex = idx;
            Project.RestoreBase();
            if (idx == 1)
            {
                List<Note> list = Project.Notes;
                for (int i = 0; i < list.Count; i++)
                {
                    Note n = list[i];
                    if (i % 5 == 0) n.Midi = Math.Min(Project.TopMidi, n.Midi + 1);
                    n.Start = Math.Max(0, n.Start + (i % 3 == 0 ? 0.04 : -0.02));
                    n.Vib = Math.Min(1, n.Vib + 0.1);
                }
            }
            else if (idx == 2)
            {
                foreach (Note n in Project.Notes) { n.Vib = Math.Max(0.1, n.Vib * 0.7); n.Rate = 5.9; }
            }
            else if (idx > 2)
            {
                List<Note> list = Project.Notes;
                for (int i = 0; i < list.Count; i++)
                {
                    Note n = list[i];
                    if (i % 7 == 0) n.Midi = Math.Max(Project.BottomMidi, n.Midi - 1);
                    n.Vib = Math.Max(0.1, Math.Min(0.95, 0.25 + (i * 37 % 60) / 100.0));
                }
            }
            SelectedId = -1;
            SelectNote(null);
            _pr.InvalidateVisual();
            _param.InvalidateVisual();
            _needRender = true;
            RenderRetakes();
            Toast("TAKE " + (idx + 1) + " ACTIVE", _takeSubs[idx]);
        }

        void GenerateTake()
        {
            int n = _takeSubs.Count + 1;
            string[] desc = { "Timing alt · breathy", "Pitch contour alt · brighter", "Hybrid · soft onset" };
            _takeSubs.Add(desc[(n - 4) % 3]);
            _takeTags.Add("NEW");
            RenderRetakes();
            ApplyTake(_takeSubs.Count - 1);
            Toast("NEW TAKE", "Non-destructive — the original performance is untouched.");
        }

        // Writes the take to a file the user keeps, with the live EQ and style applied.
        void ExportTake(bool normalize)
        {
            string mode = normalize ? "wav" : "raw";
            Microsoft.Win32.SaveFileDialog dlg = ExportDialog(mode,
                normalize ? "Export rendered take" : "Export raw-level take",
                normalize ? "Voice box take.wav" : "Voice box take raw.wav");
            if (dlg.ShowDialog() != true) return;

            string full = System.IO.Path.GetFullPath(dlg.FileName);
            AppSettings.RememberExport(mode, full);
            if (string.Equals(full, System.IO.Path.GetFullPath(_wavBase), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(full, System.IO.Path.GetFullPath(_wavAlt), StringComparison.OrdinalIgnoreCase))
            {
                Toast("EXPORT", "Pick another folder — that file is the live playback buffer.");
                return;
            }

            List<Note> snapshot = Project.Notes.Select(x => x.Copy()).ToList();
            double bpm = Project.Bpm, ten = _tension, bre = _breath, gen = _gender;
            bool met = _metro;
            string name = System.IO.Path.GetFileName(full);
            double trim = _trimDb;
            _engineStatus.Text = "EXPORTING WAV…";
            Task.Factory.StartNew(delegate
            {
                VocalSynth.Render(full, bpm, ten, bre, gen, met, snapshot, normalize, trim);
            }).ContinueWith(delegate
            {
                long bytes = 0;
                try { if (File.Exists(full)) bytes = new FileInfo(full).Length; } catch { }
                if (bytes > 0) Toast("WAV EXPORTED", name + " · " + (bytes / 1024) + " KB · " + (normalize ? "peak-normalised" : "raw session level") +
                    (Math.Abs(trim) < 0.05 ? "" : " · trim " + (trim > 0 ? "+" : "") + trim.ToString("0.#") + " dB") + " with the current EQ and style.");
                else Toast("EXPORT", "Could not write " + name + ".");
                _engineStatus.Text = _playing ? "SYNTHESIZING" : "ENGINE READY";
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        // Export dialogs open where the last one went, with the name used last for that mode.
        Microsoft.Win32.SaveFileDialog ExportDialog(string mode, string title, string fallbackName)
        {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.Filter = "Wave audio (*.wav)|*.wav";
            dlg.DefaultExt = ".wav";
            dlg.Title = title;
            dlg.FileName = AppSettings.ExportName(mode, fallbackName);
            if (!string.IsNullOrEmpty(AppSettings.LastExportDir) && Directory.Exists(AppSettings.LastExportDir))
                dlg.InitialDirectory = AppSettings.LastExportDir;
            return dlg;
        }

        // key=value beside the stems: the EQ, style, fade, expression and track mix that made
        // them, so the same balance can be rebuilt in a later session.
        string SessionText(List<string> stemPaths)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("# Voice Box session written with the stems");
            sb.AppendLine("created=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture));
            sb.AppendLine("bpm=" + N(Project.Bpm));
            sb.AppendLine("sampleRate=" + VocalSynth.Sr);
            sb.AppendLine("channels=2");
            sb.AppendLine("export.trim=" + N(_trimDb));
            sb.AppendLine("expr.tension=" + N(_tension));
            sb.AppendLine("expr.breath=" + N(_breath));
            sb.AppendLine("expr.gender=" + N(_gender));
            sb.AppendLine("eq.low=" + N(RenderStyle.Eq.LowDb));
            sb.AppendLine("eq.mid=" + N(RenderStyle.Eq.MidDb));
            sb.AppendLine("eq.high=" + N(RenderStyle.Eq.HighDb));
            sb.AppendLine("eq.bypass=" + (RenderStyle.Eq.Bypass ? "1" : "0"));

            StyleProfile st = RenderStyle.Current;
            sb.AppendLine("style.source=" + (st != null ? st.Source : ""));
            if (st != null)
            {
                sb.AppendLine("style.median=" + N(st.MedianNote));
                sb.AppendLine("style.vibRate=" + N(st.VibratoRate));
                sb.AppendLine("style.vibDepth=" + N(st.VibratoDepth));
                sb.AppendLine("style.brightness=" + N(st.Brightness));
                sb.AppendLine("style.breath=" + N(st.Breath));
            }

            sb.AppendLine("fade.ms=" + N(_xfadeMs));
            sb.AppendLine("fade.curve=" + (_xfadeLinear ? "linear" : "equal-power"));

            foreach (Track t in Project.Tracks)
            {
                sb.AppendLine("track." + t.Id + ".name=" + t.Name);
                sb.AppendLine("track." + t.Id + ".gain=" + N(t.GainDb));
                sb.AppendLine("track." + t.Id + ".pan=" + N(t.Pan));
                sb.AppendLine("track." + t.Id + ".mute=" + (t.Muted ? "1" : "0"));
                sb.AppendLine("track." + t.Id + ".solo=" + (t.Solo ? "1" : "0"));
            }

            foreach (string p in stemPaths) sb.AppendLine("stem=" + System.IO.Path.GetFileName(p));
            return sb.ToString();
        }

        static string N(double v)
        {
            return v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

        // One WAV per track at the synth's own level — that is what keeps a stem mix balanced.
        void ExportStems()
        {
            Microsoft.Win32.SaveFileDialog dlg = ExportDialog("stems", "Choose where to write the stems", "Voice box stems.wav");
            if (dlg.ShowDialog() != true) return;

            string full = System.IO.Path.GetFullPath(dlg.FileName);
            AppSettings.RememberExport("stems", full);
            string dir = System.IO.Path.GetDirectoryName(full);
            string baseName = System.IO.Path.GetFileNameWithoutExtension(full);
            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(baseName))
            {
                Toast("STEMS", "Pick a folder to write the stems into.");
                return;
            }
            WriteStems(dir, baseName);
        }

        void WriteStems(string dir, string baseName)
        {
            List<Note> snapshot = Project.Notes.Select(x => x.Copy()).ToList();
            List<string> paths = new List<string>();
            List<List<Note>> parts = new List<List<Note>>();
            List<string> silent = new List<string>();
            bool anySolo = VocalSynth.AnySolo();

            foreach (Track t in Project.Tracks)
            {
                List<Note> mine = snapshot.Where(n => n.Track == t.Id).ToList();
                if (mine.Count == 0) continue;
                // A muted or solo-silenced track gets no stem: its audio is not in the mix either.
                if (!VocalSynth.TrackAudible(t, anySolo)) { silent.Add(t.Name); continue; }
                paths.Add(System.IO.Path.Combine(dir, SafeName(baseName + " - " + t.Name) + ".wav"));
                parts.Add(mine);
            }

            if (paths.Count == 0)
            {
                Toast("STEMS", "No audible tracks to write — check mute and solo.");
                return;
            }

            double bpm = Project.Bpm, ten = _tension, bre = _breath, gen = _gender;
            double trim = _trimDb;
            string session = SessionText(paths);
            _engineStatus.Text = "WRITING STEMS…";
            Task.Factory.StartNew(delegate
            {
                for (int i = 0; i < paths.Count; i++)
                    VocalSynth.Render(paths[i], bpm, ten, bre, gen, false, parts[i], false, trim);
            }).ContinueWith(delegate
            {
                int made = 0;
                for (int i = 0; i < paths.Count; i++) if (File.Exists(paths[i])) made++;
                if (made > 0)
                {
                    string note = System.IO.Path.Combine(dir, SafeName(baseName) + " - session.txt");
                    try { System.IO.File.WriteAllText(note, session); } catch { }
                    string quiet = silent.Count > 0 ? " · skipped " + string.Join(", ", silent.ToArray()) + " (mute/solo)" : "";
                    Toast("STEMS EXPORTED", made + " stems at raw level, plus " + System.IO.Path.GetFileName(note) + quiet + ".");
                }
                else Toast("STEMS", "Could not write the stems.");
                _engineStatus.Text = _playing ? "SYNTHESIZING" : "ENGINE READY";
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        static string SafeName(string name)
        {
            char[] bad = System.IO.Path.GetInvalidFileNameChars();
            string s = (name ?? "").Trim();
            for (int i = 0; i < bad.Length; i++) s = s.Replace(bad[i], '_');
            return s.Length == 0 ? "stem" : s;
        }

        void SimulateRender()
        {
            _engineStatus.Text = "RENDERING TAKES…";
            DispatcherTimer t = new DispatcherTimer();
            t.Interval = TimeSpan.FromMilliseconds(1400);
            t.Tick += delegate
            {
                t.Stop();
                _engineStatus.Text = "ENGINE READY";
                Toast("RENDER COMPLETE", "3 takes written · " + SampleRateLabel() + " / 16-bit · non-destructive.");
            };
            t.Start();
        }

        // =========================================================
        //  lyrics, style match, equalizer
        // =========================================================
        UIElement BuildLyricsPanel()
        {
            StackPanel root = new StackPanel();
            StackPanel right = new StackPanel();
            Border badge = new Border();
            badge.CornerRadius = new CornerRadius(5);
            badge.Padding = new Thickness(6, 2, 6, 2);
            badge.Background = T.SA("#46c8f0", 34);
            badge.BorderBrush = T.SA("#8ce0ff", 50);
            badge.BorderThickness = new Thickness(1);
            badge.Child = T.Txt("TRACK", 8, T.C("#9ec8f5"), FontWeights.SemiBold);
            right.Children.Add(badge);
            root.Children.Add(T.PanelHead("Lyrics", null, true, right));

            StackPanel body = new StackPanel();
            body.Margin = new Thickness(11, 9, 11, 10);

            _lyricsBox = T.Input("Type a lyric line…");
            _lyricsBox.AcceptsReturn = true;
            _lyricsBox.TextWrapping = TextWrapping.Wrap;
            _lyricsBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            _lyricsBox.Height = 58;
            _lyricsBox.Padding = new Thickness(7, 5, 7, 5);
            _lyricsBox.VerticalContentAlignment = VerticalAlignment.Top;
            body.Children.Add(_lyricsBox);

            Grid row = new Grid();
            row.Margin = new Thickness(0, 8, 0, 0);
            row.ColumnDefinitions.Add(ColStar());
            row.ColumnDefinitions.Add(ColFix(78));
            Border apply = T.Btn("SING LYRICS", delegate { ApplyLyrics(); }, true);
            row.Children.Add(apply);
            _lyricsCount = T.Txt("", 8.3, T.TextDim, FontWeights.SemiBold);
            _lyricsCount.HorizontalAlignment = HorizontalAlignment.Right;
            _lyricsCount.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_lyricsCount, 1);
            row.Children.Add(_lyricsCount);
            body.Children.Add(row);

            TextBlock hint = T.Txt("Sung on the selected track · one syllable per note · leftover notes hold the last vowel.", 8.3, T.C("#5f7897"));
            hint.TextWrapping = TextWrapping.Wrap;
            hint.Margin = new Thickness(0, 7, 0, 0);
            body.Children.Add(hint);

            root.Children.Add(body);
            return T.Glass(14, root, new Thickness(0), true);
        }

        UIElement BuildStylePanel()
        {
            StackPanel root = new StackPanel();
            StackPanel right = new StackPanel();
            right.Orientation = Orientation.Horizontal;
            right.VerticalAlignment = VerticalAlignment.Center;

            Border styleKey = T.KeyCap("S");
            styleKey.Margin = new Thickness(0, 0, 6, 0);
            styleKey.ToolTip = "S steps through presets · 1–9 loads one by number";
            right.Children.Add(styleKey);

            _styleBadge = new Border();
            _styleBadge.CornerRadius = new CornerRadius(5);
            _styleBadge.Padding = new Thickness(6, 2, 6, 2);
            _styleBadge.Background = T.SA("#46c8f0", 30);
            _styleBadge.BorderBrush = T.SA("#8ce0ff", 45);
            _styleBadge.BorderThickness = new Thickness(1);
            _styleBadge.Child = T.Txt("OFF", 8, T.C("#9ec8f5"), FontWeights.SemiBold);
            _styleBadge.VerticalAlignment = VerticalAlignment.Center;
            right.Children.Add(_styleBadge);
            root.Children.Add(T.PanelHead("Style Match", null, true, right));

            StackPanel body = new StackPanel();
            body.Margin = new Thickness(11, 9, 11, 10);
            _styleInfo = T.Txt("Learn a sung phrase — range, vibrato, brightness and air — then sing new notes in that style.", 8.5, T.C("#7e9ab9"));
            _styleInfo.TextWrapping = TextWrapping.Wrap;
            body.Children.Add(_styleInfo);

            Grid row = new Grid();
            row.Margin = new Thickness(0, 8, 0, 0);
            row.ColumnDefinitions.Add(ColStar());
            row.ColumnDefinitions.Add(ColFix(6));
            row.ColumnDefinitions.Add(ColFix(88));
            Border learn = T.Btn("LEARN FROM TAKE", delegate { LearnStyle(); }, true);
            row.Children.Add(learn);
            Border load = T.Btn("LOAD WAV…", delegate { LoadStyleWav(); }, false);
            Grid.SetColumn(load, 2);
            row.Children.Add(load);
            body.Children.Add(row);

            _styleName = T.Input("Preset name…");
            _styleName.Margin = new Thickness(0, 8, 0, 0);
            body.Children.Add(_styleName);

            Border saveStyle = T.Btn("SAVE STYLE", delegate { SaveStylePreset(); }, false);
            saveStyle.Margin = new Thickness(0, 6, 0, 0);
            body.Children.Add(saveStyle);
            _styleSaveBtn = saveStyle;
            _styleSaveLabel = saveStyle.Child as TextBlock;

            Border import = T.Btn("IMPORT STYLE FILE…", delegate { ImportStylePreset(); }, false);
            import.Margin = new Thickness(0, 5, 0, 0);
            body.Children.Add(import);

            _styleList = new StackPanel();
            _styleList.Margin = new Thickness(0, 6, 0, 0);
            body.Children.Add(_styleList);

            Border off = T.Btn("STYLE OFF", delegate { ClearStyle(); }, false);
            off.Margin = new Thickness(0, 6, 0, 0);
            body.Children.Add(off);

            TextBlock hint = T.Txt("Keeps phrasing, colour and air — the melody and words stay yours. Saved styles live in %LOCALAPPDATA%\\VoiceBox\\Styles.", 8.3, T.C("#5f7897"));
            hint.TextWrapping = TextWrapping.Wrap;
            hint.Margin = new Thickness(0, 7, 0, 0);
            body.Children.Add(hint);

            root.Children.Add(body);
            RenderStylePresets();
            return T.Glass(14, root, new Thickness(0), true);
        }

        void RenderStylePresets()
        {
            if (_styleList == null) return;
            _styleList.Children.Clear();

            List<string> names = StyleStore.Names();
            if (names.Count == 0)
            {
                _styleList.Children.Add(T.Txt("No saved styles yet.", 8.3, T.C("#5f7897")));
                return;
            }

            if (_renameFrom != null)
            {
                TextBlock edit = T.Txt("Renaming “" + _renameFrom + "” — type the new name above, then press RENAME.", 8.2, T.C("#8ce6ff"));
                edit.TextWrapping = TextWrapping.Wrap;
                edit.Margin = new Thickness(0, 0, 0, 5);
                _styleList.Children.Add(edit);
            }

            foreach (string name in names)
            {
                string key = name;
                Grid row = new Grid();
                row.Margin = new Thickness(0, 0, 0, 4);
                row.ColumnDefinitions.Add(ColStar());
                row.ColumnDefinitions.Add(ColFix(40));
                row.ColumnDefinitions.Add(ColFix(30));
                row.ColumnDefinitions.Add(ColFix(22));
                row.ColumnDefinitions.Add(ColFix(24));

                StackPanel info = new StackPanel();
                info.Children.Add(T.Txt(key, 9, T.C("#dceeff"), FontWeights.SemiBold));
                StyleProfile p = StyleStore.Load(key);
                if (p != null && p.Valid)
                {
                    info.Children.Add(T.Txt(
                        MidiName((int)Math.Round(p.LowNote)) + "–" + MidiName((int)Math.Round(p.HighNote)) +
                        " · vib " + p.VibratoRate.ToString("0.0") + " Hz", 8, T.C("#7e9ab9")));
                }
                row.Children.Add(info);

                Border use = T.Btn("USE", delegate { ApplyStylePreset(key); }, false);
                use.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(use, 1);
                row.Children.Add(use);

                Border exp = MiniBtn("EXP", "#bcdcff", "#1c3a63", "#84bdff", delegate { ExportStylePreset(key); });
                Grid.SetColumn(exp, 2);
                row.Children.Add(exp);

                Border ren = MiniBtn("✎", "#d6ecff", "#1c3a63", "#84bdff", delegate { BeginRename(key); });
                Grid.SetColumn(ren, 3);
                row.Children.Add(ren);

                Border del = new Border();
                del.CornerRadius = new CornerRadius(5);
                del.Padding = new Thickness(5, 2, 5, 2);
                del.Background = T.SA("#3a1a2c", 110);
                del.BorderBrush = T.SA("#ff9cc4", 60);
                del.BorderThickness = new Thickness(1);
                del.Cursor = Cursors.Hand;
                del.Margin = new Thickness(4, 0, 0, 0);
                del.VerticalAlignment = VerticalAlignment.Center;
                del.Child = T.Txt("✕", 8, T.C("#ffc7da"), FontWeights.Bold);
                del.MouseLeftButtonUp += delegate { DeleteStylePreset(key); };
                Grid.SetColumn(del, 4);
                row.Children.Add(del);

                _styleList.Children.Add(row);
            }
        }

        Border MiniBtn(string text, string fg, string bg, string border, Action click)
        {
            Border b = new Border();
            b.CornerRadius = new CornerRadius(5);
            b.Padding = new Thickness(4, 2, 4, 2);
            b.Background = T.SA(bg, 140);
            b.BorderBrush = T.SA(border, 60);
            b.BorderThickness = new Thickness(1);
            b.Cursor = Cursors.Hand;
            b.Margin = new Thickness(3, 0, 0, 0);
            b.VerticalAlignment = VerticalAlignment.Center;
            b.Child = T.Txt(text, 8, T.C(fg), FontWeights.SemiBold);
            b.MouseLeftButtonUp += delegate { if (click != null) click(); };
            return b;
        }

        void BeginRename(string name)
        {
            if (_styleName == null) return;
            if (_renameFrom == name) { EndRename(); return; }
            _renameFrom = name;
            _styleName.Text = name;
            _styleName.Focus();
            _styleName.SelectAll();
            if (_styleSaveLabel != null) _styleSaveLabel.Text = "RENAME ▸";
            RenderStylePresets();
            Toast("RENAME STYLE", "Type a new name, then press RENAME.");
        }

        void EndRename()
        {
            _renameFrom = null;
            if (_styleSaveLabel != null) _styleSaveLabel.Text = "SAVE STYLE";
            if (_styleName != null) _styleName.Text = "";
            RenderStylePresets();
        }

        void ExportStylePreset(string name)
        {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.Filter = "VoiceBox style (*.style)|*.style|All files (*.*)|*.*";
            dlg.DefaultExt = ".style";
            dlg.FileName = name + ".style";
            dlg.Title = "Export style preset";
            if (dlg.ShowDialog() != true) return;

            string file = System.IO.Path.GetFileName(dlg.FileName);
            if (StyleStore.Export(name, dlg.FileName)) Toast("STYLE EXPORTED", name + " saved as " + file + ".");
            else Toast("STYLE EXPORT", "Could not write " + file + ".");
        }

        void ImportStylePreset()
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "VoiceBox style (*.style)|*.style|All files (*.*)|*.*";
            dlg.Title = "Import a style preset";
            if (dlg.ShowDialog() != true) return;

            string name = StyleStore.Import(dlg.FileName);
            if (name == null)
            {
                Toast("STYLE IMPORT", "That file is not a VoiceBox style preset.");
                return;
            }
            RenderStylePresets();
            Toast("STYLE IMPORTED", name + " added to your styles.");
        }

        void SaveStylePreset()
        {
            string typed = _styleName == null ? "" : _styleName.Text.Trim();
            if (_renameFrom != null)
            {
                string from = _renameFrom;
                if (typed.Length == 0)
                {
                    Toast("RENAME", "Type the new name for " + from + ".");
                    return;
                }
                if (string.Compare(typed, from, true) == 0)
                {
                    EndRename();
                    Toast("RENAME", "That is already the name.");
                    return;
                }
                if (StyleStore.Exists(typed))
                {
                    Toast("RENAME", "A style called " + typed + " already exists.");
                    return;
                }
                if (!StyleStore.Rename(from, typed))
                {
                    Toast("RENAME", "Could not rename " + from + ".");
                    return;
                }
                EndRename();
                Toast("STYLE RENAMED", from + " → " + typed + ".");
                return;
            }

            StyleProfile p = RenderStyle.Current;
            if (p == null || !p.Valid)
            {
                Toast("STYLE", "Learn a style first, then save it.");
                return;
            }
            string name = _styleName == null ? "" : _styleName.Text.Trim();
            if (name.Length == 0) name = "Style " + DateTime.Now.ToString("HHmm");
            StyleStore.Save(name, p);
            if (_styleName != null) _styleName.Text = "";
            RenderStylePresets();
            Toast("STYLE SAVED", name + " kept for later.");
        }

        void ApplyStylePreset(string name)
        {
            if (_renameFrom != null) EndRename();
            StyleProfile p = StyleStore.Load(name);
            if (p == null || !p.Valid)
            {
                Toast("STYLE", "Could not read " + name + ".");
                return;
            }
            ApplyStyle(p);
            if (_styleName != null) _styleName.Text = name;
        }

        void DeleteStylePreset(string name)
        {
            if (_renameFrom == name) EndRename();
            StyleStore.Delete(name);
            RenderStylePresets();
            Toast("STYLE REMOVED", name + " deleted.");
        }

        void SyncEqFromCurve()
        {
            if (_eqLowF == null) return;
            _eqLowF.Value = RenderStyle.Eq.LowDb;
            _eqMidF.Value = RenderStyle.Eq.MidDb;
            _eqHighF.Value = RenderStyle.Eq.HighDb;
            _eqLowF.InvalidateVisual();
            _eqMidF.InvalidateVisual();
            _eqHighF.InvalidateVisual();
            _eqLowVal.Text = RenderStyle.Eq.LowDb.ToString("0.0") + " dB";
            _eqMidVal.Text = RenderStyle.Eq.MidDb.ToString("0.0") + " dB";
            _eqHighVal.Text = RenderStyle.Eq.HighDb.ToString("0.0") + " dB";
            MarkAudioDirty();
        }

        UIElement BuildEqPanel()
        {
            StackPanel root = new StackPanel();
            StackPanel right = new StackPanel();
            Border badge = new Border();
            badge.CornerRadius = new CornerRadius(5);
            badge.Padding = new Thickness(6, 2, 6, 2);
            badge.Background = T.SA("#46c8f0", 30);
            badge.BorderBrush = T.SA("#8ce0ff", 45);
            badge.BorderThickness = new Thickness(1);
            badge.Child = T.Txt("3-BAND", 8, T.C("#9ec8f5"), FontWeights.SemiBold);
            right.Children.Add(badge);
            root.Children.Add(T.PanelHead("Equalizer", null, true, right));

            StackPanel body = new StackPanel();
            body.Margin = new Thickness(11, 9, 11, 10);

            _eqCurve = new EqCurve();
            _eqCurve.Eq = RenderStyle.Eq;
            _eqCurve.Changed = delegate { SyncEqFromCurve(); };
            _eqCurve.Margin = new Thickness(0, 0, 0, 8);
            body.Children.Add(_eqCurve);

            body.Children.Add(SliderRow("LOW", -12, 12, RenderStyle.Eq.LowDb, delegate(double v)
            {
                RenderStyle.Eq.LowDb = v;
                _eqLowVal.Text = v.ToString("0.0") + " dB";
                if (_eqCurve != null) _eqCurve.InvalidateVisual();
                MarkAudioDirty();
            }, out _eqLowVal, out _eqLowF));
            body.Children.Add(SliderRow("MID", -12, 12, RenderStyle.Eq.MidDb, delegate(double v)
            {
                RenderStyle.Eq.MidDb = v;
                _eqMidVal.Text = v.ToString("0.0") + " dB";
                if (_eqCurve != null) _eqCurve.InvalidateVisual();
                MarkAudioDirty();
            }, out _eqMidVal, out _eqMidF));
            body.Children.Add(SliderRow("HIGH", -12, 12, RenderStyle.Eq.HighDb, delegate(double v)
            {
                RenderStyle.Eq.HighDb = v;
                _eqHighVal.Text = v.ToString("0.0") + " dB";
                if (_eqCurve != null) _eqCurve.InvalidateVisual();
                MarkAudioDirty();
            }, out _eqHighVal, out _eqHighF));

            _eqLowVal.Text = RenderStyle.Eq.LowDb.ToString("0.0") + " dB";
            _eqMidVal.Text = RenderStyle.Eq.MidDb.ToString("0.0") + " dB";
            _eqHighVal.Text = RenderStyle.Eq.HighDb.ToString("0.0") + " dB";

            _eqChip = T.Chip("EQ ON", !RenderStyle.Eq.Bypass, delegate { ToggleEqBypass(); }, "E");
            _eqChip.ToolTip = "E bypasses or re-enables the equalizer";
            _eqChip.Margin = new Thickness(0, 4, 0, 0);
            body.Children.Add(_eqChip);

            _fadeDockHost = new StackPanel();
            _fadeDockHost.Margin = new Thickness(0, 9, 0, 0);
            body.Children.Add(_fadeDockHost);

            _fadeDockHint = T.Txt("Fade controls are floating — press FLOAT again to dock them here.", 8.3, T.C("#5f7897"));
            _fadeDockHint.TextWrapping = TextWrapping.Wrap;
            _fadeDockHint.Visibility = Visibility.Collapsed;
            _fadeDockHost.Children.Add(_fadeDockHint);
            _fadeDockHost.Children.Add(BuildFadeCard());

            root.Children.Add(body);
            return T.Glass(14, root, new Thickness(0), true);
        }

        // The fade strip belongs to the EQ panel, but it can be popped out into a small card that
        // floats over the workspace and stays visible while other panels are open.
        Border BuildFadeCard()
        {
            _fadeBody = new StackPanel();
            _fadeBody.Children.Add(SliderRow("LIVE FADE MS", 0, 300, _xfadeMs, delegate(double v)
            {
                _xfadeMs = Math.Round(v);
                _xfadeVal.Text = ((int)_xfadeMs).ToString();
            }, out _xfadeVal, out _xfadeFader));
            // Save on release rather than on every drag step, so a slide writes once.
            if (_xfadeFader != null) _xfadeFader.MouseLeftButtonUp += delegate { PersistFade(); };
            _xfadeVal.Text = ((int)_xfadeMs).ToString();

            _xfadeCurveChip = T.Chip(_xfadeLinear ? "FADE LINEAR" : "FADE EQUAL-POWER", true, delegate { ToggleFadeCurve(); });
            _xfadeCurveChip.Margin = new Thickness(0, 6, 0, 0);
            _xfadeCurveChip.HorizontalAlignment = HorizontalAlignment.Left;
            _fadeBody.Children.Add(_xfadeCurveChip);

            TextBlock hint = T.Txt("Heard live while a take plays: 0 ms cuts straight over, 300 ms glides. Keys [ and ] nudge it from the transport.", 8.3, T.C("#5f7897"));
            hint.TextWrapping = TextWrapping.Wrap;
            hint.Margin = new Thickness(0, 6, 0, 0);
            _fadeBody.Children.Add(hint);

            Grid head = new Grid();
            head.Background = Brushes.Transparent;
            head.ColumnDefinitions.Add(ColStar());
            head.ColumnDefinitions.Add(ColFix(48));
            head.ColumnDefinitions.Add(ColFix(38));
            TextBlock title = T.Txt("LIVE FADE", 8.5, T.C("#9ec8f5"), FontWeights.SemiBold);
            title.VerticalAlignment = VerticalAlignment.Center;
            head.Children.Add(title);

            _fadeFloatBtn = T.Chip("FLOAT", false, delegate { ToggleFadeFloat(); });
            _fadeFloatBtn.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(_fadeFloatBtn, 1);
            head.Children.Add(_fadeFloatBtn);

            _fadeMinBtn = T.Chip("–", false, delegate { ToggleFadeCollapse(); });
            _fadeMinBtn.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(_fadeMinBtn, 2);
            head.Children.Add(_fadeMinBtn);

            head.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                if (!_fadeDetached) return;
                head.CaptureMouse();
                _fadeDragOrigin = e.GetPosition(_floatLayer);
                _fadeDragLeft = Canvas.GetLeft(_fadeFloat);
                _fadeDragTop = Canvas.GetTop(_fadeFloat);
                e.Handled = true;
            };
            head.MouseMove += delegate(object s, MouseEventArgs e)
            {
                if (!_fadeDetached || !head.IsMouseCaptured) return;
                Point p = e.GetPosition(_floatLayer);
                Canvas.SetLeft(_fadeFloat, _fadeDragLeft + (p.X - _fadeDragOrigin.X));
                Canvas.SetTop(_fadeFloat, Math.Max(0, _fadeDragTop + (p.Y - _fadeDragOrigin.Y)));
            };
            head.MouseLeftButtonUp += delegate { head.ReleaseMouseCapture(); };

            StackPanel card = new StackPanel();
            card.Children.Add(head);
            card.Children.Add(_fadeBody);

            _fadeFloat = new Border();
            _fadeFloat.Child = card;
            return _fadeFloat;
        }

        void ToggleFadeFloat()
        {
            _fadeDetached = !_fadeDetached;
            if (_fadeDetached) DetachFade(); else AttachFade();

            TextBlock label = _fadeFloatBtn != null ? _fadeFloatBtn.Child as TextBlock : null;
            if (label != null) label.Text = _fadeDetached ? "DOCK" : "FLOAT";
        }

        void DetachFade()
        {
            if (_fadeFloat == null || _floatLayer == null) return;
            Panel owner = _fadeFloat.Parent as Panel;
            if (owner != null) owner.Children.Remove(_fadeFloat);

            _fadeFloat.Width = 246;
            _fadeFloat.Padding = new Thickness(12, 7, 12, 10);
            _fadeFloat.CornerRadius = new CornerRadius(12);
            _fadeFloat.Background = new LinearGradientBrush(Color.FromArgb(244, 22, 56, 104), Color.FromArgb(248, 9, 22, 44), 90);
            _fadeFloat.BorderBrush = T.SA("#8ccdff", 100);
            _fadeFloat.BorderThickness = new Thickness(1);
            _fadeFloat.Effect = T.Glow(T.C("#4aa8ff"), 24, 0.5);
            _floatLayer.Children.Add(_fadeFloat);

            double right = _root.ActualWidth > 400 ? _root.ActualWidth - 270 : 40;
            Canvas.SetLeft(_fadeFloat, Math.Max(16, right));
            Canvas.SetTop(_fadeFloat, 132);
            if (_fadeDockHint != null) _fadeDockHint.Visibility = Visibility.Visible;
        }

        void AttachFade()
        {
            if (_fadeFloat == null) return;
            Panel owner = _fadeFloat.Parent as Panel;
            if (owner != null) owner.Children.Remove(_fadeFloat);

            _fadeFloat.Width = double.NaN;
            _fadeFloat.Padding = new Thickness(0);
            _fadeFloat.CornerRadius = new CornerRadius(0);
            _fadeFloat.Background = null;
            _fadeFloat.BorderBrush = null;
            _fadeFloat.BorderThickness = new Thickness(0);
            _fadeFloat.Effect = null;
            if (_fadeDockHost != null) _fadeDockHost.Children.Add(_fadeFloat);
            if (_fadeDockHint != null) _fadeDockHint.Visibility = Visibility.Collapsed;
        }

        void ToggleFadeCollapse()
        {
            _fadeCollapsed = !_fadeCollapsed;
            if (_fadeBody != null) _fadeBody.Visibility = _fadeCollapsed ? Visibility.Collapsed : Visibility.Visible;

            TextBlock label = _fadeMinBtn != null ? _fadeMinBtn.Child as TextBlock : null;
            if (label != null) label.Text = _fadeCollapsed ? "+" : "–";
        }

        void ApplyLyrics()
        {
            if (_lyricsBox == null) return;
            List<Note> notes = Project.VisibleNotes(CurrentTrack);
            int n = Lyrics.Assign(notes, _lyricsBox.Text);
            if (n == 0)
            {
                Toast("LYRICS", "Type some words, and make sure the selected track has notes.");
                return;
            }
            _lyricsCount.Text = n + " sung";
            _pr.InvalidateVisual();
            _arr.InvalidateVisual();
            _needRender = true;
            List<Note> ordered = notes.Where(x => x.Track != "p1").OrderBy(x => x.Start).ToList();
            if (ordered.Count > 0) SelectNote(ordered[0]);
            Toast("LYRICS SET", n + " syllables mapped onto the selected track.");
        }

        void LearnStyle()
        {
            if (_takeSamples == null || _takeSamples.Length < Recorder.SampleRate / 2)
            {
                Toast("STYLE MATCH", "Record a phrase first, or load a WAV sample.");
                return;
            }
            ApplyStyle(StyleProfile.Analyze(_takeSamples, Recorder.SampleRate, "recorded take"));
        }

        void LoadStyleWav()
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Filter = "Wave audio (*.wav)|*.wav|All files (*.*)|*.*";
            dlg.Title = "Choose a vocal sample";
            if (dlg.ShowDialog() != true) return;

            float[] samples;
            int sr;
            string err = WavIO.Read(dlg.FileName, out samples, out sr);
            if (err != null)
            {
                Toast("STYLE MATCH", err);
                return;
            }
            ApplyStyle(StyleProfile.Analyze(samples, sr, System.IO.Path.GetFileName(dlg.FileName)));
        }

        void ApplyStyle(StyleProfile p)
        {
            if (p == null || !p.Valid)
            {
                Toast("STYLE MATCH", "That sample had too little steady pitched singing to learn from.");
                return;
            }
            RenderStyle.Current = p;
            MarkAudioDirty();
            _styleInfo.Text = p.Source + "\nrange " + MidiName((int)Math.Round(p.LowNote)) + "–" + MidiName((int)Math.Round(p.HighNote)) +
                " · vibrato " + p.VibratoRate.ToString("0.0") + " Hz ×" + p.VibScale.ToString("0.00") +
                "\nbright " + ((int)(p.Brightness * 100)) + "% · air " + ((int)(p.Breath * 100)) + "%";
            _styleInfo.TextWrapping = TextWrapping.Wrap;
            _styleBadge.Child = T.Txt("ACTIVE", 8, T.C("#8ff0d4"), FontWeights.SemiBold);
            Toast("STYLE APPLIED", "New notes now follow this vocal's colour and phrasing.");
        }

        void ClearStyle()
        {
            RenderStyle.Current = null;
            MarkAudioDirty();
            _styleInfo.Text = "Learn a sung phrase — range, vibrato, brightness and air — then sing new notes in that style.";
            _styleBadge.Child = T.Txt("OFF", 8, T.C("#9ec8f5"), FontWeights.SemiBold);
            Toast("STYLE OFF", "Back to the default vocal character.");
        }

        static string MidiName(int m)
        {
            string[] names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            int n = Math.Max(0, Math.Min(127, m));
            return names[n % 12] + ((n / 12) - 1).ToString();
        }

        // =========================================================
        //  recording + MIDI transcription
        // =========================================================
        UIElement BuildRecordPanel()
        {
            StackPanel root = new StackPanel();
            StackPanel right = new StackPanel();
            Border badge = new Border();
            badge.CornerRadius = new CornerRadius(5);
            badge.Padding = new Thickness(6, 2, 6, 2);
            badge.Background = T.SA("#ff5a72", 40);
            badge.BorderBrush = T.SA("#ff9fb0", 70);
            badge.BorderThickness = new Thickness(1);
            badge.Child = T.Txt("LOCAL", 8, T.C("#ffc2cc"), FontWeights.SemiBold);
            right.Children.Add(badge);
            root.Children.Add(T.PanelHead("Record · MIDI", null, true, right));

            StackPanel body = new StackPanel();
            body.Margin = new Thickness(11, 9, 11, 10);

            Grid top = new Grid();
            top.ColumnDefinitions.Add(ColStar());
            top.ColumnDefinitions.Add(ColFix(52));
            _recInfo = T.Txt(Recorder.InputDevices + " input device(s) · " + RateLabel(Recorder.SampleRate), 8.5, T.TextDim, FontWeights.SemiBold);
            top.Children.Add(_recInfo);
            _recTimer = T.Txt("0.0 s", 8.5, T.C("#cfe9ff"), FontWeights.SemiBold);
            _recTimer.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(_recTimer, 1);
            top.Children.Add(_recTimer);
            body.Children.Add(top);

            Border meter = new Border();
            meter.Height = 5;
            meter.CornerRadius = new CornerRadius(3);
            meter.Margin = new Thickness(0, 6, 0, 0);
            meter.Background = T.SA("#08182c", 200);
            meter.BorderBrush = T.SA("#82b9ff", 26);
            meter.BorderThickness = new Thickness(1);
            _recMeterFill = new Border();
            _recMeterFill.Width = 2;
            _recMeterFill.CornerRadius = new CornerRadius(3);
            _recMeterFill.HorizontalAlignment = HorizontalAlignment.Left;
            _recMeterFill.Background = T.HGrad(T.C("#ff6d84"), T.C("#ffb36b"));
            meter.Child = _recMeterFill;
            body.Children.Add(meter);

            Grid buttons = new Grid();
            buttons.Margin = new Thickness(0, 9, 0, 0);
            buttons.ColumnDefinitions.Add(ColStar());
            buttons.ColumnDefinitions.Add(ColFix(8));
            buttons.ColumnDefinitions.Add(ColFix(62));
            Border convert = T.Btn("CONVERT TO MIDI", delegate { ConvertTake(); }, true);
            buttons.Children.Add(convert);
            Border clear = T.Btn("CLEAR", delegate { ClearTake(); }, false);
            Grid.SetColumn(clear, 2);
            buttons.Children.Add(clear);
            body.Children.Add(buttons);

            TextBlock hint = T.Txt("Records from the playhead, then extracts pitch and timing as notes on the selected track.", 8.3, T.C("#5f7897"));
            hint.TextWrapping = TextWrapping.Wrap;
            hint.Margin = new Thickness(0, 7, 0, 0);
            body.Children.Add(hint);

            root.Children.Add(body);
            return T.Glass(14, root, new Thickness(0), true);
        }

        Border RecordButton()
        {
            Border b = new Border();
            b.Width = 30;
            b.Height = 29;
            b.CornerRadius = new CornerRadius(8);
            b.Background = T.SA("#a83248", 80);
            b.BorderBrush = T.SA("#ff8fa0", 100);
            b.BorderThickness = new Thickness(1);
            b.Cursor = Cursors.Hand;
            _recordDot = new Ellipse();
            _recordDot.Width = 10;
            _recordDot.Height = 10;
            _recordDot.Fill = T.S(T.C("#ff5a72"));
            b.Child = _recordDot;
            b.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { e.Handled = true; };
            b.MouseLeftButtonUp += delegate { ToggleRecord(); };
            return b;
        }

        void ToggleRecord()
        {
            if (_recording) { StopRecording(); return; }
            if (_playing) PausePlayback();

            _takeStartBeat = _playBeat;
            if (!_recorder.Start())
            {
                Toast("MICROPHONE", _recorder.Error != null ? _recorder.Error : "No audio input device found.");
                return;
            }

            _recording = true;
            _takeSamples = null;
            _takeEndBeat = _takeStartBeat;
            _arr.TakeStart = _takeStartBeat;
            _arr.TakeEnd = _takeStartBeat;
            _arr.TakeEnv = new float[0];
            _arr.TakeLane = Math.Max(0, Project.Tracks.FindIndex(t => t.Id == CurrentTrack));
            _arr.InvalidateVisual();

            _recordBtn.Background = T.SA("#ff5a72", 150);
            _recInfo.Text = "Recording microphone input…";
            _recTimer.Text = "0.0 s";
            _engineStatus.Text = "RECORDING";
            Toast("RECORDING", "Sing now. Press R or the red button to stop.");
        }

        void StopRecording()
        {
            _recorder.Stop();
            _recording = false;
            _recordBtn.Background = T.SA("#a83248", 80);
            _recordDot.Opacity = 1;
            _recMeterFill.Width = 2;

            _takeSamples = _recorder.Snapshot();
            double secs = _takeSamples.Length / (double)Recorder.SampleRate;
            _takeEndBeat = _takeStartBeat + secs * Project.Bpm / 60.0;
            _recorder.SaveTake();
            _arr.TakeEnd = _takeEndBeat;
            _arr.TakeEnv = EnvelopeOf(_takeSamples, 220);
            _arr.InvalidateVisual();

            _recInfo.Text = "Take · " + secs.ToString("0.0") + " s · " + Recorder.SampleRate + " Hz";
            _recTimer.Text = secs.ToString("0.0") + " s";
            _engineStatus.Text = "ENGINE READY";

            if (secs < 0.4) Toast("TAKE TOO SHORT", "Record at least half a second of singing.");
            else Toast("TAKE RECORDED", secs.ToString("0.0") + " s captured · convert it to notes when ready.");
        }

        void UpdateRecordingUi()
        {
            double secs = _recorder.Seconds;
            _takeEndBeat = _takeStartBeat + secs * Project.Bpm / 60.0;
            _recTimer.Text = secs.ToString("0.0") + " s";
            _recordDot.Opacity = 0.45 + 0.55 * Math.Abs(Math.Sin(secs * 5));

            FrameworkElement host = _recMeterFill.Parent as FrameworkElement;
            double pw = host != null ? host.ActualWidth : 280;
            _recMeterFill.Width = Math.Max(2, Math.Min(1, _recorder.LastRms * 7) * pw);

            _recTick++;
            if (_recTick % 6 == 0)
            {
                _arr.TakeEnd = _takeEndBeat;
                _arr.TakeEnv = EnvelopeOf(_recorder.Snapshot(), 220);
                _arr.InvalidateVisual();
            }
        }

        void ConvertTake()
        {
            if (_takeSamples == null || _takeSamples.Length < Recorder.SampleRate / 4)
            {
                Toast("NO TAKE", "Record a vocal phrase first.");
                return;
            }
            _engineStatus.Text = "TRANSCRIBING…";
            List<Note> found = PitchTracker.ToNotes(_takeSamples, Recorder.SampleRate, Project.Bpm, _takeStartBeat, CurrentTrack);
            if (found.Count == 0)
            {
                _engineStatus.Text = "ENGINE READY";
                Toast("NO PITCH FOUND", "The take had no steady pitched material to transcribe.");
                return;
            }

            double from = _takeStartBeat;
            double to = _takeEndBeat;
            Project.Notes.RemoveAll(n => n.Track == CurrentTrack && n.Start < to && n.Start + n.Dur > from);
            Project.Notes.AddRange(found);
            Project.Notes.Sort((a, b) => a.Start.CompareTo(b.Start));

            _pr.InvalidateVisual();
            _arr.InvalidateVisual();
            _needRender = true;
            SelectNote(found[0]);
            _engineStatus.Text = "ENGINE READY";
            Toast("MIDI CONVERTED", found.Count + " notes extracted from " + (_takeEndBeat - _takeStartBeat).ToString("0.0") + " beats of audio.");
        }

        void ClearTake()
        {
            _takeSamples = null;
            _arr.TakeEnv = new float[0];
            _arr.TakeEnd = _arr.TakeStart;
            _arr.InvalidateVisual();
            _recInfo.Text = Recorder.InputDevices + " input device(s) · " + RateLabel(Recorder.SampleRate);
            _recTimer.Text = "0.0 s";
        }

        static float[] EnvelopeOf(float[] s, int buckets)
        {
            if (s == null || s.Length == 0) return new float[0];
            int n = Math.Min(buckets, Math.Max(2, s.Length));
            float[] e = new float[n];
            int per = Math.Max(1, s.Length / n);
            for (int b = 0; b < n; b++)
            {
                int from = b * per;
                int to = Math.Min(s.Length, from + per);
                float peak = 0;
                for (int i = from; i < to; i++)
                {
                    float a = Math.Abs(s[i]);
                    if (a > peak) peak = a;
                }
                e[b] = Math.Min(1f, peak * 3f);
            }
            return e;
        }

        // =========================================================
        //  transport
        // =========================================================
        public void SeekBeat(double beat)
        {
            beat = Math.Max(0, Math.Min(62, beat));
            _playBeat = beat;
            if (_playing)
            {
                CancelXfade();
                try { _media.Position = TimeSpan.FromSeconds(beat * Project.Spb); } catch { }
                _startBeat = beat;
                _sw.Restart();
            }
            RefreshPlayheads();
            UpdateClock(beat);
        }

        string OtherWav()
        {
            return _wav == _wavBase ? _wavAlt : _wavBase;
        }

        // EQ and style edits are auditioned while the take keeps playing.
        void MarkAudioDirty()
        {
            _needRender = true;
            if (_velLane != null) _velLane.InvalidateVisual();
            if (!_playing) return;
            if (_liveTimer == null)
            {
                _liveTimer = new DispatcherTimer();
                _liveTimer.Interval = TimeSpan.FromMilliseconds(220);
                _liveTimer.Tick += delegate
                {
                    _liveTimer.Stop();
                    StartLiveRender();
                };
            }
            _liveTimer.Stop();
            _liveTimer.Start();
        }

        void StartLiveRender()
        {
            if (_rendering || _liveRendering)
            {
                _livePending = true;
                return;
            }
            _liveRendering = true;
            _engineStatus.Text = "RE-RENDERING LIVE";
            string target = OtherWav();
            List<Note> snapshot = Project.Notes.Select(x => x.Copy()).ToList();
            double bpm = Project.Bpm, ten = _tension, bre = _breath, gen = _gender;
            bool met = _metro;
            Task.Factory.StartNew(delegate
            {
                VocalSynth.Render(target, bpm, ten, bre, gen, met, snapshot);
            }).ContinueWith(delegate
            {
                _liveRendering = false;
                _needRender = false;
                SwapLiveTake(target);
                UpdateTrackMeters();
                _engineStatus.Text = _playing ? "SYNTHESIZING" : "ENGINE READY";
                if (_livePending)
                {
                    _livePending = false;
                    StartLiveRender();
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        // Start the fresh take on a second player and crossfade the two, so the seam has
        // no gap and no click. The old take keeps playing until the new one is audible.
        void SwapLiveTake(string target)
        {
            if (!_playing)
            {
                _wav = target;
                return;
            }
            if (_xfade.IsEnabled)
            {
                // A fade is already running; render again once it lands.
                _livePending = true;
                return;
            }
            _xfadeWav = target;
            _xfadePending = true;
            try
            {
                _mediaNext.Volume = 0;
                _mediaNext.Open(new Uri(target));
                _mediaNext.Play();
            }
            catch
            {
                _xfadePending = false;
                _needRender = true;
            }
        }

        void NextTakeOpened(object sender, EventArgs e)
        {
            if (!_xfadePending) return;
            _xfadePending = false;
            if (!_playing)
            {
                try { _mediaNext.Stop(); } catch { }
                return;
            }

            // Line the new take up with where the old one is right now, then fade across.
            double beat = _startBeat + _sw.Elapsed.TotalSeconds / Project.Spb;
            if (beat >= 60) beat = 0;
            try { _mediaNext.Position = TimeSpan.FromSeconds(beat * Project.Spb); } catch { }

            if (_xfadeMs < XfadeIntervalMs)
            {
                // Instant setting: no ramp, hand over as soon as the new take is ready.
                if (_fadeStat != null) _fadeStat.Text = "instant";
                FinishXfade();
                return;
            }
            _xfadeClock.Restart();
            _xfade.Start();
        }

        void NextTakeFailed(object sender, ExceptionEventArgs e)
        {
            _xfadePending = false;
            _needRender = true;
            try { _mediaNext.Close(); } catch { }
        }

        // 48 ms equal-power crossfade: sin/cos ramps keep the summed level steady.
        // Volume pair for the crossfade at progress t. Equal-power holds the summed level
        // flat through the join; linear trades a mid-fade dip for a quicker-feeling handover.
        internal static void FadeGains(double t, bool linear, out double gainIn, out double gainOut)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            if (linear)
            {
                gainIn = t;
                gainOut = 1 - t;
            }
            else
            {
                gainIn = Math.Sin(t * Math.PI / 2);
                gainOut = Math.Cos(t * Math.PI / 2);
            }
        }

        // Fade length from the keyboard, so the Equalizer panel does not have to be open.
        // Meters read the level of the last render, so balances can be judged without exporting.
        // They show each track's own contribution after its level and pan, not the master EQ.
        void UpdateTrackMeters()
        {
            bool anySolo = VocalSynth.AnySolo();
            foreach (Track t in Project.Tracks)
            {
                Meter m;
                if (!_meters.TryGetValue(t.Id, out m)) continue;

                double peak = VocalSynth.TrackPeak(t.Id);
                double db = peak > 0.000001 ? 20 * Math.Log10(peak) : -60;
                m.Level = Math.Max(0, Math.Min(1, (db + 48) / 48));
                m.Dimmed = !VocalSynth.TrackAudible(t, anySolo);
                m.InvalidateVisual();

                TextBlock txt;
                if (_levelTexts.TryGetValue(t.Id, out txt))
                    txt.Text = peak > 0.000001 ? db.ToString("0") + " dB" : "silent";
            }
        }

        // Track levels and pans are stored per track id, so a mix reopens as it was left.
        void ApplySavedMix()
        {
            foreach (Track t in Project.Tracks)
            {
                t.GainDb = AppSettings.MixGainOf(t.Id);
                t.Pan = AppSettings.MixPanOf(t.Id);
                t.Muted = AppSettings.MixMutedOf(t.Id);
                t.Solo = AppSettings.MixSoloOf(t.Id);
            }
        }

        void PersistMix()
        {
            AppSettings.MixGain.Clear();
            AppSettings.MixPan.Clear();
            AppSettings.MixMuted.Clear();
            AppSettings.MixSolo.Clear();
            foreach (Track t in Project.Tracks)
            {
                AppSettings.MixGain[t.Id] = t.GainDb;
                AppSettings.MixPan[t.Id] = t.Pan;
                AppSettings.MixMuted[t.Id] = t.Muted;
                AppSettings.MixSolo[t.Id] = t.Solo;
            }
            AppSettings.Save();
        }

        // The fade settings are remembered, so a session reopens with the glide already chosen.
        void PersistFade()
        {
            AppSettings.FadeMs = _xfadeMs;
            AppSettings.FadeLinear = _xfadeLinear;
            AppSettings.Save();
        }

        void NudgeFade(int delta)
        {
            double next = Math.Round(Math.Max(0, Math.Min(300, _xfadeMs + delta)));
            if (Math.Abs(next - _xfadeMs) < 0.5)
            {
                Toast("FADE LENGTH", (next <= 0 ? "Instant" : ((int)next + " ms")) + " — already at the limit.");
                return;
            }
            _xfadeMs = next;
            if (_xfadeVal != null) _xfadeVal.Text = ((int)_xfadeMs).ToString();
            if (_xfadeFader != null)
            {
                _xfadeFader.Value = _xfadeMs;
                _xfadeFader.InvalidateVisual();
            }
            PersistFade();
            Toast("FADE LENGTH", (_xfadeMs <= 0 ? "Instant handover" : ((int)_xfadeMs + " ms glide")) +
                " · " + (_xfadeLinear ? "linear" : "equal-power"));
        }

        void ToggleFadeCurve()
        {
            _xfadeLinear = !_xfadeLinear;
            if (_xfadeCurveChip != null)
            {
                TextBlock label = _xfadeCurveChip.Child as TextBlock;
                if (label != null) label.Text = _xfadeLinear ? "FADE LINEAR" : "FADE EQUAL-POWER";
            }
            PersistFade();
            Toast("FADE CURVE", _xfadeLinear
                ? "Linear: the level dips mid-fade, so the handover feels quicker."
                : "Equal-power: the summed level stays flat through the join.");
        }

        void XfadeTick(object sender, EventArgs e)
        {
            // Ramp on the clock, not on tick count: windows gives roughly one tick per
            // system quantum, so counting ticks would stretch every fade to twice its length.
            double t = _xfadeClock.Elapsed.TotalMilliseconds / Math.Max(1.0, _xfadeMs);
            if (t > 1) t = 1;

            double gainIn, gainOut;
            FadeGains(t, _xfadeLinear, out gainIn, out gainOut);
            try
            {
                _mediaNext.Volume = MasterVolume * gainIn;
                _media.Volume = MasterVolume * gainOut;
            }
            catch { }

            if (t < 1) return;

            // Record what the handover actually took, next to what it was asked to take.
            double measured = _xfadeClock.Elapsed.TotalMilliseconds;
            FinishXfade();
            if (_fadeStat != null)
                _fadeStat.Text = ((int)Math.Round(measured)) + " / " + ((int)Math.Round(_xfadeMs)) + " ms";
        }

        // The new take is up: close the old player, make the new one the transport's player.
        void FinishXfade()
        {
            _xfade.Stop();
            try { _media.Close(); } catch { }
            MediaPlayer spent = _media;
            _media = _mediaNext;
            _mediaNext = spent;
            _media.Volume = MasterVolume;
            _wav = _xfadeWav;
            _engineStatus.Text = _playing ? "SYNTHESIZING" : "ENGINE READY";

            if (_livePending)
            {
                _livePending = false;
                StartLiveRender();
            }
        }

        void CancelXfade()
        {
            if (_xfade != null) _xfade.Stop();
            _xfadePending = false;
            _xfadeClock.Reset();
            if (_mediaNext != null)
            {
                try { _mediaNext.Stop(); } catch { }
                _mediaNext.Volume = 0;
            }
            if (_media != null) _media.Volume = MasterVolume;
        }

        void Play()
        {
            if (_rendering) return;
            if (_playBeat >= 60) _playBeat = 0;

            if (_needRender || !File.Exists(_wav))
            {
                _rendering = true;
                _engineStatus.Text = "RENDERING TAKE…";
                List<Note> snapshot = Project.Notes.Select(x => x.Copy()).ToList();
                double bpm = Project.Bpm, ten = _tension, bre = _breath, gen = _gender;
                bool met = _metro;
                string target = OtherWav();
                Task.Factory.StartNew(delegate
                {
                    VocalSynth.Render(target, bpm, ten, bre, gen, met, snapshot);
                }).ContinueWith(delegate
                {
                    _rendering = false;
                    _wav = target;
                    _needRender = false;
                    StartMedia();
                    UpdateTrackMeters();
                }, TaskScheduler.FromCurrentSynchronizationContext());
                return;
            }
            StartMedia();
        }

        void StartMedia()
        {
            CancelXfade();
            try
            {
                _media.Close();
                _media.Open(new Uri(_wav));
                _media.Play();
            }
            catch
            {
                Toast("AUDIO", "Could not open the rendered take.");
                return;
            }
            _startBeat = _playBeat;
            _sw.Restart();
            _playing = true;
            _playBorder.Child = PlayIcon(true);
            _engineStatus.Text = "SYNTHESIZING";
        }

        void PausePlayback()
        {
            _playing = false;
            CancelXfade();
            try { _media.Pause(); } catch { }
            _playBorder.Child = PlayIcon(false);
            _engineStatus.Text = "ENGINE READY";
        }

        void StopPlayback()
        {
            _playing = false;
            CancelXfade();
            try { _media.Stop(); } catch { }
            _playBorder.Child = PlayIcon(false);
            _engineStatus.Text = "ENGINE READY";
            _playBeat = 0;
            RefreshPlayheads();
            UpdateClock(0);
        }

        // Transport cue for background work: a sweeping highlight plus a matching status label.
        void UpdateRenderIndicator()
        {
            if (_shimmerHost == null) return;

            bool live = _liveRendering;
            bool fading = _xfade != null && _xfade.IsEnabled;
            if (!live && !fading && !_rendering)
            {
                if (_shimmerHost.Visibility != Visibility.Collapsed)
                {
                    _shimmerHost.Visibility = Visibility.Collapsed;
                    _shimmerPhase = 0;
                }

                // Hand the label back rather than leaving a stale "working" message.
                string t = _engineStatus.Text;
                if (t == "RE-RENDERING · LIVE" || t == "FADING IN" || t == "RENDERING TAKE")
                    _engineStatus.Text = _playing ? "SYNTHESIZING" : "ENGINE READY";
                return;
            }

            if (_shimmerHost.Visibility != Visibility.Visible)
            {
                _shimmerHost.Visibility = Visibility.Visible;
                _shimmerPhase = 0;
            }

            _shimmerPhase += 0.033 / 0.7;
            if (_shimmerPhase > 1) _shimmerPhase -= 1;
            _shimmerShift.X = -26 + 90 * _shimmerPhase;
            _shimmerFill.Opacity = 0.7 + 0.3 * Math.Sin(_shimmerPhase * 2 * Math.PI);

            string label = live ? "RE-RENDERING · LIVE" : (fading ? "FADING IN" : "RENDERING TAKE");
            if (_engineStatus.Text != label) _engineStatus.Text = label;
        }

        void Tick(object sender, EventArgs e)
        {
            if (_recording)
            {
                _recorder.Poll();
                UpdateRecordingUi();
            }
            UpdateRenderIndicator();
            if (!_playing) return;
            double beat = _startBeat + _sw.Elapsed.TotalSeconds / Project.Spb;

            if (_loop && beat >= 32)
            {
                try { _media.Position = TimeSpan.Zero; } catch { }
                _startBeat = 0;
                _sw.Restart();
                _playBeat = 0;
                RefreshPlayheads();
                UpdateClock(0);
                return;
            }
            if (beat >= 60)
            {
                StopPlayback();
                return;
            }
            _playBeat = beat;
            RefreshPlayheads();
            UpdateClock(beat);
            Follow(beat);

            int pct = _playing ? 48 + (int)(_sw.ElapsedMilliseconds / 90 % 26) : 22;
            if (pct > 96) pct = 96;
            _engineMeterFill.Width = 74 * pct / 100.0;
        }

        void RefreshPlayheads()
        {
            if (_arr != null) { _arr.PlayheadBeat = _playBeat; _arr.InvalidateVisual(); }
            if (_pr != null) { _pr.PlayheadBeat = _playBeat; _pr.InvalidateVisual(); }
        }

        void Follow(double beat)
        {
            if (_arrScroll == null) return;
            double x = beat * _ppb;
            double view = _arrScroll.ViewportWidth;
            double off = _arrScroll.HorizontalOffset;
            if (view <= 0) return;
            if (x < off + 40 || x > off + view - 90)
                _arrScroll.ScrollToHorizontalOffset(Math.Max(0, x - view * 0.35));
        }

        void UpdateClock(double beat)
        {
            double secs = beat * Project.Spb;
            int mm = (int)(secs / 60);
            int ss = (int)(secs % 60);
            int ms = (int)((secs - Math.Floor(secs)) * 1000);
            if (_clock != null)
                _clock.Text = mm.ToString("00") + ":" + ss.ToString("00") + "." + ms.ToString("000");
            if (_clockSub != null)
            {
                int bar = (int)(beat / 4) + 1;
                int b = (int)(beat % 4) + 1;
                int tick = (int)((beat - Math.Floor(beat)) * 960);
                _clockSub.Text = "BAR " + bar + " · BEAT " + b + " · " + tick.ToString("000");
            }
        }

        void SetZoom(int dir)
        {
            double[] z = { 22, 30, 38, 46, 58, 72, 90 };
            int i = Array.IndexOf(z, _ppb);
            if (i < 0) i = 3;
            i = Math.Max(0, Math.Min(z.Length - 1, i + dir));
            if (z[i] == _ppb) return;
            _ppb = z[i];
            _arr.PpB = _ppb;
            _arr.Width = Project.TotalBeats * _ppb;
            _arr.InvalidateVisual();
            _pr.PpB = _ppb;
            _pr.Width = Project.TotalBeats * _ppb;
            _pr.InvalidateVisual();
            _param.PpB = _ppb;
            _param.Width = Project.TotalBeats * _ppb;
            _param.InvalidateVisual();
            SyncParam();
            RefreshPlayheads();
        }

        void SyncParam()
        {
            if (_param == null || _prScroll == null) return;
            _param.Offset = _prScroll.HorizontalOffset;
            _param.InvalidateVisual();
        }

        // =========================================================
        //  misc
        // =========================================================
        // Read from the engine so the transport cannot advertise a rate the renderer never writes.
        static string SampleRateLabel()
        {
            return RateLabel(VocalSynth.Sr);
        }

        static string RateLabel(int sr)
        {
            string frac = sr % 1000 == 0 ? "" : "." + (sr % 1000 / 100);
            return (sr / 1000) + frac + " kHz";
        }

        UIElement BuildMixPanel()
        {
            StackPanel root = new StackPanel();
            root.Children.Add(T.PanelHead("Mix Presets", "LEVELS · PAN", false, null));

            StackPanel body = new StackPanel();
            body.Margin = new Thickness(11, 9, 11, 10);

            Grid saver = new Grid();
            saver.ColumnDefinitions.Add(ColStar());
            saver.ColumnDefinitions.Add(ColFix(58));
            saver.ColumnDefinitions.Add(ColFix(60));
            _mixName = new TextBox();
            _mixName.Height = 26;
            _mixName.FontSize = 10.5;
            _mixName.Margin = new Thickness(0, 0, 6, 0);
            _mixName.Padding = new Thickness(7, 3, 7, 3);
            _mixName.Background = T.SA("#061427", 170);
            _mixName.Foreground = T.S(T.C("#dceeff"));
            _mixName.CaretBrush = T.S(T.C("#8ce0ff"));
            _mixName.BorderBrush = T.SA("#82b9ff", 40);
            _mixName.BorderThickness = new Thickness(1);
            saver.Children.Add(_mixName);

            Border save = T.Btn("SAVE", delegate { SaveMixPreset(); }, false);
            save.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(save, 1);
            saver.Children.Add(save);

            Border reset = T.Btn("RESET", delegate { ResetMix(); }, false);
            reset.VerticalAlignment = VerticalAlignment.Center;
            reset.Margin = new Thickness(6, 0, 0, 0);
            reset.ToolTip = "Every track back to centre and unity, mute and solo cleared";
            Grid.SetColumn(reset, 2);
            saver.Children.Add(reset);
            body.Children.Add(saver);

            _mixList = new StackPanel();
            _mixList.Margin = new Thickness(0, 8, 0, 0);
            body.Children.Add(_mixList);

            root.Children.Add(body);
            RenderMixPresets();
            return T.Glass(14, root, new Thickness(0), true);
        }

        void RenderMixPresets()
        {
            if (_mixList == null) return;
            _mixList.Children.Clear();

            List<string> names = MixStore.Names();
            if (names.Count == 0)
            {
                _mixList.Children.Add(T.Txt("No saved mixes yet — set the levels and pans, then SAVE.", 8.3, T.C("#5f7897")));
                return;
            }

            foreach (string key in names)
            {
                string name = key;
                Grid row = new Grid();
                row.Margin = new Thickness(0, 0, 0, 4);
                row.ColumnDefinitions.Add(ColStar());
                row.ColumnDefinitions.Add(ColFix(46));
                row.ColumnDefinitions.Add(ColFix(24));

                StackPanel info = new StackPanel();
                info.Children.Add(T.Txt(name, 9, T.C("#dceeff"), FontWeights.SemiBold));
                info.Children.Add(T.Txt(MixSummary(name), 8, T.C("#7e9ab9")));
                row.Children.Add(info);

                Border use = T.Btn("USE", delegate { ApplyMixPreset(name); }, false);
                use.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(use, 1);
                row.Children.Add(use);

                Border del = MiniBtn("✕", false, delegate { MixStore.Delete(name); RenderMixPresets(); });
                del.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(del, 2);
                row.Children.Add(del);
                _mixList.Children.Add(row);
            }
        }

        static string MixSummary(string name)
        {
            MixPreset p = MixStore.Load(name);
            if (p == null) return "unreadable";

            string line = "";
            foreach (Track t in Project.Tracks)
            {
                double g, pan;
                p.Gain.TryGetValue(t.Id, out g);
                p.Pan.TryGetValue(t.Id, out pan);
                if (line.Length > 0) line += " · ";
                line += t.Name.Split(' ')[0] + " " + (g > 0 ? "+" : "") + g.ToString("0.#") +
                        (Math.Abs(pan) > 0.01 ? " " + (pan < 0 ? "L" : "R") + Math.Abs((int)Math.Round(pan * 100)) : "");
            }
            return line.Length == 0 ? "no tracks" : line;
        }

        // Back to a neutral mix in one click: unity gain, centred, nothing muted or soloed.
        void ResetMix()
        {
            foreach (Track t in Project.Tracks)
            {
                t.GainDb = 0;
                t.Pan = 0;
                t.Muted = false;
                t.Solo = false;
            }

            BuildTrackHeads();
            SyncTrackMix();
            PersistMix();
            MarkAudioDirty();
            UpdateTrackMeters();
            Toast("MIX RESET", "Every track back to centre and unity, mute and solo cleared.");
        }

        void SaveMixPreset()
        {
            string name = _mixName != null ? _mixName.Text.Trim() : "";
            if (name.Length == 0) name = "Mix " + DateTime.Now.ToString("HHmm");

            if (!MixStore.Save(name, MixStore.Capture()))
            {
                Toast("MIX", "Could not save " + name + ".");
                return;
            }

            if (_mixName != null) _mixName.Text = "";
            RenderMixPresets();
            Toast("MIX SAVED", name + " · " + Project.Tracks.Count + " tracks kept.");
        }

        void ApplyMixPreset(string name)
        {
            MixPreset p = MixStore.Load(name);
            if (p == null)
            {
                Toast("MIX", "Could not read " + name + ".");
                return;
            }

            MixStore.Apply(p);
            BuildTrackHeads();
            SyncTrackMix();
            PersistMix();
            MarkAudioDirty();
            UpdateTrackMeters();
            Toast("MIX APPLIED", name + " · levels, pans, mute and solo restored.");
        }

        // The EQ chip's own action, reachable from the keyboard as well as by click.
        void ToggleEqBypass()
        {
            RenderStyle.Eq.Bypass = !RenderStyle.Eq.Bypass;
            if (_eqChip != null) T.SetChipState(_eqChip, !RenderStyle.Eq.Bypass);
            if (_eqCurve != null) _eqCurve.InvalidateVisual();
            MarkAudioDirty();
            Toast("EQUALIZER " + (RenderStyle.Eq.Bypass ? "BYPASSED" : "ON"), RenderStyle.Eq.Bypass
                ? "The take plays without the EQ curve."
                : "The three bands are back in the render.");
        }

        // Steps through the saved style presets in the order the panel lists them.
        // Number keys jump straight to a preset, so a specific style is one key away instead of
        // several presses of S. Out-of-range numbers say so rather than doing nothing.
        void LoadStyleByIndex(int index)
        {
            List<string> names = StyleStore.Names();
            if (index < 0 || index >= names.Count)
            {
                Toast("STYLE PRESET", "No preset " + (index + 1) + " yet — save one first.");
                return;
            }

            ApplyStylePreset(names[index]);
            Toast("STYLE PRESET", names[index] + " · " + (index + 1) + " of " + names.Count);
        }

        void CycleStylePreset()
        {
            List<string> names = StyleStore.Names();
            if (names.Count == 0)
            {
                Toast("STYLE PRESETS", "Nothing saved yet — learn a sample and save it as a preset first.");
                return;
            }

            int next = 0;
            string current = _styleName != null ? _styleName.Text : "";
            int at = names.FindIndex(x => string.Equals(x, current, StringComparison.OrdinalIgnoreCase));
            if (at >= 0) next = (at + 1) % names.Count;

            ApplyStylePreset(names[next]);
            Toast("STYLE PRESET", names[next] + " · " + (next + 1) + " of " + names.Count);
        }

        // Point a first-time user at the ? chip: it is the only way in that is visible on screen,
        // and F1 alone is not discoverable. Shown once per installation, then never again.
        void ShowHelpHintOnce()
        {
            if (AppSettings.HelpHintSeen || _helpChip == null || _floatLayer == null) return;
            AppSettings.HelpHintSeen = true;
            AppSettings.Save();
            ShowHelpHint();
        }

        void ShowHelpHint()
        {
            if (_helpHint != null || _helpChip == null || _floatLayer == null) return;

            Border b = new Border();
            b.CornerRadius = new CornerRadius(10);
            b.Padding = new Thickness(11, 7, 11, 7);
            b.Background = new LinearGradientBrush(Color.FromArgb(242, 24, 60, 112), Color.FromArgb(246, 9, 24, 48), 90);
            b.BorderBrush = T.SA("#8ccdff", 120);
            b.BorderThickness = new Thickness(1);
            b.Effect = T.Glow(T.C("#4aa8ff"), 22, 0.5);
            b.Cursor = Cursors.Hand;
            b.ToolTip = "Open the shortcut legend";

            StackPanel sp = new StackPanel();
            TextBlock h = T.Txt("NEW HERE?", 7.6, T.C("#7fd3ff"), FontWeights.Bold);
            h.Margin = new Thickness(0, 0, 0, 2);
            sp.Children.Add(h);
            TextBlock m = T.Txt("Press ? or click the ? chip for every shortcut.", 9.6, T.C("#e2f3ff"));
            m.TextWrapping = TextWrapping.Wrap;
            m.MaxWidth = 190;
            sp.Children.Add(m);
            b.Child = sp;
            b.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                DismissHelpHint();
                if (_helpLayer == null || _helpLayer.Visibility != Visibility.Visible) ToggleHelp();
            };

            // The float layer is a sibling of the chip's subtree, not an ancestor, so measure
            // against the root they share. A layout surprise must never crash the launch.
            double chipX = 8, chipY = 56;
            try
            {
                Point p = _helpChip.TransformToAncestor(_root).Transform(new Point(0, 0));
                chipX = p.X;
                chipY = p.Y + _helpChip.ActualHeight;
            }
            catch { }

            Canvas.SetLeft(b, Math.Max(8, chipX - 96));
            Canvas.SetTop(b, chipY + 8);
            _floatLayer.Children.Add(b);
            _helpHint = b;

            _hintTimer = new DispatcherTimer();
            _hintTimer.Interval = TimeSpan.FromMilliseconds(6500);
            _hintTimer.Tick += delegate
            {
                _hintTimer.Stop();
                _hintTimer = null;
                System.Windows.Media.Animation.DoubleAnimation fade =
                    new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(420));
                fade.Completed += delegate { DismissHelpHint(); };
                b.BeginAnimation(OpacityProperty, fade);
            };
            _hintTimer.Start();
        }

        void DismissHelpHint()
        {
            if (_hintTimer != null)
            {
                _hintTimer.Stop();
                _hintTimer = null;
            }
            if (_helpHint == null) return;
            if (_floatLayer != null) _floatLayer.Children.Remove(_helpHint);
            _helpHint = null;
        }

        void ToggleHelp()
        {
            if (_helpLayer == null) return;
            DismissHelpHint();
            BuildHelp();
            _helpLayer.Visibility = _helpLayer.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }

        void BuildHelp()
        {
            if (_helpBuilt) return;
            _helpBuilt = true;

            _helpLayer.Background = new SolidColorBrush(Color.FromArgb(205, 5, 14, 30));
            _helpLayer.MouseLeftButtonUp += delegate { ToggleHelp(); };

            Border card = new Border();
            card.CornerRadius = new CornerRadius(16);
            card.Padding = new Thickness(26, 20, 26, 18);
            card.MaxWidth = 640;
            // Stretch up to the cap rather than centring at a fixed width: on a narrow viewport the
            // card shrinks and its rows wrap, instead of running off the sides of the screen.
            card.HorizontalAlignment = HorizontalAlignment.Stretch;
            card.Margin = new Thickness(16);
            card.VerticalAlignment = VerticalAlignment.Center;
            card.Background = new LinearGradientBrush(Color.FromArgb(246, 22, 56, 104), Color.FromArgb(250, 9, 22, 44), 90);
            card.BorderBrush = T.SA("#8ccdff", 110);
            card.BorderThickness = new Thickness(1);
            card.Effect = T.Glow(T.C("#4aa8ff"), 34, 0.55);
            card.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e) { e.Handled = true; };

            StackPanel body = new StackPanel();
            body.Children.Add(T.Txt("KEYBOARD SHORTCUTS", 14, T.C("#f2fbff"), FontWeights.Bold));
            TextBlock sub = T.Txt("F1 or ? opens this list · Esc closes it", 9.8, T.C("#a8c6e4"));
            sub.Margin = new Thickness(0, 5, 0, 10);
            body.Children.Add(sub);

            Border rule = new Border();
            rule.Height = 1;
            rule.Margin = new Thickness(0, 0, 0, 10);
            rule.Background = T.SA("#8ccdff", 46);
            body.Children.Add(rule);

            body.Children.Add(HelpRow("Space", "Play or pause the song"));
            body.Children.Add(HelpRow("Home", "Jump back to the start"));
            body.Children.Add(HelpRow("[  /  ]", "Shorter or longer live crossfade, 8 ms a press"));
            body.Children.Add(HelpRow("Shift + [ / ]", "Bigger fade moves — 48 ms a press"));
            body.Children.Add(HelpRow("R", "Start or stop recording"));
            body.Children.Add(HelpRow("E", "Bypass or re-enable the equalizer"));
            body.Children.Add(HelpRow("S  /  1–9", "Step to the next style preset, or load one by number"));
            body.Children.Add(HelpRow("Delete", "Remove the selected note"));
            body.Children.Add(HelpRow("F1  or  ?", "Show or hide this list"));
            body.Children.Add(HelpRow("Esc", "Close this list"));

            TextBlock mouseHead = T.Txt("MOUSE & GESTURES", 8.6, T.C("#8fb8dd"), FontWeights.Bold);
            mouseHead.Margin = new Thickness(0, 8, 0, 3);
            body.Children.Add(mouseHead);

            Border mouseRule = new Border();
            mouseRule.Height = 1;
            mouseRule.Margin = new Thickness(0, 0, 0, 4);
            mouseRule.Background = T.SA("#8ccdff", 46);
            body.Children.Add(mouseRule);

            body.Children.Add(HelpRow("Drag note", "Move it in time and pitch, snapped to the grid", 2));
            body.Children.Add(HelpRow("Drag note edge", "Stretch or shorten that note", 2));
            body.Children.Add(HelpRow("Drag velocity bar", "Redraw a note's dynamics in the velocity lane", 2));
            body.Children.Add(HelpRow("Drag any fader", "Set track level, pan, fade length or export trim", 2));
            body.Children.Add(HelpRow("Drag fade header", "Move the floating fade card around the workspace", 2));
            body.Children.Add(HelpRow("Drag EQ node", "Boost or cut the low, mid and high bands", 2));

            TextBlock foot = T.Txt("Fade keys work while a take plays — the FADE readout in the status bar reports the measured handover against the setting.", 10, T.C("#93aecb"));
            foot.TextWrapping = TextWrapping.Wrap;
            foot.Margin = new Thickness(0, 10, 0, 8);
            body.Children.Add(foot);

            // The list scrolls when the viewport is short (a laptop at 150%), but CLOSE sits
            // outside the scroller so the card can never hide its own way out.
            ScrollViewer scroller = new ScrollViewer();
            scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scroller.Content = body;

            Border close = T.Btn("CLOSE", delegate { ToggleHelp(); }, false);
            close.HorizontalAlignment = HorizontalAlignment.Right;
            close.Margin = new Thickness(0, 12, 0, 0);

            // A Grid, not a StackPanel: a StackPanel measures children at infinite height, which
            // would leave the scroller with nothing to scroll and push CLOSE outside the card.
            Grid cardBody = new Grid();
            cardBody.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            cardBody.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cardBody.Children.Add(scroller);
            Grid.SetRow(scroller, 0);
            cardBody.Children.Add(close);
            Grid.SetRow(close, 1);
            card.Child = cardBody;

            _helpLayer.SizeChanged += delegate
            {
                card.MaxHeight = Math.Max(240, _helpLayer.ActualHeight - 32);
            };
            card.MaxHeight = Math.Max(240, _helpLayer.ActualHeight - 32);

            _helpLayer.Children.Add(card);
        }

        static UIElement HelpRow(string keys, string what)
        {
            return HelpRow(keys, what, 4);
        }

        // Gesture rows sit a little tighter than the key rows, so the extra section still fits
        // inside the card at the minimum window size.
        static UIElement HelpRow(string keys, string what, double gap)
        {
            Grid g = new Grid();
            g.Margin = new Thickness(0, gap, 0, gap);
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(126) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border chip = new Border();
            chip.CornerRadius = new CornerRadius(5);
            chip.Padding = new Thickness(8, 4, 8, 4);
            chip.Background = T.SA("#1a4b86", 150);
            chip.BorderBrush = T.SA("#8ccdff", 70);
            chip.BorderThickness = new Thickness(1);
            chip.HorizontalAlignment = HorizontalAlignment.Left;
            chip.Child = T.Txt(keys, 9.5, T.C("#dff4ff"), FontWeights.SemiBold);
            Grid.SetColumn(chip, 0);
            g.Children.Add(chip);

            TextBlock d = T.Txt(what, 11, T.C("#e6f4ff"));
            d.TextWrapping = TextWrapping.Wrap;
            d.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(d, 1);
            g.Children.Add(d);
            return g;
        }

        void Toast(string title, string message)
        {
            Border b = new Border();
            b.CornerRadius = new CornerRadius(10);
            b.Padding = new Thickness(12, 8, 12, 8);
            b.MaxWidth = 320;
            b.Margin = new Thickness(0, 8, 0, 0);
            b.Background = new LinearGradientBrush(Color.FromArgb(238, 26, 62, 116), Color.FromArgb(242, 10, 26, 52), 90);
            b.BorderBrush = T.SA("#8ccdff", 90);
            b.BorderThickness = new Thickness(1);
            b.Effect = T.Glow(T.C("#4aa8ff"), 24, 0.5);
            StackPanel sp = new StackPanel();
            TextBlock h = T.Txt(title, 8, T.C("#7fd3ff"), FontWeights.Bold);
            h.Margin = new Thickness(0, 0, 0, 2);
            sp.Children.Add(h);
            TextBlock m = T.Txt(message, 10.5, T.C("#e2f3ff"));
            m.TextWrapping = TextWrapping.Wrap;
            sp.Children.Add(m);
            b.Child = sp;

            Canvas.SetRight(b, 18);
            Canvas.SetBottom(b, 48 + _toastLayer.Children.Count * 62);
            _toastLayer.Children.Add(b);

            DispatcherTimer t = new DispatcherTimer();
            t.Interval = TimeSpan.FromMilliseconds(2800);
            t.Tick += delegate
            {
                t.Stop();
                _toastLayer.Children.Remove(b);
            };
            t.Start();
        }

        void OnKey(object sender, KeyEventArgs e)
        {
            if (Keyboard.FocusedElement is TextBox) return;

            if (e.Key == Key.F1 || (e.Key == Key.OemQuestion && (Keyboard.Modifiers & ModifierKeys.Shift) != 0))
            {
                ToggleHelp();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape && _helpLayer != null && _helpLayer.Visibility == Visibility.Visible)
            {
                ToggleHelp();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Space)
            {
                e.Handled = true;
                if ((DateTime.Now - _lastSpace).TotalMilliseconds < 250) return;
                _lastSpace = DateTime.Now;
                if (_playing) PausePlayback(); else Play();
            }
            if (e.Key == Key.Home) { SeekBeat(0); e.Handled = true; }
            if (e.Key == Key.OemOpenBrackets) { NudgeFade((Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? -48 : -8); e.Handled = true; }
            if (e.Key == Key.OemCloseBrackets) { NudgeFade((Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 48 : 8); e.Handled = true; }
            if (e.Key == Key.R) { ToggleRecord(); e.Handled = true; }
            if (e.Key == Key.E) { ToggleEqBypass(); e.Handled = true; }
            if (e.Key == Key.S) { CycleStylePreset(); e.Handled = true; }
            if (e.Key >= Key.D1 && e.Key <= Key.D9) { LoadStyleByIndex((int)e.Key - (int)Key.D1); e.Handled = true; }
            if (e.Key >= Key.NumPad1 && e.Key <= Key.NumPad9) { LoadStyleByIndex((int)e.Key - (int)Key.NumPad1); e.Handled = true; }
            if (e.Key == Key.Delete && SelectedId >= 0)
            {
                Project.Notes.RemoveAll(n => n.Id == SelectedId);
                SelectedId = -1;
                SelectNote(null);
                _pr.InvalidateVisual();
                _needRender = true;
                e.Handled = true;
            }
        }
    }
}

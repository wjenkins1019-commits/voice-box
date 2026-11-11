using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace VoiceBox
{
    public static class Geo
    {
        public static readonly string[] Names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        public static string NoteName(int midi)
        {
            return Names[midi % 12] + (midi / 12 - 1).ToString(CultureInfo.InvariantCulture);
        }
    }

    public abstract class Surface : FrameworkElement
    {
        protected double Ppd
        {
            get { return VisualTreeHelper.GetDpi(this).PixelsPerDip; }
        }

        protected FormattedText FT(string s, double size, Brush b, FontWeight w, double maxW)
        {
            FormattedText f = new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(T.font, FontStyles.Normal, w, FontStretches.Normal), size, b, Ppd);
            if (maxW > 0)
            {
                f.MaxTextWidth = maxW;
                f.MaxLineCount = 1;
                f.Trimming = TextTrimming.CharacterEllipsis;
            }
            return f;
        }

        protected void DrawTxt(DrawingContext dc, string s, double x, double y, double size, Brush b, FontWeight w, double maxW)
        {
            if (string.IsNullOrEmpty(s)) return;
            dc.DrawText(FT(s, size, b, w, maxW), new Point(x, y));
        }

        protected static Pen PenOf(Brush b, double w)
        {
            Pen p = new Pen(b, w);
            p.Freeze();
            return p;
        }
    }

    // =========================================================
    //  ARRANGEMENT
    // =========================================================
    public class ArrangementView : Surface
    {
        public double PpB = 46;
        public double PlayheadBeat;
        public MainWindow Host;

        // recorded take overlay
        public double TakeStart;
        public double TakeEnd;
        public float[] TakeEnv = new float[0];
        public int TakeLane;

        public const double RulerH = 24;
        public const double LaneH = 58;

        public ArrangementView(MainWindow host)
        {
            Host = host;
            ClipToBounds = true;
            Cursor = Cursors.Hand;
            Height = RulerH + LaneH * Project.Tracks.Count;
            Width = Project.TotalBeats * PpB;
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = Width;
            double h = Height;

            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(50, 5, 15, 32)), null, new Rect(0, 0, w, h));

            // ruler
            LinearGradientBrush rb = new LinearGradientBrush();
            rb.StartPoint = new Point(0.5, 0);
            rb.EndPoint = new Point(0.5, 1);
            rb.GradientStops.Add(new GradientStop(Color.FromArgb(230, 26, 58, 108), 0));
            rb.GradientStops.Add(new GradientStop(Color.FromArgb(215, 14, 34, 66), 1));
            dc.DrawRectangle(rb, null, new Rect(0, 0, w, RulerH));
            dc.DrawLine(PenOf(new SolidColorBrush(Color.FromArgb(46, 130, 185, 255)), 1), new Point(0, RulerH), new Point(w, RulerH));

            int bars = Project.TotalBeats / Project.BeatsPerBar;
            Pen barPen = PenOf(new SolidColorBrush(Color.FromArgb(48, 150, 200, 255)), 1);
            Pen beatPen = PenOf(new SolidColorBrush(Color.FromArgb(22, 150, 200, 255)), 1);
            Pen barGrid = PenOf(new SolidColorBrush(Color.FromArgb(22, 150, 200, 255)), 1);
            Pen beatGrid = PenOf(new SolidColorBrush(Color.FromArgb(11, 150, 200, 255)), 1);

            for (int b = 0; b < bars; b++)
            {
                double x = b * Project.BeatsPerBar * PpB;
                dc.DrawLine(barPen, new Point(x, 0), new Point(x, RulerH));
                dc.DrawLine(barGrid, new Point(x, RulerH), new Point(x, h));
                DrawTxt(dc, (b + 1).ToString(CultureInfo.InvariantCulture), x + 5, 5, 9,
                    new SolidColorBrush(Color.FromArgb(220, 143, 178, 216)), FontWeights.SemiBold, 0);
            }
            for (int b = 0; b <= Project.TotalBeats; b++)
            {
                double x = b * PpB;
                dc.DrawLine(beatPen, new Point(x, RulerH - 5), new Point(x, RulerH));
                dc.DrawLine(beatGrid, new Point(x, RulerH), new Point(x, h));
            }

            // lanes
            for (int i = 0; i < Project.Tracks.Count; i++)
            {
                double y = RulerH + i * LaneH;
                Brush laneBg = i % 2 == 0
                    ? (Brush)new SolidColorBrush(Color.FromArgb(14, 120, 175, 255))
                    : new SolidColorBrush(Color.FromArgb(6, 120, 175, 255));
                dc.DrawRectangle(laneBg, null, new Rect(0, y, w, LaneH));
                dc.DrawLine(PenOf(new SolidColorBrush(Color.FromArgb(20, 130, 185, 255)), 1),
                    new Point(0, y + LaneH), new Point(w, y + LaneH));

                Track tr = Project.Tracks[i];
                Color baseColor = T.C(tr.Color);

                for (int c = 0; c < tr.Clips.Count; c++)
                {
                    Clip clip = tr.Clips[c];
                    double cx = clip.Start * PpB + 1;
                    double cw = clip.Dur * PpB - 3;
                    Rect r = new Rect(cx, y + 5, cw, LaneH - 11);
                    if (r.Width < 12) continue;

                    Color top = Color.FromArgb(235, baseColor.R, baseColor.G, baseColor.B);
                    Color bottom = Color.FromArgb(150, (byte)(baseColor.R * 0.55), (byte)(baseColor.G * 0.55), (byte)(baseColor.B * 0.55));
                    LinearGradientBrush fill = new LinearGradientBrush(top, bottom, 90);
                    Pen border = PenOf(new SolidColorBrush(Color.FromArgb(90, 220, 240, 255)), 1);
                    dc.DrawRoundedRectangle(fill, border, r, 6, 6);

                    StreamGeometry wg = Wave(c, (int)(tr.Id.Length + c), r.Width, r.Height);
                    dc.PushClip(new RectangleGeometry(r, 6, 6));
                    dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(70, 225, 245, 255)), null, wg);
                    dc.Pop();

                    dc.DrawLine(PenOf(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 1),
                        new Point(r.X + 1, r.Y + 15), new Point(r.Right - 1, r.Y + 15));

                    DrawTxt(dc, tr.Voice, r.X + 7, r.Y + 3, 9,
                        new SolidColorBrush(Color.FromArgb(240, 235, 248, 255)), FontWeights.SemiBold, r.Width - 14);

                    string gain = "-" + (3.8 + c * 0.4).ToString("0.0", CultureInfo.InvariantCulture) + " dB";
                    DrawTxt(dc, gain, r.X + 7, r.Bottom - 13, 7.8,
                        new SolidColorBrush(Color.FromArgb(200, 225, 244, 255)), FontWeights.SemiBold, r.Width - 14);
                }
            }

            // recorded take overlay
            if (TakeEnv != null && TakeEnv.Length > 1 && TakeEnd > TakeStart)
            {
                double tx = TakeStart * PpB;
                double tw = Math.Max(3, (TakeEnd - TakeStart) * PpB);
                double ty = RulerH + Math.Max(0, TakeLane) * LaneH;
                Rect tr = new Rect(tx, ty + 5, tw, LaneH - 11);
                dc.PushClip(new RectangleGeometry(tr, 6, 6));
                LinearGradientBrush tf = new LinearGradientBrush(
                    Color.FromArgb(200, 255, 110, 132), Color.FromArgb(130, 160, 40, 70), 90);
                dc.DrawRectangle(tf, null, tr);

                StreamGeometry wave = new StreamGeometry();
                int n = TakeEnv.Length;
                double mid = tr.Y + tr.Height * 0.6;
                double amp = tr.Height * 0.4;
                using (StreamGeometryContext wc = wave.Open())
                {
                    wc.BeginFigure(new Point(tr.X, mid), true, true);
                    for (int i = 0; i < n; i++)
                        wc.LineTo(new Point(tr.X + (double)i / (n - 1) * tr.Width, mid - TakeEnv[i] * amp), true, false);
                    for (int i = n - 1; i >= 0; i--)
                        wc.LineTo(new Point(tr.X + (double)i / (n - 1) * tr.Width, mid + TakeEnv[i] * amp * 0.7), true, false);
                }
                wave.Freeze();
                dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(125, 255, 235, 240)), null, wave);
                dc.Pop();

                dc.DrawRoundedRectangle(null, PenOf(new SolidColorBrush(Color.FromArgb(180, 255, 160, 175)), 1), tr, 6, 6);
                DrawTxt(dc, "TAKE", tr.X + 6, tr.Y + 3, 8,
                    new SolidColorBrush(Color.FromArgb(240, 255, 230, 236)), FontWeights.SemiBold,
                    Math.Max(10, tr.Width - 12));
            }

            // playhead
            double px = PlayheadBeat * PpB;
            if (px >= 0 && px <= w)
            {
                LinearGradientBrush ph = new LinearGradientBrush();
                ph.StartPoint = new Point(0.5, 0);
                ph.EndPoint = new Point(0.5, 1);
                ph.GradientStops.Add(new GradientStop(Color.FromArgb(245, 150, 235, 255), 0));
                ph.GradientStops.Add(new GradientStop(Color.FromArgb(120, 90, 170, 255), 1));
                dc.DrawRectangle(ph, null, new Rect(px - 0.5, 0, 1, h));

                StreamGeometry tri = new StreamGeometry();
                using (StreamGeometryContext c2 = tri.Open())
                {
                    c2.BeginFigure(new Point(px - 4.5, 0), true, true);
                    c2.LineTo(new Point(px + 4.5, 0), true, false);
                    c2.LineTo(new Point(px, 9), true, false);
                }
                tri.Freeze();
                dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(240, 182, 236, 255)), null, tri);
            }
        }

        static StreamGeometry Wave(int clipIndex, int seed, double w, double h)
        {
            Random r = new Random(seed * 7919 + clipIndex * 31 + 13);
            int n = Math.Max(28, (int)(w / 3));
            double[] up = new double[n];
            double energy = 0.35;
            for (int i = 0; i < n; i++)
            {
                energy += (r.NextDouble() - 0.5) * 0.28;
                if (energy < 0.08) energy = 0.08;
                if (energy > 1) energy = 1;
                double t = (double)i / n;
                double env = Math.Sin(Math.PI * Math.Pow(t, 0.72)) * (0.55 + 0.45 * energy);
                up[i] = (0.22 + r.NextDouble() * 0.78) * env;
            }
            double mid = h * 0.58;
            double amp = h * 0.4;
            StreamGeometry g = new StreamGeometry();
            using (StreamGeometryContext c = g.Open())
            {
                c.BeginFigure(new Point(0, mid), true, true);
                for (int i = 0; i < n; i++) c.LineTo(new Point((double)i / n * w, mid - up[i] * amp), true, false);
                for (int i = n - 1; i >= 0; i--) c.LineTo(new Point((double)i / n * w, mid + up[i] * (0.6 + (i % 5) * 0.06) * amp), true, false);
            }
            g.Freeze();
            return g;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            Point p = e.GetPosition(this);
            if (Host != null) Host.SeekBeat(p.X / PpB);
            e.Handled = true;
        }
    }

    // =========================================================
    //  PIANO ROLL
    // =========================================================
    public class PianoRollView : Surface
    {
        public double PpB = 46;
        public double RowH = 19;
        public double PlayheadBeat;
        public MainWindow Host;
        public string Track = "v1";

        Note _drag;
        bool _resize;
        Point _p0;
        double _s0, _d0;
        int _m0;

        public PianoRollView(MainWindow host)
        {
            Host = host;
            ClipToBounds = true;
            Height = Project.Rows * RowH;
            Width = Project.TotalBeats * PpB;
            Focusable = true;
        }

        public double NoteY(int midi)
        {
            return (Project.TopMidi - midi) * RowH;
        }

        public Rect NoteRect(Note n)
        {
            return new Rect(n.Start * PpB, NoteY(n.Midi) + 1, Math.Max(7, n.Dur * PpB - 1.5), RowH - 2.5);
        }

        // Scales a colour toward black so velocity shows up as brightness.
        static Color Shade(Color c, double k)
        {
            if (k >= 0.999) return c;
            return Color.FromArgb(c.A,
                (byte)Math.Round(c.R * k),
                (byte)Math.Round(c.G * k),
                (byte)Math.Round(c.B * k));
        }

        Note Hit(Point p)
        {
            List<Note> list = Project.VisibleNotes(Track);
            for (int i = list.Count - 1; i >= 0; i--)
                if (NoteRect(list[i]).Contains(p)) return list[i];
            return null;
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = Width;
            double h = Height;

            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(36, 4, 14, 30)), null, new Rect(0, 0, w, h));

            for (int m = Project.TopMidi; m >= Project.BottomMidi; m--)
            {
                int pc = m % 12;
                bool black = pc == 1 || pc == 3 || pc == 6 || pc == 8 || pc == 10;
                double y = NoteY(m);
                byte a = black ? (byte)42 : (byte)0;
                if (a > 0) dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(a, 2, 10, 24)), null, new Rect(0, y, w, RowH));
                Pen sep = PenOf(new SolidColorBrush(pc == 0
                    ? Color.FromArgb(48, 150, 205, 255)
                    : Color.FromArgb(16, 130, 185, 255)), 1);
                dc.DrawLine(sep, new Point(0, y + RowH), new Point(w, y + RowH));
            }

            // vertical grid
            Pen barPen = PenOf(new SolidColorBrush(Color.FromArgb(34, 150, 200, 255)), 1);
            Pen beatPen = PenOf(new SolidColorBrush(Color.FromArgb(15, 150, 200, 255)), 1);
            Pen subPen = PenOf(new SolidColorBrush(Color.FromArgb(7, 150, 200, 255)), 1);
            for (int b = 0; b <= Project.TotalBeats; b++)
            {
                double x = b * PpB;
                dc.DrawLine(b % 4 == 0 ? barPen : beatPen, new Point(x, 0), new Point(x, h));
            }
            for (double b = 0; b <= Project.TotalBeats; b += 0.25)
            {
                if (Math.Abs(b - Math.Round(b)) < 0.001) continue;
                double x = b * PpB;
                dc.DrawLine(subPen, new Point(x, 0), new Point(x, h));
            }

            List<Note> list = Project.VisibleNotes(Track);
            Brush noteTop = new SolidColorBrush(Color.FromArgb(255, 92, 192, 255));
            Brush noteBottom = new SolidColorBrush(Color.FromArgb(255, 29, 99, 196));
            Brush selTop = new SolidColorBrush(Color.FromArgb(255, 179, 242, 255));
            Brush selBottom = new SolidColorBrush(Color.FromArgb(255, 60, 155, 238));
            Pen notePen = PenOf(new SolidColorBrush(Color.FromArgb(150, 190, 235, 255)), 1);
            Pen selPen = PenOf(new SolidColorBrush(Color.FromArgb(255, 240, 252, 255)), 1.4);

            foreach (Note n in list)
            {
                Rect r = NoteRect(n);
                if (r.Right < 0 || r.Left > w) continue;
                bool sel = Host != null && Host.SelectedId == n.Id;
                // Shade by velocity: a loud note stays bright, a quiet one darkens towards the
                // grid, so the shape of the dynamics reads at a glance without opening the lane.
                double vel = Math.Max(0.0, Math.Min(1.0, n.Velocity));
                double shade = 0.34 + 0.66 * vel;
                Color fillTop = Shade(sel ? Color.FromArgb(255, 179, 242, 255) : Color.FromArgb(255, 92, 192, 255), shade);
                Color fillBottom = Shade(sel ? Color.FromArgb(255, 60, 155, 238) : Color.FromArgb(255, 29, 99, 196), shade);
                LinearGradientBrush fill = new LinearGradientBrush(fillTop, fillBottom, 90);
                if (sel)
                {
                    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(70, 150, 225, 255)), null,
                        new Rect(r.X - 2, r.Y - 2, r.Width + 4, r.Height + 4), 6, 6);
                }
                dc.DrawRoundedRectangle(fill, sel ? selPen : notePen, r, 4.5, 4.5);

                // Velocity as a bare number in the note's trailing corner, so an exact value can be
                // read without the lane. Only notes wide enough carry it, and the lyric label then
                // gives up that much width instead of running underneath it.
                double velTextW = 0;
                if (r.Width >= 30)
                {
                    string vs = ((int)Math.Round(vel * 100)).ToString(CultureInfo.InvariantCulture);
                    // A small dark badge carries the number, so it stays readable on both a bright
                    // loud note and a note shaded down to the grid instead of depending on the fill.
                    FormattedText vf = FT(vs, 7.5, new SolidColorBrush(Color.FromArgb(240, 226, 244, 255)), FontWeights.Bold, 0);
                    double top = r.Y + (r.Height - vf.Height) / 2;
                    Rect badge = new Rect(r.Right - vf.Width - 6, top - 0.5, vf.Width + 4, vf.Height + 1);
                    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(160, 4, 20, 36)), null, badge, 3, 3);
                    dc.DrawText(vf, new Point(badge.X + 2, top));
                    velTextW = badge.Width + 3;
                }

                if (r.Width >= 24)
                {
                    string txt = n.Word;
                    // The label dims with the note so a quiet note's text does not glow brighter than its body.
                    Brush txtBrush = new SolidColorBrush(sel
                        ? Color.FromArgb(255, 6, 32, 57)
                        : Color.FromArgb(255, (byte)Math.Round(150 + 92 * vel), (byte)Math.Round(200 + 51 * vel), 255));
                    DrawTxt(dc, txt, r.X + 4, r.Y + Math.Max(1, (r.Height - 12) / 2), 9.2, txtBrush, FontWeights.SemiBold,
                        Math.Max(8, r.Width - 8 - velTextW));
                }
            }

            // pitch curves
            Brush curve = new SolidColorBrush(Color.FromArgb(215, 214, 244, 255));
            Brush curveSel = new SolidColorBrush(Colors.White);
            foreach (Note n in list)
            {
                double x0 = n.Start * PpB;
                double x1 = (n.Start + n.Dur) * PpB;
                if (x1 < 0 || x0 > w) continue;
                double baseY = NoteY(n.Midi) + RowH / 2;
                double amp = Math.Max(0, Math.Min(1, n.Vib)) * Math.Min(RowH * 0.5, 5.5);
                int steps = Math.Max(8, (int)(n.Dur * 14));
                StreamGeometry g = new StreamGeometry();
                using (StreamGeometryContext c = g.Open())
                {
                    for (int i = 0; i <= steps; i++)
                    {
                        double t = (double)i / steps;
                        double x = x0 + (x1 - x0) * t;
                        double on = Math.Max(0, Math.Min(1, (t - 0.3) / 0.55));
                        double wob = Math.Sin(t * n.Dur * n.Rate * Math.PI * 2) * amp * on;
                        double scoop = t < 0.18 ? -(1 - t / 0.18) * 2.6 : 0;
                        double y = baseY + n.Drift + scoop + wob;
                        if (i == 0) c.BeginFigure(new Point(x, y), false, false);
                        else c.LineTo(new Point(x, y), true, false);
                    }
                }
                g.Freeze();
                bool sel = Host != null && Host.SelectedId == n.Id;
                dc.DrawGeometry(null, PenOf(sel ? curveSel : curve, sel ? 1.7 : 1.1), g);
            }

            double px = PlayheadBeat * PpB;
            if (px >= 0 && px <= w)
            {
                LinearGradientBrush ph = new LinearGradientBrush();
                ph.StartPoint = new Point(0.5, 0);
                ph.EndPoint = new Point(0.5, 1);
                ph.GradientStops.Add(new GradientStop(Color.FromArgb(235, 150, 235, 255), 0));
                ph.GradientStops.Add(new GradientStop(Color.FromArgb(110, 90, 170, 255), 1));
                dc.DrawRectangle(ph, null, new Rect(px - 0.5, 0, 1.2, h));
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            Point p = e.GetPosition(this);
            Note n = Hit(p);
            if (n == null)
            {
                if (Host != null) Host.SeekBeat(p.X / PpB);
                return;
            }
            if (Host != null) Host.SelectNote(n);
            _drag = n;
            _p0 = p;
            _s0 = n.Start;
            _d0 = n.Dur;
            _m0 = n.Midi;
            _resize = p.X > NoteRect(n).Right - 6;
            CaptureMouse();
            Focus();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_drag == null || !IsMouseCaptured) return;
            Point p = e.GetPosition(this);
            double dx = p.X - _p0.X;
            double dy = p.Y - _p0.Y;

            if (_resize)
            {
                double d = _d0 + dx / PpB;
                _drag.Dur = Math.Max(Host.Snap, Math.Round(d / Host.Snap) * Host.Snap);
            }
            else
            {
                double s = _s0 + dx / PpB;
                _drag.Start = Math.Max(0, Math.Min(Project.TotalBeats - 0.25, Math.Round(s / Host.Snap) * Host.Snap));
                int semi = (int)Math.Round(dy / RowH);
                _drag.Midi = Math.Max(Project.BottomMidi, Math.Min(Project.TopMidi, _m0 - semi));
            }
            InvalidateVisual();
            if (Host != null) Host.OnNoteDragged(_drag);
            e.Handled = true;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (_drag != null && IsMouseCaptured)
            {
                ReleaseMouseCapture();
                _drag = null;
                InvalidateVisual();
            }
        }
    }

    // =========================================================
    //  KEYS COLUMN
    // =========================================================
    public class KeysView : Surface
    {
        public double RowH = 19;
        public KeysView()
        {
            Width = 62;
            Height = Project.Rows * RowH;
            ClipToBounds = true;
        }

        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(120, 10, 24, 46)), null, new Rect(0, 0, Width, Height));
            for (int m = Project.TopMidi; m >= Project.BottomMidi; m--)
            {
                int pc = m % 12;
                bool black = pc == 1 || pc == 3 || pc == 6 || pc == 8 || pc == 10;
                bool isC = pc == 0;
                double y = (Project.TopMidi - m) * RowH;
                Brush bg = black
                    ? (Brush)new SolidColorBrush(Color.FromArgb(140, 2, 10, 24))
                    : new SolidColorBrush(Color.FromArgb(26, 210, 232, 255));
                dc.DrawRectangle(bg, null, new Rect(0, y, Width, RowH));
                dc.DrawLine(PenOf(new SolidColorBrush(isC
                    ? Color.FromArgb(60, 150, 205, 255)
                    : Color.FromArgb(16, 130, 185, 255)), 1), new Point(0, y + RowH), new Point(Width, y + RowH));

                string label = isC ? Geo.NoteName(m) : (pc == 4 || pc == 9 ? Geo.Names[pc] : "");
                if (label.Length > 0)
                {
                    Brush lb = new SolidColorBrush(Color.FromArgb(isC ? (byte)235 : (byte)165, 175, 200, 230));
                    FormattedText ft = FT(label, 8, lb, FontWeights.SemiBold, 0);
                    dc.DrawText(ft, new Point(Width - ft.Width - 6, y + RowH / 2 - 6));
                }
            }
            dc.DrawLine(PenOf(new SolidColorBrush(Color.FromArgb(50, 130, 185, 255)), 1),
                new Point(Width - 0.5, 0), new Point(Width - 0.5, Height));
        }
    }

    // =========================================================
    //  PARAMETER STRIP
    // =========================================================
    public class ParamView : Surface
    {
        public double PpB = 46;
        public double Offset;
        public string Mode = "vibrato";
        public string Track = "v1";
        public string Lang = "en";

        public ParamView()
        {
            Height = 60;
            Width = Project.TotalBeats * PpB;
            ClipToBounds = true;
        }

        protected override void OnRender(DrawingContext dc)
        {
            double h = Height;
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(90, 4, 12, 26)), null, new Rect(0, 0, Width, h));

            // min/max guides
            dc.DrawLine(PenOf(new SolidColorBrush(Color.FromArgb(16, 150, 205, 255)), 1), new Point(0, 10), new Point(Width, 10));
            dc.DrawLine(PenOf(new SolidColorBrush(Color.FromArgb(16, 150, 205, 255)), 1), new Point(0, h - 8), new Point(Width, h - 8));

            dc.PushTransform(new TranslateTransform(-Offset, 0));

            List<Note> list = Project.VisibleNotes(Track);
            Pen barPen = PenOf(new SolidColorBrush(Color.FromArgb(20, 150, 200, 255)), 1);
            for (int b = 0; b <= Project.TotalBeats; b++)
                dc.DrawLine(barPen, new Point(b * PpB, 0), new Point(b * PpB, h));

            if (Mode == "vibrato")
            {
                StreamGeometry line = new StreamGeometry();
                StreamGeometry area = new StreamGeometry();
                int span = (int)(Width / 4);
                using (StreamGeometryContext c = area.Open())
                {
                    c.BeginFigure(new Point(0, h), true, true);
                    for (int i = 0; i <= span; i++)
                    {
                        double x = i * 4;
                        double beat = x / PpB;
                        double v = 0;
                        foreach (Note n in list)
                        {
                            if (beat < n.Start || beat > n.Start + n.Dur) continue;
                            double t = (beat - n.Start) / n.Dur;
                            double on = Math.Max(0, Math.Min(1, (t - 0.3) / 0.55));
                            v = (n.Vib * on + 0.12) * (0.6 + 0.4 * Math.Sin(beat * n.Rate * Math.PI * 2));
                        }
                        c.LineTo(new Point(x, h), true, false);
                        c.LineTo(new Point(x, 52 - v * 34), true, false);
                    }
                    c.LineTo(new Point(Width, h), true, false);
                }
                area.Freeze();

                using (StreamGeometryContext c = line.Open())
                {
                    for (int i = 0; i <= span; i++)
                    {
                        double x = i * 4;
                        double beat = x / PpB;
                        double v = 0;
                        foreach (Note n in list)
                        {
                            if (beat < n.Start || beat > n.Start + n.Dur) continue;
                            double t = (beat - n.Start) / n.Dur;
                            double on = Math.Max(0, Math.Min(1, (t - 0.3) / 0.55));
                            v = (n.Vib * on + 0.12) * (0.6 + 0.4 * Math.Sin(beat * n.Rate * Math.PI * 2));
                        }
                        if (i == 0) c.BeginFigure(new Point(x, 52 - v * 34), false, false);
                        else c.LineTo(new Point(x, 52 - v * 34), true, false);
                    }
                }
                line.Freeze();

                LinearGradientBrush fill = new LinearGradientBrush();
                fill.StartPoint = new Point(0.5, 0);
                fill.EndPoint = new Point(0.5, 1);
                fill.GradientStops.Add(new GradientStop(Color.FromArgb(110, 130, 225, 255), 0));
                fill.GradientStops.Add(new GradientStop(Color.FromArgb(4, 60, 130, 255), 1));
                dc.DrawGeometry(fill, null, area);
                dc.DrawGeometry(null, PenOf(new SolidColorBrush(Color.FromArgb(215, 160, 235, 255)), 1.3), line);
                DrawTxt(dc, "VIBRATO DEPTH", 6, 4, 8, T.SA("#6d88a8", 230), FontWeights.SemiBold, 200);
            }
            else if (Mode == "phoneme")
            {
                foreach (Note n in list)
                {
                    double x0 = n.Start * PpB;
                    double w = n.Dur * PpB - 1;
                    if (w < 3) continue;
                    double cons = Math.Min(w * 0.34, 16);
                    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(120, 150, 150, 255)),
                        PenOf(T.SA("#beccff", 150), 1), new Rect(x0, 22, cons, 20), 3, 3);
                    dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(110, 90, 190, 255)),
                        PenOf(T.SA("#96e1ff", 170), 1), new Rect(x0 + cons, 22, Math.Max(2, w - cons), 20), 3, 3);
                    DrawTxt(dc, n.Phon.Split(' ')[0], x0 + 2, 26, 8, T.SA("#e8f8ff", 240), FontWeights.Normal, Math.Max(4, w - 4));
                }
                DrawTxt(dc, "CONSONANT / VOWEL TIMING", 6, 4, 8, T.SA("#6d88a8", 230), FontWeights.SemiBold, 260);
            }
            else
            {
                StreamGeometry area = new StreamGeometry();
                int span = (int)(Width / 4);
                using (StreamGeometryContext c = area.Open())
                {
                    c.BeginFigure(new Point(0, 58), true, true);
                    for (int i = 0; i <= span; i++)
                    {
                        double x = i * 4;
                        double beat = x / PpB;
                        double v = 0.02;
                        foreach (Note n in list)
                        {
                            double center = n.Start + n.Dur / 2;
                            double dist = Math.Abs(beat - center) / (n.Dur / 2 + 0.35);
                            if (dist < 2.4) v += Math.Exp(-dist * dist * 2.2) * (0.5 + (n.Midi - 55) / 30.0);
                        }
                        double y = 58 - v * 34;
                        if (y < 4) y = 4;
                        c.LineTo(new Point(x, y), true, false);
                    }
                    c.LineTo(new Point(Width, 58), true, false);
                }
                area.Freeze();
                LinearGradientBrush fill = new LinearGradientBrush();
                fill.StartPoint = new Point(0.5, 0);
                fill.EndPoint = new Point(0.5, 1);
                fill.GradientStops.Add(new GradientStop(Color.FromArgb(120, 130, 225, 255), 0));
                fill.GradientStops.Add(new GradientStop(Color.FromArgb(4, 60, 130, 255), 1));
                dc.DrawGeometry(fill, PenOf(new SolidColorBrush(Color.FromArgb(180, 160, 235, 255)), 1.1), area);
                DrawTxt(dc, "LOUDNESS / DYNAMICS", 6, 4, 8, T.SA("#6d88a8", 230), FontWeights.SemiBold, 260);
            }

            dc.Pop();
        }
    }
}

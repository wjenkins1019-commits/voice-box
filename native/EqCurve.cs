using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace VoiceBox
{
    // Draggable three-band EQ curve. The line is the real filter response.
    public class EqCurve : FrameworkElement
    {
        public EqSettings Eq;
        public Action Changed;

        const double Range = 15.0;     // dB shown from centre to edge
        const double MinHz = 40.0;
        const double MaxHz = 16000.0;
        // Taken from the engine so the drawn response can never describe a different rate
        // than the filter that runs at render time.
        const int SampleRate = VocalSynth.Sr;

        int _drag = -1;

        public EqCurve()
        {
            Height = 96;
            Cursor = Cursors.Hand;
            SnapsToDevicePixels = true;
        }

        double Pad { get { return 12.0; } }

        double XFor(double hz)
        {
            double span = Math.Max(1, ActualWidth - Pad * 2);
            double t = (Math.Log(hz) - Math.Log(MinHz)) / (Math.Log(MaxHz) - Math.Log(MinHz));
            return Pad + t * span;
        }

        double YFor(double db)
        {
            double half = Math.Max(1, ActualHeight / 2 - 12);
            return ActualHeight / 2 - (db / Range) * half;
        }

        double DbFor(double y)
        {
            double half = Math.Max(1, ActualHeight / 2 - 12);
            return (ActualHeight / 2 - y) / half * Range;
        }

        int BandAt(Point p)
        {
            double[] freqs = { 180, 1200, 6000 };
            for (int i = 0; i < 3; i++)
                if (Math.Abs(p.X - XFor(freqs[i])) <= 17) return i;
            return -1;
        }

        void SetGain(int band, Point p)
        {
            if (Eq == null) return;
            double db = Math.Max(-12, Math.Min(12, Math.Round(DbFor(p.Y) * 2) / 2));
            if (band == 0) Eq.LowDb = db;
            else if (band == 1) Eq.MidDb = db;
            else Eq.HighDb = db;
            InvalidateVisual();
            if (Changed != null) Changed();
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 8 || h <= 8) return;

            dc.DrawRoundedRectangle(T.SA("#07162a", 190), new Pen(T.SA("#82b9ff", 42), 1), new Rect(0, 0, w, h), 8, 8);

            double[] grid = { 100, 1000, 10000 };
            for (int i = 0; i < grid.Length; i++)
            {
                double x = XFor(grid[i]);
                dc.DrawLine(new Pen(T.SA("#2b5c8f", 95), 1), new Point(x, 6), new Point(x, h - 6));
            }

            double zero = YFor(0);
            dc.DrawLine(new Pen(T.SA("#7fb6ee", 80), 1), new Point(8, zero), new Point(w - 8, zero));

            if (Eq == null) return;

            StreamGeometry path = new StreamGeometry();
            using (StreamGeometryContext c = path.Open())
            {
                int steps = Math.Max(60, (int)(w / 2));
                for (int i = 0; i <= steps; i++)
                {
                    double x = Pad + (w - Pad * 2) * i / steps;
                    double hz = Math.Exp(Math.Log(MinHz) +
                        (Math.Log(MaxHz) - Math.Log(MinHz)) * i / steps);
                    Point p = new Point(x, YFor(Eq.ResponseDb(hz, SampleRate)));
                    if (i == 0) c.BeginFigure(p, false, false);
                    else c.LineTo(p, true, false);
                }
            }
            path.Freeze();
            Pen line = new Pen(T.SA("#7fe3ff", 240), 1.9);
            line.Freeze();
            dc.DrawGeometry(null, line, path);

            double[] freqs2 = { 180, 1200, 6000 };
            double[] gains = { Eq.LowDb, Eq.MidDb, Eq.HighDb };
            string[] names = { "LOW", "MID", "HIGH" };
            Brush label = T.S(T.C("#9ec8f5"));

            for (int i = 0; i < 3; i++)
            {
                double x = XFor(freqs2[i]);
                double y = YFor(gains[i]);
                dc.DrawEllipse(T.SA("#7fd8ff", 60), null, new Point(x, y), 9, 9);
                dc.DrawEllipse(T.S(T.C("#e8fbff")), null, new Point(x, y), 5, 5);

                FormattedText ft = new FormattedText(
                    names[i] + " " + gains[i].ToString("0.0", CultureInfo.InvariantCulture),
                    CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 7.5, label,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(ft, new Point(x - ft.Width / 2, Math.Max(1, y - 22)));
            }

            FormattedText dbLbl = new FormattedText("+12", CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, new Typeface("Segoe UI"), 7, label,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(dbLbl, new Point(3, 2));
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            int band = BandAt(e.GetPosition(this));
            if (band < 0) return;
            _drag = band;
            CaptureMouse();
            SetGain(band, e.GetPosition(this));
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_drag < 0 || !IsMouseCaptured) return;
            SetGain(_drag, e.GetPosition(this));
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            _drag = -1;
            ReleaseMouseCapture();
        }
    }
}

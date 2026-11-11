using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace VoiceBox
{
    // Velocity lane under the piano roll: one bar per note, drag up/down to draw the dynamics.
    public class VelocityLane : FrameworkElement
    {
        public Func<string> TrackOf;
        public Action<Note> Edited;

        Note _drag;

        public VelocityLane()
        {
            Height = 54;
            Cursor = Cursors.SizeNS;
            SnapsToDevicePixels = true;
            ClipToBounds = true;
        }

        double BarH { get { return Math.Max(8, ActualHeight - 14); } }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(110, 5, 16, 34)), null, new Rect(0, 0, w, h));

            Pen faint = new Pen(new SolidColorBrush(Color.FromArgb(46, 130, 185, 255)), 1);
            faint.Freeze();
            for (int i = 0; i <= 2; i++)
            {
                double y = h - 7 - BarH * (i / 2.0);
                dc.DrawLine(faint, new Point(0, y), new Point(w, y));
            }

            string track = TrackOf != null ? TrackOf() : null;
            if (string.IsNullOrEmpty(track)) return;

            double perBeat = w / Math.Max(1.0, Project.TotalBeats);
            foreach (Note n in Project.VisibleNotes(track))
            {
                double v = Math.Max(0.03, Math.Min(1.0, n.Velocity));
                double barH = BarH * v;
                double barW = Math.Max(3.0, n.Dur * perBeat * 0.72);
                bool on = n == _drag;
                SolidColorBrush b = new SolidColorBrush(on
                    ? Color.FromArgb(250, 255, 226, 118)
                    : Color.FromArgb(245, 120, 214, 255));
                b.Freeze();
                dc.DrawRoundedRectangle(b, null, new Rect(n.Start * perBeat, h - 7 - barH, barW, barH), 2, 2);
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            Note hit = HitTest(e.GetPosition(this));
            if (hit == null) return;
            _drag = hit;
            CaptureMouse();
            SetFromPoint(hit, e.GetPosition(this));
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_drag == null || !IsMouseCaptured) return;
            SetFromPoint(_drag, e.GetPosition(this));
            e.Handled = true;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (_drag == null) return;
            _drag = null;
            if (IsMouseCaptured) ReleaseMouseCapture();
            InvalidateVisual();
            if (Edited != null) Edited(null);
            e.Handled = true;
        }

        Note HitTest(Point p)
        {
            string track = TrackOf != null ? TrackOf() : null;
            if (string.IsNullOrEmpty(track)) return null;
            double perBeat = ActualWidth / Math.Max(1.0, Project.TotalBeats);
            Note best = null;
            double bestDx = double.MaxValue;
            foreach (Note n in Project.VisibleNotes(track))
            {
                double x0 = n.Start * perBeat;
                double x1 = x0 + Math.Max(6.0, n.Dur * perBeat * 0.72);
                if (p.X < x0 - 4 || p.X > x1 + 4) continue;
                double dx = Math.Abs(p.X - x0);
                if (dx < bestDx)
                {
                    bestDx = dx;
                    best = n;
                }
            }
            return best;
        }

        void SetFromPoint(Note n, Point p)
        {
            double h = ActualHeight;
            double v = (h - 7 - p.Y) / BarH;
            v = Math.Round(Math.Max(0.0, Math.Min(1.0, v)) * 100) / 100;
            if (Math.Abs(v - n.Velocity) < 0.005) return;
            n.Velocity = v;
            InvalidateVisual();
            if (Edited != null) Edited(n);
        }
    }
}

using System;
using System.Windows;
using System.Windows.Media;

namespace VoiceBox
{
    // A small horizontal level bar for a track header. Level is 0..1 (0 = silent,
    // 1 = full scale) and the fill turns warm as it approaches the top.
    public class Meter : FrameworkElement
    {
        public double Level;
        public bool Dimmed;

        static readonly LinearGradientBrush Fill =
            new LinearGradientBrush(Color.FromRgb(0x35, 0xB6, 0xFF), Color.FromRgb(0x9C, 0xE8, 0xFF), 0);

        public Meter()
        {
            Height = 5;
            SnapsToDevicePixels = true;
            IsHitTestVisible = false;
            Fill.Freeze();
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth;
            if (w <= 0) return;

            SolidColorBrush back = new SolidColorBrush(Color.FromArgb(150, 0x07, 0x18, 0x2E));
            back.Freeze();
            dc.DrawRoundedRectangle(back, null, new Rect(0, 0, w, ActualHeight), 2.5, 2.5);

            double pct = Level;
            if (pct < 0) pct = 0;
            if (pct > 1) pct = 1;
            if (pct <= 0.001) return;

            double fillW = Math.Max(2, w * pct);
            double alpha = Dimmed ? 0.35 : 1.0;
            dc.PushOpacity(alpha);
            dc.DrawRoundedRectangle(Fill, null, new Rect(0, 0, fillW, ActualHeight), 2.5, 2.5);
            dc.Pop();
        }
    }
}

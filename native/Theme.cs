using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace VoiceBox
{
    public static class T
    {
        public static Color C(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }

        public static SolidColorBrush S(Color c)
        {
            SolidColorBrush b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        public static SolidColorBrush SA(string hex, byte a)
        {
            return S(CA(hex, a));
        }

        public static Color CA(string hex, byte a)
        {
            Color c = C(hex);
            c.A = a;
            return c;
        }

        public static readonly Color TextMain = C("#dcebff");
        public static readonly Color TextDim = C("#93aac9");
        public static readonly Color TextFaint = C("#6a80a1");
        public static readonly Color Accent = C("#4fadff");
        public static readonly Color Accent2 = C("#8ce6ff");
        public static readonly Color Ink = C("#050d1b");

        public static readonly FontFamily font = new FontFamily("Segoe UI");
        public static readonly FontFamily mono = new FontFamily("Consolas");

        public static LinearGradientBrush VGrad(Color top, Color bottom)
        {
            LinearGradientBrush g = new LinearGradientBrush();
            g.StartPoint = new Point(0.5, 0);
            g.EndPoint = new Point(0.5, 1);
            g.GradientStops.Add(new GradientStop(top, 0));
            g.GradientStops.Add(new GradientStop(bottom, 1));
            g.Freeze();
            return g;
        }

        public static LinearGradientBrush HGrad(Color left, Color right)
        {
            LinearGradientBrush g = new LinearGradientBrush();
            g.StartPoint = new Point(0, 0.5);
            g.EndPoint = new Point(1, 0.5);
            g.GradientStops.Add(new GradientStop(left, 0));
            g.GradientStops.Add(new GradientStop(right, 1));
            g.Freeze();
            return g;
        }

        public static TextBlock Txt(string s, double size, Color c)
        {
            return Txt(s, size, c, FontWeights.Normal);
        }

        public static TextBlock Txt(string s, double size, Color c, FontWeight w)
        {
            TextBlock t = new TextBlock();
            t.Text = s;
            t.FontSize = size;
            t.Foreground = S(c);
            t.FontWeight = w;
            t.FontFamily = font;
            t.VerticalAlignment = VerticalAlignment.Center;
            t.TextTrimming = TextTrimming.CharacterEllipsis;
            return t;
        }

        public static TextBlock Label(string s)
        {
            TextBlock t = Txt(s.ToUpperInvariant(), 9, TextFaint, FontWeights.SemiBold);
            t.FontSize = 9;
            t.Opacity = 0.95;
            return t;
        }

        // ---------------------------------------------------------------
        // frosted glass surface: shadow behind, translucent gradient fill,
        // 1px highlight border. Content sits above so text stays crisp.
        // ---------------------------------------------------------------
        public static Brush GlassFill()
        {
            LinearGradientBrush g = new LinearGradientBrush();
            g.StartPoint = new Point(0.2, 0);
            g.EndPoint = new Point(0.9, 1);
            g.GradientStops.Add(new GradientStop(CA("#1f4079", 150), 0));
            g.GradientStops.Add(new GradientStop(CA("#0e2244", 158), 0.55));
            g.GradientStops.Add(new GradientStop(CA("#0a1830", 165), 1));
            return g;
        }

        public static Border GlassFillBox(double radius)
        {
            Border b = new Border();
            b.CornerRadius = new CornerRadius(radius);
            b.Background = GlassFill();
            b.BorderBrush = SA("#84bdff", 46);
            b.BorderThickness = new Thickness(1);
            DropShadowEffect sh = new DropShadowEffect();
            sh.Color = Colors.Black;
            sh.BlurRadius = 34;
            sh.ShadowDepth = 14;
            sh.Direction = 270;
            sh.Opacity = 0.7;
            b.Effect = sh;
            return b;
        }

        public static Grid Glass(double radius, UIElement content, Thickness padding, bool shadow)
        {
            Grid g = new Grid();
            Border fill = new Border();
            fill.CornerRadius = new CornerRadius(radius);
            fill.Background = GlassFill();
            fill.BorderBrush = SA("#84bdff", 46);
            fill.BorderThickness = new Thickness(1);
            if (shadow)
            {
                DropShadowEffect sh = new DropShadowEffect();
                sh.Color = Colors.Black;
                sh.BlurRadius = 34;
                sh.ShadowDepth = 14;
                sh.Direction = 270;
                sh.Opacity = 0.7;
                fill.Effect = sh;
            }
            g.Children.Add(fill);

            // top-left sheen
            Border sheen = new Border();
            sheen.CornerRadius = new CornerRadius(radius);
            RadialGradientBrush rg = new RadialGradientBrush();
            rg.Center = new Point(0.1, 0);
            rg.GradientOrigin = new Point(0.1, 0);
            rg.RadiusX = 1.1;
            rg.RadiusY = 0.9;
            rg.GradientStops.Add(new GradientStop(CA("#b9e0ff", 30), 0));
            rg.GradientStops.Add(new GradientStop(CA("#b9e0ff", 0), 0.7));
            sheen.Background = rg;
            sheen.IsHitTestVisible = false;
            g.Children.Add(sheen);

            if (content != null)
            {
                Border host = new Border();
                host.CornerRadius = new CornerRadius(radius);
                host.Padding = padding;
                host.Child = content;
                g.Children.Add(host);
            }
            return g;
        }

        public static Grid PanelHead(string title, string badge, bool showLead, FrameworkElement right)
        {
            Grid g = new Grid();
            g.Height = 32;
            Border bg = new Border();
            LinearGradientBrush bgb = VGrad(CA("#2a5a9f", 60), CA("#16305a", 6));
            bg.Background = bgb;
            bg.BorderBrush = SA("#82b9ff", 24);
            bg.BorderThickness = new Thickness(0, 0, 0, 1);
            bg.CornerRadius = new CornerRadius(14, 14, 0, 0);
            g.Children.Add(bg);

            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            sp.Margin = new Thickness(12, 0, 12, 0);
            sp.VerticalAlignment = VerticalAlignment.Center;

            if (showLead)
            {
                Border lead = new Border();
                lead.Width = 3;
                lead.Height = 12;
                lead.CornerRadius = new CornerRadius(2);
                lead.Background = VGrad(Accent2, C("#3f8cf5"));
                lead.Margin = new Thickness(0, 0, 8, 0);
                sp.Children.Add(lead);
            }

            TextBlock t = Txt(title.ToUpperInvariant(), 9.5, C("#a9c9ee"), FontWeights.Bold);
            t.Margin = new Thickness(0, 0, 8, 0);
            sp.Children.Add(t);

            if (badge != null)
            {
                Border bd = new Border();
                bd.CornerRadius = new CornerRadius(5);
                bd.Background = SA("#4696f0", 40);
                bd.BorderBrush = SA("#82c3ff", 44);
                bd.BorderThickness = new Thickness(1);
                bd.Padding = new Thickness(6, 2, 6, 2);
                bd.Child = Txt(badge, 8, C("#9ec8f5"), FontWeights.SemiBold);
                sp.Children.Add(bd);
            }
            g.Children.Add(sp);

            if (right != null)
            {
                right.HorizontalAlignment = HorizontalAlignment.Right;
                right.VerticalAlignment = VerticalAlignment.Center;
                right.Margin = new Thickness(0, 0, 10, 0);
                g.Children.Add(right);
            }
            return g;
        }

        // clickable border used everywhere instead of themed buttons
        public static Border Btn(string text, Action click, bool primary)
        {
            Border b = new Border();
            b.CornerRadius = new CornerRadius(8);
            b.Padding = new Thickness(11, 6, 11, 6);
            b.Cursor = Cursors.Hand;
            b.BorderThickness = new Thickness(1);
            if (primary)
            {
                b.Background = VGrad(C("#58b2ff"), C("#246ad6"));
                b.BorderBrush = SA("#96cfff", 120);
            }
            else
            {
                b.Background = SA("#3269b9", 44);
                b.BorderBrush = SA("#84bdff", 56);
            }
            TextBlock t = Txt(text, primary ? 9.5 : 9, primary ? Colors.White : TextDim, FontWeights.SemiBold);
            b.Child = t;
            Brush normal = b.Background;
            Brush hover = primary ? (Brush)VGrad(C("#76c6ff"), C("#3078e6")) : (Brush)SA("#4f8fdc", 80);
            b.MouseEnter += delegate { b.Background = hover; if (primary) t.Foreground = S(Colors.White); else t.Foreground = S(TextMain); };
            b.MouseLeave += delegate { b.Background = normal; if (!primary) t.Foreground = S(TextDim); };
            b.MouseLeftButtonDown += delegate(object s2, MouseButtonEventArgs e2) { e2.Handled = true; };
            b.MouseLeftButtonUp += delegate { if (click != null) click(); };
            return b;
        }

        public static Border Chip(string text, bool on, Action click)
        {
            return Chip(text, on, click, null);
        }

        // A small keycap is how a shortcut announces itself in the panel it acts on, instead of
        // only in the legend.
        public static Border KeyCap(string key)
        {
            Border c = new Border();
            c.CornerRadius = new CornerRadius(3);
            c.Padding = new Thickness(3, 0, 3, 1);
            c.Background = SA("#0a1e36", 190);
            c.BorderBrush = SA("#8ccdff", 80);
            c.BorderThickness = new Thickness(1);
            c.VerticalAlignment = VerticalAlignment.Center;
            c.Child = Txt(key, 7.2, C("#9dc6ea"), FontWeights.Bold);
            return c;
        }

        public static Border Chip(string text, bool on, Action click, string key)
        {
            Border b = new Border();
            b.CornerRadius = new CornerRadius(8);
            b.Padding = new Thickness(10, 5, 10, 5);
            b.Cursor = Cursors.Hand;
            b.BorderThickness = new Thickness(1);
            b.Background = on ? SA("#4aa8ff", 90) : SA("#3269b9", 36);
            b.BorderBrush = on ? SA("#8ce6ff", 150) : SA("#84bdff", 50);
            TextBlock t = Txt(text, 9, on ? C("#eaf9ff") : TextFaint, FontWeights.SemiBold);
            if (string.IsNullOrEmpty(key))
            {
                b.Child = t;
            }
            else
            {
                StackPanel row = new StackPanel();
                row.Orientation = Orientation.Horizontal;
                row.Children.Add(t);
                Border cap = KeyCap(key);
                cap.Margin = new Thickness(6, 0, 0, 0);
                row.Children.Add(cap);
                b.Child = row;
            }
            b.MouseLeftButtonDown += delegate(object s2, MouseButtonEventArgs e2) { e2.Handled = true; };
            b.MouseLeftButtonUp += delegate
            {
                if (click != null) click();
            };
            b.MouseEnter += delegate { b.Opacity = 0.85; };
            b.MouseLeave += delegate { b.Opacity = 1; };
            return b;
        }

        public static void SetChipState(Border chip, bool on)
        {
            chip.Background = on ? SA("#4aa8ff", 90) : SA("#3269b9", 36);
            chip.BorderBrush = on ? SA("#8ce6ff", 150) : SA("#84bdff", 50);
            TextBlock t = chip.Child as TextBlock;
            if (t == null)
            {
                // Chips that carry a keycap wrap their label in a row, so find it there.
                StackPanel row = chip.Child as StackPanel;
                if (row != null && row.Children.Count > 0) t = row.Children[0] as TextBlock;
            }
            if (t != null) t.Foreground = on ? S(C("#eaf9ff")) : S(TextFaint);
        }

        // styled dropdown: a pill that opens a context menu
        public static Border Select(string[] items, int index, Action<int> onChange)
        {
            Border b = new Border();
            b.CornerRadius = new CornerRadius(7);
            b.Background = SA("#06142a", 170);
            b.BorderBrush = SA("#82b9ff", 46);
            b.BorderThickness = new Thickness(1);
            b.Padding = new Thickness(8, 5, 8, 5);
            b.Cursor = Cursors.Hand;
            b.MinHeight = 26;

            Grid content = new Grid();
            TextBlock t = Txt(items[Math.Max(0, index)], 10.5, TextMain);
            t.Margin = new Thickness(0, 0, 14, 0);
            content.Children.Add(t);
            TextBlock caret = Txt("▾", 9, C("#7fb4e8"));
            caret.HorizontalAlignment = HorizontalAlignment.Right;
            content.Children.Add(caret);
            b.Child = content;

            b.MouseLeftButtonDown += delegate(object s2, MouseButtonEventArgs e2) { e2.Handled = true; };
            b.MouseLeftButtonUp += delegate
            {
                ContextMenu m = new ContextMenu();
                m.PlacementTarget = b;
                m.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                StyleMenu(m);
                for (int i = 0; i < items.Length; i++)
                {
                    MenuItem mi = new MenuItem();
                    mi.Header = items[i];
                    int idx = i;
                    mi.Click += delegate
                    {
                        t.Text = items[idx];
                        if (onChange != null) onChange(idx);
                    };
                    m.Items.Add(mi);
                }
                m.IsOpen = true;
            };
            return b;
        }

        public static void StyleMenu(ContextMenu m)
        {
            m.Background = S(C("#0a1c36"));
            m.Foreground = S(TextMain);
            m.BorderBrush = SA("#84bdff", 70);
            m.BorderThickness = new Thickness(1);
            m.Padding = new Thickness(4);
            m.HasDropShadow = true;
            foreach (object o in m.Items)
            {
                MenuItem mi = o as MenuItem;
                if (mi == null) continue;
                mi.Foreground = S(TextMain);
                mi.Background = Brushes.Transparent;
                mi.Padding = new Thickness(10, 4, 10, 4);
            }
            m.Resources[SystemColors.HighlightBrushKey] = SA("#3f8ce6", 150);
            m.Resources[SystemColors.HighlightTextBrushKey] = S(Colors.White);
            m.Resources[SystemColors.MenuTextBrushKey] = S(TextMain);
        }

        public static TextBox Input(string placeholder)
        {
            TextBox tb = new TextBox();
            tb.Background = SA("#06142a", 170);
            tb.Foreground = S(TextMain);
            tb.BorderBrush = SA("#82b9ff", 46);
            tb.BorderThickness = new Thickness(1);
            tb.CaretBrush = S(Accent2);
            tb.FontSize = 11;
            tb.FontFamily = font;
            tb.Padding = new Thickness(7, 4, 7, 4);
            tb.VerticalContentAlignment = VerticalAlignment.Center;
            tb.MinHeight = 26;
            tb.Tag = placeholder;
            return tb;
        }

        public static Slider Slider(double min, double max, double value, Action<double> onChange)
        {
            Slider s = new Slider();
            s.Minimum = min;
            s.Maximum = max;
            s.Value = value;
            s.Height = 16;
            s.IsMoveToPointEnabled = true;
            s.Background = SA("#06142a", 120);
            s.Foreground = S(Accent);
            s.ValueChanged += delegate(object a, RoutedPropertyChangedEventArgs<double> e)
            {
                if (onChange != null) onChange(e.NewValue);
            };
            return s;
        }

        public static Border Sep()
        {
            Border b = new Border();
            b.Width = 1;
            b.Height = 13;
            b.Background = SA("#8cc3ff", 36);
            b.Margin = new Thickness(4, 0, 4, 0);
            return b;
        }

        public static UIElement Row(string label, UIElement control, double labelWidth)
        {
            Grid g = new Grid();
            g.Margin = new Thickness(0, 2, 0, 2);
            ColumnDefinition c0 = new ColumnDefinition();
            c0.Width = new GridLength(labelWidth);
            ColumnDefinition c1 = new ColumnDefinition();
            c1.Width = new GridLength(1, GridUnitType.Star);
            g.ColumnDefinitions.Add(c0);
            g.ColumnDefinitions.Add(c1);

            TextBlock l = Label(label);
            l.FontSize = 9;
            Grid.SetColumn(l, 0);
            g.Children.Add(l);

            Grid.SetColumn(control, 1);
            g.Children.Add(control);
            return g;
        }

        // ---------------------------------------------------------------
        // thin dark scrollbars (the stock Aero ones are white and heavy)
        // ---------------------------------------------------------------
        public static DropShadowEffect Glow(Color c, double blur, double opacity)
        {
            DropShadowEffect e = new DropShadowEffect();
            e.Color = c;
            e.BlurRadius = blur;
            e.ShadowDepth = 0;
            e.Opacity = opacity;
            return e;
        }
    }

    // slim themed horizontal fader used for the expression sliders
    public class Fader : FrameworkElement
    {
        public double Min = 0;
        public double Max = 100;
        public double Value = 50;
        public Action<double> Changed;
        bool _drag;

        public Fader()
        {
            Height = 16;
            Cursor = Cursors.Hand;
            SnapsToDevicePixels = true;
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth;
            double h = ActualHeight;
            double y = h / 2 - 2;
            dc.DrawRoundedRectangle(T.S(T.C("#08182c")), null, new Rect(0, y, w, 4), 2, 2);

            double pct = (Value - Min) / Math.Max(0.0001, Max - Min);
            if (pct < 0) pct = 0;
            if (pct > 1) pct = 1;
            if (pct > 0.001)
            {
                LinearGradientBrush g = T.HGrad(T.C("#3f9cff"), T.C("#8ce6ff"));
                dc.DrawRoundedRectangle(g, null, new Rect(0, y, Math.Max(3, w * pct), 4), 2, 2);
            }
            double cx = Math.Max(6, Math.Min(w - 6, w * pct));
            dc.DrawEllipse(T.SA("#7fd8ff", 70), null, new Point(cx, h / 2), 9, 9);
            dc.DrawEllipse(T.S(T.C("#e8fbff")), null, new Point(cx, h / 2), 5.5, 5.5);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            CaptureMouse();
            _drag = true;
            SetFromX(e.GetPosition(this).X);
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_drag && IsMouseCaptured) SetFromX(e.GetPosition(this).X);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            _drag = false;
            ReleaseMouseCapture();
        }

        void SetFromX(double x)
        {
            double w = Math.Max(1, ActualWidth);
            double pct = Math.Max(0, Math.Min(1, x / w));
            Value = Min + pct * (Max - Min);
            InvalidateVisual();
            if (Changed != null) Changed(Value);
        }
    }
}

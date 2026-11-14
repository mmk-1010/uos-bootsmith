using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

public enum MsgKind { Info, Warn, Error, Done }

// 自绘对话框：跟主界面同一套卡片风格，替代系统 MessageBox
public static class Dlg {
    static readonly Brush CardBg = Brushes.White;
    static readonly Brush CardBorder = new SolidColorBrush(Color.FromRgb(0xE6, 0xEA, 0xF2));
    static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(0x1B, 0x1F, 0x27));
    static readonly Brush Sub = new SolidColorBrush(Color.FromRgb(0x5A, 0x64, 0x78));
    static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(0x2F, 0x6F, 0xED));

    public static bool Confirm(Window owner, string title, string message, string okText, string cancelText) {
        return Show(owner, title, message, okText, cancelText, MsgKind.Info);
    }

    public static void Info(Window owner, string title, string message) {
        Show(owner, title, message, "好", null, MsgKind.Done);
    }

    public static void Info(Window owner, string title, string message, MsgKind kind) {
        Show(owner, title, message, "好", null, kind);
    }

    public static Window Build(Window owner, string title, string message, string okText, string cancelText, MsgKind kind) {
        var w = new Window {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            SizeToContent = SizeToContent.Height,
            Width = 488,
            WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
            Title = title
        };
        if (owner != null) w.Owner = owner;

        var card = new Border {
            Background = CardBg,
            CornerRadius = new CornerRadius(14),
            BorderBrush = CardBorder,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(22, 20, 22, 18),
            Margin = new Thickness(18),
            Effect = new DropShadowEffect { BlurRadius = 26, ShadowDepth = 4, Opacity = 0.16, Color = Color.FromRgb(0x1B, 0x2A, 0x4A), Direction = 270 }
        };

        var root = new StackPanel();

        var head = new StackPanel { Orientation = Orientation.Horizontal };
        var color = kind == MsgKind.Error ? Color.FromRgb(0xD0, 0x45, 0x45)
                  : kind == MsgKind.Warn ? Color.FromRgb(0xE0, 0x8A, 0x1E)
                  : kind == MsgKind.Done ? Color.FromRgb(0x2E, 0x9E, 0x62)
                  : Color.FromRgb(0x2F, 0x6F, 0xED);
        var glyph = kind == MsgKind.Error ? "\uE711"
                  : kind == MsgKind.Warn ? "\uE7BA"
                  : kind == MsgKind.Done ? "\uE73E"
                  : "\uE946";

        var tile = new Border {
            Width = 34, Height = 34, CornerRadius = new CornerRadius(9),
            Background = new SolidColorBrush(color)
        };
        tile.Child = new TextBlock {
            Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 15,
            Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        head.Children.Add(tile);

        var tb = new TextBlock {
            Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Ink,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(11, 0, 0, 0),
            FontFamily = new FontFamily("Microsoft YaHei UI")
        };
        head.Children.Add(tb);
        root.Children.Add(head);

        var body = new TextBlock {
            Text = message, FontSize = 12.5, Foreground = Sub, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(1, 14, 1, 0), LineHeight = 21,
            FontFamily = new FontFamily("Microsoft YaHei UI")
        };
        root.Children.Add(body);

        var bar = new StackPanel {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 20, 0, 0)
        };

        if (cancelText != null) {
            bar.Children.Add(MakeButton(cancelText, false, Accent, delegate { w.DialogResult = false; }));
        }
        bar.Children.Add(MakeButton(okText, true, Accent, delegate { w.DialogResult = true; }));
        root.Children.Add(bar);

        card.Child = root;
        w.Content = card;

        // 拖动 + 键盘
        card.MouseLeftButtonDown += delegate { try { w.DragMove(); } catch { } };
        w.KeyDown += delegate (object s, KeyEventArgs e) {
            if (e.Key == Key.Escape) { w.DialogResult = false; }
            else if (e.Key == Key.Enter) { w.DialogResult = true; }
        };

        return w;
    }

    static bool Show(Window owner, string title, string message, string okText, string cancelText, MsgKind kind) {
        var w = Build(owner, title, message, okText, cancelText, kind);
        return w.ShowDialog() == true;
    }

    static Button MakeButton(string text, bool primary, Brush accent, RoutedEventHandler onClick) {
        var b = new Button {
            Content = text, Height = 38, MinWidth = primary ? 96 : 84,
            FontSize = 13, FontFamily = new FontFamily("Microsoft YaHei UI"),
            Cursor = Cursors.Hand, Margin = new Thickness(10, 0, 0, 0),
            Padding = new Thickness(18, 0, 18, 0)
        };
        b.Click += onClick;
        if (primary) {
            b.Foreground = Brushes.White;
            b.Background = accent;
            b.FontWeight = FontWeights.SemiBold;
            b.Template = RoundedTemplate(accent, true);
        } else {
            b.Foreground = Sub;
            b.Background = Brushes.White;
            b.BorderBrush = new SolidColorBrush(Color.FromRgb(0xDC, 0xE3, 0xF0));
            b.Template = RoundedTemplate(null, false);
        }
        return b;
    }

    static ControlTemplate RoundedTemplate(Brush accent, bool primary) {
        var cv = new FrameworkElementFactory(typeof(Border));
        cv.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        cv.Name = "bd";
        if (primary) cv.SetValue(Border.BackgroundProperty, accent);
        else {
            cv.SetValue(Border.BackgroundProperty, Brushes.White);
            cv.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0xDC, 0xE3, 0xF0)));
            cv.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        }
        var cp = new FrameworkElementFactory(typeof(ContentPresenter));
        cp.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cp.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        cv.AppendChild(cp);

        var t = new ControlTemplate(typeof(Button)) { VisualTree = cv };
        var hover = new Trigger { Property = IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(primary ? Color.FromRgb(0x3F, 0x7C, 0xF0) : Color.FromRgb(0xF3, 0xF7, 0xFF)), "bd"));
        t.Triggers.Add(hover);
        return t;
    }

    static readonly DependencyProperty IsMouseOverProperty = UIElement.IsMouseOverProperty;
}

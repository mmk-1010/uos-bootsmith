using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Reflection;
using System.Windows.Threading;

// ---------------- 入口 + 启动加载页 ----------------
public class App : Application {
    public static string ArgIso = null, ArgUimg = null;
    public static bool ArgDemo = false, ArgAuto = false;
    public static string ArgShot = null, ArgShotTab = null, ArgShotDialog = null, ArgShotSplash = null;
    public static string ArgFix = null; public static bool ArgFixRestore = false; public static int ArgShotDelay = 0;

    [STAThread]
    public static void Main(string[] args) {
        for (int i = 0; i < args.Length; i++) {
            string a = args[i];
            if (a == "--demo") ArgDemo = true;
            else if (a == "--auto") ArgAuto = true;
            else if (a == "--iso" && i + 1 < args.Length) ArgIso = args[++i];
            else if (a == "--uimg" && i + 1 < args.Length) ArgUimg = args[++i];
            else if (a == "--shot" && i + 1 < args.Length) ArgShot = args[++i];
            else if (a == "--shot-tab" && i + 1 < args.Length) ArgShotTab = args[++i];
            else if (a == "--shot-dialog" && i + 1 < args.Length) ArgShotDialog = args[++i];
            else if (a == "--shot-splash" && i + 1 < args.Length) ArgShotSplash = args[++i];
                    else if (a == "--fix" && i + 1 < args.Length) ArgFix = args[++i];
                    else if (a == "--fix-restore") ArgFixRestore = true;
                    else if (a == "--shot-delay" && i + 1 < args.Length) ArgShotDelay = int.Parse(args[++i]);
        }
        var app = new App();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        app.Run(new Splash());
    }

    public static void Enter(MainWindow w) {
        w.Show();
        Current.MainWindow = w;
        Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
        if (!string.IsNullOrEmpty(ArgShot)) StartShot(w);
    }

    public static string ResolveOemDir() {
        try {
            string b = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string p = Path.Combine(b, "payload\\oem");
            if (Directory.Exists(p)) return p;
            string q = Path.Combine(b, "oem");
            if (Directory.Exists(q)) return q;
        } catch { }
        return null;
    }

    public static string ResolvePatchFile() {
        string external = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"payload\patch\verify-patch.sh");
        if (File.Exists(external)) return external;
        try {
            var asm = Assembly.GetExecutingAssembly();
            using (var s = asm.GetManifestResourceStream("verify-patch.sh")) {
                if (s == null) return external;
                string dir = Path.Combine(Path.GetTempPath(), "UosUsbMaker");
                Directory.CreateDirectory(dir);
                string p = Path.Combine(dir, "verify-patch.sh");
                using (var fs = File.Create(p)) s.CopyTo(fs);
                return p;
            }
        } catch { return external; }
    }

    static void StartShot(MainWindow w) {
        if (ArgShotTab == "fix") {
            var m0 = typeof(MainWindow).GetMethod("SwitchPage", BindingFlags.Instance | BindingFlags.NonPublic);
            if (m0 != null) m0.Invoke(w, new object[] { "fix" });
        }

        int iv = ArgShotDelay > 0 ? ArgShotDelay : (ArgShotTab == "fix" ? 1600 : (!string.IsNullOrEmpty(ArgIso) ? 4800 : 900));
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(iv) };
        t.Tick += delegate {
            t.Stop();
            try {
                w.UpdateLayout();
                var dpi = VisualTreeHelper.GetDpi(w);
                var rtb = new RenderTargetBitmap((int)(w.ActualWidth * dpi.DpiScaleX), (int)(w.ActualHeight * dpi.DpiScaleY),
                                                 dpi.DpiScaleX * 96, dpi.DpiScaleY * 96, PixelFormats.Pbgra32);
                rtb.Render(w);
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(rtb));
                using (var fs = File.Create(ArgShot)) enc.Save(fs);
            } catch (Exception ex) {
                try { File.WriteAllText(ArgShot + ".err.txt", ex.ToString()); } catch { }
            }
            Current.Shutdown();
        };
        t.Start();
    }
}

public class Splash : Window {
    TextBlock _status;
    public Splash() {
        Width = 420; Height = 202;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = true;
        Title = "BootSmith 启动盘工具";

        var outer = new Border {
            CornerRadius = new CornerRadius(16), Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xE4, 0xEA, 0xF5)), BorderThickness = new Thickness(1),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 30, ShadowDepth = 3, Opacity = 0.16, Color = Colors.Black }
        };
        var root = new Grid { Margin = new Thickness(28, 24, 28, 22) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var head = new StackPanel { Orientation = Orientation.Horizontal };
        head.Children.Add(Theme.IconTile("\uE88E", 40, 11, 21));
        var titles = new StackPanel { Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(new TextBlock {
            Text = "BootSmith 启动盘工具", FontSize = 17, FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x16, 0x1B, 0x26))
        });
        titles.Children.Add(new TextBlock {
            Text = "UOS / 麒麟 / Debian 启动盘制作", FontSize = 11, Margin = new Thickness(0, 2, 0, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x94, 0xA8))
        });
        head.Children.Add(titles);
        Grid.SetRow(head, 0);
        root.Children.Add(head);

        var bar = new Border { Height = 4, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Color.FromRgb(0xEC, 0xF1, 0xFA)), Margin = new Thickness(0, 22, 0, 0), ClipToBounds = true };
        var glow = new Border { Width = 110, HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(2), Background = Theme.AccentBrush };
        bar.Child = glow;
        var anim = new DoubleAnimation { From = -110, To = 420, Duration = new Duration(TimeSpan.FromSeconds(1.1)), RepeatBehavior = RepeatBehavior.Forever };
        glow.BeginAnimation(WidthProperty, null);
        glow.RenderTransform = new TranslateTransform();
        ((TranslateTransform)glow.RenderTransform).BeginAnimation(TranslateTransform.XProperty, anim);
        Grid.SetRow(bar, 1);
        root.Children.Add(bar);

        _status = new TextBlock {
            Text = "正在初始化…", FontSize = 12.5, Margin = new Thickness(0, 16, 0, 0),
            Foreground = new SolidColorBrush(Color.FromRgb(0x5A, 0x64, 0x78)), TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(_status, 2);
        root.Children.Add(_status);

        outer.Child = root;
        Content = outer;

        ContentRendered += delegate {
            if (!string.IsNullOrEmpty(App.ArgShotSplash)) { ShotSplash(); return; }
            StartInit();
        };
    }

    void ShotSplash() {
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(420) };
        t.Tick += delegate {
            t.Stop();
            try {
                UpdateLayout();
                var dpi = VisualTreeHelper.GetDpi(this);
                var rtb = new RenderTargetBitmap((int)(ActualWidth * dpi.DpiScaleX), (int)(ActualHeight * dpi.DpiScaleY),
                                                 dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
                rtb.Render(this);
                var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb));
                using (var fs = File.Create(App.ArgShotSplash)) enc.Save(fs);
            } catch (Exception ex) { try { File.WriteAllText(App.ArgShotSplash + ".err.txt", ex.ToString()); } catch { } }
            Application.Current.Shutdown();
        };
        t.Start();
    }

    void Say(string s) { _status.Text = s; }

    void StartInit() {
        var t = new Thread(delegate () {
            try {
                Dispatcher.Invoke(new Action(delegate { Say("正在读取磁盘列表…"); }));
                var disks = Drives.ListUsb();
                Dispatcher.Invoke(new Action(delegate { Say(string.Format("发现 {0} 块 USB 磁盘，准备界面…", disks.Count)); }));
                Thread.Sleep(260);
                Dispatcher.Invoke(new Action(delegate {
                    var w = new MainWindow(disks, App.ArgIso, App.ArgUimg, App.ArgDemo, App.ArgAuto);
                    App.Enter(w);
                    Close();
                }));
            } catch (Exception ex) {
                Dispatcher.Invoke(new Action(delegate { Say("初始化失败：" + ex.Message); }));
            }
        });
        t.IsBackground = true;
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
    }
}

// ---------------- 主题 ----------------
public static class Theme {
    public static readonly Brush AccentBrush = new SolidColorBrush(Color.FromRgb(0x10, 0x12, 0x15));
    public static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(0x1B, 0x1F, 0x27));
    public static readonly Brush Ink2 = new SolidColorBrush(Color.FromRgb(0x5A, 0x64, 0x78));
    public static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(0x8A, 0x94, 0xA8));
    public static readonly Brush Ok = new SolidColorBrush(Color.FromRgb(0x2E, 0x9E, 0x62));
    public static readonly Brush Warn = new SolidColorBrush(Color.FromRgb(0xE0, 0x8A, 0x1E));
    public static readonly Brush Err = new SolidColorBrush(Color.FromRgb(0xD0, 0x45, 0x45));

    public static Border IconTile(string glyph, double size, double radius, double fontSize) {
        var b = new Border { Width = size, Height = size, CornerRadius = new CornerRadius(radius) };
        var g = new LinearGradientBrush(Color.FromRgb(0x2A, 0x30, 0x3A), Color.FromRgb(0x0B, 0x0E, 0x13), 45);
        g.StartPoint = new Point(0, 0); g.EndPoint = new Point(1, 1);
        b.Background = g;
        b.Child = new TextBlock {
            Text = glyph, FontFamily = glyph.Length == 1 && glyph[0] < 0xE000 ? new FontFamily("Segoe UI") : new FontFamily("Segoe MDL2 Assets"), FontSize = fontSize,
            Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        return b;
    }

    public static TextBlock Glyph(string code, double size, Brush brush) {
        return new TextBlock {
            Text = code, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = size,
            Foreground = brush, VerticalAlignment = VerticalAlignment.Center
        };
    }
}

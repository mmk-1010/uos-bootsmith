using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;

public class DiskVm {
    public string Num { get; set; }
    public string Title { get; set; }
    public string Info { get; set; }
    public string State { get; set; }
    public Brush StateBrush { get; set; }
    public UsbDisk Disk { get; set; }
}

public class VolVm {
    public string Root { get; set; }
    public string Title { get; set; }
    public string Info { get; set; }
}

public class MainWindow : Window {
    // ---------------- 状态 ----------------
    List<UsbDisk> _disks = new List<UsbDisk>();
    Thread _worker;
    volatile bool _cancel;
    volatile bool _busy;
    volatile bool _fixing;
    string _fixRootOverride;
    readonly Dictionary<string, string> _stateCache = new Dictionary<string, string>();
    System.DateTime _fixLast = System.DateTime.MinValue;
    string _isoPath = "";
    string _uimgPath = "";
    string _patchFile;
    DateTime _lastSample = DateTime.MinValue;
    long _lastDone;
    double _speed;
    DateTime _lastUi = DateTime.MinValue;
    string _page = "make";

    // ---------------- 控件 ----------------
    Button _tabMake, _tabFix;
    TextBlock _txtIso, _txtUimg;
    TextBox _txtP1, _logBox, _fixLog;
    TextBlock _lblIsoInfo, _lblUimgInfo, _txtScheme, _txtSchemeSub, _lblPhase, _lblPct, _lblSpeed, _lblEta;
    Border _badge, _track, _fill, _logCard;
    TextBlock _lblNoDisk;
    CheckBox _chkRemove;
    TextBlock _lblPatchState;
    ListBox _lstDisks, _lstVols;
    Button _btnStart, _btnRefresh, _btnClearUimg, _btnFixRemove, _btnFixRestore, _btnRefreshVols, _btnToggleLog, _btnCheckIso;
    TextBlock _lblFixState;
    Border _fixCard;
    FrameworkElement _host;

    bool _demo, _autoYes;
    public MainWindow(List<UsbDisk> pre, string iso, string uimg, bool demo, bool auto) {
        _disks = pre;
        _demo = demo; _autoYes = auto;
        _patchFile = App.ResolvePatchFile();

        Title = "BootSmith 启动盘工具";
        Width = 1120; Height = 862; MinWidth = 1000; MinHeight = 720;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF7));
        FontFamily = new FontFamily("Microsoft YaHei UI");
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;

        var host = (FrameworkElement)XamlReader.Parse(Ui.MainXaml);
        _host = host;
        Content = host;

        Grab();
        Wire();
        FillDisks(_disks);
        SwitchPage("make");
        if (_demo) Dispatcher.BeginInvoke(new Action(delegate { StartDemo(); }));
        else if (_autoYes && string.IsNullOrEmpty(App.ArgFix)) Dispatcher.BeginInvoke(new Action(delegate { StartOrCancel(); }));
        if (!string.IsNullOrEmpty(App.ArgFix)) {
            Dispatcher.BeginInvoke(new Action(delegate {
                _fixRootOverride = App.ArgFix;
                SwitchPage("fix");
                RunFix(!App.ArgFixRestore);
            }));
        }
        UpdateScheme();
        if (!string.IsNullOrEmpty(iso)) { _isoPath = iso; _txtIso.Text = iso; ShowIsoInfo(); CheckPatchCompatAsync(_isoPath); }
        if (!string.IsNullOrEmpty(uimg)) { _uimgPath = uimg; _txtUimg.Text = uimg; ShowUimgInfo(); }
        UpdateScheme();
        Log("就绪。选好镜像和目标 U 盘，点「开始制作」。");
    }

    void Grab() {
        var lbVer = (TextBlock)_host.FindName("LblVer"); if (lbVer != null) lbVer.Text = Ver.Text;
        _tabMake = (Button)_host.FindName("TabMake");
        _tabFix = (Button)_host.FindName("TabFix");
        _txtIso = (TextBlock)_host.FindName("TxtIso");
        _txtUimg = (TextBlock)_host.FindName("TxtUimg");
        _txtP1 = (TextBox)_host.FindName("TxtP1");
        _lblIsoInfo = (TextBlock)_host.FindName("LblIsoInfo");
        _lblUimgInfo = (TextBlock)_host.FindName("LblUimgInfo");
        _txtScheme = (TextBlock)_host.FindName("TxtScheme");
        _txtSchemeSub = (TextBlock)_host.FindName("TxtSchemeSub");
        _badge = (Border)_host.FindName("Badge");
        _chkRemove = (CheckBox)_host.FindName("ChkRemove");
        _lblPatchState = (TextBlock)_host.FindName("LblPatchState");
        _lstDisks = (ListBox)_host.FindName("LstDisks");
        _lstVols = (ListBox)_host.FindName("LstVols");
        _btnStart = (Button)_host.FindName("BtnStart");
        _btnRefresh = (Button)_host.FindName("BtnRefresh");
        _btnClearUimg = (Button)_host.FindName("BtnClearUimg");
        _lblNoDisk = (TextBlock)_host.FindName("LblNoDisk");
        _track = (Border)_host.FindName("Track");
        _fill = (Border)_host.FindName("Fill");
        _lblPhase = (TextBlock)_host.FindName("LblPhase");
        _lblPct = (TextBlock)_host.FindName("LblPct");
        _lblSpeed = (TextBlock)_host.FindName("LblSpeed");
        _lblEta = (TextBlock)_host.FindName("LblEta");
        _logBox = (TextBox)_host.FindName("LogBox");
        _logCard = (Border)_host.FindName("LogCard");
        _btnToggleLog = (Button)_host.FindName("BtnToggleLog");
        _fixLog = (TextBox)_host.FindName("FixLog");
        _fixCard = (Border)_host.FindName("FixCard");
        _lblFixState = (TextBlock)_host.FindName("LblFixState");
        _btnFixRemove = (Button)_host.FindName("BtnFixRemove");
        _btnFixRestore = (Button)_host.FindName("BtnFixRestore");
        _btnRefreshVols = (Button)_host.FindName("BtnRefreshVols");
        _btnCheckIso = (Button)_host.FindName("BtnCheckIso");
    }

    void Wire() {
        _tabMake.Click += delegate { SwitchPage("make"); };
        _tabFix.Click += delegate { SwitchPage("fix"); };

        ((Button)_host.FindName("BtnBrowseIso")).Click += delegate {
            var d = new OpenFileDialog();
            d.Filter = "UOS 安装镜像 (*.iso)|*.iso|所有文件 (*.*)|*.*";
            d.Title = "选择 UOS 安装镜像";
            if (d.ShowDialog() == true) {
                _isoPath = d.FileName;
                _txtIso.Text = _isoPath;
                ShowIsoInfo();
        CheckPatchCompatAsync(_isoPath);
                UpdateScheme();
            }
        };

        ((Button)_host.FindName("BtnBrowseUimg")).Click += delegate {
            var d = new OpenFileDialog();
            d.Filter = "整机备份镜像 (*.uimg)|*.uimg|所有文件 (*.*)|*.*";
            d.Title = "选择备份还原镜像";
            if (d.ShowDialog() == true) {
                _uimgPath = d.FileName;
                _txtUimg.Text = _uimgPath;
                ShowUimgInfo();
                UpdateScheme();
            }
        };

        _btnClearUimg.Click += delegate {
            _uimgPath = "";
            _txtUimg.Text = "";
            ShowUimgInfo();
            UpdateScheme();
        };

        _btnRefresh.Click += delegate { RefreshDisks(true); };
        _btnRefreshVols.Click += delegate { RefreshVolumes(); };
        _btnCheckIso.Click += delegate { CheckIsoCompat(); };
        _btnToggleLog.Click += delegate {
            _logCard.Visibility = _logCard.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        };
        _chkRemove.Checked += delegate { };
        _chkRemove.Unchecked += delegate { };

        _btnStart.Click += delegate { StartOrCancel(); };
        _btnFixRemove.Click += delegate { RunFix(true); };
        _btnFixRestore.Click += delegate { RunFix(false); };
        _lstVols.SelectionChanged += delegate { ShowVolState(); };
    }

    // ---------------- 页面 ----------------
    void SwitchPage(string p) {
        _page = p;
        var make = (FrameworkElement)_host.FindName("PageMake");
        var fix = (FrameworkElement)_host.FindName("PageFix");
        make.Visibility = p == "make" ? Visibility.Visible : Visibility.Collapsed;
        fix.Visibility = p == "fix" ? Visibility.Visible : Visibility.Collapsed;
        StyleOn(_tabMake, p == "make");
        StyleOn(_tabFix, p == "fix");
        if (_btnStart != null) _btnStart.IsEnabled = (p == "make") && !_fixing;
        AnimateIn(p == "make" ? make : fix);
        if (p == "fix") RefreshVolumes();
    }

    static void AnimateIn(FrameworkElement page) {
        if (page == null) return;
        var tt = page.RenderTransform as TranslateTransform;
        if (tt == null) { tt = new TranslateTransform(0, 0); page.RenderTransform = tt; }
        page.Opacity = 0;
        tt.Y = 12;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        page.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease });
        tt.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease });
    }

    void StyleOn(Button b, bool on) {
        b.Background = on ? (Brush)new BrushConverter().ConvertFromString("#101215") : Brushes.Transparent;
        b.Foreground = on ? Brushes.White : Theme.Ink2;
        b.FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
    }

    // ---------------- 镜像信息 ----------------
    void ShowIsoInfo() {
        try {
            var fi = new FileInfo(_isoPath);
            _lblIsoInfo.Text = string.Format("{0}  ·  {1:N2} GB", fi.Name, fi.Length / 1073741824.0);
            _lblIsoInfo.Foreground = Theme.Ok;
            ProbeIsoAsync(_isoPath);
        } catch { _lblIsoInfo.Text = "无法读取该文件"; _lblIsoInfo.Foreground = Theme.Err; }
    }

    // 后台识别发行版，补一句类型说明
    void ProbeIsoAsync(string iso) {
        var t = new Thread(delegate () {
            string desc = null; bool isUos = false;
            try {
                using (var img = IsoImage.Open(iso)) {
                    var p = IsoProfile.Detect(img, System.IO.Path.GetFileName(iso));
                    desc = p.Describe(); isUos = p.IsUos;
                }
            } catch { desc = null; }
            if (desc == null) return;
            Dispatcher.BeginInvoke(new Action(delegate () {
                if (_isoPath != iso) return;
                try {
                    var fi = new FileInfo(iso);
                    _lblIsoInfo.Text = string.Format("{0}  ·  {1:N2} GB  ·  {2}", fi.Name, fi.Length / 1073741824.0, desc);
                } catch { }
                ApplyIsoFamily(isUos);
            }));
        });
        t.IsBackground = true; t.Start();
    }

    void ShowUimgInfo() {
        if (string.IsNullOrEmpty(_uimgPath)) {
            _lblUimgInfo.Text = "未选择 → 单分区（整盘只做引导）";
            _lblUimgInfo.Foreground = Theme.Muted;
            return;
        }
        try {
            var fi = new FileInfo(_uimgPath);
            _lblUimgInfo.Text = string.Format("{0}  ·  {1:N2} GB", fi.Name, fi.Length / 1073741824.0);
            _lblUimgInfo.Foreground = Theme.Ok;
        } catch { _lblUimgInfo.Text = "无法读取该文件"; _lblUimgInfo.Foreground = Theme.Err; }
    }

    bool _isoIsUos = true;
    void ApplyIsoFamily(bool isUos) {
        if (_isoIsUos == isUos) return;
        _isoIsUos = isUos;
        if (!isUos) {
            _uimgPath = null;
            _txtUimg.Text = "（不适用 · 非 UOS 镜像）";
            _lblUimgInfo.Text = "这套镜像不需要备份区，直接用整盘 FAT32 引导";
            _lblUimgInfo.Foreground = Theme.Muted;
        }
        foreach (var n in new[] { "BtnBrowseUimg", "BtnClearUimg" }) {
            var b = _host.FindName(n) as Button;
            if (b != null) { b.IsEnabled = isUos; b.Opacity = isUos ? 1.0 : 0.45; }
        }
        UpdateScheme();
    }

    void UpdateScheme() {
        bool dual = !string.IsNullOrEmpty(_uimgPath);
        if (dual) {
            _txtScheme.Text = "双分区";
            _txtScheme.Foreground = (Brush)new BrushConverter().ConvertFromString("#2E3A4E");
            _txtSchemeSub.Text = "P1 FAT32 引导区  +  P2 NTFS 镜像区（clone\\）";
            _txtSchemeSub.Foreground = (Brush)new BrushConverter().ConvertFromString("#6E7C91");
            _badge.Background = (Brush)new BrushConverter().ConvertFromString("#EEF2F8");
            _badge.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#D8E1EE");
            _txtP1.IsEnabled = true;
        } else {
            _txtScheme.Text = "单分区";
            _txtScheme.Foreground = (Brush)new BrushConverter().ConvertFromString("#9A6A10");
            _txtSchemeSub.Text = "整盘一个 FAT32 分区，只放安装文件";
            _txtSchemeSub.Foreground = (Brush)new BrushConverter().ConvertFromString("#A08556");
            _badge.Background = (Brush)new BrushConverter().ConvertFromString("#FFF7E8");
            _badge.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#F1DFBE");
            _txtP1.IsEnabled = false;
        }
    }

    // ---------------- 磁盘 ----------------
    void FillDisks(List<UsbDisk> disks) {
        var vms = new List<DiskVm>();
        foreach (var d in disks) {
            var v = new DiskVm();
            v.Num = "Disk " + d.Number;
            v.Title = d.Model;
            v.Info = string.Format("{0} GB · USB{1}", d.SizeGB, d.Letters.Count > 0 ? " · " + string.Join(" ", d.Letters.ToArray()) : "");
            if (d.IsSystem) { v.State = "系统盘，不可用"; v.StateBrush = Theme.Muted; }
            else if (d.SizeGB < 16) { v.State = "容量偏小"; v.StateBrush = Theme.Warn; }
            else { v.State = "可用"; v.StateBrush = Theme.Ok; }
            v.Disk = d;
            vms.Add(v);
        }
        if (_lblNoDisk != null) _lblNoDisk.Visibility = vms.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var keep = _lstDisks.SelectedItem as DiskVm;
        _lstDisks.ItemsSource = vms;
        if (vms.Count > 0) {
            if (keep != null) {
                foreach (var v in vms) if (v.Disk.Number == keep.Disk.Number) _lstDisks.SelectedItem = v;
            }
            if (_lstDisks.SelectedItem == null) {
                foreach (var v in vms) if (v.State == "可用") { _lstDisks.SelectedItem = v; break; }
            }
        }
        if (vms.Count == 0) Log("没有检测到 USB 磁盘，请插上 U 盘后点「刷新」。");
    }

    void RefreshDisks(bool manual) {
        if (_busy) return;
        if (manual) Log("正在刷新磁盘列表…");
        var t = new Thread(delegate () {
            var list = Drives.ListUsb();
            Dispatcher.BeginInvoke(new Action(delegate () {
                _disks = list;
                FillDisks(list);
                if (manual) Log(string.Format("刷新完成，发现 {0} 块 USB 磁盘。", list.Count));
            }));
        });
        t.IsBackground = true;
        t.Start();
    }

    // ---------------- 日志 ----------------
    void Log(string s) {
        if (_logBox == null) return;
        _logBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + s + "\r\n");
        _logBox.ScrollToEnd();
    }

    // 主界面：选完 ISO 自动检测这个镜像能不能打「移除校验」补丁
    void CheckPatchCompatAsync(string iso) {
        if (_lblPatchState == null) return;
        if (string.IsNullOrEmpty(iso) || !File.Exists(iso)) { _lblPatchState.Text = ""; return; }
        _lblPatchState.Foreground = Theme.Muted;
        _lblPatchState.Text = "检测中…";
        _chkRemove.IsEnabled = false;
        var th = new Thread(delegate () {
            bool ok = false;
            try {
                string patch = File.ReadAllText(_patchFile);
                ok = IsoCheck.Compatible(iso, patch);
            } catch { ok = false; }
            Dispatcher.BeginInvoke(new Action(delegate {
                if (ok) {
                    _lblPatchState.Text = "✓ 该镜像可移除校验";
                    _lblPatchState.Foreground = Theme.Ok;
                    _chkRemove.IsEnabled = true;
                    _chkRemove.IsChecked = true;
                } else {
                    _lblPatchState.Text = "✗ 该镜像不支持，已自动取消";
                    _lblPatchState.Foreground = Theme.Warn;
                    _chkRemove.IsEnabled = false;
                    _chkRemove.IsChecked = false;
                }
            }));
        });
        th.IsBackground = true;
        th.Start();
    }

    void CheckIsoCompat() {
        var dlg = new Microsoft.Win32.OpenFileDialog();
        dlg.Title = "选一个 ISO 做兼容性体检";
        dlg.Filter = "UOS 安装镜像 (*.iso)|*.iso|所有文件 (*.*)|*.*";
        if (dlg.ShowDialog(this) != true) return;
        string iso = dlg.FileName;
        _btnCheckIso.IsEnabled = false;
        FixLogLine("开始体检：" + System.IO.Path.GetFileName(iso));
        var th = new Thread(delegate () {
            string patch = "";
            try { patch = File.ReadAllText(_patchFile); } catch { }
            bool ok = false;
            var log = new Action<string>(delegate (string msg) { Dispatcher.BeginInvoke(new Action(delegate { FixLogLine(msg); })); });
            string rep;
            try { rep = IsoCheck.Run(iso, patch, out ok, log); }
            catch (Exception ex) { rep = "体检出错：" + ex.Message; }
            Dispatcher.BeginInvoke(new Action(delegate {
                _fixLog.AppendText(rep + "\r\n");
                _fixLog.ScrollToEnd();
                _btnCheckIso.IsEnabled = true;
            }));
        });
        th.IsBackground = true;
        th.Start();
    }

    void FixLogLine(string s) {
        _fixLog.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + s + "\r\n");
        _fixLog.ScrollToEnd();
    }

    // ---------------- 制作 ----------------
    void StartOrCancel() {
        if (_busy) { _cancel = true; _lblPhase.Text = "正在取消…"; return; }
        if (_demo) { StartDemo(); return; }

        if (string.IsNullOrEmpty(_isoPath) || !File.Exists(_isoPath)) { Dlg.Info(this, "还没选镜像", "请先选一个 UOS 安装镜像（.iso 文件）。"); return; }
        var sel = _lstDisks.SelectedItem as DiskVm;
        if (sel == null) { Dlg.Info(this, "还没选 U 盘", "请在下面的列表里选中要写入的 U 盘。"); return; }
        if (sel.Disk.IsSystem) { Dlg.Info(this, "这块是系统盘", "系统盘不能拿来做安装盘，请换一块 U 盘。", MsgKind.Warn); return; }

        int p1 = 16384;
        int.TryParse(_txtP1.Text.Trim(), out p1);
        if (p1 < 8192) p1 = 8192;
        if (p1 > 65536) p1 = 65536;

        bool dual = !string.IsNullOrEmpty(_uimgPath);
        string msg = string.Format(
            "将清空 Disk {0}（{1}，{2} GB）上的全部数据。\n\n安装镜像：{3}\n备份镜像：{4}\n分区方案：{5}\n\n确认继续吗？",
            sel.Disk.Number, sel.Disk.Model, sel.Disk.SizeGB, Path.GetFileName(_isoPath),
            dual ? Path.GetFileName(_uimgPath) : "（无）", dual ? "双分区" : "单分区");
        if (!_autoYes && !Dlg.Confirm(this, "确认制作", msg, "开始制作", "再想想")) return;

        var o = new JobOptions();
        o.IsoPath = _isoPath;
        o.UimgPath = dual ? _uimgPath : null;
        o.DiskNumber = sel.Disk.Number;
        o.P1SizeMb = p1;
        o.RemoveVerify = _chkRemove.IsChecked == true;
        o.PatchFile = _patchFile;

        _busy = true; _cancel = false;
        _btnStart.Content = "取消";
        SetUiEnabled(false);
        _logCard.Visibility = Visibility.Visible;
        ResetProgress();
        Log("开始制作：Disk " + o.DiskNumber + (dual ? "（双分区）" : "（单分区）"));

        var job = new MakeJob();
        job.OnLog = delegate (string s) {
            Dispatcher.BeginInvoke(new Action(delegate { Log(s); }));
        };
        job.OnProgress = delegate (ProgressInfo p) {
            Dispatcher.BeginInvoke(new Action(delegate { UpdateProgress(p); }));
        };
        job.Cancelled = delegate { return _cancel; };

        _worker = new Thread(delegate () {
            JobResult r = null;
            string err = null;
            try { r = job.Run(o); }
            catch (OperationCanceledException) { err = "__CANCELLED__"; }
            catch (Exception ex) { err = ex.Message; }
            Dispatcher.BeginInvoke(new Action(delegate { FinishJob(r, err); }));
        });
        _worker.IsBackground = true;
        _worker.Start();
    }

    void StartDemo() {
        _busy = true; _cancel = false;
        _btnStart.Content = "取消";
        SetUiEnabled(false);
        _logCard.Visibility = Visibility.Visible;
        ResetProgress();
        Log("演示模式：模拟写入 5.12 GB 安装镜像（不碰任何磁盘）");
        long total = 5492646953L;
        var t = new Thread(delegate () {
            long done = 0;
            var rnd = new Random(7);
            while (done < total) {
                if (_cancel) {
                    Dispatcher.BeginInvoke(new Action(delegate { FinishJob(null, "__CANCELLED__"); }));
                    return;
                }
                Thread.Sleep(150);
                double v = 110 + rnd.NextDouble() * 60;
                done += (long)(v * 1048576 * 0.15);
                if (done > total) done = total;
                var p = new ProgressInfo();
                p.Phase = done < total * 0.92 ? "正在写入安装文件" : "正在回读校验";
                p.Detail = "演示进度";
                p.Done = done; p.Total = total; p.Indeterminate = false;
                Dispatcher.BeginInvoke(new Action(delegate { UpdateProgress(p); }));
            }
            var r = new JobResult();
            r.Ok = true; r.P1 = "X"; r.Files = 750; r.Bytes = total;
            r.Summary = "演示完成";
            Dispatcher.BeginInvoke(new Action(delegate { FinishJob(r, null); }));
        });
        t.IsBackground = true;
        t.Start();
    }

    void SetUiEnabled(bool on) {
        _btnRefresh.IsEnabled = on;
        _tabFix.IsEnabled = on;
        _chkRemove.IsEnabled = on;
        _txtP1.IsEnabled = on && !string.IsNullOrEmpty(_uimgPath);
        foreach (var b in new[] { "BtnBrowseIso", "BtnBrowseUimg" })
            ((Button)_host.FindName(b)).IsEnabled = on;
        _btnClearUimg.IsEnabled = on;
        _lstDisks.IsEnabled = on;
    }

    void ResetProgress() {
        _fill.Width = 0;
        _lblPct.Text = "0%";
        _lblSpeed.Text = "速度 —";
        _lblEta.Text = "剩余 —";
        _lblPhase.Text = "准备中…";
        _lastDone = 0; _speed = 0; _lastSample = DateTime.Now;
    }

    void UpdateProgress(ProgressInfo p) {
        if (!string.IsNullOrEmpty(p.Phase)) _lblPhase.Text = p.Phase;
        double pct = p.Total > 0 ? (double)p.Done / p.Total : 0;
        if (p.Indeterminate && pct <= 0) {
            _lblPct.Text = "处理中";
        } else {
            int v = (int)Math.Round(pct * 100);
            if (v > 100) v = 100;
            _lblPct.Text = v + "%";
            _fill.Width = Math.Max(0, _track.ActualWidth * pct);
        }

        if (p.SpeedBytes > 0) {
            _lblSpeed.Text = "速度 " + FmtSpeed(p.SpeedBytes);
            _lblEta.Text = p.EtaBytes > 0 ? ("剩余约 " + FmtTime(p.EtaBytes / p.SpeedBytes)) : "";
            if (!string.IsNullOrEmpty(p.Detail)) _lblEta.ToolTip = p.Detail;
            return;
        }

        var now = DateTime.Now;
        double since = (now - _lastSample).TotalSeconds;
        if (since >= 0.5) {
            long delta = p.Done - _lastDone;
            if (delta > 0) {
                double inst = delta / since;
                _speed = _speed <= 0 ? inst : _speed * 0.65 + inst * 0.35;
            }
            _lastDone = p.Done; _lastSample = now;
        }
        if (_speed > 0) {
            _lblSpeed.Text = "速度 " + FmtSpeed(_speed);
            if (p.Total > 0 && p.Done < p.Total) {
                double left = (p.Total - p.Done) / _speed;
                _lblEta.Text = "剩余约 " + FmtTime(left);
            } else _lblEta.Text = "";
        }
        if (!string.IsNullOrEmpty(p.Detail)) {
            _lblEta.ToolTip = p.Detail;
        }
    }

    static string FmtSpeed(double bps) {
        if (bps >= 1073741824) return (bps / 1073741824.0).ToString("F2") + " GB/s";
        if (bps >= 1048576) return (bps / 1048576.0).ToString("F1") + " MB/s";
        if (bps >= 1024) return (bps / 1024.0).ToString("F0") + " KB/s";
        return bps.ToString("F0") + " B/s";
    }

    static string FmtTime(double s) {
        if (s < 1) return "1 秒";
        if (s < 60) return ((int)Math.Round(s)) + " 秒";
        int m = (int)(s / 60), sec = (int)(s % 60);
        return m + " 分 " + sec + " 秒";
    }

    void FinishJob(JobResult r, string err) {
        _busy = false;
        _btnStart.Content = "开始制作";
        SetUiEnabled(true);

        if (err == "__CANCELLED__") {
            Log("已取消。磁盘可能处于半完成状态，重新选盘再做一次即可。");
            _lblPhase.Text = "已取消";
            _fill.Width = 0; _lblPct.Text = "—";
            Dlg.Info(this, "已取消", "这次操作已中止。磁盘可能停在半完成状态，重新选盘再做一次就行。", MsgKind.Warn);
        } else if (err != null) {
            Log("失败：" + err);
            _lblPhase.Text = "失败";
            Dlg.Info(this, "制作失败", err, MsgKind.Error);
        } else {
            Log("完成：" + r.Summary);
            if (r.P2 != null) Log("  安装区 " + r.P1 + ":  镜像区 " + r.P2 + ":\\clone");
            _lblPhase.Text = "完成";
            _fill.Width = _track.ActualWidth;
            _lblPct.Text = "100%";
            RefreshDisks(false);
            Dlg.Info(this, "制作完成",
                r.Summary + "\n\n开机按 F12 / F11 选这块 U 盘启动，" + (r.P2 != null ? "安装方式选「从整机备份安装」。" : "直接进入安装程序。"),
                MsgKind.Done);
        }
        // 清掉可能残留的文件句柄，避免下一次操作踩到
        _worker = null;
    }

    // ---------------- 去校验 ----------------
    void RefreshVolumes() {
        var t = new Thread(delegate () {
            var vols = Drives.Fat32VolumesWithLive();
            Dispatcher.BeginInvoke(new Action(delegate () {
                var vms = new List<VolVm>();
                foreach (var v in vols) {
                    var vm = new VolVm();
                    vm.Root = v;
                    vm.Title = v.TrimEnd('\\');
                    try {
                        var di = new DriveInfo(v);
                        vm.Info = string.Format("{0} · {1:N1} GB 可用", di.VolumeLabel, di.AvailableFreeSpace / 1073741824.0);
                    } catch { vm.Info = ""; }
                    vms.Add(vm);
                }
                _lstVols.ItemsSource = vms;
                if (vms.Count > 0) _lstVols.SelectedIndex = 0;
                else _lblFixState.Text = "没有找到 UOS 安装盘（需要有 live\\filesystem.squashfs）。";
                _btnFixRemove.IsEnabled = vms.Count > 0;
                _btnFixRestore.IsEnabled = vms.Count > 0;
            }));
        });
        t.IsBackground = true;
        t.Start();
    }

    void ShowVolState() {
        var vm = _lstVols.SelectedItem as VolVm;
        if (vm == null) return;
        string root = vm.Root;
        string cached;
        if (_stateCache.TryGetValue(root, out cached)) { _lblFixState.Text = cached; return; }
        _lblFixState.Text = "正在检测…";
        var t = new Thread(delegate () {
            string s2 = CheckState(root);
            _stateCache[root] = s2;
            Dispatcher.BeginInvoke(new Action(delegate { _lblFixState.Text = s2; }));
        });
        t.IsBackground = true;
        t.Start();
    }

    string CheckState(string root) {
        var parts = new List<string>();
        foreach (var name in new[] { "initrd.lz", "initrd-5.10.0.lz" }) {
            string f = Path.Combine(root, "live", name);
            if (!File.Exists(f)) { parts.Add(name + " 缺失"); continue; }
            try {
                var ir = InitrdFile.Load(f);
                string txt = Cpio.GetText(ir.CpioBody, "scripts/init-bottom/deepin-squashfs-verify.sh");
                if (txt == null) parts.Add(name + " 无校验脚本");
                else if (txt.IndexOf("no-verify patch", StringComparison.Ordinal) >= 0) parts.Add(name + " 已打补丁");
                else parts.Add(name + " 原厂");
            } catch { parts.Add(name + " 无法解析"); }
        }
        return string.Join("    ", parts.ToArray());
    }

    void FixReport(long done, long total, string phase, string detail, bool force, double speed = -1, long etaBytes = -1) {
        var now = System.DateTime.Now;
        if (!force && (now - _fixLast).TotalMilliseconds < 100) return;
        _fixLast = now;
        Dispatcher.BeginInvoke(new Action(delegate () {
            var p = new ProgressInfo();
            p.Phase = string.IsNullOrEmpty(detail) ? phase : phase + " · " + detail; p.Detail = detail; p.Done = done; p.Total = total;
            p.SpeedBytes = speed; p.EtaBytes = etaBytes;
            UpdateProgress(p);
        }));
    }

    static string Md5Of(string file) {
        using (var md5 = System.Security.Cryptography.MD5.Create())
        using (var fs = File.OpenRead(file)) {
            byte[] h = md5.ComputeHash(fs);
            var sb = new System.Text.StringBuilder();
            foreach (byte b in h) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    static void SetMd5Line(string md5file, string rel, string hash) {
        if (!File.Exists(md5file)) return;
        var lines = new System.Collections.Generic.List<string>(File.ReadAllLines(md5file));
        string pat = @"^\s*[0-9a-fA-F]{32}\s+\*?\.?/?" + rel.Replace(".", "\\.").Replace("/", "/") + @"\s*$";
        bool hit = false;
        for (int i = 0; i < lines.Count; i++) {
            if (System.Text.RegularExpressions.Regex.IsMatch(lines[i], pat)) {
                lines[i] = hash + "  " + rel; hit = true;
            }
        }
        if (hit) File.WriteAllLines(md5file, lines.ToArray(), new System.Text.UTF8Encoding(false));
    }

    void RunFix(bool remove) {
        string root;
        if (_fixRootOverride != null) root = _fixRootOverride;
        else {
            var vm0 = _lstVols.SelectedItem as VolVm;
            if (vm0 == null) return;
            root = vm0.Root;
        }
        if (_fixing) return;
        if (remove && !File.Exists(_patchFile)) { Dlg.Info(this, "缺少补丁文件", _patchFile, MsgKind.Error); return; }
        if (_fixRootOverride == null && !_autoYes && !Dlg.Confirm(this, remove ? "移除引导镜像校验" : "恢复原厂校验",
                          (remove ? "将对下面这块盘改写引导校验逻辑，原文件会备份为 *.orig：\n" : "将把 *.orig 还原回去：\n") + root,
                          remove ? "开始处理" : "开始还原", "取消")) return;

        string patch = remove && File.Exists(_patchFile) ? File.ReadAllText(_patchFile) : null;
        string phase = remove ? "正在移除引导校验" : "正在恢复原厂校验";

        var names = new System.Collections.Generic.List<string>();
        long totalAll = 0;
        foreach (var name in new[] { "initrd.lz", "initrd-5.10.0.lz" }) {
            string f = Path.Combine(root, "live", name);
            if (!File.Exists(f)) continue;
            names.Add(f);
            totalAll += new FileInfo(f).Length;
        }
        if (names.Count == 0) { Dlg.Info(this, "没找到 initrd", "这个盘里没有 live\\initrd.lz，可能不是 UOS 安装盘。", MsgKind.Warn); return; }

        _btnFixRemove.IsEnabled = false; _btnFixRestore.IsEnabled = false;
        _btnStart.IsEnabled = false;
        _fixing = true;
        _fixLast = System.DateTime.MinValue;
        ResetProgress();
        _lblPhase.Text = phase;
        _lblSpeed.Text = "速度 —";
        _lblEta.Text = "剩余 —";

        var t = new Thread(delegate () {
            try {
                long baseDone = 0;
                foreach (string f in names) {
                    string name = Path.GetFileName(f);
                    long fsz = new FileInfo(f).Length;
                    long b0 = baseDone;
                    var swFile = System.Diagnostics.Stopwatch.StartNew();
                    Dispatcher.BeginInvoke(new Action(delegate { FixLogLine((remove ? "处理 " : "还原 ") + name + " …"); }));

                    if (remove) {
                        var onRead = new Action<long, long>(delegate (long d, long tt) {
                            double sp = swFile.Elapsed.TotalSeconds > 0 ? d / swFile.Elapsed.TotalSeconds : -1;
                            FixReport(b0 + (long)(fsz * 0.30 * (tt > 0 ? (double)d / tt : 0)), totalAll, phase, "读取 " + name, false, sp, tt - d);
                        });
                        var onInf = new Action<long, long>(delegate (long d, long tt) {
                            double r = tt > 0 ? (double)d / tt : 0;
                            if (r > 1) r = 1;
                            double sp = swFile.Elapsed.TotalSeconds > 0 ? d / swFile.Elapsed.TotalSeconds : -1;
                            FixReport(b0 + (long)(fsz * (0.30 + 0.40 * r)), totalAll, phase, "解包 " + name, false, sp, tt - d);
                        });
                        var ir = InitrdFile.Load(f, onRead, onInf);
                        int[] loc = Cpio.Locate(ir.CpioBody, "scripts/init-bottom/deepin-squashfs-verify.sh");
                        if (loc[0] < 0) {
                            Dispatcher.BeginInvoke(new Action(delegate { FixLogLine("  找不到校验脚本，跳过"); }));
                            baseDone += fsz; continue;
                        }
                        string old = Cpio.GetText(ir.CpioBody, "scripts/init-bottom/deepin-squashfs-verify.sh");
                        if (old != null && old.IndexOf("no-verify patch", StringComparison.Ordinal) >= 0) {
                            Dispatcher.BeginInvoke(new Action(delegate { FixLogLine("  已是补丁版本"); }));
                            baseDone += fsz; continue;
                        }
                        byte[] data = System.Text.Encoding.UTF8.GetBytes(patch);
                        if (data.Length > loc[1]) throw new Exception("补丁脚本过长（槽位 " + loc[1] + " 字节）");
                        Cpio.ReplaceAt(ir.CpioBody, loc[0], loc[1], data);
                        if (!File.Exists(f + ".orig")) File.Copy(f, f + ".orig", true);
                        var swW = System.Diagnostics.Stopwatch.StartNew();
                        long expOut = (long)(fsz * 0.86);
                        var onWrite = new Action<long, long>(delegate (long d, long tt) {
                            double r = expOut > 0 ? (double)d / expOut : 0;
                            if (r > 1) r = 1;
                            double sp = swW.Elapsed.TotalSeconds > 0 ? d / swW.Elapsed.TotalSeconds : -1;
                            long eta = expOut > d ? expOut - d : 0;
                            FixReport(b0 + (long)(fsz * (0.70 + 0.30 * r)), totalAll, phase, "写入 " + name, false, sp, eta);
                        });
                        string tmp = f + ".new";
                        ir.Save(tmp, onWrite);
                        if (File.Exists(f)) File.Delete(f);
                        File.Move(tmp, f);
                        SetMd5Line(Path.Combine(root, "md5sum.txt"), "./live/" + name, Md5Of(f));
                        FixReport(b0 + fsz, totalAll, phase, "完成 " + name, true);
                        Dispatcher.BeginInvoke(new Action(delegate { FixLogLine("  已打补丁（槽位 " + loc[1] + " B）"); }));
                    } else {
                        string bak = f + ".orig";
                        if (!File.Exists(bak)) {
                            Dispatcher.BeginInvoke(new Action(delegate { FixLogLine("  没有 .orig 备份，跳过"); }));
                            baseDone += fsz; continue;
                        }
                        string tmp2 = f + ".new";
                        InitrdFile.CopyWithProgress(bak, tmp2, delegate (long d, long tt) {
                            double sp = swFile.Elapsed.TotalSeconds > 0 ? d / swFile.Elapsed.TotalSeconds : -1;
                            FixReport(b0 + (long)(fsz * (tt > 0 ? (double)d / tt : 0)), totalAll, phase, "还原 " + name, false, sp, tt - d);
                        });
                        if (File.Exists(f)) File.Delete(f);
                        File.Move(tmp2, f);
                        SetMd5Line(Path.Combine(root, "md5sum.txt"), "./live/" + name, Md5Of(f));
                        FixReport(b0 + fsz, totalAll, phase, "完成 " + name, true);
                        Dispatcher.BeginInvoke(new Action(delegate { FixLogLine("  已还原"); }));
                    }
                    baseDone += fsz;
                }

                Dispatcher.BeginInvoke(new Action(delegate {
                    FixLogLine("完成");
                    _lblPhase.Text = "完成";
                    _lblPct.Text = "100%";
                    _fill.Width = Math.Max(0, _track.ActualWidth);
                    _lblEta.Text = "";
                    _btnFixRemove.IsEnabled = true; _btnFixRestore.IsEnabled = true; _btnStart.IsEnabled = true;
                    _fixing = false;
                    _stateCache.Remove(root);
                    ShowVolState();
                }));
            } catch (Exception ex) {
                Dispatcher.BeginInvoke(new Action(delegate {
                    FixLogLine("失败：" + ex.Message);
                    _lblPhase.Text = "失败";
                    _btnFixRemove.IsEnabled = true; _btnFixRestore.IsEnabled = true; _btnStart.IsEnabled = true;
                    _fixing = false;
                    _stateCache.Remove(root);
                }));
            }
        });
        t.IsBackground = true;
        t.Start();
    }
}

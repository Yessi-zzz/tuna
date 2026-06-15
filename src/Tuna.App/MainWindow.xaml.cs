using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using Tuna.App.Localization;
using Tuna.App.ViewModels;
using Tuna.Core.Hardware;
using Application = System.Windows.Application;

namespace Tuna.App;

public partial class MainWindow : Window
{
    private NotifyIcon? _tray;
    private bool _reallyExit;
    private IntPtr _hwnd;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => { EnableDarkTitleBar(); RegisterHotkeys(); };
        Loaded += (_, _) => SetupTray();
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) Hide(); };  // 最小化 → 收进托盘
        Closing += OnClosing;
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    // ===================== 系统托盘常驻 =====================
    private void SetupTray()
    {
        _tray = new NotifyIcon { Text = "Tuna", Visible = true };
        try
        {
            var info = Application.GetResourceStream(new Uri("Assets/Tuna.ico", UriKind.Relative));
            if (info?.Stream is { } s) _tray.Icon = new System.Drawing.Icon(s);
        }
        catch { /* 没图标不致命 */ }

        _tray.DoubleClick += (_, _) => ShowFromTray();
        BuildTrayMenu();
        Localizer.Instance.LanguageChanged += BuildTrayMenu;   // 切语言 → 重建菜单文案
    }

    private void BuildTrayMenu()
    {
        if (_tray is null) return;
        var loc = Localizer.Instance;
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem(loc["Tray.Show"], null, (_, _) => ShowFromTray()));
        menu.Items.Add(new ToolStripSeparator());
        foreach (var (mode, key) in new[]
                 {
                     (PowerMode.Quiet, "Mode.Quiet"),
                     (PowerMode.Balanced, "Mode.Balanced"),
                     (PowerMode.Performance, "Mode.Performance"),
                     (PowerMode.Custom, "Mode.Custom"),
                 })
        {
            var m = mode;
            menu.Items.Add(new ToolStripMenuItem(loc[key], null, (_, _) => Vm?.SwitchTo(m)));
        }
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem(loc["Tray.Exit"], null, (_, _) => { _reallyExit = true; Close(); }));
        _tray.ContextMenuStrip = menu;
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_reallyExit)
        {
            UnregisterHotkeys();
            _tray?.Dispose();
            return;
        }
        e.Cancel = true;   // 关闭按钮 → 收进托盘常驻,不退出(托盘菜单"退出"才真退)
        Hide();
    }

    // ===================== 全局快捷键 Ctrl+Alt+1/2/3/4 =====================
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001, MOD_CONTROL = 0x0002;

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private void RegisterHotkeys()
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        if (_hwnd == IntPtr.Zero) return;
        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
        for (var i = 0; i < 4; i++)
            RegisterHotKey(_hwnd, i + 1, MOD_CONTROL | MOD_ALT, (uint)(0x31 + i));  // 0x31='1'
    }

    private void UnregisterHotkeys()
    {
        if (_hwnd == IntPtr.Zero) return;
        for (var i = 1; i <= 4; i++) UnregisterHotKey(_hwnd, i);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            PowerMode? mode = wParam.ToInt32() switch
            {
                1 => PowerMode.Quiet,
                2 => PowerMode.Balanced,
                3 => PowerMode.Performance,
                4 => PowerMode.Custom,
                _ => null,
            };
            if (mode is { } m) { Vm?.SwitchTo(m); handled = true; }
        }
        return IntPtr.Zero;
    }

    // ===================== 深色标题栏 =====================
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private void EnableDarkTitleBar()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        var useDark = 1;
        try { DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int)); }
        catch { /* 旧系统不支持则忽略 */ }
    }
}

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using Tuna.App.Localization;
using Tuna.App.Services;
using Tuna.Core.Abstractions;
using Tuna.Core.Hardware;

namespace Tuna.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly IPowerController _controller;
    private readonly IDeviceFeatures? _device;
    private readonly SensorService _sensors = new();
    private readonly DisplayService _display = new();
    private readonly PowerSourceService _power = new();
    private readonly AppSettings _settings = SettingsService.Load();
    private readonly DispatcherTimer _sensorTimer;
    private readonly Localizer _loc = Localizer.Instance;

    public ObservableCollection<CapabilityComparisonRow> CpuRows { get; } = new();
    public ObservableCollection<CapabilityComparisonRow> GpuRows { get; } = new();
    public ObservableCollection<ModeOption> Modes { get; } = new();
    public ObservableCollection<FanCurveRow> FanCurves { get; } = new();
    public ObservableCollection<RefreshRateOption> RefreshRates { get; } = new();

    private bool _hasFanCurves;
    public bool HasFanCurves { get => _hasFanCurves; private set => Set(ref _hasFanCurves, value); }

    private bool _hasDisplayCard;
    public bool HasDisplayCard { get => _hasDisplayCard; private set => Set(ref _hasDisplayCard, value); }

    private bool _supportsOverdrive;
    public bool SupportsOverdrive { get => _supportsOverdrive; private set => Set(ref _supportsOverdrive, value); }

    private bool _overdrive;
    public bool Overdrive
    {
        get => _overdrive;
        set
        {
            if (_overdrive == value) return;
            _overdrive = value;
            Raise(nameof(Overdrive));
            if (_device is null) return;
            try
            {
                _device.SetPanelOverdrive(value);
                _overdrive = _device.GetPanelOverdrive();   // 回读确认
                Raise(nameof(Overdrive));
                Status = string.Format(_loc["Status.OdSet"], _loc[_overdrive ? "Disp.On" : "Disp.Off"]);
            }
            catch (Exception ex) { Status = string.Format(_loc["Status.SwitchFailed"], ex.Message); }
        }
    }

    private bool _hasGpuInfo;
    public bool HasGpuInfo { get => _hasGpuInfo; private set => Set(ref _hasGpuInfo, value); }

    private string _gpuModeLabel = string.Empty;
    public string GpuModeLabel { get => _gpuModeLabel; private set => Set(ref _gpuModeLabel, value); }
    private int _gpuModeRaw = -1;

    private string _gsyncLabel = string.Empty;
    public string GSyncLabel { get => _gsyncLabel; private set => Set(ref _gsyncLabel, value); }

    private bool _autoSwitch;
    /// <summary>插拔电自动切档:接电→性能,断电→均衡。</summary>
    public bool AutoSwitch
    {
        get => _autoSwitch;
        set
        {
            if (!Set(ref _autoSwitch, value)) return;
            _settings.AutoSwitch = value;
            SettingsService.Save(_settings);
            if (value) ApplyAutoMode(_power.IsOnAc());
        }
    }

    // ---- 系统开关(关 Win 键 / 触控板) ----
    private bool _hasSystemToggles;
    public bool HasSystemToggles { get => _hasSystemToggles; private set => Set(ref _hasSystemToggles, value); }

    private bool _supportsWinKey;
    public bool SupportsWinKey { get => _supportsWinKey; private set => Set(ref _supportsWinKey, value); }

    private bool _winKeyDisabled;
    public bool WinKeyDisabled
    {
        get => _winKeyDisabled;
        set
        {
            if (_winKeyDisabled == value) return;
            _winKeyDisabled = value; Raise(nameof(WinKeyDisabled));
            if (_device is null) return;
            try
            {
                _device.SetWinKeyDisabled(value);
                _winKeyDisabled = _device.GetWinKeyDisabled(); Raise(nameof(WinKeyDisabled));
                Status = string.Format(_loc["Status.SysToggle"], _loc["Sys.WinKey"], _loc[_winKeyDisabled ? "Disp.On" : "Disp.Off"]);
            }
            catch (Exception ex) { Status = string.Format(_loc["Status.SwitchFailed"], ex.Message); }
        }
    }

    private bool _supportsTouchpad;
    public bool SupportsTouchpad { get => _supportsTouchpad; private set => Set(ref _supportsTouchpad, value); }

    private bool _touchpadEnabled = true;
    public bool TouchpadEnabled
    {
        get => _touchpadEnabled;
        set
        {
            if (_touchpadEnabled == value) return;
            _touchpadEnabled = value; Raise(nameof(TouchpadEnabled));
            if (_device is null) return;
            try
            {
                _device.SetTouchpadEnabled(value);
                _touchpadEnabled = _device.GetTouchpadEnabled(); Raise(nameof(TouchpadEnabled));
                Status = string.Format(_loc["Status.SysToggle"], _loc["Sys.Touchpad"], _loc[_touchpadEnabled ? "Disp.On" : "Disp.Off"]);
            }
            catch (Exception ex) { Status = string.Format(_loc["Status.SwitchFailed"], ex.Message); }
        }
    }

    private bool _supportsBattery;
    public bool SupportsBatteryConservation { get => _supportsBattery; private set => Set(ref _supportsBattery, value); }

    private bool _batteryConservation;
    /// <summary>电池养护(限充约 80%)。实测可读写生效。</summary>
    public bool BatteryConservation
    {
        get => _batteryConservation;
        set
        {
            if (_batteryConservation == value) return;
            _batteryConservation = value; Raise(nameof(BatteryConservation));
            if (_device is null) return;
            try
            {
                _device.SetBatteryConservation(value);
                _batteryConservation = _device.GetBatteryConservation(); Raise(nameof(BatteryConservation));
                Status = string.Format(_loc["Status.SysToggle"], _loc["Sys.Battery"], _loc[_batteryConservation ? "Disp.On" : "Disp.Off"]);
            }
            catch (Exception ex) { Status = string.Format(_loc["Status.SwitchFailed"], ex.Message); }
        }
    }

    // ---- 自定义档编辑(仅自定义模式;不生效机型隐藏滑块改显说明) ----
    public ObservableCollection<CustomParamEditor> CustomEditors { get; } = new();

    /// <summary>本机自定义写入是否生效(龙峰等为 false)。</summary>
    public bool CustomEffective => _controller.SupportsEffectiveCustomTuning;

    private bool _revealEditor;   // 不生效机型用户手动"仍要显示"

    private bool _isCustomMode;
    public bool IsCustomMode { get => _isCustomMode; private set => Set(ref _isCustomMode, value); }

    private bool _editorsVisible;
    public bool EditorsVisible { get => _editorsVisible; private set => Set(ref _editorsVisible, value); }

    private bool _noticeVisible;
    public bool NoticeVisible { get => _noticeVisible; private set => Set(ref _noticeVisible, value); }

    public string BackendName => _controller.BackendName;
    public string MachineName => _controller.MachineName;

    private ModeOption? _selectedMode;
    public ModeOption? SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (Set(ref _selectedMode, value) && value is not null)
            {
                CurrentMode = value.Value;
                UpdateCustomVisibility();
                ApplyPowerMode(value.Value);
            }
        }
    }

    // 供高亮列的 DataTrigger 绑定。
    private PowerMode _currentMode;
    public PowerMode CurrentMode { get => _currentMode; private set => Set(ref _currentMode, value); }

    private string _cpuReadout = "CPU  …";
    public string CpuReadout { get => _cpuReadout; private set => Set(ref _cpuReadout, value); }

    private string _gpuReadout = "GPU  …";
    public string GpuReadout { get => _gpuReadout; private set => Set(ref _gpuReadout, value); }

    private string _status = string.Empty;
    public string Status { get => _status; set => Set(ref _status, value); }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand ToggleLanguageCommand { get; }
    public RelayCommand<RefreshRateOption> SetRefreshRateCommand { get; }
    public RelayCommand ApplyCustomCommand { get; }
    public RelayCommand RevealCustomEditorCommand { get; }
    public RelayCommand ToggleGpuModeCommand { get; }

    public MainViewModel(IPowerController controller)
    {
        _controller = controller;
        _device = controller as IDeviceFeatures;
        if (_settings.Language is "zh" or "en") _loc.SetLanguage(_settings.Language);
        RefreshCommand = new RelayCommand(Refresh);
        ToggleLanguageCommand = new RelayCommand(() =>
        {
            _loc.Toggle();
            _settings.Language = _loc.Language;
            SettingsService.Save(_settings);
        });
        SetRefreshRateCommand = new RelayCommand<RefreshRateOption>(SetRefreshRate);
        ApplyCustomCommand = new RelayCommand(ApplyCustom);
        RevealCustomEditorCommand = new RelayCommand(() => { _revealEditor = true; UpdateCustomVisibility(); });
        ToggleGpuModeCommand = new RelayCommand(ToggleGpuMode);
        _power.AcStatusChanged += OnAcChanged;
        _loc.LanguageChanged += Refresh;   // 切语言 → 重建模式名与参数名

        Refresh();

        if (_settings.AutoSwitch) AutoSwitch = true;   // 恢复"自动切档"设置并立即应用

        UpdateSensors();
        _sensorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _sensorTimer.Tick += (_, _) => UpdateSensors();
        _sensorTimer.Start();
    }

    private void Refresh()
    {
        try
        {
            _currentMode = _controller.GetPowerMode();

            Modes.Clear();
            foreach (var m in new[] { PowerMode.Quiet, PowerMode.Balanced, PowerMode.Performance, PowerMode.Custom })
                Modes.Add(new ModeOption(m, _loc[ModeKey(m)], ModeIcon(m)));
            _selectedMode = Modes.FirstOrDefault(o => o.Value == _currentMode);
            Raise(nameof(SelectedMode));
            Raise(nameof(CurrentMode));

            CpuRows.Clear();
            GpuRows.Clear();
            var caps = _controller.GetCapabilities()
                .Where(c => c.IsKnown && (c.Max > 0 || c.IsDiscrete))
                .ToList();

            foreach (var g in caps.GroupBy(c => c.Id.ParameterKey).OrderBy(g => g.Key))
            {
                var quiet = g.FirstOrDefault(c => c.Id.Mode == CapabilityMode.Quiet);
                var balanced = g.FirstOrDefault(c => c.Id.Mode == CapabilityMode.Balanced);
                var perf = g.FirstOrDefault(c => c.Id.Mode == CapabilityMode.Performance);
                var any = quiet ?? balanced ?? perf ?? g.First();

                var range = any.IsDiscrete
                    ? string.Join(" / ", any.DiscreteValues!)
                    : $"{any.Min}–{any.Max}";

                var row = new CapabilityComparisonRow(
                    _loc[any.NameKey], any.Unit, range,
                    quiet?.Current, balanced?.Current, perf?.Current);

                if (any.Id.Category == HardwareCategory.Gpu) GpuRows.Add(row);
                else CpuRows.Add(row);
            }

            LoadFanCurves(_currentMode);
            LoadDisplayFeatures();

            BuildCustomEditors(caps);
            _revealEditor = false;
            UpdateCustomVisibility();

            Status = string.Format(_loc["Status.Loaded"], CpuRows.Count + GpuRows.Count, _loc[ModeKey(_currentMode)]);
        }
        catch (Exception ex)
        {
            Status = string.Format(_loc["Status.ReadFailed"], ex.Message);
        }
    }

    private void LoadFanCurves(PowerMode mode)
    {
        FanCurves.Clear();
        try
        {
            var curves = _controller.GetFanCurves()
                .Where(c => c.Mode == mode)
                .GroupBy(c => c.FanId)
                .Select(g => g.OrderBy(c => c.ModeRaw).First())   // 自定义档优先 0xE0 而非 0xFF 快照
                .OrderBy(c => c.FanId)
                .ToList();

            var n = 1;
            foreach (var c in curves)
                FanCurves.Add(new FanCurveRow($"{_loc["Fan.Name"]} {n++}", c));
        }
        catch { /* 风扇读取失败不致命,隐藏该卡片 */ }
        HasFanCurves = FanCurves.Count > 0;
    }

    private void LoadDisplayFeatures()
    {
        RefreshRates.Clear();
        try
        {
            var cur = _display.GetCurrentRefreshRate();
            foreach (var hz in _display.GetAvailableRefreshRates())
                RefreshRates.Add(new RefreshRateOption(hz, hz == cur));
        }
        catch { /* 读不到刷新率不致命 */ }

        SupportsOverdrive = _device is { SupportsPanelOverdrive: true };
        if (SupportsOverdrive)
            try { _overdrive = _device!.GetPanelOverdrive(); Raise(nameof(Overdrive)); } catch { }

        HasGpuInfo = _device is { SupportsGpuDisplayMode: true };
        if (HasGpuInfo)
        {
            _gpuModeRaw = _device!.GetGpuDisplayMode();
            GpuModeLabel = _loc[_gpuModeRaw == 0 ? "Disp.Discrete" : "Disp.Hybrid"];   // 0=iGPU关=独显直连
        }

        GSyncLabel = _device is { SupportsGSync: true }
            ? $"G-Sync · {_loc[_device.GetGSync() == 1 ? "Disp.On" : "Disp.Off"]}"
            : string.Empty;

        HasDisplayCard = RefreshRates.Count > 1 || SupportsOverdrive || HasGpuInfo;

        SupportsWinKey = _device is { SupportsWinKeyToggle: true };
        if (SupportsWinKey) { _winKeyDisabled = _device!.GetWinKeyDisabled(); Raise(nameof(WinKeyDisabled)); }
        SupportsTouchpad = _device is { SupportsTouchpadToggle: true };
        if (SupportsTouchpad) { _touchpadEnabled = _device!.GetTouchpadEnabled(); Raise(nameof(TouchpadEnabled)); }
        SupportsBatteryConservation = _device is { SupportsBatteryConservation: true };
        if (SupportsBatteryConservation) { _batteryConservation = _device!.GetBatteryConservation(); Raise(nameof(BatteryConservation)); }
        HasSystemToggles = SupportsWinKey || SupportsTouchpad || SupportsBatteryConservation;
    }

    private void SetRefreshRate(RefreshRateOption? opt)
    {
        if (opt is null) return;
        try
        {
            if (_display.SetRefreshRate(opt.Hz))
            {
                Status = string.Format(_loc["Status.RefreshSet"], opt.Hz);
                var cur = _display.GetCurrentRefreshRate();
                var rates = RefreshRates.Select(r => r.Hz).ToList();
                RefreshRates.Clear();
                foreach (var hz in rates)
                    RefreshRates.Add(new RefreshRateOption(hz, hz == cur));
            }
            else
                Status = string.Format(_loc["Status.RefreshFailed"], opt.Hz);
        }
        catch (Exception ex) { Status = string.Format(_loc["Status.SwitchFailed"], ex.Message); }
    }

    private void BuildCustomEditors(IReadOnlyList<AdjustableCapability> caps)
    {
        CustomEditors.Clear();
        foreach (var c in caps.Where(c => c.Id.Mode == CapabilityMode.Custom).OrderBy(c => c.Id.Raw))
            CustomEditors.Add(new CustomParamEditor(c, _loc[c.NameKey]));
    }

    private void UpdateCustomVisibility()
    {
        IsCustomMode = _currentMode == PowerMode.Custom;
        if (!IsCustomMode) _revealEditor = false;
        EditorsVisible = IsCustomMode && CustomEditors.Count > 0 && (CustomEffective || _revealEditor);
        NoticeVisible = IsCustomMode && !CustomEffective && !_revealEditor;
    }

    private void ApplyCustom()
    {
        int ok = 0, fail = 0;
        foreach (var e in CustomEditors)
        {
            try { _controller.SetValue(e.CustomId, e.ResolvedValue); ok++; }
            catch { fail++; }
        }
        var note = CustomEffective ? string.Empty : _loc["Custom.MaybeIneffective"];
        Status = string.Format(_loc["Status.CustomApplied"], ok, fail, note).TrimEnd();
    }

    private void OnAcChanged(bool ac)
    {
        if (!_autoSwitch) return;
        Application.Current?.Dispatcher.Invoke(() => ApplyAutoMode(ac));
    }

    private void ApplyAutoMode(bool ac)
    {
        var target = ac ? PowerMode.Performance : PowerMode.Balanced;
        if (_selectedMode?.Value == target) return;
        var opt = Modes.FirstOrDefault(m => m.Value == target);
        if (opt is null) return;
        SelectedMode = opt;   // 触发真实切档
        Status = string.Format(_loc["Status.AutoSwitched"], _loc[ac ? "Auto.Ac" : "Auto.Dc"], _loc[ModeKey(target)]);
    }

    private void ToggleGpuMode()
    {
        if (_device is not { SupportsGpuDisplayMode: true }) return;
        var target = _gpuModeRaw == 0 ? 1 : 0;                       // 0=独显直连 ⇄ 1=混合
        var targetLabel = _loc[target == 0 ? "Disp.Discrete" : "Disp.Hybrid"];
        var confirm = MessageBox.Show(
            string.Format(_loc["Disp.GpuConfirm"], targetLabel),
            _loc["Disp.GpuMode"], MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.OK) return;
        try
        {
            _device.SetGpuDisplayMode(target);
            _gpuModeRaw = target;
            GpuModeLabel = targetLabel;
            Status = string.Format(_loc["Disp.GpuSwitched"], targetLabel);
        }
        catch (Exception ex) { Status = string.Format(_loc["Status.SwitchFailed"], ex.Message); }
    }

    /// <summary>供托盘/快捷键切换性能模式(走 SelectedMode → 真实生效)。</summary>
    public void SwitchTo(PowerMode mode)
    {
        var opt = Modes.FirstOrDefault(m => m.Value == mode);
        if (opt is not null) SelectedMode = opt;
    }

    private void ApplyPowerMode(PowerMode mode)
    {
        try
        {
            _controller.SetPowerMode(mode);
            var note = mode == PowerMode.Custom ? _loc["Status.CustomNote"] : string.Empty;
            Status = string.Format(_loc["Status.Switched"], _loc[ModeKey(mode)], note).TrimEnd();
        }
        catch (Exception ex)
        {
            Status = string.Format(_loc["Status.SwitchFailed"], ex.Message);
        }
    }

    private static string ModeKey(PowerMode mode) => mode switch
    {
        PowerMode.Quiet => "Mode.Quiet",
        PowerMode.Balanced => "Mode.Balanced",
        PowerMode.Performance => "Mode.Performance",
        PowerMode.Custom => "Mode.Custom",
        _ => mode.ToString(),
    };

    private static string ModeIcon(PowerMode mode) => mode switch
    {
        PowerMode.Quiet => "🌙",
        PowerMode.Balanced => "⚖",
        PowerMode.Performance => "⚡",
        PowerMode.Custom => "⚙",
        _ => "•",
    };

    private void UpdateSensors()
    {
        var r = _sensors.Read();
        CpuReadout = FormatReadout("CPU", r.CpuPower, r.CpuTemp);
        GpuReadout = FormatReadout("GPU", r.GpuPower, r.GpuTemp);
    }

    private static string FormatReadout(string label, float? power, float? temp)
    {
        var p = power.HasValue ? $"{power.Value:F1} W" : "— W";
        var t = temp.HasValue ? $"{temp.Value:F0} °C" : "— °C";
        return $"{label}  {p}  ·  {t}";
    }
}

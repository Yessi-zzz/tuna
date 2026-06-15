using System.ComponentModel;
using System.Globalization;

namespace Tuna.App.Localization;

/// <summary>
/// 轻量运行时本地化:单例 + 索引器。XAML 用 {Binding [Key], Source={x:Static loc:Localizer.Instance}}。
/// 切换语言时 raise "Item[]",全界面绑定即时刷新。字符串集中此处(中英),便于后续扩展/迁移 resx。
/// </summary>
public sealed class Localizer : INotifyPropertyChanged
{
    public static Localizer Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>当前语言:"zh" 或 "en"。</summary>
    public string Language { get; private set; }

    private Localizer()
    {
        Language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh" ? "zh" : "en";
    }

    public string this[string key]
    {
        get
        {
            if (Strings.TryGetValue(key, out var pair))
                return Language == "zh" ? pair.Zh : pair.En;
            return key; // 缺失键回退为键名,便于发现遗漏
        }
    }

    /// <summary>切到另一种语言(中⇄英),通知全部绑定刷新。</summary>
    public void Toggle() => SetLanguage(Language == "zh" ? "en" : "zh");

    public void SetLanguage(string lang)
    {
        if (lang != "zh" && lang != "en") return;
        if (lang == Language) return;
        Language = lang;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NextLanguageLabel)));
        LanguageChanged?.Invoke();
    }

    /// <summary>语言切换后触发,供 ViewModel 重建依赖语言的列表(如参数行)。</summary>
    public event Action? LanguageChanged;

    /// <summary>切换按钮上显示的"目标语言"文字。</summary>
    public string NextLanguageLabel => Language == "zh" ? "EN" : "中";

    private static readonly Dictionary<string, (string En, string Zh)> Strings = new()
    {
        ["App.Title"]          = ("Tuna — Gaming Laptop Power Tuner", "Tuna — 游戏本功耗调节"),
        ["Mode.Label"]         = ("Performance mode:", "性能模式:"),
        ["Mode.Quiet"]         = ("Quiet", "安静"),
        ["Mode.Balanced"]      = ("Balanced", "均衡"),
        ["Mode.Performance"]   = ("Performance", "性能"),
        ["Mode.Custom"]        = ("Custom", "自定义"),
        ["Action.Refresh"]     = ("Refresh", "刷新"),
        ["Tray.Show"]          = ("Show Tuna", "显示主界面"),
        ["Tray.Exit"]          = ("Exit", "退出"),
        ["Live.Label"]         = ("Live:", "实时:"),
        ["Hint.Main"]          = ("Switching mode takes effect instantly (Quiet / Balanced / Performance); the active mode column is highlighted. Parameters below are per-mode presets, read-only.",
                                  "切换性能模式即时生效(安静 / 均衡 / 性能),当前档以高亮列标示。下方为各档功耗参数配置,仅供查看。"),
        ["Section.Modes"]      = ("Select mode", "选择模式"),
        ["Auto.Label"]         = ("Auto-switch on AC/battery (AC → Performance, battery → Balanced)",
                                  "插拔电自动切档(接电 → 性能,断电 → 均衡)"),
        ["Auto.Ac"]            = ("AC", "接电"),
        ["Auto.Dc"]            = ("Battery", "断电"),
        ["Status.AutoSwitched"]= ("Auto-switch: {0} → {1}", "自动切档:{0} → {1}"),
        ["Section.Scenarios"]  = ("Scenarios", "场景一键切换"),
        ["Section.Params"]     = ("Per-mode parameters (read-only)", "各档参数对比(只读)"),
        ["Section.Fan"]        = ("Fan curve · current mode", "风扇曲线 · 当前档"),
        ["Fan.Name"]           = ("Fan", "风扇"),
        ["Fan.Note"]           = ("Read-only: temperature (X) → fan speed (Y) for the active mode. Curve editing is under reverse-engineering (Fan_Set_Table), pending load-tested verification.",
                                  "只读:横轴温度 → 纵轴转速,展示当前档曲线。曲线编辑正在逆向(Fan_Set_Table),需负载实测验证后开放。"),

        ["Section.Display"]    = ("Display & GPU", "显示与显卡"),
        ["Disp.RefreshRate"]   = ("Refresh rate", "刷新率"),
        ["Disp.Overdrive"]     = ("Panel overdrive", "屏幕超频"),
        ["Disp.OverdriveHint"] = ("Faster pixel response (may add slight overshoot)", "更快像素响应(可能轻微过冲拖影)"),
        ["Disp.GpuMode"]       = ("GPU mode", "显卡模式"),
        ["Disp.Hybrid"]        = ("Hybrid", "混合模式"),
        ["Disp.Discrete"]      = ("Discrete (dGPU direct)", "独显直连"),
        ["Disp.On"]            = ("On", "开"),
        ["Disp.Off"]           = ("Off", "关"),
        ["Disp.GpuNote"]       = ("switching needs a restart", "切换需重启生效"),
        ["Disp.GpuSwitch"]     = ("Switch", "切换"),
        ["Disp.GpuConfirm"]    = ("Switch GPU mode to {0}? This takes effect after a restart.", "切换显卡模式到 {0}?重启后生效。"),
        ["Disp.GpuSwitched"]   = ("GPU mode will switch to {0} after restart", "显卡模式将在重启后切到 {0}"),
        ["Status.RefreshSet"]  = ("Refresh rate set to {0} Hz", "刷新率已切到 {0} Hz"),
        ["Status.RefreshFailed"]= ("Failed to set {0} Hz", "切到 {0} Hz 失败"),
        ["Status.OdSet"]       = ("Panel overdrive: {0}", "屏幕超频:{0}"),
        ["Section.System"]     = ("System switches", "系统开关"),
        ["Sys.WinKey"]         = ("Disable Win key (anti-mistouch in games)", "禁用 Win 键(游戏防误触)"),
        ["Sys.Touchpad"]       = ("Touchpad enabled", "启用触控板"),
        ["Sys.Battery"]        = ("Battery conservation (limit charge to ~80%)", "电池养护(限充约 80%)"),
        ["Status.SysToggle"]   = ("{0}: {1}", "{0}:{1}"),

        ["Section.Custom"]     = ("Custom tuning", "自定义档调节"),
        ["Custom.Notice"]      = ("Your platform (AMD Dragon Range) routes custom CPU/GPU power through the AMD SMU, so WMI writes are ignored by the EC — values set here won't take effect. Presets (Quiet/Balanced/Performance), fans and display controls still work normally. See README for details.",
                                  "你的平台(AMD 龙峰)自定义 CPU/GPU 功耗走 AMD SMU,WMI 写入被 EC 忽略 —— 这里设的值不会生效。预设档(安静/均衡/性能)、风扇、显示控制照常可用。详见 README。"),
        ["Custom.RevealBtn"]   = ("Show sliders anyway (may not apply on this machine)", "仍要显示调节滑块(本机可能不生效)"),
        ["Custom.Apply"]       = ("Apply", "应用"),
        ["Custom.ApplyHint"]   = ("Writes all values above to the custom profile.", "把上面所有值写入自定义档。"),
        ["Custom.MaybeIneffective"] = ("(may not take effect on this machine)", "(本机可能不生效)"),
        ["Status.CustomApplied"]= ("Custom applied: {0} written, {1} failed {2}", "自定义已写入:成功 {0} 项,失败 {1} 项 {2}"),

        ["Scn.Office"]         = ("Office", "办公续航"),
        ["Scn.Office.Desc"]    = ("Quiet · low power, long battery", "安静档 · 省电安静、续航优先"),
        ["Scn.Media"]          = ("Media", "影音"),
        ["Scn.Media.Desc"]     = ("Balanced · low noise", "均衡档 · 低噪、温度适中"),
        ["Scn.Game"]           = ("Gaming", "游戏"),
        ["Scn.Game.Desc"]      = ("Performance · high power & FPS", "性能档 · 高功耗高帧率"),
        ["Scn.Esports"]        = ("Esports", "电竞竞技"),
        ["Scn.Esports.Desc"]   = ("Performance · max & stable", "性能档 · 拉满功耗、稳定优先"),
        ["Scn.Creator"]        = ("Creator", "创作渲染"),
        ["Scn.Creator.Desc"]   = ("Performance · sustained multi-core", "性能档 · 持续多核负载"),
        ["Col.Parameter"]      = ("Parameter", "参数"),
        ["Col.Unit"]           = ("Unit", "单位"),
        ["Col.Range"]          = ("Range", "可选范围"),
        ["Status.Loaded"]      = ("Loaded {0} parameters · Current mode: {1}", "已加载 {0} 项参数 · 当前模式:{1}"),
        ["Status.Switched"]    = ("Switched to: {0} {1}", "性能模式已切换到:{0} {1}"),
        ["Status.CustomNote"]  = ("(Custom is managed by Legion; fine-tuning not yet supported)", "(自定义档由 Legion 管理,细调暂未支持)"),
        ["Status.ReadFailed"]  = ("Read failed: {0}", "读取失败:{0}"),
        ["Status.SwitchFailed"]= ("Switch failed: {0}", "切换失败:{0}"),

        ["Param.Cpu.Pl2"]      = ("CPU Short-term Power (PL2)", "CPU 短时功率 PL2/sPL"),
        ["Param.Cpu.Pl1"]      = ("CPU Long-term Power (PL1)", "CPU 长时功率 PL1"),
        ["Param.Cpu.Sppt"]     = ("CPU Peak Power (sPPT)", "CPU 峰值功率 sPPT"),
        ["Param.Cpu.Temp"]     = ("CPU Temp Limit", "CPU 温度墙"),
        ["Param.Cpu.Apu"]      = ("APU sPPT", "APU sPPT"),
        ["Param.Cpu.CrossLoad"]= ("CPU Cross-load Power", "CPU 交叉负载功率"),
        ["Param.Gpu.Boost"]    = ("GPU Power Boost", "GPU 功率加成 Boost"),
        ["Param.Gpu.Ctgp"]     = ("GPU Configurable TGP (cTGP)", "GPU 可配置功耗 cTGP"),
        ["Param.Gpu.Temp"]     = ("GPU Temp Limit", "GPU 温度墙"),
        ["Param.Gpu.Tpp"]      = ("GPU Target Power Offset (TPP)", "GPU 目标功耗偏移 TPP"),
        ["Param.Gpu.DynBoost"] = ("GPU Dynamic Boost", "GPU Dynamic Boost"),
    };
}

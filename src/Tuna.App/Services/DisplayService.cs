using System.Runtime.InteropServices;

namespace Tuna.App.Services;

/// <summary>
/// 主显示器刷新率读取/切换。走 Windows 显示 API(ChangeDisplaySettingsEx),
/// 品牌无关、即时生效、可逆(写错系统会自动回退),不依赖任何驱动/EC。
/// </summary>
public sealed class DisplayService
{
    public int GetCurrentRefreshRate()
    {
        var dm = NewDevMode();
        return EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref dm) ? (int)dm.dmDisplayFrequency : 0;
    }

    /// <summary>当前分辨率/色深下可用的刷新率(去重升序)。</summary>
    public IReadOnlyList<int> GetAvailableRefreshRates()
    {
        var cur = NewDevMode();
        if (!EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref cur))
            return Array.Empty<int>();

        var rates = new SortedSet<int>();
        for (var i = 0; ; i++)
        {
            var dm = NewDevMode();
            if (!EnumDisplaySettings(null, i, ref dm))
                break;
            if (dm.dmPelsWidth == cur.dmPelsWidth && dm.dmPelsHeight == cur.dmPelsHeight &&
                dm.dmBitsPerPel == cur.dmBitsPerPel && dm.dmDisplayFrequency > 1)
                rates.Add((int)dm.dmDisplayFrequency);
        }
        return rates.ToList();
    }

    /// <returns>true 表示切换成功。</returns>
    public bool SetRefreshRate(int hz)
    {
        var dm = NewDevMode();
        if (!EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref dm))
            return false;
        dm.dmDisplayFrequency = (uint)hz;
        dm.dmFields = DM_DISPLAYFREQUENCY;
        return ChangeDisplaySettingsEx(null, ref dm, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero) == DISP_CHANGE_SUCCESSFUL;
    }

    // ---- P/Invoke ----

    private const int ENUM_CURRENT_SETTINGS = -1;
    private const uint CDS_UPDATEREGISTRY = 0x01;
    private const int DISP_CHANGE_SUCCESSFUL = 0;
    private const uint DM_DISPLAYFREQUENCY = 0x400000;

    private static DEVMODE NewDevMode() => new() { dmSize = (ushort)Marshal.SizeOf<DEVMODE>() };

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE devMode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ChangeDisplaySettingsEx(string? deviceName, ref DEVMODE devMode, IntPtr hwnd, uint flags, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public ushort dmSpecVersion;
        public ushort dmDriverVersion;
        public ushort dmSize;
        public ushort dmDriverExtra;
        public uint dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public uint dmDisplayOrientation;
        public uint dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel;
        public uint dmPelsWidth;
        public uint dmPelsHeight;
        public uint dmDisplayFlags;
        public uint dmDisplayFrequency;
        public uint dmICMMethod;
        public uint dmICMIntent;
        public uint dmMediaType;
        public uint dmDitherType;
        public uint dmReserved1;
        public uint dmReserved2;
        public uint dmPanningWidth;
        public uint dmPanningHeight;
    }
}

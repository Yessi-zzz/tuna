# Tuna 🐟

开源的游戏本功耗 / 性能调节工具 —— Legion Zone / Vantage 的轻量替代。
**数据驱动**:程序向硬件查询"能调什么、范围多少",再自动生成界面,跨机型几乎免适配。零后台、无广告、无登录、无遥测。

> 状态:可用早期版本。当前实现 Lenovo Legion / LOQ(WMI)后端;架构预留多品牌扩展点。
> 仅支持游戏本,不支持台式机。开发验证机:Legion R9000P 2025(Ryzen 9 8945HX + RTX 5070 Ti)。

## 现在能做什么
- **性能模式切换**(安静 / 均衡 / 性能 / 自定义)—— 即时生效,当前档高亮。
- **各档参数对比**(只读):CPU PL1/PL2/sPPT/温度墙/交叉负载,GPU cTGP/Boost/温度墙/TPP —— 直接读机器自报的能力表。
- **实时监控**:GPU 功耗 / 温度(经 NVIDIA 驱动,正常)。CPU 功耗/温度在部分机型显示「—」,见下方说明。
- **电池养护**(限充约 80%)、关 Win 键、触控板开关、插拔电自动切档(接电→性能 / 断电→均衡)。
- **记住设置**:语言、自动切档等持久化到 `%AppData%\Tuna\settings.json`。
- **风扇曲线**(只读可视化):每档每风扇 温度→转速 折线。
- **显示与显卡**:刷新率切换(Windows 显示 API,即时可逆)、屏幕超频 OD 开关、独显直连/混合模式与 G-Sync 状态。
- 中英双语即时切换、深色主题。

## ⚠️ 关于"自定义档"功耗细调(重要,务必先读)

**在 2025 AMD 龙峰平台(如 8945HX)上,无法通过 WMI 写入自定义 CPU/GPU 功耗值。** 这是本项目实测后的明确结论:

- 预设档(安静/均衡/性能)的功耗值**由 EC 内置、真实生效**(实测:CPU PPT 63→130W、GPU TGP 59→140W 随档变化)。
- 但写"自定义档"的具体值(`SetFeatureValue`)**写得进存储、EC 进档时却不读取**,回退固定基准(CPU ~63W、GPU ~80W)。CPU 与 GPU 均如此,是**平台级**行为,非个别参数。
- **原因**:这代 AMD 龙峰的自定义功耗实际走 **AMD SMU(Ryzen Master 驱动)**,不是 Lenovo WMI —— 官方 Legion Zone 正是装了整套 AMD Ryzen Master SDK 来做。第三方走 SMU 需 `WinRing0`,而 **Win11 24H2 默认开启的易受攻击驱动黑名单会拦截 WinRing0**。产品级唯一干净路是复用 AMD 官方签名驱动,工程量大,列为长期 R&D。
- **LLT(LenovoLegionToolkit)也一样**:它调自定义功耗用的是和本项目相同的 Lenovo WMI,**没有** SMU 旁路;在 AMD 龙峰上同样失效甚至崩溃(见
  [#1111](https://github.com/BartoszCichecki/LenovoLegionToolkit/issues/1111) 自定义档使 7945HX 机型重启、
  [#919](https://github.com/BartoszCichecki/LenovoLegionToolkit/issues/919) AMD 上 PPT Fast 调不动、
  [#235](https://github.com/BartoszCichecki/LenovoLegionToolkit/issues/235))。**这不是工具实现问题,是平台墙。**

技术细节与实测数据见 [`FINDINGS.md`](FINDINGS.md)。

## CPU 实时监控为什么可能显示「—」

GPU 功耗/温度走 NVIDIA 驱动读取,正常。但 **CPU 功耗/温度在某些机型会显示「—」**,原因:

- 读 CPU 功耗/温度需要内核驱动访问 CPU 的 MSR/SMU 寄存器。监控库(LibreHardwareMonitor)用的是 `WinRing0` 驱动,而 **Win11 24H2 默认开启的「易受攻击驱动黑名单」会拦截 WinRing0**,导致读不到 → 显示「—」。
- **这跟机型/系统有关,不是 Tuna 的 bug**:在 **Win10、或未开启该黑名单、或 CPU 传感器走其它路径的机型上,CPU 读数正常**。本仓库的开发机(8945HX + Win11 24H2)恰好同时撞上 AMD 龙峰 + 驱动黑名单,所以显示「—」。
- 不想假装有数,所以读不到时如实显示「—」,而不是填 0。
- 想在这类机型上看 CPU 读数,可让 **HWiNFO 常驻并开启「共享内存支持」**,后续版本可选接入它的共享内存(只读、无需驱动)。

## 其他机型能调吗?——能

Tuna 的写入面只有一个数据驱动方法 `SetValue(功能ID, 值)`,范围由机器经 `LENOVO_CAPABILITY_DATA_01` 自报。因此:

- **在 EC 认 WMI 自定义写入的机型上(Intel Legion、部分较早 AMD 机型),这套代码本就能直接调功耗,无需改动** —— 换台兼容机即可生效。当前在 8945HX 龙峰上失效,纯粹是该平台把自定义路由到了 SMU。
- 计划把"自定义档编辑"做成**按机型能力开放**:能写入生效的机型提供滑块;经检测不生效的机型(如龙峰)隐藏滑块并给出本节说明,避免误导用户以为调了其实没调。
- **跨品牌**(华硕 / 微星 / 戴尔 …)各走完全不同的私有接口,没有"通吃所有游戏本"的统一调参 API。架构已是品牌无关(`IPowerController`),欢迎有对应**真机**的人贡献后端 —— 因写错他人 EC 可能过热,每个后端须实测验证后合并。

## 架构
```
Tuna.App (WPF, 能力驱动 UI)
    │
    ▼
Tuna.Core            ← IPowerController / IDeviceFeatures 抽象 + 硬件模型(与品牌无关)
    ▲
    │  实现
Tuna.Backends.Lenovo ← Lenovo WMI(root\WMI)读写
    （未来：Tuna.Backends.Asus / .Msi …，社区贡献，需真机实测）
```

## 构建与运行
需要 .NET 8 SDK:
```powershell
winget install Microsoft.DotNet.SDK.8
dotnet build src/Tuna.App/Tuna.App.csproj -c Release
dotnet run --project src/Tuna.App        # 会请求管理员权限(读写硬件需要)
```

### 发布单文件 exe(framework-dependent,约 5 MB)
```powershell
dotnet publish src/Tuna.App/Tuna.App.csproj -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o publish
```
产物 `publish/Tuna.exe` 单文件即可分发,**运行需目标机装有 [.NET 8 桌面运行时](https://dotnet.microsoft.com/download/dotnet/8.0)**。
要完全独立(无需装运行时)改 `--self-contained true`,体积约 150 MB。

## 使用提示
- **托盘常驻**:关闭窗口会最小化到系统托盘(双击托盘图标恢复,右键菜单可快速切档/退出)。
- **全局快捷键**:`Ctrl+Alt+1/2/3/4` = 安静 / 均衡 / 性能 / 自定义,任意界面下都能切。
- 语言、"插拔电自动切档"等设置记忆在 `%AppData%\Tuna\settings.json`。

## ⚠️ 安全
- 写入前一律做范围 / 白名单校验(连续值卡 Min/Max/Step,离散值必须命中 `DISCRETE_DATA`)。
- 理解超功率 / 降温度墙可能带来的发热与稳定性风险,风险自负。风扇曲线写入在负载实测验证前不开放。

## 许可证
MIT,见 [LICENSE](LICENSE)。

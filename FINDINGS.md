# Legion Tuner — 第一步探针结论

> 机型:Lenovo Legion R9000P 2025 (83RV / ADR10H) · Ryzen 9 8945HX · RTX 5070 Ti Laptop
> 探针:`tools/Probe-LegionCapabilities.ps1`(只读)· 原始报告:`tools/capability-report.txt`

## 结论一句话
这台 MY2025 的整套 CPU/GPU/风扇调节接口**全部在线可用**,LLT 那套"数据驱动"思路在新机型上成立。跨 Lenovo 机型通用 = 可行;架构按下面来。

## 写入 API(整个项目的核心)
- 读:`LENOVO_OTHER_METHOD.GetFeatureValue(IDs)`
- 写:`LENOVO_OTHER_METHOD.SetFeatureValue(IDs, Value)`
- 可调范围来源:`LENOVO_CAPABILITY_DATA_01`(连续型:Min/Max/Step/Default)
- 离散值白名单:`LENOVO_DISCRETE_DATA`(GPU cTGP / Boost 用)
- 开关类能力:`LENOVO_CAPABILITY_DATA_00`
- **整个写入面就这一个方法 `SetFeatureValue` + 一个功能ID** → 跨机型只需让机器自己报能力,无需为每款机器写死参数。

## 功能ID 编码规律(重要)
```
0x[类别][参数][模式][00]
类别: 01=CPU  02=GPU  03=显示 04=电池 05=风扇 06=键盘
模式: 01=安静 02=均衡 03=性能 E0=自定义 FF=当前生效
```
→ 每个参数有 5 个变体(每种性能模式各一份)。**自定义模式(E0)是可编辑的那一档**。程序应按"模式 → 参数"组织成可保存的配置档。

## 本机实测可调项

### CPU(连续滑块,单位 W / °C)
| 参数 | 范围 | 性能档默认 | 自定义档默认 |
|---|---|---|---|
| 长时功率 PL1 | 50–140 | 130 | 135 |
| 短时功率 PL2/sPL | 60–145 | 145 | 145 |
| 峰值功率 PL3/sPPT | 70–174 | 174 | 174 |
| 温度墙 | 85–100 | 100 | 100 |
| 交叉负载功率 | 30–100 | 75 | 100 |

### GPU
| 参数 | 类型 | 范围/可选值 | 性能档默认 |
|---|---|---|---|
| 可配置功耗 cTGP | 离散 | 70 / 80 / 90 / 100 / 115 W | 115 |
| 功率加成 Boost | 离散 | 0 / 15 / 20 / 25 W | 25 |
| 温度墙 | 连续 | 75–87 °C | 87 |
| 目标功耗偏移 TPP | 连续 | 10–130 W | 105 |
| 核心频率偏移 | 连续 | 0–200 MHz | (LENOVO_GAMEZONE_GPU_OC_DATA, ClockID=0) |
| 显存频率偏移 | 连续 | 0–400 MHz | (ClockID=1) |

### 风扇 / 其它
- 风扇:3 个风扇(Fan_Id 1/2/4),每模式 10 点曲线;最高转速 5200 / 5400 / 6500 RPM(`LENOVO_FAN_TABLE_DATA`,可写 `LENOVO_FAN_METHOD`)
- 屏幕刷新率:60–240 Hz(`LENOVO_INTERNAL_PANEL_REFRESH_RATE_DATA`)
- 灯光:亮度分级(`LENOVO_LIGHTING_DATA` / `LENOVO_LIGHTING_METHOD`)
- 性能模式切换:`LENOVO_GAMEZONE_DATA.SetThermalMode` / 当前 = `GetThermalMode`(255 = 自定义)

## 已确认(2026-06-15 无头自测)
- ⚠️ **`GetFeatureValue(IDs)` 在本机恒返回 0** —— 读不到实时当前值(对 FF/E0 等所有档都试过)。
  → **当前/配置值的真实来源 = `CAPABILITY_DATA_01` 每条记录的 `DefaultValue` 字段**,各性能模式各存一份
  (安静 55 / 均衡 90 / 性能 130 / 自定义 135 W,互不相同,即各档配置值)。
  程序里 `Current = 本档 DefaultValue`、`Default = 性能档(03) 的值`(作"恢复出厂"参考),**不再逐条调 GetFeatureValue**(慢且无用)。
- `CAPABILITY_DATA_01` **含重复记录**(同一 IDs 出现两次),读取后须按 IDs 去重。
- 噪声/未知参数:CPU `0x08`、GPU `0x05`(Dynamic Boost,范围 0-0)、GPU `0x0B`(离散 0/15/20/25,疑似 Boost 镜像,含义未明)。
  主界面按 `IsKnown`(catalog 能命名)+ 有效范围过滤,**本机最终呈现 9 项可调参数**(CPU 5 + GPU 4)。
- `LENOVO_OTHER_METHOD` 共 4 方法:`GetFeatureValue / SetFeatureValue / GetDataByCommand / GetDataByPackage`。

## 写入实测结论(2026-06-15,用 HWiNFO 反推 PPT 验证)
观测法:HWiNFO「CPU 封装功率 W」÷「CPU PPT SLOW Limit %」≈ 当前 PL1 限制瓦特(SLOW=PL1,FAST=PL2)。

- ❌ **直接 `SetFeatureValue(0x0102E000=PL1自定义, 120/140)` 不改变实际 PPT** —— 反推限制始终钉在 ~60–66W。
  写 120、写 140、加 `SetSmartFanMode(255)` 触发、停服务独占,全部无效。能力表 DefaultValue 也不随写入更新。
- ✅ **预设档切换有效(独占下也有效)**:`SetSmartFanMode(1/2/3)` 切到 安静/均衡/**性能** → HWiNFO 反推 PPT 跟着变,
  切性能档 PPT 由 ~63W 升到 **~123–130W**(性能档存储值 130)。停服务独占状态下照样生效 → EC 内置预设档表自带值,不依赖外部写入。
- ❌ **自定义档(255)的值施加无效**:写自定义档 PL1=100 + 真切换(255→2→255)重载后,PPT 仍钉在 **~63W**,未采用 100。
  即 `SetFeatureValue` 把值写进了存储,但**进入自定义档时 EC 不读这些存储值**,回退安全默认 ~63W。
  → 自定义值的「施加」走的是 Legion 服务的另一套机制(LZService/GAService),**直接 WMI 复现不出来,需逆向**。
- 各档 PL1 存储值:安静 55 / 均衡 90 / 性能 130 / 自定义 135 W。`PL1[当前/FF]` 恒显示 90,不反映实时模式。
- ✅❌ **GPU 同样验证(2026-06-15,用 `nvidia-smi --query-gpu=enforced.power.limit` 反推 TGP)**:预设档 TGP **真生效**(安静 59W / 均衡·自定义 80W / **性能 140W** = cTGP115+Boost25);但 **GPU 自定义写入(`SetFeatureValue 0x0202E000=cTGP / 0x0204E000=TPP` 写 90 + 切走切回重载)仍钉在 80W,EC 不采用** —— 与 CPU 完全一致。
  → **定论:本机"自定义档值写入"机制(CPU+GPU 全部)被 EC 无视**,非参数个例,是平台级(龙峰自定义走 SMU 旁路,非 WMI)。`enforced.power.limit` 是 GPU 端免负载反推 TGP 的好探针(类比 CPU 用 HWiNFO)。

## 结论:Tuna 写入功能分两层
1. **预设档切换(安静/均衡/性能)= 立即可做** ✅ —— `SetSmartFanMode` 可靠,独占/非独占都生效。这是 v1 的核心写入能力。
2. **自定义档细调(改具体 PL1/cTGP 等值)= 待逆向** 🔍 —— `SetFeatureValue` 不被 EC 采用;Legion Zone 用别的途径施加
   (候选:`LENOVO_OTHER_METHOD.GetDataByCommand/GetDataByPackage`、`LENOVO_CPU_METHOD/GPU_METHOD`、或带 owner 获取的序列)。
   下一步靠反编译 LegionZone/LZService 或 ETW 抓 WMI 调用序列来定位。

## 风扇曲线(2026-06-15 探测,LENOVO_FAN_TABLE_DATA)
**读取已打通**:`LENOVO_FAN_TABLE_DATA` 直接给出结构化的每档每风扇完整曲线(非打包字节,易读):
- 3 风扇 `Fan_Id` 1/2/4,各绑定温度传感器 `Sensor_ID` 1/5/4;每条曲线 10 点
- `SensorTable_Data[10]` = 温度断点(℃),`FanTable_Data[10]` = 对应目标转速(满值 RPM,如 6500)
- `Mode` 字节:1安静 2均衡 3性能 0xE0(224)自定义 0xFF(255)当前快照 → 每档各一组(3 风扇 × 各模式)
- `CurrentFanMin/MaxSpeed`、`Min/MaxSensorTemperature` 给出该风扇转速/温度区间
- 本机三风扇转速上限:5200 / 5400 / 6500 RPM
- Tuna 已实现 `GetFanCurves()` + 主界面「风扇曲线 · 当前档」可视化(Polyline,只读)

**写入格式(待实测验证,切勿盲写)**:`LENOVO_FAN_METHOD.Fan_Set_Table(UInt8Array FanTable)`。
按 LLT `LenovoFanTable` 结构(Pack=1,共 26 字节):
`FSTM=1(1B) | FSID=0(1B) | FSTL=0(4B) | FSS0..FSS9(各 2B 小端,10 个转速设定点)`。
温度断点不在此设(沿用 `Fan_Get_Table` 读到的固定断点),只设 10 个转速点。
**未决**:FSSn 是「满转速 RPM」还是「RPM/100」——读到的是满值 RPM,需用**回写恒等测试**确认:
先把当前曲线原值原样写回 → 风扇无变化即格式正确;若转速骤降则为 RPM/100。
**风险**:写错会让风扇在高负载下转太慢 → 过热。必须 ① 全程 HWiNFO 监控转速;② 设安全下限(不低于安静档最小);③ 出错立即 `SetSmartFanMode` 切回预设兜底。属"有人监督"的实测步骤,不自动执行。
「风扇全速降温」`Fan_Get/Set_FullSpeed` 本机 `IsSupportFanCooling=0` 不支持。

## 扩展参数 · 独立路径(2026-06-15 探测,部分已实现)
功耗自定义档写入受阻,但下列"设备开关"走 GameZone 独立方法,与受阻的 `SetFeatureValue` 不同路,实测可用:
- **刷新率**:面板 60–240Hz(`LENOVO_INTERNAL_PANEL_REFRESH_RATE_DATA` 报范围,DefaultRefreshRate=240)。**切换走 Windows `ChangeDisplaySettingsEx`**(品牌无关、即时、可逆、不碰驱动/EC),不用 WMI。Tuna 已实现 `DisplayService` + UI 刷新率按钮。✅
- **屏幕超频 OD**(响应时间):`IsSupportOD=1`/`GetODStatus`/`SetODStatus(Data=0/1)`。本机支持且已开。Tuna 已实现开关(写后回读确认)。✅
- **独显直连/显卡模式**:`IsSupportIGPUMode=3`、`GetIGPUModeStatus=0`(混合)、`SetIGPUModeStatus(mode)`。**可写但切换需重启**,Tuna 暂只读展示,写入待实测后开放。
- **G-Sync/Advanced Optimus**:`IsSupportGSync=2`、`GetGSyncStatus=1`(开)。只读展示。`NotifyDGPUStatus`/`GetDGPUHWId` 备用。
- 其它面板能力(`LENOVO_PANEL_METHOD`,均有 Get/Set):低延迟模式、MPRT 防抖、色域切换、Game-Aid 准星/FPS/计时——后续可挖。
- **充电阈值:本机无干净 WMI 写入路径** —— GameZone 只有 `GetPowerChargeMode=1`(无 Set);`CAPABILITY_DATA_01` 仅 01CPU/02GPU 两类(无电池/显示类);`Lenovo_BatteryInformation` 无方法。需另行逆向 Legion 电池路径,暂缓。
- **坑**:GameZone 方法须用实例调用(`Get-WmiObject … | InvokeMethod`,即程序里的 `WmiHelper.Invoke`);`Invoke-CimMethod` 静态调用一律报 `0x8004102f` 无效参数。

## 注意
- GPU 频率偏移走 `GAMEZONE_GPU_OC_DATA` + 对应 OC 方法,和 SetFeatureValue 不是同一路。
- 写入必须做范围/白名单校验(连续值卡 Min/Max/Step,离散值命中 DISCRETE_DATA)。
- 观测依赖 HWiNFO:本机 `GetFeatureValue`/`GetCPUTemp` 等 WMI 读数恒 0,无内置可靠实时读数。
- 进度:解决方案 0/0 编译通过;只读自测全过;写入路径已定位方向(切档重载),**下一步验证「改值+切档重载」是否生效**。

## 下一步(第二步)
按 C#/.NET + WPF 出项目骨架:`IPowerController` 抽象 + `LenovoWmiController`(封装上面这些)+ 能力驱动的 UI。预留多品牌 backend 接口,台式机不做。

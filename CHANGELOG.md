# NTPX//RUST · Distributed Stress Console 更新日志（CHANGELOG）

> 按时间倒序排列。基于 git 提交历史整理。

---

## 2026-10 · v4.0.0 — "honest prop"

- **feat: 启动诚实卡** — 每次启动显示 SIMULATION 声明页（4s 自动进入，点击 / `Esc` 跳过），明示不开 socket、不解析域名、不发包、不启动进程。
- **feat: INTEGRITY AUDIT 面板** — 新增 `SimAudit`：运行时反射扫描自身程序集，报告 `net assemblies` / `System.Net types` / `packets · sockets · procs` 并给出 PASS 判定。网络引用、网络类型与结构性计数在正常构建下恒为 0。
- **feat: PHASE TRACK 相位轨** — 右侧新增 7 段相位指示（STANDBY → … → IDLE），当前相位高亮。
- **feat: 快捷键** — `F5` 初始化 / `F6` ENGAGE / `F7` ABORT / `F12` 导出当前帧 PNG / `Esc` 跳过诚实卡。
- **feat: CLI** — `--snapshot <png> [--state engaged|armed|idle|splash]` 离屏 1:1 渲染截图；`--selftest <txt>` 同步驱动相位机跑 15 条断言并以退出码返回结果。
- **feat: DPI 感知** — 新增 `app.manifest`（PerMonitorV2）；修正缩放屏上窗口被系统位图拉伸、右侧被裁出屏幕的问题；启动时按工作区收敛窗口尺寸。
- **style: 控件重绘** — 按钮（hover / pressed / focus / disabled 状态）、输入框、进度条、滚动条全部替换为暗色 HUD 模板，去掉 Win32 默认外观。
- **style: 图表修复** — `TrafficFill` 由空心 `Polyline` 改为带渐变填充的 `Polygon`；刻度平滑跟随峰值，曲线不再顶到画布上沿；新增 `cosmetic · generated` 角标。
- **fix: AMP FACTOR** — 未开打时显示 `—` 而非 `0.0x`；abort 完成后归零。
- **fix: 重新初始化** — `INITIALIZE FLEET` 现在会清零峰值、进度、曲线与运行时长。
- **fix: 上线计数** — `AliveNodes` 按节点比例计算，全上线时正确显示 `1000/1000`（此前固定显示 `500/1000`）。
- **fix: egress 抖动** — 由随机游走改为向目标值均值回归，ENGAGED 后不再把数值抖到阈值以下。
- **perf: 舰队单元** — 缓存 cell 背景画刷，避免每个 tick 分配新画刷。
- **docs: README / CHANGELOG** — 补充 4.0 特性、CLI 用法、截图与自检说明；重录 `preview.png` 与 `docs/splash.png`。

---

## 2026-09 · v3.7.1

- **feat: NTPX//RUST stress console visual simulator（初版）** — 非功能性 WPF 视觉模拟器：50 组 × 10 节点舰队矩阵、四色 pattern LED、实时滚动控制台日志、出口遥测折线图、INITIALIZE → ENGAGE → ABORT 控制流。纯本地随机数据，不产生任何真实网络流量。

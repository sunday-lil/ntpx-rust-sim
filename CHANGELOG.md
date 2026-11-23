# NTPX//RUST · Distributed Stress Console 更新日志（CHANGELOG）

> 按时间倒序排列。基于 git 提交历史整理。

---

## 2026-11 · v4.0.2 — 状态机、可复现性与窄屏

- **fix: ABORT → IDLE 后仍有节点亮着** — 排空途中被杀掉的节点比例会让进入 IDLE 时残留 `Alive=true` 的单元。新增 `EnterIdle()` 统一收口：全部节点下线、PPS 归零、相位落到 IDLE。
- **fix: IDLE 残留 HUD** — `FRAG EVASION` / `HOPS` / `ROUTE` 会停在上一轮的 `on · MF=1` / `4 transit` / 旧链路；现在复位为 `off` / `--` / `—`。
- **fix: ARMED 进度条瞬间归零** — 编队完成时把 `Prog` 写成 0，满格一闪而过；现在 ARMED 保持 100%，进度条在 ENGAGE 时归零重新计。
- **fix: 显式 INITIALIZE 无法取消排队中的 ENGAGE** — 先按 ENGAGE 再按 INITIALIZE，稍后仍会自动开打。`BeginArm(bool preserveEngageQueue)` 区分「主动初始化」与「为 ENGAGE 自动补编队」。
- **fix: 静默无效操作** — ENGAGED 下按 INITIALIZE / ENGAGE、IDLE 下按 ABORT 现在都会在控制台留一行 `… ignored`。
- **fix: `--state idle` 渲染成 STANDBY** — 现在渲染真正的 IDLE 相位（进度 0、矩阵全灭、HUD 复位）。
- **fix: `--snapshot` 不可复现** — 每次出图字节都不同：渲染前定时器仍在跑、日志时间戳取 `DateTime.Now`、`SIM` 呼吸灯动画未停。现在快照模式冻结 tick、固定日志时钟、停掉动画；连续 5 次渲染 sha256 完全一致，CI 已加回归。
- **fix: 快照失败仍返回 0** — 渲染异常写 stderr 并以 1 退出。
- **fix: 参数误用静默通过** — `--snapshot` 缺值、未知 `--state`、未知选项、`--snapshot` 与 `--selftest` 混用，统一打印用法并以 2 退出；新增 `--version` / `--help`。
- **fix: 窄窗口头部重叠** — 头部右侧压成单行（`TX · SOCKS · PROC · UPTIME · SIM`）；最小窗口 1200×700 → 1120×680。
- **fix: 日志裁剪可能留下无时间戳的残行** — 改为按整行（3 个 inline）回收。
- **fix: 同一秒内两次 F12 互相覆盖** — 帧文件名精确到毫秒。
- **fix: 窗口极小时 F12 会输出 1×1 图** — 增加最小尺寸校验并给出明确错误。
- **fix: 关闭时 splash 定时器未停止** — `Closing` 统一清理定时器。
- **perf: 舰队单元复用字体与边框画刷**，不再为 50 个格子各建一份。
- **chore: 移除死代码** — 未使用的 `BG` / `Panel2` 画刷、`FleetNode.Pattern` / `PatternName`、未用局部变量。
- **test: 自检 18 → 28 条** — 覆盖 IDLE 全灭、HUD 复位、ARMED 进度、重复 ENGAGE / INITIALIZE no-op、排队取消、日志上限。
- **ci: 快照可复现性检查** — 连续两次 `--snapshot` 比较 sha256，并 smoke `--version`。

## 2026-11 · v4.0.1 — 相位机修复

- **fix: 空舰队却报 ENGAGED** — `ENGAGE` 不再直接把相位切成 Engaging。舰队未就绪（STANDBY / IDLE / ABORTING / 未完全上线）时先入队并自动执行 `INITIALIZE`，等真正 ARMED 后再开打。此前「ABORT 后直接按 ENGAGE」会出现 `ENGAGED` 配 `0/1000`、全灭舰队仍在跑 80 Gbps 的假象。
- **fix: 舰队单元标签被裁** — 单元标题由 `G05 NTP-AMP` 改为短码 `G05 AMP` / `SYN` / `H2S` / `H2P`，字号 9 → 8.5，不再被列宽截成 `NTP-AM`。
- **ci: 修复工作流** — `actions/setup-dotnet` 的 `8.0.x` 改成 `10.0.x`（项目是 `net10.0-windows`，原来的配置必然构建失败）；构建之后追加 `--selftest` 步骤，并把报告作为 artifact 上传。
- **test: 自检 15 → 18 条** — 新增三条回归断言：ABORT 后 ENGAGE 必须重新武装、ENGAGED 时舰队必须全在线、重新武装后计数必须回到 `1000/1000`。

## 2026-11 · 仓库维护（无代码变更）

- **docs: README** — 增加 shields 徽章（release / license / platform / .NET）、运行环境表、FAQ（是否真发包 / 杀软误报 / host 输入框行为 / 诚实卡开关）、CHANGELOG 入口。
- **chore: `.gitattributes`** — 统一换行策略（`* text=auto eol=lf`）并标注二进制类型，消除 Windows 上 `core.autocrlf` 反复出现的 CRLF 转换告警。
- **chore: GitHub 元数据** — 补充仓库 topics（`dotnet` / `simulator` / `cyberpunk`）。
- **chore: 本地清理** — 移除可重建的 `bin/`、`obj/` 构建产物（约 287 MB）与临时自检输出；源码与发布资产不受影响。

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

# WPF 前端视觉探针准备（2026-10-25）

状态：合成探针准备；真实 Windows / 像素验收 **NotRun**。本轮没有启动 WPF、Windows CI、旧 window/source 窗口或 Chromium，也没有新增依赖。此前 UI 工程成果 `419082f9863abf109a3d9ee7274dde59697bb85a` 的测试与未验限制保留。

## 范围和原合同

新增 GrowthHarness 的独立 `--ci-ui-polish <exclusive-output> <before|after> <settings|overlay>` 入口。它在创建 Application 前要求 Windows、显式 opt-in、新独立 `refs/heads/dev/ui-polish-visual`、对应 GitHub 仓库/Actions 环境和首次 attempt；异常拼写亦被新门拒绝，不能落到原 P0 窗口。没有修改或新增 workflow；没有触发 CI。这些环境检查是防误执行合同，不能替代用户授权或源码来源证明。

原 UI、模型/推理强度、生产 pins/issuer、250/600 ms、密码遮罩、取消、清理及注册资格门没有修改。工具只引用既有依赖。`KuaiYiVisualProductRoot` 允许用同一新探针分别链接固定原产品树；默认项目引用保持原树。

固定 before 为 `e4d57e77426ab235c7219b375debd76bc072e00b`，after 为 `419082f9863abf109a3d9ee7274dde59697bb85a`。运行会检查实际 App 的 informational revision，记录 App DLL SHA256；版本字符串只防止选错侧。真正来源证明必须包含固定 Git blob → 构建输入清单 → 实际 DLL 哈希。before 的独立本地副本从原 Git archive 取得，12,315 文件逐 blob 比对，不是从旧实例恢复制品。未来 Windows wrapper 必须重做该链，不能仅传 `SourceRevisionId` 冒称原源码。

## 采集方案

| 实际生产窗口 | 原配置/视口 | 每侧场景 |
| --- | --- | --- |
| SettingsWindow | Light / Dark；940×700 与 420×680 DIP；FontSize 13；原“浮窗”页 | 每主题 wide、narrow 原输入获得程序焦点、原按钮程序焦点、原输入程序禁用，共 8 张 |
| ShellWindow | Light / Dark × Full / Reduce；配置正文字号 16 DIP，首选宽 480 DIP | 每组合生成中、完成、原 Stop 控件程序禁用、通过原 Stop 命令停止；Full 另取首次实际观察到的中间帧，共 18 张 |

焦点通过真实 `Activate` / `Keyboard.Focus` 获取并验证原控件的 `IsKeyboardFocused`；不伪造 IsPressed/IsMouseOver。禁用样本操作原控件的 IsEnabled，保存并恢复原 local value。完成后的 Stop 并不天然禁用，证据明确称程序禁用，未为探针修改产品行为。

每张记录请求视口、实际窗口/内容 DIP、像素尺寸和实际 DPI、主题/动作偏好、目标 focus/enabled/hover、原标签/tooltip、HWND/native bounds、card/anchor/button、动画 epoch/phase/opacity、正文哈希与可见性、原请求身份和时钟。渲染前后核同一状态，要求 native=card、button=anchor、A⊆C⊆W；不一致的已写 PNG 留存并标失败。终态必须有真实可见正文和固定合成内容。仅匹配侧别外的主题、动作、视口、实际 DPI、焦点/禁用、终态和内容条件才可做前后像素对照。各进程 HWND/请求 ID 不要求跨侧相同；Full 中间帧记录实际时刻，不声称两版处于同一毫秒。不能把这些 raster 当作 DWM 截屏、物理键盘验证或 60Hz 流畅度证明。

原来源标签经 `TextTranslationCoordinator.Prepare/Submit`、原 snapshot cache、Shell 原 `TranslationChanged` 和原标签 formatter 得到；不直接填标签、不重复注入 Begin。before 按旧“生成版本/版本”格式核验，after 按新状态格式核验，并同时核真实版本状态。公开固定合成源与 `.invalid`、AuthNone profile 被严格约束；无密钥/custom headers/DevelopmentBoundary。唯一 held connector 不建立 socket、不返回 Stream；合成 Delta/Usage/Complete 明确不是真模型输出。每个 fixture/provider 仅一次 Start，首身份固定，完成和停止分别拥有不同原 Shell。原 gate 取消、Flow 和 connector Task 必须结算；原 Shell 关闭前不能释放附加 Provider。这里只覆盖普通合成选区来源，不覆盖 OCR/图片/本地 development 来源标签。

## 副作用、所有权和预算

这不是纯离屏工具。未来执行会创建原 HWND，设置页会激活/程序聚焦；原 Shell 会拥有托盘、消息与 session subscription，并在独占测试目录写原配置事务。不会修改系统 DPI/theme/motion。高对比处于开启状态时拒绝普通调色对照，不改系统设置。控件和框架的实际平台行为仍须未来 Windows 证据；不声明零系统读取。

入口以 CreateNew claim 占有空输出目录；新 fixture root 不复用。所有原件和 fixture state 留存，成功也不递归删除。每窗口固定原 retirement Task，失败不重建关闭者、不重新调用产品 retry。原关闭/Closed、Flow、connector 分别观察；清理 UNKNOWN 保留原 owner/任务/dispatcher，等待外部原进程边界，不能当作成功。

每次驱动 45 秒，每张最多 8,000,000 像素/2 MiB，每进程最多 24 张/16 MiB PNG，JSON 512 KiB。拟议新窗口为 **15 分钟 job、视觉阶段最多 6 分钟、4 个顺序进程各 45 秒内层与 60 秒原外层**；同一标准 Windows runner、同 DPI/session，分别 before/after × settings/overlay。外层必须保留原进程/输出、区分超时与真实关闭。该窗口和新 wrapper/workflow **待确认，未创建/未执行**。不恢复旧窗口，不真实安装、注册/删除、变更安全权限或操作用户电脑。

## 本轮现有环境证据

本轮只使用已批准、已安装的微软 .NET SDK 10.0.402 / runtime 10.0.13 和现有锁定缓存；locked restore、audit 保持。原件仅在本云环境 `artifacts/2026-10-25-ui-visual-probe/` 保存，不提交 TRX、DLL、baseline 副本或二进制归档。

- r01：Harness 首编译 3 错（枚举歧义、DpiScale 比较、nullable）；原日志保留。
- r02：Harness 0 warning/0 error；Provider 测试文件缺 Xunit using 导致 38 条编译诊断，未测试。
- r03：Provider 裸 Assert.Single 返回 Task 导致 CS4014，未测试；修为显式消费断言返回值。
- r04：Harness/Provider 编译均 0 warning/0 error；新 UiPolishProbeTests 16、原 OverlayProbeEvidenceTests 20，合计 **36 pass / 0 fail / 0 skipped**。885 项输入、130 个 DLL 的前后 SHA256 全同；所有 5 命令 exit 0、原进程组已空。脚本原报告的 filterClasses 元数据只列了新类，实际 CLI filter 与 TRX 均包含上述两类；保留原报告，范围以命令和 TRX 为准。
- r04 后仅改 WPF probe 的 raster 前后资格与输出 claim；组件测试输入未变，不重复运行测试。after r05 与固定 before 独立构建均 0 warning/0 error；各 2 命令 exit 0、原进程组空。before 构建后另核 12,315 原产品文件 SHA256/Git blob 全同；原 885 项清单仅覆盖主仓库/探针，不拿它证明 before 产品。实际产物与时刻见同名证据 JSON。

独审发现并修复：迟到 connector 串代、旧文案断言错误、误认为 Stop 自然禁用、失败重复关闭、像素/状态不同步、执行后异常误称准入拒绝、测试全局代理污染。专用探针进程在第一 Provider 前设置空代理；fixture 本身不改进程代理。组件测试集合禁止与其他集合并行，保存并恢复精确原代理对象，异步原件先结算。

**尚未有任何新 WPF PNG**；视觉质量、真实窗口可用性/焦点、Full/Reduce 像素与清理终态均 NotRun。组件通过与 Linux 交叉构建不能替代它们。

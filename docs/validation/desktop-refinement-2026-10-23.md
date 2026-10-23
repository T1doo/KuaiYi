# 桌面视觉与连续生长：2026-10-23

基线 `ea75f18c7e6189eece53f3df3adee66e9aaeac65`，原指定 Linux 环境、原 `dev/p7-fixed-signoff` 分支、root 唯一写入。短暂环境不可用后已恢复，实际 `pwd`/Git/SPEC 读取成功。最终项目无猫、噜噜、其他吉祥物及新增图像资源。

## 产品变化

- 白色阅读工作台和浅蓝导航，去掉旧版统一外层卡片与重复眉题。设置默认 940×740，仍受真实工作区约束、保留原最小尺寸；页标题、分组、标签、帮助文字各有层级。
- “交互与浮窗”重排为左侧名称/说明、右侧原控件的偏好行；其余页增加有意义的分组，长操作行可换行。原控件实例、选择值、事件、Mask、只读/恢复/保存和发送合同保留。
- API 空态只依赖真实 `profiles.Items.Count`；加载文字只在原保存任务进行时显示。状态区使用现有真实结果文字和 polite 元数据，不伪造成功。增加字段 Label/HelpText。
- 导航增加线性功能图标，选中与焦点有独立表现。按钮焦点环为不命中的叠层，不改变内容尺寸；焦点色使用实际控件前景，保留高对比的 Highlight/HighlightText 配对。保留系统输入框、密码框、下拉模板。
- 浮窗工具 hover、失败反馈和动态“翻译图片”提示细化。48/28/16 DIP、正文测量/位置、默认 Off、红绿状态、来源/密码/完整性、250/600 ms、取消/清理/隔离均未改变。
- 生长时 WPF 底板圆角现在复用同一帧已经量化的原生裁切半径，在原 PrepareVisuals/CommitLayout/ApplyBounds 顺序内更新。原代码底板固定 16 DIP、原生半径 24→16，存在参数不一致；此次签的是参数一致，未宣称已经实机看到或修复像素裁边。

## 核心动效的准确口径

原 SPEC §4.3—4.8（206—333 行）已经明确从原按钮向外连续生长，不允许普通弹窗代替。生产接线完整：

`ReceivePresentationAsync → ApplyResult/50ms FlushResult → OpenMeasured/Grow → CompositionTarget.Rendering → OverlayFrames.Tick → ApplyOverlayFrame → NativeDesktop.ApplyBounds → 同一 HWND 的 SetWindowPos(NOACTIVATE) / SetWindowRgn`。

`OverlayFrames` 从实际 Current 开始或反向，展开 220 ms cubic ease-out、收起 180 ms、首 80 ms 正文淡出、后续高度 70 ms 指数跟随。锚点固定、方向冻结、正文按目标宽度排版、每帧 `A ⊆ C(t) ⊆ W`；ReducedMotion 沿原立即布局。这些时限和算法本轮未调参。

GrowthHarness 默认 ProbeWindow 是独立原型；`--actual-overlay` / `--ci-actual-overlay` 才构造正式 ShellWindow 并采实际 HWND。既有自动观察只覆盖几何，不能据此签 §15/G04 的平均 ≥55fps、>50ms 帧比例、输入延迟或 50 次连续反向收放。父级已独核历史 4ff83e5 的 210 次 Rendering/九项合同；Opening 仅一次观察、最大 482.858 ms，不能证明流畅。本轮没有重跑或回填历史原件。

## 本轮实际验证及阻塞

- Core 定向 **109P/0F/0skip**：OverlayPresentationTests、OverlayReviewFixTests、GeometryTests、SettingsRecoveryRegressionTests。Provider **2P/0F/0skip**：DesktopPaletteTests，检查浅/深色文本、选择、主动作和输入边缘对比。均为本轮原 TRX，不相加旧轮次；候选树运行，对应输入摘要保全，不冒称整套或 Windows 运行。
- 候选 App 交叉构建通过；去除装饰及最终圆角补丁后的固定树构建结果将单独追加。原两次候选构建保留在 superseded，不借记为最终产物。
- Chromium `file:///workspace/KuaiYi/artifacts/2026-10-23-desktop-refinement/desktop-preview.html` 返回 “This page is blocked / Your organization doesn’t allow you to view this site”。所有所谓 before/after、深浅色、窄屏、大字、高对比截图实际都是拒绝页，均记 **Blocked / NotRun**，保全在 blocked-render，未改变策略、换 URL 绕过或宣称渲染成功。浏览器主进程均已结算退出。
- [独立 HTML 设计候选](../previews/desktop-refinement.html) 使用合成文本、没有联网/保存/清除/发送行为。只做了 HTML 结构与 Node 脚本语法检查；未能视觉验收。Library 当前 **libfile_8c82cb38f7888191804d61c0e25bab11 / v1**；v0 是已纠正的生成模板/废弃素材候选，不能使用。
- 本环境没有 Windows WPF 桌面。真实像素、DPI/200%文字、系统下拉/焦点/UIA、高对比、真实流式长文帧率、取消反向、录屏、250/600 ms 和完整 G04/P2/P7 **未验**。本轮没有 Windows CI 投递、用户电脑、真实模型或安装器执行；原四 DLL 回收和生产 Unavailable pins 仍保持。

## 只读学习

设计原则来自 [Anthropic frontend-design](https://raw.githubusercontent.com/anthropics/skills/main/skills/frontend-design/SKILL.md)、[Impeccable craft-floor](https://raw.githubusercontent.com/pbakaus/impeccable/main/.agent/skills/impeccable/reference/craft-floor.md)、[Web Interface Guidelines](https://raw.githubusercontent.com/vercel-labs/web-interface-guidelines/main/command.md)、[design-motion-principles](https://raw.githubusercontent.com/kylezantos/design-motion-principles/main/skills/design-motion-principles/SKILL.md)。只迁移适合 WPF 的真实任务结构、明确层级、克制装饰、即时高频反馈、焦点和减少动画原则；未安装/执行其脚本、launcher、hooks、npx 或二进制，也未把网页参考当 WPF 实机证据。

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

## 固定产品收口

产品完整 SHA `b89a9253706fb48a0218277ddd133dad256bc3ba`。该提交 App Windows 目标 Release 构建 exit0、0 Warning、0 Error；不是 Windows 执行。App DLL 583680B，SHA256 `f27effb37ec98e8b6bc2e3e4307b52601ae1949af2f2e890c2ad1b2408d8f72a`；源码和新 DLL 均无废弃插画字节/资源名。版本声明包含完整产品 SHA，实际 DLL 本体在 review-only-not-runnable 中供独审字节核验，不是完整可运行包。

9 个产品源码经只读独审限定 ACK；固定构建没有再扩大测试或重复整套。两份定向 TRX 是前述候选树实际执行，不改写为固定提交运行；相关 Core/链接源和 Provider 配色源与最终树一致，最终圆角仅属 App WPF 绘制，验证为交叉编译与源码审查。

完整原件 Library **libfile_ec54e1fd2a74819192bd5d1918ec5f52 / v0**：523442B、46 成员，ZIP SHA256 `4ef1bd1ed7c5fd5a533348fd8a4884446f52c715b3d1842b9423a9915b245aa0`。含9源Git blob、源码/输入及产物摘要、原TRX/日志/命令、明确归为阻断证据的浏览器图片、最终独立HTML和只读DLL。CRC和成员摘要在本环境校验；上传返回成功，全部返回xattr已应用并复核摘要。无私有下载URL。

本轮可以交付源码候选和可打开的设计文件，**不能交付已验收的精美像素或流畅录屏**。最小缺口是已有权限范围内可实际渲染当前WPF的Windows会话及原始帧/视频回收；本Linux环境的浏览器file://又被组织策略明确阻断，未尝试其他URL绕路。没有重新投递旧CI、访问已拒Actions API或使用用户电脑。

## 2026-10-24 独立暖橙生产主题候选

基于040553d7ce89f1042d46464533139642fcb29ecc，仅本地修改五份生产UI源：DesktopPalette浅深暖橙资源、DesktopTheme辅助工具透明底、SettingsChrome七页共用页头/独立反馈区、SettingsSections功能分区标题、OverlayPresentation工具区背景/朝向分隔线及来源标签字重。七类设置共用导航、页头、分区、反馈、保存主操作；浮窗保留正文为主，辅助操作区有轻背景与分隔。工具区背景与分隔线均非hit-test，保留Canvas原空白命中；不增加正文占位或修改测量、锚点、动画、命令事件、开关红绿及高对比分支。无宠物。附属独立对话框不纳入本片七页覆盖声明。

原有离线Provider定向11P/0F/0skip＝DesktopPaletteTests浅深对比度2例＋SettingsLayoutTests原布局规划9例，未新增测试或重跑全套。测试时857输入原件保留，随后仅OverlayPresentation从Canvas背景修成非hit-test背景层；四个相关测试/链接源字节不变，不把11P当WPF绘制测试。最终App＋GrowthHarness交叉构建exit0、0W0E、8.42秒；前一Canvas版15.60秒构建单独保留在superseded，不借记最终源码。测试快照source、最终快照final-source/最终build-inputs、命令/TRX/完整日志位于artifacts/2026-10-24-warm-ui。独立源码审查限定ACK，无剩余确定P1/P2。

此片未网络push、投递CI、原生执行、安装或调用模型。颜色对比度与离线编译不等于像素验收：暖橙后的真实WPF截图、窄窗/大字实际排版、高对比/键盘焦点、DWM/FPS、生长动画和250/600ms仍未验，旧版截图不能代签本候选。此前被拒的Windows诊断保持拒绝，未改workflow或来源绑定；原pins与全部安全门保持。

父级随后独审7d519cf1cd2e0c697ee29d9bdc0d09ff5ca3b0e9并单独授权正常Git推送。实际只读远端确认仍为040553d7ce89f1042d46464533139642fcb29ecc、待推仅原7文件且.github零差异后，普通push一次并读回远端7d519cf。现11项workflow均限定分支及自身yml路径，本次不匹配；未修改触发器或投递Windows诊断。此发布事实不回填暖橙原生像素验收。

## SPEC8.2 显示原文偏好，本地候选

既有Overlay.ShowSource从未被生产读取，本片在交互设置增加默认关闭的草稿复选框，经原LoadGeneral/CapturePreferences和原子保存/取消链生效。生产读取精确SelectedVersion对应TextTranslationSnapshot的不可变设置，引用不符或OCR/图片不自动展开；不读取当前live设置替代旧版本偏好，不调用主动ShowOverlay/Expand/BringIntoView，不取新源或发送。

正文仅保存retained版本的人工bool选择，不持有额外原文/图像owner；测量视图只同步正文实际状态。初始化不冒充人工输入；人工折叠优先，流式刷新和同版本视图退役不覆盖它。显式人工展开只留在当前所选版本，源切换立即剪枝，保持原失败回滚默认折叠合同。Clear全文清状态，ClearSource只退役视图。初始化按人工覆盖优先的实际初始值检查可读门，属性重入后复查；被拒则仅退役原installed引用与版本匹配的区域，不清掉重入的新区域。

实际本地证据分列：r01新11P；独审两初始化P2反例r02同方法0P2F，修后两方法字节不变；r03相关集合88P1F，暴露原默认false的失败回滚被人工展开污染，原断言未改；修正后r04同89集合全部通过＝新14＋原MarkdownSource38/ProductionBridge9/OverlayStopFlow28。不同轮次不相加。r05最终App＋工具交叉build0W0E16.72秒，859源输入与r04一致且无漂移。命令/完整TRX/日志/源码/DLL保留于artifacts/2026-10-24-source-display，均在提交前构建，不伪称含之后的Git版本。独立源码审查限定ACK；本片不push、不CI、不原生执行、不运行真实模型，WPF像素/原生焦点及安全性能门未验。

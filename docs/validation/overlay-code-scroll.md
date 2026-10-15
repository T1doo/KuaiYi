# SPEC §4.2/§11.5：围栏代码局部滚动

本批从已接收的 `93fa2eed5ab5287f58affb9daa3e8bf7bc7baace` 建立普通开发分支 `dev/overlay-code-scroll`。先核实际工作区、HEAD、worktree、进程、默认网络远端：起始工作树干净，远端表格分支与 HEAD 一致，main 仍 `51d077481cf6303602fcf5d2718213da9cf05178`。本任务唯一写入者，独审只读，不恢复旧实例，不删除未知锁或进程。读取 AGENTS、SPEC 4.2/4.6/11.5/B12 及前批生产链，使用本环境实际存在的 SDK/资产，不假设跨容器文件共享。

## 生产构造与尺寸

围栏代码由 `MarkdownFlowView` 直接构造 `BlockUIContainer → TextBox`，完整保留代码体，设置 IsReadOnly、禁 Undo、禁 Drop、可见只读光标、Consolas/Normal、透明背景及当前正文前景色。TextBox 不解释 HTML、链接、XAML 或代码；无新依赖、图片/资源/执行通道。原工具栏复制继续取完整 `OverlayResults.Text`（含围栏），局部代码复制使用原生只读文本控件。没有从渲染树重建源文。

Code TextBox 明确 `NoWrap`，水平和垂直滚动条均 `Visible`，Width/MaxWidth 为有限正文页宽，MaxHeight 为 `min(240 DIP, 可用正文高度上限)`；没有把整份 FlowDocument 设为无限宽。外层两份 RichTextBox 均只读、禁 Undo、HDisabled/VVisible、ClipToBounds，FlowDocument 的 PageWidth/ColumnWidth 为同一有限宽度，普通段落继续按该页宽排版。局部双滚动条和外层可见纵向条保留空间，短代码也承担这部分空间，避免正文/测量因 Auto 是否出现而使用不同策略。

统一 `MarkdownViewport` 从 LayoutPlan 计算：正文宽 `max(1, targetWidthDip-32)`，正文高度上限 `max(1, maximumHeightDip-80-labelHeight)`，标签为 0/20 DIP；页宽再扣 WPF `SystemParameters.VerticalScrollBarWidth`，至少 1 DIP。两份 host 和所有代码区同步使用同一参数。生产 `MeasureBody` 委托同一 `MeasureMarkdown`，以有限 Size 调用真实 measurement.Measure，不再传 Infinity；它读回的 DesiredSize 仍需 WPF 实测。外层可见高度仍由原几何动画决定，上述值是共同上限，不把首帧硬设为最大高度。

PrepareVisuals 同步该策略；拖动时计划为空，则按当前卡片的有限尺寸约束，跨屏缩小时先把冻结的文字宽度夹到当前卡片宽度。原 HWND/OverlayFrames 的几何规划和锚点不变量没有改变，不从代码长行反推窗口宽度。极小空间以至少 1 DIP 的有限约束避免负值/无限值；这不证明极小工作区的控件可操作性已通过。

WPF [BlockUIContainer 文档](https://learn.microsoft.com/en-us/dotnet/api/system.windows.documents.blockuicontainer?view=windowsdesktop-10.0)支持嵌入本地 UIElement；[IsDocumentEnabled 文档](https://learn.microsoft.com/en-us/dotnet/api/system.windows.controls.richtextbox.isdocumentenabled?view=windowsdesktop-10.0)说明必须启用文档交互才能操作嵌入控件，因此两份 host 从 false 改 true。工厂仍仅构造本文列出的本地文本/表格元素，未增加 Hyperlink/Image/命令。滚动条属性本身会影响测量，故与有限尺寸一起配置，不把 Auto 当作完成证据。

## 输入、流式和清理

代码区显式接鼠标滚轮、左键、按住左键移动、触摸、导航键/Ctrl+A 以及 ScrollBar.Scroll。它们调用生产 `ObserveCodeReaderIntent`，同步失效旧滚动恢复并暂停外层跟随。祖先 body 可能重复观察同一输入；其 Background 复判仍经过原修订/结果/锁屏守卫，并在代码阅读暂停时保持暂停，即使外层已到底。用户点击“有新内容”明确恢复，或 Begin/版本切换重置；保留同版本回看时的暂停。

稳定代码块保引用；同一选中版本、同一位置、Source 追加的开放代码尾也复用 TextBox。正文 AppendText 或关闭围栏导致的文本修正后，合并尚未交付的选择范围和双偏移快照，Loaded 回调仅在以下条件仍成立时恢复：本代码区最新 revision、原全局 reader-intent revision、resultRevision、未锁屏/退出/Dispose。普通 Flush 的滚动修订不被当成新用户输入；真实输入、隐藏/收起、明确跟随或清空则否决旧回调。新输入优先于旧选择快照；多个追加先于布局时保留首个有效快照，逆序回调只允许最新一份应用。

测量代码区不持用户恢复任务。版本替换、非追加/语法类型变化、容量回退、清空或退出会 Dispose 旧区域：解绑全部事件、失效回调、清 Text、选择和偏移；旧捕获的委托也不能触发当前阅读状态。模型/原始三版本缓存仍按原规范在内存保留，清控件不作物理擦除字符串的承诺。

独审发现并修正了两个初版缺口：仅复用原 ObserveBodyReaderIntent 会因外层到底而错误恢复跟随；每次开放尾段重建会确定性丢局部状态。初版 171/171 绿色回执未覆盖它们，保留为中间结果，未冒充最终验证。

## 实际执行与失败保留

Linux x86_64，SDK 10.0.401；所有 dotnet 前显式 `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`，也 export 给子进程，复用已有 activate.sh。没有新依赖/restore，43 锁文件原字节保持；证书不读库、不读配置、不 trust/delete，原存储/信任仍 Unknown。

```bash
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
DOTNET_GENERATE_ASPNET_CERTIFICATE=false dotnet test tests/KuaiYi.Provider.Tests/KuaiYi.Provider.Tests.csproj --no-restore -c Release --filter 'FullyQualifiedName~Markdown|FullyQualifiedName~OverlayScroll|FullyQualifiedName~OverlayReader' --logger 'trx;LogFileName=review-fixed.trx' --results-directory artifacts/code-scroll
DOTNET_GENERATE_ASPNET_CERTIFICATE=false dotnet build src/KuaiYi.App/KuaiYi.App.csproj --no-restore -c Release -p:EnableWindowsTargeting=true
DOTNET_GENERATE_ASPNET_CERTIFICATE=false dotnet test tests/KuaiYi.Provider.Tests/KuaiYi.Provider.Tests.csproj --no-restore -c Release --logger 'trx;LogFileName=provider-final-guards.trx' --results-directory artifacts/code-scroll
```

最终 Provider **699/699 Pass，0 Fail/Skip** = 原 657 + 新 42。专项修后 174/174（新增最后四个守卫例之前）、中间全量 695/695 均保留。App Release Windows 目标交叉构建 **0 Warning / 0 Error**，此后仅补四个测试和文档，没有生产变更。完整 Core.Tests 工程本轮 NotRun。

Linux 测试直接链接真实 FlowView、CodeRegion、Viewport、OverlayMarkdown 和 OverlayScrollFlow：50000 字符无空格长行完整性、普通段落和有限页宽、相同正文/测量局部属性、DPI/标签/小卡片、非法 NaN/Infinity 预先拒绝、实际 MeasureMarkdown 的有限 Measure 参数、字体/前景、版本/失败/取消/清空/容量原文路径、显式事件委托、双观察、外层到底时暂停、开放尾段复用、合并追加选择恢复、逆序/输入抢先/版本/锁屏/退出/Dispose 守卫。数据端口记录构造/属性/委托与恢复调用；对 Text 改动刻意清选择/偏移以测试恢复协议，不模拟或宣称 WPF 的真实行为。

首轮编译失败：数据端口 EventHandler 的 sender 可空性与生产事件方法不匹配，CS8622 共 12 条诊断（订阅/解绑），0 测试执行；改方法接收 `object?` 后通过，未关 warnings-as-errors。原 `baseline-first.log` 保留。其余运行无测试或构建失败。前两批原始 19 份回执（含更早 71/72 失败）按既有 hash 保全；本批日志/TRX 位于 `artifacts/code-scroll/`，精确字节/hash/计数与独审见[固定证据](../test-evidence/2026-10-15-overlay-code-scroll.json)。

## 未验边界

真正的 WPF 测量/字体/模板滚动条空间、代码局部水平和纵向滚动、选择及跨区域复制、附加视觉树、父子路由、阅读锚点和开放代码追加后的实际选择/偏移恢复均 **NotRun**。数据端口传回的尺寸和选择值不能替代 WPF。原生 TextBox 独立选择不证明整份富文本的统一选择复制；原始完整复制入口保留但系统剪贴板未运行。表格实际宽度/选择、完整 B12、B06/B07、P2/B 组和完整 §11.5 验收仍未通过；P2-04 保持 InProgress。

Actions Forbidden 状态保持停止，本批未查询、未绕路、未发起 CI，运行数 Unknown。无 Windows/CMake/探针实验。250/600ms、来源/密码/完整性/取消/清理门、生产离线复用 Off 保持，所有 Core 安全生产文件原字节不变。无 main 修改、强推、部署、新费用/凭据/权限/协议或真实模型。最终远端和未提交/未推送差异另附交接回执。

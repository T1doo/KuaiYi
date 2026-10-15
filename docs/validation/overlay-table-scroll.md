# SPEC §4.2：长表格局部横向滚动

本批唯一范围是基础表格的局部视口。从已接收代码滚动提交 `06d1df264c737a09bda81ac2e597cdc7dcec0e1d`（父 `93fa2eed5ab5287f58affb9daa3e8bf7bc7baace`）建立普通开发分支 `dev/overlay-table-scroll`。先核实际文件、Git/HEAD/远端祖先、默认网络、单一 worktree 和进程；起始工作树干净，远端代码分支一致，main 仍 `51d077481cf6303602fcf5d2718213da9cf05178`。本任务唯一写入者，独审只读；不恢复旧实例，不假设跨容器共享，不删除未知锁或进程。按 AGENTS 和 SPEC 4.2/11.5/B12 继续，解析器及原安全门未改。

## 原生表格和有限尺寸

生产 `MarkdownFlowView` 将原生 Table 放入 `BlockUIContainer → 只读 RichTextBox → FlowDocument → Table`。原有列、表头、RowGroup/Row/Cell、左中右对齐、Run/Span 粗体/斜体/行内代码和精确 Source 模型继续使用同一工厂；没有把行列关系压成文本。局部 RichTextBox 禁 Undo/Drop、禁文档命令交互、透明背景、当前前景和字体、ClipToBounds；双滚动条均 Visible，Width/MaxWidth 为外层共同有限页宽，MaxHeight 为 `min(320 DIP, 可用正文高度上限)`。正文和测量使用相同 `MarkdownViewport` 和构造链，外层仍 HDisabled/VVisible、有限 PageWidth/ColumnWidth；代码控件和普通段落路径未改。

每列按所有显示 cell 的最长 UTF-16 长度估算宽度，长度只取至 512，`clamp(8 + length × fontSize × 0.65, 96, 480)` DIP；这是确定性宽度策略，**不是字体实测**。TableColumn.Width 显式设为固定 GridLength，原 CellSpacing=4、Padding=4/2 保持。局部页宽为列宽与间距之和、局部可用宽度二者较大值，最多 8192 DIP；16 列均为最大宽时实际策略值 7748。长单格可在有限列内换行，多列/宽列通过局部宽页滚动，外层页宽不跟随表格增长。短表也保留可见滚动条空间；极小窗口约束有限，但可操作性未因此验收。

WPF [TableColumn.Width 文档](https://learn.microsoft.com/en-us/dotnet/api/system.windows.documents.tablecolumn.width?view=windowsdesktop-10.0)说明列宽包括内容、padding 和 border；[GetPositionAtOffset 文档](https://learn.microsoft.com/en-us/dotnet/api/system.windows.documents.textpointer.getpositionatoffset?view=windowsdesktop-10.0)说明 offset 计数文档符号；[GetInsertionPosition 文档](https://learn.microsoft.com/en-us/dotnet/api/system.windows.documents.textpointer.getinsertionposition?view=windowsdesktop-10.0)提供向内找有效插入位置的 API。以上用于设计和交叉编译核对，不能代替 WPF 布局/选择实测。

## 尾段、阅读和清理

同版本、同位置、Source 追加且列对齐/行数兼容的开放表尾复用局部控件、Table 和既有 cell；只替换有变化 cell 的 Paragraph，追加新行。稳定表头和不变 cell 的原生 Run/Span 引用保留，字体、前景和有限视口更新不主动归零偏移。Measure-only 更新测量表格，正文等待正常 Flush。

局部滚轮、左键/拖选、触摸、导航键/Ctrl+A、滚动条输入接现有生产阅读意图守卫；表内阅读使外层保持暂停，外层到底不会自行解除，明确“有新内容”跟随或新结果才恢复。没有修改既有 OverlayScrollFlow、OverlayMarkdown 或代码区实现。

追加前捕获选择两端所在稳定 cell 的行列坐标及 cell 内部 symbol offset，表/行间隙和 ContentEnd 映射到旧 cell 内有效插入位置，排除 Run/Paragraph/Cell/Table 的闭合结构符。这样追加新行或增长原末格都不以旧闭符 offset 选择新增文字。逆序/多次布局前追加合并首个有效快照，仅最新局部 revision 且全局 reader-intent、resultRevision、未锁屏/退出/Dispose 时 Loaded 恢复选择和双偏移；真实新输入优先。测量表格不排队用户状态恢复。

版本替换、语法/容量整文回退、清空会 Dispose 旧区域：解绑七类委托、失效旧回调，清文档、原 Table 行组/列、选择和双偏移，释放模型引用；迟到捕获委托不能影响新版本。原模型/三版本原文仍按原策略在内存保存，不作物理擦除承诺。工具栏完整复制仍取 `OverlayResults.Text`，不从渲染树重建源文。

独审修正了两层选择边界问题：初版全局绝对 offset 会落入新增行；仅改 cell-local offset 仍会包含末格增长时后移的 Run/Paragraph 闭符。最终使用单元格锚点加有效插入边界，并保留三项显式结构偏移回归。它们是生产协议测试，不是 Windows 交互回执。

## 实际运行

Linux x86_64，已有 SDK 10.0.401 与资产；本批不 restore、不增依赖。每个 dotnet 命令均显式 `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`，并 export 给子进程；不读证书库/配置，不 trust/delete，原存储/信任 Unknown。

```bash
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
DOTNET_GENERATE_ASPNET_CERTIFICATE=false dotnet test tests/KuaiYi.Provider.Tests/KuaiYi.Provider.Tests.csproj --no-restore -c Release --logger 'trx;LogFileName=provider-final.trx' --results-directory artifacts/table-scroll
DOTNET_GENERATE_ASPNET_CERTIFICATE=false dotnet build src/KuaiYi.App/KuaiYi.App.csproj --no-restore -c Release -p:EnableWindowsTargeting=true
```

最终 Provider **740/740 Pass，0 Fail/Skip** = 前批 699 + 新表格滚动 41；相关九类在最终全量中共 219。真实链接生产 FlowView/TableRegion/Viewport/OverlayMarkdown/OverlayScrollFlow，覆盖有限列宽/页面及长中文完整性、表格结构/样式/代码惰性、正文和测量同步、DPI/标签/小视口、稳定表/尾格/追加行复用、选择锚点/合并快照/逆序恢复、局部/祖先输入和版本/锁屏/退出/Dispose 守卫、实际三版本缓存/失败回退/取消清空/迟到及整份原文回退。前批 699 原有用例全部复跑；原六项表格桥接断言仅改原生 Table 的包装定位，行列/样式/原文断言保留。

最终 App Windows 目标 Release 交叉构建 **0 Warning / 0 Error**，在最终生产代码及 740 全量之后运行。先前专项 **218/218** 和初版 App 0W/E 的原回执保留；它们不覆盖最后的末格闭符修复。首轮测试编译因 `Assert.Single` 前使用 Where 触发 **xUnit2031**，0 测试执行、Exit 1；改用 predicate overload，未关警告规则，原 `focused-first.log` 保留。本批没有其他失败。完整 Core.Tests 工程 NotRun。

数据端口只记录构造、属性、事件委托、排队和恢复协议；SymbolStart/End/InsertionStart/End 是显式测试输入，不是 WPF 树或选择仿真。前三级批次 29 份原始日志/TRX（含原失败）逐字节/hash 复核保全。当前七份执行日志/TRX、22 份相关源码/测试 hash、既有文件保持及独审记录见[固定证据](../test-evidence/2026-10-15-overlay-table-scroll.json)，原文件在 `artifacts/table-scroll/`。

## 保留的未验收项

真实 WPF 表格水平/纵向滚动、滚动条模板空间、实际列宽/字体/换行/DesiredSize、Attached visual tree、单格/多格/跨区域选择及原生系统剪贴板、追加后的实际选区/偏移/阅读锚点、父子输入路由均 **NotRun**。独立 RichTextBox 的表内选择不能证明整份富文本统一选择复制；完整原文复制代码保持，系统剪贴板未执行。真实代码滚动也未在此批验收。**B12、B06/B07、P2-04 和完整 §11.5 均未标完成，P2-04 仍 InProgress。**

Actions 先前 Forbidden 后保持停止，本批不查询、不换路、不主动 dispatch，服务端运行数 Unknown。没有 Windows/CMake/probe 实验。八 workflow、43 锁、全部既有证据和 Core 生产文件原字节保持；250/600ms、来源/密码/完整性/取消/清理门及生产离线复用 Off 不变。无 main/旧分支修改、强推、部署、费用/凭据/权限/新协议或真实模型。推送后的完整 SHA、远端 refs 及未提交/未推送差异另见交接回执。

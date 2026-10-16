# SPEC §11.5：基础表格接正式正文和测量

本批唯一父提交为已接收的 `063969062de54c6fda50bc9d683d185789e7d25c`，正常新分支 `dev/overlay-markdown-tables`。先实际核 `/workspace/KuaiYi` 文件/Git/worktree、默认网络远端和进程：HEAD 与远端行内分支一致、工作树干净；main 仍 `51d077481cf6303602fcf5d2718213da9cf05178`。没有恢复旧实例，本任务唯一写入者；独审代理只读。既有 Git 僵尸、SDK 编译服务和未知 `codex-index-refresh.lock` 均未删除或终止。未假设跨容器 artifacts 共享，本轮使用本环境实际存在的已恢复 SDK/obj，并单独保存本批回执。

读取 AGENTS 与 SPEC §11.5，沿用[行内切片](restricted-markdown-inlines.md)。本批实现基础管道表格的完整生产路径，不把完整 §11.5、P2-04、B 组或生产验收登记为完成；P2-04 仍 InProgress。

## 表格规则和原文保全

- 支持普通单行表头、紧邻的分隔行及可选数据行；每行至少有一个代码段之外、未转义的管道分隔符。外侧管道可选；一列格式使用外侧管道。单元格两端仅裁展示用空格/Tab。表头若以既有标题、列表或围栏块语法开头，仍走原块语法，不将其升级为表格。
- 分隔行每格至少三个 `-`，可在左右加 `:`；无冒号为默认左对齐，左/两侧/右冒号分别左/中/右对齐。内部空白、短横线不足、额外字符、列数不一致均不做猜测、补列或截列。
- 表头与数据单元格继续消费真实受限行内解析器，支持已实现的粗体/斜体/行内代码和转义。反斜杠在代码外保护其后字符不成为管道分隔符，奇偶规则与行内转义一致；匹配反引号段内的管道与反斜杠都为惰性原文。代码内不解码转义。
- 一张候选表内任意单元格未闭、嵌套/交叉/歧义或含不支持链接/HTML 等语法，或者结构/列数不合法，整个候选表退为一个完整原始 Plain 块。候选表覆盖表头、分隔行及连续含管道的尾行；无管道行是表格边界，继续按后续块处理。图片/链接/HTML/资源和命令从不加载或执行。
- 每个表块 `Source`/`Text` 保存整张表原文；独立表数据保存原始分隔行、含原换行的各行 Source，以及含裁剪前空白的 cell.Source。行内 Source 保留裁展示空白后的单元格正文。任何更新后的块 Source 拼接等于整个输入；各表行 Source 加分隔行精确组成表块 Source，包含 CRLF、拆开的 UTF-16 代理对和尾行。复制仍取 `overlayResults.Text`，不从表树重建。实际系统剪贴板未测。

有限范围：每表最多 **16 列、128 行（含表头，不含分隔行）**，整份文档累计 **2048 单元格**；原有 **4096 行内片段、2048 块、120000 UTF-16** 继续生效，空格/代码/Plain 块等的行内计数规则不变。任一容量超出，整份当前有限结果回退为一个完整原文块；不截断，也不只保留已经解析的行。原上游 1048576 单元仍明确拒绝且不改已有模型。所有表计数随清空、非追加重建、版本变化和容量回退重置。

## 流式前瞻和原生接线

表头不能在分隔行尚未完整到达时冻结为普通段落。普通段落扫描在完整管道行前切开，留出表头前瞻；表头已终止、分隔行已终止或生成结束时才判断表结构。尾部裸 CR 等待可能的 LF。表体行可以跨分片，暂时不合法的内容完整字面显示，后来闭合时再转表格。

开放表格始终是一个可变尾块，会有界地重解析/重建整张开放表，不宣称逐 cell 更新。只有遇到已终止的无管道行或生成结束才冻结表块；尚未终止的无管道尾行可能在后续追加中变成表行，因此不能提前冻结。稳定前缀只按连续的 stableEnd 推进，未决表尾后的其他块不能跳过它推进前缀。确定边界后的表块/原生元素与前面的稳定段落按引用复用。

`MarkdownFlowView` 为真实表数据构造 WPF `Table → TableRowGroup → TableRow → TableCell → Paragraph → Run/Span`，单元格复用原有安全行内构造；一份行列顺序和对齐模型同时供正文和测量消费。表头段落 SemiBold，代码 Span 的 Normal 样式保留；CellSpacing=4、单元格内边距 4/2 DIP，不指定固定列宽。没有 UI 外挂网格或孤立解析器。此原生容纳关系依据 [Microsoft Table Overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/table-overview)，没有使用 XAML 解析、外部资源或新依赖。

既有 `OverlayMarkdown` 的 selected-version/Complete/Stopped 选择、50ms FlushResult、MeasureBody、CopyResult、阅读意图守卫、取消/隐藏/锁屏/清空入口及三版本策略原字节保持。表块与所有新增计数经既有 ClearMarkdown 清理。

## 本轮实际执行

Linux x86_64、SDK 10.0.401；每个 dotnet 命令前显式 `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`，且 export 给子进程继承。使用现有 `activate.sh` 提供的 SDK/CLI home，未新增依赖或 restore。App 使用已有 locked 资产 no-restore 交叉构建；43 份锁文件原字节核对。证书存储/信任仍 Unknown，不读证书库或配置，不 trust/delete。

```bash
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
DOTNET_GENERATE_ASPNET_CERTIFICATE=false dotnet test tests/KuaiYi.Provider.Tests/KuaiYi.Provider.Tests.csproj --no-restore -c Release --filter 'FullyQualifiedName~Markdown' --logger 'trx;LogFileName=tables-first.trx' --results-directory artifacts/markdown-tables
DOTNET_GENERATE_ASPNET_CERTIFICATE=false dotnet test tests/KuaiYi.Provider.Tests/KuaiYi.Provider.Tests.csproj --no-restore -c Release --logger 'trx;LogFileName=provider-final.trx' --results-directory artifacts/markdown-tables
DOTNET_GENERATE_ASPNET_CERTIFICATE=false dotnet build src/KuaiYi.App/KuaiYi.App.csproj --no-restore -c Release -p:EnableWindowsTargeting=true
```

最终完整 Provider **657/657 Pass，0 Fail/Skip** = 前批 610 + 新增 47（真实 Core 表格 41、链接生产代码桥接 6）。完整 Core.Tests 工程本轮 NotRun。覆盖 14 份样例全部两段切点及逐 UTF-16 单元、30 轮固定种子分片、表头/分隔行前瞻、跨片尾行和强调闭合、CRLF/Unicode、反斜杠奇偶及代码内管道、四种对齐、空格/空单元格、整表不支持回退、16/17 列、128/129 行、2048 累计单元格和 4096 行内片段两侧、字符超限、稳定表引用及版本/清空重置。流式相等按索引比较表的对齐、行、单元格、Source/Text 与行内片段，避免顺序被无序等价掩盖。

桥接工程直接编译生产 `MarkdownFlowView`/`OverlayMarkdown`，数据端口只承载构造形状和属性，核正文与测量都实际构造 Table、行列/对齐/代码样式、测量先于正文、未决尾行变成表行、稳定元素和字号、开放表遇不支持单元格后整张字面回退，以及真实 OverlayResults/RuntimeGate 三版本选中/失败回退/取消/锁屏清空/迟到拒绝及原始复制数据。不是复制解析器的测试，也不是 WPF 运行证据。

App Release Windows 目标交叉构建 **0 Warning / 0 Error**。本批无测试/构建失败；新增测试前 Markdown 回归 72/72、初版表格专项 117/117、增强后中间全量 657/657 回执全部保留。独审建议的顺序及转义覆盖补强后重新执行上述最终全量；不把父级接收或独审当作另一轮 SDK 执行。前批 71/72 的真实失败 log/TRX 等十份旧回执按旧证据 hash 原样复核保留，没有回填为本批全绿历史。原始本批回执目录为 `artifacts/markdown-tables/`，固定 hash/字节/计数见[证据 JSON](../test-evidence/2026-10-15-restricted-markdown-tables.json)。

## 未验和授权边界

WPF 实际表宽、换行、DesiredSize、字体继承、选择/复制、开放表整块重建时的阅读锚点、实际原生元素复用、输入刷新交错及 B06/B07/完整 B 组均 **NotRun**。数据端口不模拟布局或选择，不能替代这些证据。代码块无折行横向滚动仍未完成/未验；复杂表格（合并单元格、多行单元格、复杂行内/嵌套块）不支持，保持完整字面文本。没有宣称完整 §11.5 验收通过。

Actions 拒绝状态保持停止，本批未再查询、未换工具/路由绕过；CI 实际运行数仍 **Unknown**。8 份 workflow 与既有证据原字节保持；未发起 Windows/CMake/探针或 workflow。250/600ms、来源/密码/完整性/取消/清理全部门与生产离线复用 Off 保持。无 main 修改、强推、部署、新费用/凭据/权限/协议或真实模型调用。最终 SHA、远端和未提交/未推送差异在推送后的交接回执记录。

# SPEC §11.5：受限 Markdown 生产展示切片

> 2026-10-16 当前状态更正：下文“未实现/未完成”是本页原批次时点。后续已接入[行内粗体/斜体/代码](restricted-markdown-inlines.md)、[基础表格](restricted-markdown-tables.md)、[代码局部横向滚动](overlay-code-scroll.md)及[表格局部横向滚动](overlay-table-scroll.md)，并有生产代码/便携测试及 App 交叉构建证据；当前不能继续记为缺少实现。真实 WPF 排版、选择、横滚、阅读锚点与 B12/完整 §11.5 验收仍 NotRun；历史数字和原始回执保持。

基线dd9c0796b3708acea92cb420128c035b6947bfe7，原产品分支dev/overlay-reading-input，root持独占写入锁、独审只读。此批直接接原OverlayResults→50ms Flush→正式浮窗，没有新的展示窗口或网络入口，没有新增依赖/许可证。**这是§11.5的受限切片，整个§11.5、P2/B组与生产验收均未完成。**

## 实际支持与回退

实现段落、1—6级ATX标题（行首#加空格）、顶层单行无序/数字列表，以及行首3—32个反引号或波浪号围栏代码。代码只用等宽字体，不做语法高亮或执行；未闭围栏保留全部已收到的代码。粗体、斜体、行内代码、表格、复杂/嵌套列表语义、链接打开仍未实现。已检测到的不支持行内/表格/HTML语法整块按原始字面显示，未识别块也保留其字面文本，不能把本批称为完整CommonMark。

每块保留精确Source；任意更新后的Source拼接等于当前结果，包含CRLF、Unicode和围栏原文。工具栏“复制”仍直接取overlayResults.Text，保留原始Markdown，不从渲染树还原。原生局部选中复制不等同完整原始Markdown复制，其实际行为仍未验。

最多格式化120000个UTF-16单元、2048块；超过任一上限时整份有限结果退为单个原始纯文本块，不截断。输入仍受原OverlayResults的1048576单元上限约束，超出该上游约束明确拒绝且不改已有模型。模型无递归、正则或外部解析器；不是性能/帧时间验收。

## 流式生产接线

RestrictedMarkdown只重新解析未稳定尾段；完整行分隔、围栏闭合等明确边界后才冻结前缀，CR末尾等待下一块LF。每次快照仍做有界前缀检查，并非宣称常数时间。原版本切换/失败回退使用实际SelectedVersionId及selected文本；同一稳定块保留对象引用，变动尾段替换。

正式正文使用只读、禁Undo的RichTextBox。MarkdownFlowView仅构造本地FlowDocument、Paragraph、List、ListItem和Run；按引用保留稳定原生块，仅替换变动尾段，字号偏好变化才重新设置稳定块格式。没有Hyperlink、Image、资源URI、HTML解释器、XamlReader或命令执行，图片/HTML/所有链接均惰性文本。[RichTextBox官方说明](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/richtextbox)和[FlowDocument说明](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/flow-document-overview)支持这一原生文本模型；本批未使用自动资源加载。

测量控件也使用相同的富文本块与字体，不再按原始Markdown TextBlock估高。正文和测量各有独立FlowDocument，不能把一个FlowDocument挂给两个宿主。原Capture offset→Flush→受修订号保护的恢复、阅读输入暂停、新内容提示、三版本规则、复制命令、隐藏/取消/迟到事件门均保留；锁屏清空/退出同时清模型与两份文档，无Undo内容历史。停用/收起仍按原规格保留允许回看的会话缓存，不更改原取消语义。

## 真实验证与失败记录

最终Provider **566/566通过，0失败/跳过**，其中28项新生产模型测试、原538项完整执行。覆盖12份样例每个两段切点及逐UTF-16单元输入、50轮固定种子任意分片、未闭围栏、CRLF/裸CR/拆开的代理对、50000字符代码长行、120000/2048精确边界后继续追加触发完整回退、稳定块复用、三版本真实淘汰/失败回退、取消/清空/迟到隔离。断言执行实际Core模型及OverlayResults/RuntimeGate，不是复制解析算法的测试替身。

第一次Core测试因缺少project.assets.json返回NETSDK1004，0测试执行；未恢复新依赖，改用已恢复的Provider测试工程引用真实Core。首轮27项有26通过/1夹具错误：第五版本产生时被淘汰的版本不应可选；修正为受保护的已选前驱，并新增明确淘汰断言，生产缓存策略未改。中间565/565回执保留；补临界两侧断言和失败回退例后最终566/566。失败日志/TRX均原样保留，不把它们写成全绿历史。

App Release、EnableWindowsTargeting、locked/no-restore交叉构建 **0警告/0错误**；生产代码在该构建后未改，仅补测试和文档。独审核源码、测试和保存回执；未另启SDK。精确hash、计数与失败记录见[固定证据](../test-evidence/2026-10-15-restricted-markdown.json)。8个workflow、43个已跟踪锁文件（含命名Alpha锁）及前批33+11+6份证据文件逐字节保留。

## 未验边界

环境仍Linux。WPF实际排版、字体度量、未挂视觉树测量控件的DesiredSize、原生块运行时复用、选中复制、阅读位置及输入/刷新交错、新内容提示可达性和B06/B07均 **NotRun**。代码目前遵循FlowDocument默认换行；设置Auto滚动条不证明长代码无折行或横向滚动可用。段落尾部重排、容量回退和版本切换仍可能影响实际阅读/选择，必须以Windows证据核验，不能用模型对象引用测试冒充。

每次dotnet前显式DOTNET_GENERATE_ASPNET_CERTIFICATE=false并由子进程继承；复用原CLI home。旧证书存储/信任仍Unknown，不读库、trust或delete。本批日志没有新的安装提示，不据此宣称存储未变。250/600ms、来源/密码/完整性/取消/清理门保持；原CMake根因Unknown，不新增参数或Windows CI实验。不改main、强推、部署、增加费用/凭据/权限或真实模型调用。正常推送仍用不匹配保留CI分支的产品分支。

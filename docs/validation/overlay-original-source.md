# 所选版本的原文折叠

本批唯一父提交为已签收 `3235cf86af6652addd032de03b6123498ec2d875`，正常开发分支 `dev/overlay-original-source`。起始工作树干净、唯一worktree，远端HEAD相同；main仍为 `51d077481cf6303602fcf5d2718213da9cf05178`。本任务唯一写入者，独审只读；既有MSBuild常驻节点、僵尸及未知锁未动。

## 原生能力和切片选择

指定环境仍是Linux x86_64，无DISPLAY/WAYLAND；实际 `dotnet --list-runtimes` 只有 Microsoft.NETCore.App / Microsoft.AspNetCore.App 10.0.12。未安装/运行Windows、Wine或额外探针，未访问Actions。B06/B12/B07，包括代码/表格暂停后Stop、历史版本重开、50次收起/展开中反向操作，全部 **NotRun**。最小解阻是已有授权的Windows11交互桌面与已安装的.NET10 Desktop Runtime；[既有清单](../p2-overlay-windows-checklist.md)补充操作和工具限制，无新增CI或外围工具。

SPEC4.2要求卡片内容区内默认折叠的“查看原文”。原生产路径 SelectionFlow.AcceptSelection 在翻译启动前就更新 selectionSource，并即时替换独立原文窗口。若A译文保留，随后B长文本确认取消，或B失败回退A，查看入口仍读取B。这是可由源码与实际Coordinator/Results路径确定的错配，不依赖WPF实测。

本批将文本原文接入正式原生卡片；图片预览不在范围内。独审另发现SPEC8/14.1的生产“一键清空当前内容”入口仍缺失，明确留为下一核心项。本批只验证既有内部Clear生命周期，不称已有用户可达清空命令。

## 生产行为

唯一来源是 `translation.SelectedSnapshot.Source.RawText`，无快照禁用入口，不回退最新采集、待确认offer或当前请求Identity。RawText是原始保留快照；实际发送的Source.Text可以经过换行/首尾清理，二者不混称。

新增MarkdownSourceRegion以原生Expander/只读TextBox显示惰性文本，不解释Markdown/HTML/链接，不加载资源。它是同一FlowDocument内独立拥有的末尾BlockUIContainer，不进入RestrictedMarkdown、applied/elements或代码/表格尾块判断。增量Markdown块插在它之前；正文复制仍只取OverlayResults.Text，稳定段与原生代码/表格规则保持。

默认折叠，TextBox仅在展开时填原文。相同版本的delta不重设原文Text、选区或偏移；版本/源变化会Dispose旧区、清文字/选区/偏移并解绑。body与measurement有各自控件；测量镜像展开状态，使用同一有限页宽和整体高度预算，原文文本最大高度min(180DIP,max(1DIP,正文视口上限/2))，Wrap且本地纵滚。展开/折叠复用原Flush/Grow，不新增动画状态机；折叠释放文档空间给译文，不强制缩卡片，既有单调生长规则保持。字体和前景跟随正文。

展开及原文输入先接原ObserveCodeReaderIntent暂停全局跟随并隔离旧恢复。仅工具栏/菜单显式查看排Loaded后的BringIntoView；捕获原阅读guard，回调实时核创建版本、SelectedSnapshot、所选版本、reader/result修订、锁屏/退出/Dispose。验证后再次失效可能较晚排入的outer restore，再定位当前区。普通Expander头展开、流式刷新和measurement不排定位。真实可见位置仍未验证。

不同来源失败回退可在不增加resultRevision时更换所选版本，因此加入按创建Guid的实时资格，不只检查FlowView缓存；正式ApplyResult末尾同步撤掉失配源区，不等50ms Flush。Begin/版本变化/收起/隐藏/内部清空/锁屏/退出均清UI引用和事件；Stop保留同版原文选区，Off仍可从保留快照本地回看，不启用、不采集、不发新请求。

## 验证与失败保全

最终Provider **840/840**（原802+新38；失败0/跳过0），App Release交叉构建 **0警告/0错误**。实际13份原始log/TRX和38文件源码manifest见[固定证据](../test-evidence/2026-10-15-overlay-original-source.json)。测试直接链接正式FlowView/MarkdownSourceRegion/OverlaySource/Apply/Flush/标签；真实Core/Coordinator/cache/Gate参与，UI数据端口只记录属性、事件调用和队列，不模拟WPF。

首专项r01为52通过/1失败（共53）：新测试漏算两段保留空段，将5个Markdown原生段落加1个footer误计为4。改为6，并断言footer前最后原生Run为last；没有删空段或改生产解析器。r02全量827/827及r03 App0W/E为初版回执。独审补显式查看的定位请求与回退前实时版本资格；r04全量837/837、r05 App0W/E、r06全量839/839均为中间回执。最终测试/构建及独审结论以固定证据为准；全部原始文件使用唯一新名称、noclobber和存在性预检，不覆盖。

覆盖源与发送Text区分、确认取消保留A、B失败回退A（Flush前旧事件及定位）、三版本/四次生成、相同版稳定块和原文选区、Stop/Off、读输入、有限预算、来源更换/Dispose/内部Clear、锁屏/退出、两种定位与outer restore交付顺序、新输入及版本等迟到隔离。旧802测试名应完整保留；少数既有Stop断言精确适配新增独立Expander块，没有放宽对正文Run的断言。

所有dotnet命令先export且命令前显式 `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`，只用既定配置和现有锁定资产。证书存储/信任继续Unknown，不读库/配置/trust/delete。前批被覆盖前字节Unknown/未保全不回填；所有旧证据原样保存。

真实Expander布局/模板宽度、BringIntoView可见位置、字体/DPI、系统选择/剪贴板、阅读锚点/输入路由/取消时限与B组实机均 **NotRun**；完整Core.Tests工程本批NotRun。交叉构建不是原生执行，P2/完整SPEC11.5不因此通过。250/600ms及来源/密码/完整性/取消/清理门控、生产离线复用Off保持；不改main，不强推/部署，无新增依赖/费用/凭据/安全权限/协议或真实模型。Actions既有Forbidden继续尊重，不查询不绕路，远端运行数Unknown。

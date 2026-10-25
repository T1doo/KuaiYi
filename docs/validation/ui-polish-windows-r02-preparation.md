# 第二次云端视觉窗口：准备与实际结果

历史准备状态（已由下节实际执行结果更新）：Prepared / Pending explicit second-window approval。本轮仅来源与执行入口绑定，不推送、不创建CI、不启动Windows/native/WPF窗口，不重复已完成24项测试。一般鼓励“继续”不当作新窗口授权。第一次run40177638553/attempt1及18原件完整保留，失败结论不改。

## 精确候选与入口

准备基线为11f13f5ebc4fcb812d98586c5f594ca3a2633990，已审NUL固定树路径与原始blob修复。候选仅一个直接子提交、5个文件（workflow、Python绑定、C#诊断门、progress、本文件），不得附带产品源码/测试/pins/依赖变化。Python源门核父提交、完整变动集合、事件SHA=HEAD、clean；保持before=e4d57e77426ab235c7219b375debd76bc072e00b / after=419082f9863abf109a3d9ee7274dde59697bb85a。候选的完整SHA在本轮最终回报给出，不做自引用文件。

本地准备分支dev/ui-polish-visual-r02-preparation，不推送。拟议未来执行目标是dev/ui-polish-visual-r02，workflow push分支门、job if、Python hosted_context及C#诊断门四处一致；旧dev/ui-polish-visual不再匹配这个新候选。工作流无workflow_dispatch、schedule或rerun动作，job仅public T1doo/KuaiYi、push、首次run_attempt=1。只有未来明确批准第二窗口并再次核唯一writer/无重复run后，才可把这个精确候选单次推至目标分支；本轮不执行。

## 保持的预算与副作用合同

标准windows-2022，contents:read、pinned checkout不保留凭据；job≤15分钟、prepare≤8分钟、visual≤6分钟；四个顺序原进程各45秒内层、60秒原owner观察，且共享视觉预算。任何失败/UNKNOWN停止后续，保留原Popen/wait/双drain及状态/失败文件，不自动重跑、不把超时当清理成功。

仅公开合成WPF内容、原测试窗口/托盘/程序焦点；原DesktopOnly preflight会只读HKLM EnableLUA。没有产品安装、系统DPI/theme/motion变更、真实采集、模型调用、注册表写入或用户电脑操作。原窗口/原tray disposed且added=false与任务结算核验保持，全部失败/UNKNOWN原日志和PNG仍由always artifact直接保全，排除源码、SDK和构建二进制。

官方SDK10.0.402/runtime10.0.13的原签名/hash、locked restore/audit以及Git原始blob hash、路径/数量/大小门保持。真实源码导出/Windows构建/WPF像素与实际清理仍NotRun；本轮静态检查及独审不能替代它们。

## 本轮定向静态核查

核查三层分支门和workflow push入口一致、直接parent与5文件集合精确、before/after/SHA与原对象读取不变、预算/permissions/actions/原owner与证据目录未放宽。只做Python AST及声明/差异静态检查，不运行旧24用例、编译、渲染或CI。原历史24P仍仅历史结果。

## 第二窗口实际终态：失败且未自动重跑

用户已明确批准本候选第二窗口；run40217224688/job121240880724，attempt1，delivery3164cf35476463d3a85361bdfb3b909b1ada26f2，专用执行分支dev/ui-polish-visual-r02。准备25命令全部exit0，两侧原始blob来源清单（12315/12317文件）、官方SDK/runtime锁定检查及真实Windows build均通过，两个build0W0E。这次实际越过了第一次archive缺陷；不是重复引用以前的本地24P。

before Settings实际8张WPF内容PNG：Light/Dark × wide/输入focus/按钮focus/输入disabled；96DPI，wide内容924×661px、narrow404×641px。8张均逐hash/尺寸/前后状态核符，原进程exit0与原Wait/双drain结算。作者看全8图，独审看4图。**只是基线实图，不是精修后前后对照**；Dark-wide的主题/动画ComboBox浅背景配浅文字，几乎不可读，不能把采集Pass写成质量Pass。after未运行，不能声称观察了after。

随后before overlay在Light/Full因`Intermediate frame not observed; no fabricated growth raster`失败，0张overlay图，原进程exit3；无timeout/rescue。原窗口Closed、tray disposed&&!added、1个原Flow/connector完成均有子报告；原wait/两IO亦结算。wrapper的ResourcesSettled=false来自整体失败重算，不覆盖子报告有限结算事实。Runner最后又终止3个dotnet+1个conhost orphan；无命令行/parent映射，既不认定其均为构建server，也不声称全作业树自主清理。失败后没有启动after。

独审定位探针P2：Opening几何220ms而ContentOpacity仅80ms到1（fromOpacity=1时全程1），所以合法几何中间帧会被0<opacity<1的额外条件排除。原件缺逐帧elapsed/fromOpacity，不能断言本次具体漏帧机制；实际配置明确Full覆盖系统ClientAreaAnimation=false，不能直接归因系统减动画。需后续本地修复观察合同，不修改产品时钟或伪造中间帧。深色ComboBox也可独立做工程修复；新的真实窗口不属于本次单次自动重试范围。

ZIP1619996B/SHA256 `9e2f546770937ac71377369b0599b73778e55fe03b83f56bbcce536cd08ea0b0`已下载核符，66实际文件，prepare30/run62 manifest项逐长度SHA全符，独审限定ACK。源清单与原Git对象核符，before App运行自报hash匹配prepared bundle；artifact无DLL本体，不冒称本地直接读取了DLL字节。原ZIP/PNG/JSON/日志只在artifacts/2026-10-26-ui-polish-r02/保留，不提交二进制。完整收据见../test-evidence/2026-10-26-ui-polish-r02.json。

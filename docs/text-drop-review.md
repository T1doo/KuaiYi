# 正式文本 Drop 限定独审

签收基线 **a870389599a6fe046d3cef62ea14a4647b58bbb1**，正常分支 dev/p0-foundations，仅本 KuaiYi 保存云 Linux 环境。本轮按监督范围复用原 OLE 基础完成正式文本来源，不推进 P4。原产品书与SPEC仍1826行、146706字节、原SHA不变；176追踪/106整体验收NotRun。

正式Shell现在建立真实私有OleReceiver、独立注册STA与App-owned HWND投放目标，出生时冻结receiver GUID、wire epoch、CaptureTicket和设置版本。On/模式/版本/暂停/隐藏/锚点变化撤销；原进程、Run、注册/撤销、读取和交付未结算不能重建。进入/离开只查询格式、反馈最多96×48DIP目标；只有实际Drop取得有限HGLOBAL副本。COPY-only、MOVE-only拒绝、同步返回前副本与正确async协商/封送/End规则继承原实现。NativeBridge缺失时明确停用，当前managed审查包不能运行正式Drop。

Unicode→HTML→RTF优先、不回退无效高优先级；文件/虚拟文件/图片/仅链接拒绝文字路径。Unicode严格代理项、50k scalar上限；HTML按UTF8字节偏移与更窄selection提取，不抓whole-page，不运行标签、脚本、图片、URL；有限RTF解析Unicode fallback、有限codepage/隐藏目的地，未知group、alternate、字体charset/codepage切换等明确拒绝。不是任意HTML/Word文档兼容声明；详见[ADR0013](adr/0013-formal-text-drop.md)。新文本只进统一SelectionSourceSnapshot，SourceKind=OleDrop、真实来源身份与投递ID，不主动重读UIA/剪贴板、不伪造RuntimeId/ranges。

各层在自身起点使用本地1500ms绝对预算：ReceiverHost在原生DropBegin建立group时记录Stopwatch；父OleReceiverClient在收到并验证DropBegin时记录自身TimeProvider；Core在接受Begin时记录自身TimeProvider。没有传递原生起点，不比较不同进程/TimeProvider的绝对timestamp，也不保证原生DropBegin到App最终交付总计1500ms。原生来源检查后复核gate/共享activation/本地elapsed，编码后再次复核才排入明文；目的队列在父取消锁和RuntimeGate锁内复核source/config/ticket/可见性/锚点及父/Core各自截止。父/Core检查不是原生Stopwatch检查。来源检查期间资格变化不能提交；跨进程排队耗时仍是未共享的预算缺口。协议各阶段绑定原source、format与Drop位置。每接收器最多256已见事务，再撤销整lease，禁止清表后重放。相同投递恰一次，相同文本的新投递仅提示内存重开；失败保留旧来源。取消立即清排队引用，迟到已完成/未完成的读取由唯一观察者清零和观察故障，原读取未settle不可释放activation。CLR不可变字符串不能保证物理擦除；无持久原文或模型调用。

## 实际运行

**144/144**新专项：Drop核心61项、文本格式61项、真实Linux子进程/生产server-client协议16项、原读取所有权6项（各组精确数量见TRX/summary，以实际计数为准）。OS/COM注册/共享映射、时钟与受控Stream为明确测试替身，仅存在测试/fixture；子进程、私有pipe、生产编码/门控/队列/所有权实际运行。未运行Windows COM/拖放。

最终全量 **Core1032 + Provider77 = 1109/1109，Node63/63**，零失败/跳过；26工程locked Release零警告/错误。旧888Core及77Provider案例按实际TRX名称多重集保留。26锁字节与基线完全相同，没有新依赖安装。运行源码/测试在最终CI后未修改；文档与独立staging随后定稿。重跑 scripts/ci-offline.ps1，专项filter为DropCaptureTests/OleDroppedTextTests/TextDropProcessTests/OleReadOwnershipTests。

失败证据保留：两次生产编译分别缺lambda括号、System.IO引用；测试移植未用字段造成编译失败。首次解析/核心113项109通过4失败：invalid surrogate放InlineData被.NET属性传输替换/重复、同version直接篡改不可变配置的无效fixture、CP936 0xff并非预期非法样本；改为名称生成原代理项、真实新version与dangling lead byte，禁止接受非法数据的断言保持。真实child136项126通过10失败为清理后访问已Disposed Process，改为实际finalize前检查真实退出，断言保留。140项139通过1失败为交付任务尚未发布即检查清零；显式等待生产原始交付任务发布和settlement后断言清零。修复后140通过，再加4项格式/净化边界得到最终144。所有原日志/TRX保留，未删测试或弱化验收。

独立审查包 `artifacts/KuaiYi-text-drop-managed-win-x64.zip` 使用 scripts/stage-text-drop-review.ps1，包含app与完全禁用真实Selection/Drop平台的public-probe、逐文件manifest和x64 PE入口。框架依赖、缺native DLL、无installer/fixture/凭据；包摘要见[summary](test-evidence/2026-10-11-text-drop-summary.json)，不能称完整安装包。15份旧包最终字节核验符合各自签收摘要。前轮绝对截止包曾被错误覆盖后按原摘要恢复，事实及证据保持，不宣称全过程未触碰。

## 未完成

[Windows清单](text-drop-windows-checklist.md)全部NotRun：真实OLE来源应用、同步/异步COM、注册组合、来源前台/焦点关联、文件格式、提示品质、多屏DPI与阻塞下主UI交互均需实测。OLE没有标准来源进程证明，当前保守前台/焦点关联不能宣称任意IDataObject来源认证。P0/E06仍Blocked；P1/P2/P3和R09/P5-01…04源码InProgress、Windows验证Blocked，E01…06整体NotRun。P4模型调度未接，所以尚无真实译文、请求闭环和日常可用软件；P5完整阶段、全快捷键、安装卸载、DPAPI/ACL和远端CI均未签收。没有扩权、真实凭据/模型调用、部署、发布、新法律协议接受或用户桌面操作。

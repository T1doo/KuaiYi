# P3-04 按应用复制兼容独审

基线 **8a10edaa9743914059f32cd893d724d922a65614**，正常开发分支 dev/p0-foundations，仅 KuaiYi 保存云 Linux 环境。本轮限定复制兼容，不推进 P4。原规格1826行、SHA256不变；176追踪项与106整体用例保持完整。

## 真实源码路径

正式 Shell 连接实际 MTA SelectionBroker 的失败证据与新的 ClipboardCaptureClient / ClipboardHost / NativeClipboardPort。UIA成功直接沿原路径，不复制；仅正常 EmptyOrUnsupported 且可信原生焦点/RuntimeId/控件/手势、完整路径明确授权、门控/版本/epoch和来源仍有效时进入复制。密码/终端/高完整性/排除来源维持拒绝；自动路径必须有真实 GetSelection 矩形关联。无原生HWND、隐藏owner等不可关联情况保守失败，未证明任何应用兼容率。

设置隐私页新增独立即时授权/撤销界面，首次确认明确说明 Ctrl+C、当前剪贴板恢复及历史无法删除；默认“否”，默认无任何授权。按完整本地可执行路径匹配，不能按basename继承。正式界面才调用授权事务；测试的public.exe/合成允许列表不是用户真实授权。普通草稿仍不能直接授予；持久化journal保证通知失败可重放，权限变更同步推进 capture epoch。原草稿不能覆盖刚生效授权，界面提示重开设置。

独立STA先枚举并深拷贝整个有限格式组合，Ready后父进程在RuntimeGate提交锁内最终检查来源/原生焦点/修饰键/sequence，再发送一次Ctrl+C，不清空、不强制前台。实际Win32 SendInput/HGLOBAL/OpenClipboard/owner等已实现，不是mock生产函数；尚未在Windows执行。

350ms单调复制变化预算共享父发送起点，检查实际owner PID/root窗口、原前台/焦点与注入时间，无变化不读旧内容。原剪贴板只留子进程内存，原格式不反序列化。只支持完整HGLOBAL组合（文本/locale/DIB/DIBV5/文件列表/注册HTML、RTF、PNG），32项/4MiB单项/8MiB总量；任何GDI/虚拟文件/未知格式拒绝整组。系统合成GDI格式也可能使图片组合被拒绝。恢复先分配全部块，在同一剪贴板锁内比对已观测sequence和owner，改变则不覆盖，任何部分失败明确Unconfirmed。不能承诺原owner、历史或同步恢复；见[ADR](adr/0012-isolated-clipboard-compatibility.md)的官方依据和工程限制。

父1200ms单调绝对结果预算覆盖启动/读取/退出/最终解码，不续期；复制后的取消用EOF让STA恢复，最多400ms独立清理宽限，再仅终止持有的自有子进程。启动/退出未结算仍拒绝第二writer，不凭token虚报阻塞原生调用被终止。未交付payload无论已完成还是迟到均由唯一观察者清零/观察异常；新旧快照数组全作用域清零。取消立即清掉排队文本owner引用，实际目标队列再次核对焦点/gate/版本。CLR不可变字符串只能释放引用，不能保证物理擦除。默认不持久化原文或翻译内容。

## 已执行的验证

新增 **118** 用例，生产事务+可控剪贴板口/时钟（仅测试替身）、生产授权文件事务与gate、真实Linux自有公开child协议/退出、生产reader未交付所有权、UIA失败证据协议、实际目的队列竞态。包括349/350/351ms、1199/1200/1201ms、多格式深拷贝/完整拒绝、恢复竞争/取消、focus变化、撤销和排队释放。均未执行Windows剪贴板或真实按键，不代表人工授权。

定向 **118/118**；最终 Core **888/888** + Provider **77/77** = **965/965**，Node **63/63**，零失败/跳过。**26**托管工程locked Release零警告/错误。所有此前847项.NET及63项Node保留。初编译缺using、xUnit单项查询写法、未用字段、fixture局部变量/EOF读取问题均修正，原日志保留；初扩77项中1失败为测试错误使用local-only gate期望自动采集，修为合成网络授权gate（无实际网络）并补生产权限epoch机制，原76/77 TRX保留，没有删测试或放宽禁止错发/覆写断言。

重跑：scripts/ci-offline.ps1；staging：scripts/stage-p3-clipboard-review.ps1；只读冻结核验：python3 docs/test-evidence/2026-10-11-p3-clipboard-verify-evidence.py。完整原始TRX/log、精确包摘要、源码/锁文件/历史commit冻结在[summary](test-evidence/2026-10-11-p3-clipboard-summary.json)。25旧锁字节不变，仅新ClipboardHost增加第26份锁，无安装依赖/新协议/凭据/模型调用。运行源码与测试在最终回归后未修改；文档和独立staging路径随后定稿。

独立框架依赖 win-x64 包：artifacts/KuaiYi-P3-clipboard-managed-win-x64.zip。包含正式ClipboardHost与CaptureBroker及独立public-probe；公开GrowthHarness实际选区平台仍禁用。逐文件manifest、PE32+x64入口核验；没有测试fixture、原生DLL、凭据或安装器。14份旧审查包保留原字节，历史报告按固定commit核验。

## 尚未验收

[Windows执行清单](p3-clipboard-windows-checklist.md)全部NotRun；真实STA剪贴板格式语义、Ctrl+C、原生UIA/跨应用/焦点/多屏DPI、DPAPI/ACL、P0/E06、安装卸载及远端GitHub CI均未执行。P0/E06 Blocked、P1/P2/P3 InProgress，P3-04源实现InProgress/Windows验证Blocked，106整体NotRun。完整OLE文本Drop、全热键及P4翻译调度尚未交付；本轮不是完整P3、安装包或可日常使用翻译软件。

打包初次路径替换遗漏了大写P3，新包短暂写入旧绝对截止包路径；已修独立目的路径，以未改旧staging恢复原ZIP，精确1615917字节及SHA256 cb8bdde0d6ed43de225082165c444725e43ece356bc24750ba9a92a847e8cb7d与原签收摘要一致。保留初次stage日志与恢复核验JSON，最终重新staging并逐一核对14份旧包。恢复首试因缺失XDG变量遇只读cache失败；使用既有工作区cache配置后恢复，不申请额外权限。

最终协议复核另发现：旧ParseStatus把未知状态归普通失败，若同时携带复制证据可能获得兼容入口。公开自有child的6项反例中此项修复前失败（5通过/1失败），已保存原客户端源码字节及TRX。现在携带CopyEvidence必须原始状态严格等于EmptyOrUnsupported，未知状态拒绝ProtocolFailure；原断言保留。补后定向118/118、全量965/965及63/63重新执行，重新staging，之前117/964结果作为中间证据保留。

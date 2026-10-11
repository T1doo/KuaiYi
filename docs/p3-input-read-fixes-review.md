# P3 两项监督修复独审

基线 `65bdbc7b8c340b46dd7427f9bba1eff4b7e68aa0`。范围是输入身份冻结与放弃读取所有权，不推进 P4，不签收完整 P3。仅保存云 Linux 环境；无安装依赖、真实凭据、模型调用或新协议。正常开发分支，main 不动。

## 旧版证据与修复

旧 `ConsumeSelection` 丢掉票据，UI 端 `InputAsync` 重新准入，hook 又可能在 Off→On、Drop→Selection、暂停→恢复后沿用。三个旧协调器回放反例均启动一次 Broker 而预期为零。该旧反例证明原解析事件路径漏洞；没有将它声称为旧 Win32 hook 实机证据。修复后的六项积压测试分别持有真正 bounded channel 或目的 SynchronizationContext，调用与 Shell 相同的生产 SelectionInputBridge，资格失效时 channel 解析前或 UI 目的队列拒绝，Broker 为零。

hook 实例出生时持有冻结 ticket（含 captureEpoch）、设置版本和唯一 ID，每个原始事件还包含触发 HWND/PID、原生毫秒时间和由其换算的 UTC 时间。回调不取得门控锁；身份在启停/暂停/设置变化后失效，Reconcile 即使看到最新 On 也必须换掉旧实例。PID 匹配、进程出生时间早于事件、解析和 UI 双重资格、下游冻结 ticket 同样检查。新手势不会被旧抬键或旧注入事件清除；当前注入事件清手势且不读 UIA。无法确认旧 hook 退出仍保留所有权并停用。

旧 `ReadAsync` 在取消 WaitAsync 后仅当底层 `!IsCompleted` 才观察。测试屏障在 finally 判断之前让原底层读成功或抛 IOException，两个反例均缺少所有者而失败。屏障仅 internal 测试入口，旧行为除可见性/屏障外保持不变。修复按 `delivered` 判断所有权：交付成功由调用者持有；未交付的读总交一个观察者，已完成和迟到结果均清零成功 payload、观察 fault 并只记录类型码。六项测试涵盖取消前完成、取消后迟到和同步成功/失败，每次放弃恰有一个观察者；敏感64字节测试缓冲清零，同步成功不提前清零。没有弱化旧断言或依靠 GC 猜测异常已观察。

## 已执行证据

新增22例（3旧输入反例、13生产桥、6读取所有权），定向72/72；旧版5/5失败 TRX 原样保留。全回归 Core755 + Provider77 = **832/832**，扩展 Node **63/63**，无失败或跳过。25托管工程 locked Release 零警告/错误；原16项 Linux 自有公开子进程测试仍通过。协议测试使用自有 localhost MockProvider；OS 解析/受控流替身仅用于测试，生产 App 使用真实 SelectionDesktop/NativeHost/CaptureBroker。没有新增依赖，锁文件相对基线逐字节不变。

独立 staging：`scripts/stage-p3-input-read-fixes.ps1`，框架依赖审查包 `artifacts/KuaiYi-P3-input-read-fixes-managed-win-x64.zip`。含实际 CaptureBroker 和禁用真实选区平台的公开浮窗探针，不含测试 fixture、凭据、原生桥 DLL 或安装器。精确包大小/摘要/逐文件manifest/PE32+x64、12旧包、293源文件、旧审阅固定commit验证见 [summary](test-evidence/2026-10-11-p3-input-read-fixes-summary.json)。

可重跑：`scripts/ci-offline.ps1`；证据核对：`python3 docs/test-evidence/2026-10-11-p3-input-read-fixes-verify-evidence.py`。证据脚本不写工作区，旧冻结文件按各自精确 Git commit 读取。

## 未完成与未执行

[Windows清单](p3-input-read-fixes-windows-checklist.md)全部 NotRun，实际hook重建/消息时序、UIA跨应用兼容、焦点、像素/字体、多屏DPI、DPAPI/ACL、原生P0/E06、安装卸载及远端GitHub CI未执行。P0 Blocked，P1/P2/P3 InProgress，176追踪项及106整体验收NotRun不变。完整OLE文本Drop、复制兼容授权、全热键和P4模型调度尚未交付，原文仅Shell内存，不是译文或可日常使用的完整翻译软件。

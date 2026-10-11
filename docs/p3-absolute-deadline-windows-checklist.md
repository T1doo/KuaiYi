# P3 单调绝对截止：Windows 待执行

全部 NotRun。框架依赖 win-x64 审查包，需要 Windows 11 与 .NET 10 Desktop Runtime，含实际 CaptureBroker，不是安装器。无凭据、无模型调用。继续执行 [P3 原清单](p3-selection-windows-checklist.md)及[输入身份清单](p3-input-read-fixes-windows-checklist.md)。

1. 使用公开测试应用和真实 Broker，记录单调启动/读取/退出/最终校验耗时与状态，不记录原文。同一600ms起点覆盖所有步骤，读取成功不能重置预算；已完成read或startup也必须复核。
2. 599ms内才准交付；达到600ms或超过后应HardTimeout且无文本。分别在已完成read、UIA返回、UTF8/范围/RuntimeId校验时取消，结果应Cancelled且无文本。软250ms和硬600ms为各自限制。
3. 调整墙钟、延迟定时器回调不能延長单调交付预算。长时间阻塞COM仍由父进程放弃并清理所持子进程，不宣称CancellationToken能终止COM。
4. 检查未交付read结果只有一个观察者，成功payload清零、fault被观察；正常成功payload由调用者finally清零。启动/退出尚未终结时不能启动第二writer。
5. Windows真实hook/UIA/stdio时序以及Linux无法执行的焦点/多屏DPI、原生P0/E06和安装卸载仍需实机证据；定向87用例与Linux公开子进程不能替代106整体验收。

P0 Blocked、P1/P2/P3 InProgress，106整体验收NotRun。完整OLE文本Drop、复制授权、全快捷键和P4调度尚未交付。

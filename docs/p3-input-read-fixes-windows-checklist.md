# P3 输入与读取修复：Windows 待执行

全部 NotRun。框架依赖 win-x64 审查包需要 Windows 11 与 .NET 10 Desktop Runtime，含实际 CaptureBroker，不是安装器。无真实凭据或模型调用。先执行 [原 P3 清单](p3-selection-windows-checklist.md)，再核以下修复。

1. 用仅含公开测试文本的应用，分别积压 hook channel、UI Dispatcher 队列中的 down/up。在释放队列前执行 Off→On、Drop→Selection、移动暂停→恢复；旧事件不得启动 Broker，不能显示旧原文。随后新手势应正常读取。
2. 记录 hook 实例 ID、冻结 ticket/epoch/配置版本、回调捕获 HWND/PID/系统时间（不记录原文）。上述往返及配置更新后实例必须更换；旧实例尚未退出时也不得被重新绑定。原实例释放未确认应明确停用，不能叠加第二 hook。
3. 前台切换、窗口销毁、PID/HWND 复用和配置变更在 channel 或 UI 队列中途发生时，最终检查应拒绝。新手势已按下后释放旧实例的抬键/注入事件，不得取消新手势；当前实例的注入事件应清掉手势且不捕获。
4. 实测低级 hook 专用线程及消息泵，仅写有界元数据；回调不取 UIA、剪贴板、门控锁、布局或网络，不能因积压冻结应用。
5. Windows Broker stdio 在 600ms 截止/取消与读取完成同一边界：结果只有调用者或放弃观察者持有；放弃成功缓冲清零，放弃异常已观察，清理未确认仍不得第二启动。Linux 六项受控读取用例不替代真实 Windows 进程验证。

跨应用兼容、真实 UIA/焦点/多屏 DPI、原生 P0/E06、安装卸载仍未执行。完整 OLE 文本 Drop、复制授权、全快捷键、P4 调度尚未交付。106 整体验收保持 NotRun。

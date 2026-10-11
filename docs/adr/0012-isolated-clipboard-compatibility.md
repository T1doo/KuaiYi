# ADR 0012：隔离复制事务与保守完整恢复

状态：源实现；Windows 实机验证 NotRun。依据 SPEC 5.5 / P3-04，基线8a10edaa9743914059f32cd893d724d922a65614。

[微软剪贴板操作](https://learn.microsoft.com/en-us/windows/win32/dataxchg/clipboard-operations)说明打开时互斥、延迟渲染可调用来源窗口、SetClipboardData转移内存所有权；GDI句柄不能作为HGLOBAL深拷贝。[SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)受UIPI限制且返回实际注入数。[GetClipboardOwner](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getclipboardowner)提供owner窗口；owner及sequence只是关联信息，不能证明所有应用会遵守同一归属方式。以下为工程选择，不是微软对跨应用兼容的保证。

独立 ClipboardHost 进程在 STA 获取有限完整快照、等待、条件恢复。父进程在生产 RuntimeGate 最终锁中检查冻结资格并发送一次 Ctrl+C；子进程 Ready 前不复制。原剪贴板字节只留子进程内存，IPC仅传状态/新原文，mutable数组全作用域清零。父1200ms单调绝对交付预算覆盖启动/读/退出/校验；350ms仅是本次复制变化等待/内容取得预算。复制后父EOF撤销，最多400ms额外清理宽限后清理持有进程，未确认退出保持单writer封闭。隔离可以避免App被同步剪贴板/COM调用长期阻塞，但不能保证已被终止的服务完成恢复。

只允许完整组合中每项都属于已实现有限HGLOBAL复制/恢复的类型；CF_TEXT/OEMTEXT/UNICODETEXT/LOCALE/DIB/DIBV5/HDROP与注册名HTML Format、Rich Text Format、PNG按原始字节保存，不解析或执行旧内容。最多32项、每项4MiB、合计8MiB。GDI/虚拟文件/任意自定义或OLE对象拒绝整个组合。系统合成格式也参与枚举，可导致原本含DIB的组合因附带CF_BITMAP而被安全拒绝。可移植测试已验证算法和深拷贝所有权；Windows真实格式语义和恢复尚未签收，不启用为已验收的正式发行能力。

新数据必须是发生于明确注入后的变化、350ms内、来源原进程/root owner/前台/原生焦点仍关联、无新输入且为完整Unicode。保守拒绝隐藏owner、虚拟原生HWND或来源不可判定情况。恢复只作用于已观测的当前source-owned写入，在锁内再次比对sequence+owner，不覆盖后续复制。预分配全部恢复块；部分SetData失败明确Unconfirmed。恢复当前格式字节不能恢复原窗口owner，也不能撤销系统历史、同步及第三方接收，授权页明确说明。

每应用授权为独立即时持久事务，按完整路径匹配；普通设置草稿不能授予或覆盖权限。事务通知失败保留journal重放，权限变更推进epoch，取消旧捕获。界面人工确认与测试中合成允许列表明确区分。复制结果在入UI队列前转换为单一可释放快照，队列闭包仅持有owner和非文本身份；取消释放引用，数组可清零。CLR不可变字符串无法保证物理擦除，不作此承诺，默认不写日志/历史。

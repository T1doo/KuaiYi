# 热键前级与安全提交 Windows 补测

全部 **NotRun**，补充 [原热键清单](p3-hotkeys-windows-checklist.md)，不替代 H01—H06/106 整体验收。采用公开合成内容与配置，不调用真实模型。

| 场景 | 所需实际证据 | 状态 |
|---|---|---|
| Native 前级队列 | 本进程 HWND 队列保留真实 WM_HOTKEY，在其消费前分别完成 Off→On、Selection→Drop→Selection、Settings/Capture/Moving 暂停往返；旧选区/截图消息不得调用 Broker/CaptureHost；记录原 MSG time、围栏和调用次数 | NotRun |
| 暂停期间消息 | 暂停/锁屏期间产生的真实按键，在恢复后才消费；旧消息拒绝，恢复后新按键正常 | NotRun |
| UI 次级队列 | native 消息已验证并排入 UI 后，再改变状态；原 GateId/revision/epoch 拒绝，Pressed 不读取新 epoch 绑定旧消息 | NotRun |
| 入场竞态 | 最终 UI 资格检查后、取词/截图入场前状态变化；原身份不得刷新；真实读取/截图计数为0 | NotRun |
| 启用不改旧 epoch | 普通、正式确认、本地启用仍推进独立输入 revision，旧 native/UI 消息拒绝；不影响原请求身份语义 | NotRun |
| 计时 | 验证 GetMessageTime 与 Environment.TickCount64/GetTickCount64 同源及回绕比较；同 tick 允许保守拒绝，不认作连续快速按键全部成功 | NotRun |
| 实际保存 | 改热键同时 clear/delete/replace A 或变模式，在真实 FenceInput/提交失败与挂起条件下，已发布配置仍通知全部安全订阅者；A停止，B保留；原提交真实结束后才释放草稿 | NotRun |
| 恢复与异常 | 提交/观察者多错误原样报告、journal 保留、磁盘不假回滚；重放后自有绑定及最终围栏确认成功才清除故障标记；其他进程组合不受影响 | NotRun |
| 隐藏截图入口 | 首按显示红色浮钮，提示再次按键；第二次的新按键可本地框选，仍不启用、不上传；本轮包无原生DLL，不声称成功抓屏或完成 P6 | NotRun |

审阅包仍是需要 .NET 10 Desktop Runtime 的框架依赖多文件，无 native DLL、安装器或真实凭据。生产安全策略的可移植故障测试与真实 Windows 执行必须分别记录。默认 Off、P0 E06 Blocked 与模型链路未接状态保持。

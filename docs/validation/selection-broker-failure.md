# Broker 非零退出诊断

恢复核验：HEAD `b904cbf8fa7b2c5a933f01e407dc96546bd28098`，tree `2c8e7143a8f9c8c6283f3fa97c074bfda8e6e43d`，工作区干净，无构建/探针进程及排队或运行 CI。保持用户手动模型设置；未操作用户桌面、安装软件、配置凭据、增加权限或调用翻译模型。

[几何修复回归 run 38355942766](https://github.com/T1doo/KuaiYi/actions/runs/38355942766)，[job 115158735868](https://github.com/T1doo/KuaiYi/actions/runs/38355942766/job/115158735868)，Actions 源码 SHA 与上述 HEAD 相同。真实 WPF 几何四项 Pass；实际取词 **Fail 0/120**。Chrome HTML 为 2 HardTimeout、4 Crash、14 Paused；其余五视图各 6 Crash、14 Paused：合计 **34 Broker 非零退出、2 硬超时、84 按原规则暂停**。所有序列、Broker、Job/root/profile、runner 原始任务清理均确认。未知退出不能沿用前次 `RangeBounds` 观察猜测根因。

原始日志仅留忽略目录：28,673 bytes，SHA256 `094ee8abcae04c4d1fc3149a22f18ca2b4ed26c49d35fe8ab86af941e0ec6854`。尚未证明非零退出究竟来自 API 参数、空引用、程序集还是元数据序列化。无屏幕/标题/路径/原文/异常消息导出。

本次最小取证仅增加预期代码故障的固定类别（Argument、NullReference、NotSupported、TypeLoad、FileLoad）与元数据序列化阶段；失败时在写出任何帧之前生成无文本的 `BrokerFailure`。完整、nonce/type/exit 验证后的该标记仍映射为 Crash，并计入原有三次失败/60 秒、暂停 30 秒规则；真正非零退出、未完整接收、硬超时、取消与协议失败仍为 Unknown。不得转换为 Success 或绕过保护。

六视图各一次的新诊断为 DiagnosticOnly，基础设施正常结束仅为 Observed。密码/排除/权限/PID/start/HWND 校验、自动几何关联、250/600ms 时间预算、固定矩阵与正确率门槛保持不变。未识别故障仍由原有进程退出路径报告，不增加泛化的 catch-all。

前一成功原生 GetData 代码完整 SHA 与 Actions 执行 SHA 均为 `220060f813a3640487d593920a1b1824aa779a00`；[run 38347142627](https://github.com/T1doo/KuaiYi/actions/runs/38347142627)，[job 115131066822](https://github.com/T1doo/KuaiYi/actions/runs/38347142627/job/115131066822)。该闭环不重跑、结论不改写。P0 整体、用户 Win11 物理划词、PDF 与 Markdown 仍无通过结论。

本地 Contracts 正常构建及实际 Broker/client/fixture/probe 源码类型检查均 0 warning / 0 error；原 xUnit 断言直接执行 165/165，含三次完整 BrokerFailure 仍触发暂停及实际序列化退避不得输出捕获文本；PowerShell 摘要边界 35/35。新 Windows 诊断尚未执行。新代码的 Crash 可为真实非零退出，也可为正常退出但完整报告代码故障，后续报告须据 BrokerObservation 区分，不能统称非零退出。

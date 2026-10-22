# 浏览器原图接入：固定产品完整离线签收

产品代码冻结于 `b554541f6cbcc8dad7f37c81a88c7fe56aeb1b6d`，正常开发分支 `dev/p7-fixed-signoff`。本轮按父级独审要求补齐前轮仅 Core141/Provider10定向范围的明确验证缺口，没有修改产品、测试、依赖或构建合同。

在原指定保存Linux云环境顺序执行**一次完整Core、一次完整Provider**，没有过滤器，没有重复完整Codec，没有访问已拒runner API、新增CI、真实模型调用或用户电脑操作。

| 本轮完整离线范围 | Passed | Failed | Skip | Exit |
|---|---:|---:|---:|---:|
| Core |2182|0|0|0|
| Provider |2464|0|0|0|

完整命令、UTC开始/结束、完整日志、原始TRX及源码/DLL摘要见[证据索引](../test-evidence/2026-10-22-browser-full-signoff.json)和Library原件包。两个TRX的UnitTestResult条目分别为2182/2464，逐条均Passed，与Counters/日志一致。839个冻结构建输入在两轮前后及最终均同字节；独立只读审查再与固定Git字节比对。测试输出目录21/22个DLL摘要亦一致。无失败、skip或零匹配过滤项被算为通过；前轮141/10、Node104和Codec488不与本轮累计。

这两套完整离线套件覆盖现有接收、owner、准备、文件来源、取消清理和两协议发送门回归，但其中Windows/Chrome Port/WPF等替代端口仍按各用例原定义标明，不能代替平台验收。独审限定ACK，不新增确定P1/P2；审查者只读原件，未另跑测试/构建/CI。

产品SHA与随后证据提交SHA分开报告。本轮仅提交此记录、证据索引及progress，产品与测试保持固定SHA的字节。main不改、不强推、不发布部署、不新增费用/凭据/权限/许可。

真实Windows、Chrome/Edge、medium正向warm/Explorer、真实来源/密码/完整性、250/600ms及画质仍未验；跨域/blob/可见裁切、旧bit4客户端实授红件等既有边界仍按原报告保留。P6/P7和完整v1.0不据此改为通过。

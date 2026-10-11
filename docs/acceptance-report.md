# 当前验收报告（P0 独立切片）

不是 v1.0 完整验收或发布报告。环境与构建入口见 environment.md / README。

- 原始规格 SHA256：`ea3aa6529405bcc703986988fd089f476ee8f991d867e2220b3b20c439126166`。
- 当前离线自动化：Core 45、Provider 73，共 118，全部通过；精确证据见 test-evidence 的 late-events-summary。首批 57 项、探针批 88 项及修复前失败证据分别保留。
- 11 个托管项目 Release 交叉构建 0 警告/0 错误；三个探针 exe 已检查为 Windows PE32+ x64，框架依赖 ZIP 已生成。这是构建/打包部分证据，Windows 实际执行均未运行。
- A—L 的 **106 条整体用例均为 NotRun**；已验证子项不能代替完整用例。
- R01—R18 的产品完整实现均尚未完成；模块/阶段/用例组定义完整保存在 traceability.json。
- 真实 Windows UIA、剪贴板、OLE、热键、DPAPI、截图、混合 DPI、焦点、包身份、OCR、现代菜单及安装卸载：Blocked（Linux 无交互 Windows）。
- Live 两协议、视觉分类/解释、费用/usage、取消：NotRun，无授权真实调用。
- 100 文本/60 图片语义质量、人工评分与八小时浸泡：NotRun。
- 开发/正式安装包、签名/信任、浏览器安装或商店：NotStarted/Blocked，无发布。

已验证的子项包括：Off/未授权实际 HTTP 为零、旧身份隔离、取消与收起门控、布局帧不变量、IPC 无符号长度先验证、独立双协议结构、实际 usage、断流部分状态、200 流内 error、限次重试、重定向拒绝及错误体不回显；Native Messaging 独立进程两帧、严格 UTF-8/字节长度、图像分片/摘要/截止及 Anthropic 块生命周期与思考内容隔离。

安全门控、协议和布局仍需后续用例补齐；不能将当前子项概括为完整 R16/R17 或 F/C/B/L 组通过。

监督方复核指出首批测试未覆盖等待响应头/非流式读取时失效请求的错误。补充真实 HTTP 屏障测试后，修复前 10/10 失败，修复后全部通过；不能用之前 57/88 项通过证明所有迟到错误路径。新增完整 identity 信封和统一接收边界，在目标队列执行时原子复核 session/version/request/epoch，所有事件类型均适用。当前是可测试的接收合同，正式 WPF App 尚未集成，未声称真实界面曾串流或已通过 Windows 界面验收。GitHub CI **NotRun**；本报告仅记录云环境本地脚本的实际运行。

# 测试模式边界 Windows 补测

当前全部 Windows 项目 NotRun。本表补充 [原 P3-06 Windows 清单](p3-text-translation-windows-checklist.md)，不替代其跨应用/焦点/动效/多 DPI/安装步骤。

1. 按原说明启动公开本地 fixture 和 `--translation-test-provider`。有效的两协议 BasePrefix/ExactEndpoint 无密钥请求应得到明确标识的测试输出；默认启动仍不选测试服务。
2. 完整设置中修改 remote HTTPS、回环端口、run/scenario、模型、认证方式、secretRef 或自定义 headers；保存后立即 Off。再次启用应提示受限测试配置，不出现外部端点授权或正文上传。只能恢复启动时授权的 fixture，无新增授权入口。
3. 长文本确认打开、HTTP 已发/流式中、旧 UI 事件积压时提交上述配置，或从备份恢复越界配置：旧请求取消，迟到文字/完成/错误不写当前 UI，密钥打开次数为零。可回看旧缓存，其原授权来源测试标识准确。
4. 用系统 HTTP 代理及 localhost 环境验证：测试连接始终固定回环 IP/原端口，不走代理/DNS 回落；重定向、cookies 和额外认证不能扩大发送范围。只用公开 fixture 内容，不连接真实模型。
5. 保存/恢复/取消/锁屏/退出不写原文/译文/真实凭据。测试临时配置关闭后清理；正式用户配置目录不被测试模式打开。

软件包无安装器/原生 DLL；Windows 原生、图形、DPAPI、兼容矩阵、远端 CI 与真实模型仍未执行，完整验收不得标 Pass。

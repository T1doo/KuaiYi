# 0023 — 普通启动的 startup 来源边界

状态：设计先获独立限定 ACK；本片 component 用例已实施并在本地通过，等待实现及原件独审。对应 SPEC 13.4、16.9/P7-04；不宣告 P7-04 或登录启动完成。

## 原计划缺口及选择

P7-05 已有非破坏性预检，但真正停止进程、撤销注册、Shell／身份卸载及产品／设置密钥删除仍需要对应真实资源和独立授权。本片不建立一个只会声称完成的卸载／修复执行器。

P7-04 的普通启动链为 `Launcher.Program → BootstrapEntry → StableLauncher → StableSelection → ControllerBridge → RunSelectedSessionAsync`。`BootstrapBinding` 仅携带 owner／active 的序列化字节，`StableSelection` 仅绑定 parent／child nonce、目录、代和指纹。它们不是原 Installer 的引用资格，也没有从可信发布来源取得独立 source pins 的入口。正确 receipt、hash 或文件路径不能补成该来源。

现有正向 startup 来源只有 `InstallationInstallerEntry.StartupSources`：原 Entry 已开始且未终结、原 embedded pins 已核，随后仅绑定同一原 Operation、Scope。原 committed deployment session 再绑定原 controller、admission、READY proof 和原 child，才能创建 startup removal owner。Entry 终结即撤销引用资格。该链不可由可序列化的描述、普通 selection 或启动后的进程身份重建。

因此选择最小片：把普通启动来源传递的限制写成可审查设计，并通过真实 `InstallationControllerBridge.RunAsync` 入口的合成输入证明拒绝路径。目前不增加正向来源、字段、wire verb、owner、registry 适配器或“trusted=true”选项；生产行为与授权边界保持原样。

## 数据传递与权威传递

允许传递的仍只是既有规范化 `StableSelection` 和 `BootstrapBinding` 数据；`Decode` 的逐字节规范化检查与 `RequireOwn` 的 nonce／目录／data 绑定均保留。不得让新 JSON 字段在某层被忽略、在另一层被当成可信授权。

本片反例会向 selection 外层或 Bootstrap 内层加入 `StartupQualified`／`StartupSources`／source hash／所谓已终结 Installer 来源等声明，即使声明包含匹配的 nonce、路径、receipt 字节和 hash，也必须在调用 start callback 前拒绝。这里的“已终结来源”是伪造的序列化声明，不声称实际创建或复现了一个旧 Installer 引用。

普通 parent 生命周期只接受既有 STOP／EOF 约定。追加伪造 startup grant／ready／begin 消息必须成为协议失败，撤销原 session，结算原 reader、取消回调和输出；不得成为新 participant 或重放权限。拒绝后不发 `CONTROLLER_EXIT_READY`。同时保留既有规范化普通启动、原写入背压和原关闭结算对照，不以全拒绝冒充正确行为。

下一步真正增加普通启动 startup 来源前，必须先说明独立可信的原 issuer 是什么、它如何与当前实际执行的 G／self-contained B 和原进程／通道绑定，以及原 issuer／读句柄／IO 未结算、取消、替换和终结时如何失效。当前仓库没有提供这条跨重启正向资格；本设计不选择签名、凭据、持久化 token 或新的系统权限来填补它。即使未来添加可序列化的 availability 提示，它也只能是数据，不能替代原资源上的授权检查。该缺口需要后续具体设计和审查，不能以本片负例通过而消失。

## 拟实施及验证边界

- 仅将既有 `InstallationLauncherSettlementTests` 拆为 partial，并增加共享原 Input／Output 的 component 用例；不引入第二套测试 owner 或 fixture 来源。
- 覆盖外层伪造声明、Bootstrap 内层声明、伪造首行 verb、nonce／目录／data 不匹配，以及追加 startup 消息；对第一类要求 start callback 零调用、输出零写、无目录创建。
- 追加消息要求原取消可见、原 task 真正结算、错误保留且没有 EXIT_READY；至少一例必须先进入原 start callback，再放行尾行，不能仅用预缓冲尾行证明在途撤销。必要时仅扩展原 Input 的单段受控尾行，不建立新的 owner 或测试输入框架。
- 增加明确正常对照：clean EOF 在 Closing 后使原 bridge return 0 并发 EXIT_READY；带规范 Bootstrap envelope 的普通 selection 可进入 callback；精确 STOP 是正常撤销并保留取消错误，而不是 InvalidData。原同类结算用例一并运行，使用 bounded task 等待并独立关闭测试流。
- 只构建测试项目并运行本 component 类；不需要生成 G／B 产品包或执行 child，不运行全 Core、Windows、registry、登录启动或模型。测试前后保留源码、相关 DLL／运行文件与原 TRX／日志 hash，失败证据不自动删除。
- 生产 Core／Launcher／Installer／Settings 代码与 pins 均不改，真实删除、registry、Shell、身份、设置密钥及登录启动仍不开放。当前入口的失败／拒绝是受限证据，不是生产来源恢复或功能可用证明。

设计独审在实现前完成。审查提出并补入了 clean EOF／规范 Bootstrap／精确 STOP 正常对照，以及 callback 进入后再放行恶意尾行的条件；已审设计原件 5,231B，SHA256 `693a547b78ba4d98870c5e0b32bc17d91cd163a0226a6a1649ff9d3cdff42aa7`，仅原环境保留。实现及本地原件还需另一次独立只读审查，之后才正常开发分支推送。

## 本片实际验证

仅改两个测试文件：原类变为 partial、原 Input 增加一段受控尾行；四个旧测试方法及断言保留。新增 19 例（18 InlineData + 1 Fact）。r01 本类实际 23P／0F／0skip、exit 0／3.968 秒，但独立实现审查发现一个 P2：四处 finally 的有界观察超时可能被捕获后离开 using，而原 task 尚未终结。因此 r01 候选 `e29f5d94bdec23522f875d19f6fbc8ed0caf30b9` 未获 ACK，原件和本地保全引用保留、未推送。

最小修复只将这四处 finally 改为先释放已有信号，再直接 await 原 task；主断言保留 5 秒观察预算，原 task 未终结前不退出 using 或关闭 registration，外层 runner 保持 120 秒兜底。修后只重编并重跑本 component 类，r02 同 23 个 test ID／名称／参数全部 Passed，0F／0skip、exit 0／1.923 秒；预算未到、原进程 join／日志 close 确认。两轮分别记录，不累加成 46。规范 Bootstrap 只证明 envelope 传递，未读取或证明真实 G／B；retired source 仍只是伪造数据，不是实际旧 Installer 引用。

本轮复用已批准的 SDK 10.0.402／Host 10.0.13 和锁定缓存，无 restore／下载；两次相关 test 构建均 0W0E、exit 0。r02 编译／测试实际基线是上述未发布 e29 候选加四处 finally 修复，不能称后来最终提交 SHA 已编译。12,311 输入、4 个主 DLL 及 127 个测试运行文件在 r02 前后逐 hash 相同，真实后验 manifest 保留；Core 源输出与实际 test Core 副本相同，SHA256 `cf5eafbd1b5faf3db253edaa54e9a83ddb9548f31225ca8e4d00c4a54c42a322`。本类未使用 189 runtime fixture，不重核或借用旧结果。

r02 起止为 2026-10-25 04:28:04.623399 — 04:28:06.549222 UTC。原 TRX 35,230B，SHA256 `975c46cf0cb87d0da637cc5e04e2016f35db572dcd06ad50c37653e6dec843e2`；原日志 4,282B，SHA256 `04ee0d75fa93c85c971b716bff2d7b79cf8735ef7112c7f77ecd7c0b22e0fb90`。r01/r02 manifest、已审设计、各轮测试前源码／DLL、TRX／日志只在本环境 `artifacts/2026-10-25-launcher-startup-boundary` 和既有 runner 日志目录保全，没有归档或上传二进制。r02 核验时任务原件约 19.93MB，低于先定 64MiB；测试前可用约 24.49GB。

[机器报告](../test-evidence/2026-10-25-launcher-startup-boundary.json) 给出 23 个精确用例名、命令、起止及源码／证据 hash。本片没有修复或启用正向来源，没有产品 child、全 Core、真实 Windows、registry、登录启动、模型或 CI 运行；没有改变生产代码／pins／Launcher／Installer／Settings、旧验证失败或 P7-05 状态。

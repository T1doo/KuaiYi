# 内部七登记引用撤回：新工作区恢复与限定验证

2026-10-25，在指定 KuaiYi 云工作区按本次具体批准安装微软官方 .NET SDK 10.0.402，并运行一次完整 `InstallationRegistrationWithdrawalTests` 类。结果为 **41P / 0F / 0skip**，原命令 exit 0、98.002 秒，600 秒预算未过期。原进程已 join、原日志已关闭；没有运行全 Core、Windows、CI 或真实模型。

## 恢复和修复范围

旧实例继续冻结，本轮没有访问或清理其目录。六份源码由已保全正文的归档迁入；新工作区先验证 Base64、gzip、tar 和六文件逐项长度/SHA256，再在唯一新写者确认后从开发基线 `2b357c9e1191f21a7fdc8bea499b142d59b12ed3` 原样恢复。原保全 tar SHA256 为 `45588da6782192692e51650815391687409c50897cc38bf655d61278478b2b29`。

内部撤回仍只处理已按内嵌 pins 和原 existing-only admission 取得资格的七个登记引用。receipt 只作比对；撤回和补偿均按原 prefix 顺序，Closing 后不补偿，不删除 payload、owner、journal、锁或目录，也不从磁盘重建写入资格。具体修复为：

- 原 IO、已触及的 registry 和原 actions 未确认结算时，保留原 admission。首次 registry probe 关闭失败发生在 pending 写入前，也不能释放 lease；只能由同一原对象的 `SettleOriginalsAsync` 结算。
- 未触及的 registry 不计入本次 cleanup 或 conflict 判定，也不被关闭。新增对照在本次 admission 已获取、数据目录资格拒绝而 registry 尚未启动的位置检查 lease 已释放，外部干净或非净 owner 均未被读写或关闭。
- Publication 的实际 Move 通过原 IO ledger；执行前或执行后抛错均锁存 UNKNOWN、保留 pending，不用事后读到的 After 状态洗成 `AbortedRestored`。已知效果的 `AfterEffect` 钩子保持在该异常边界之外。正向四个文件步骤的前/后错误，以及补偿中逆向 Move 的前/后错误均有独立用例。
- 审计输出失败设置 `Keep=true`，保留原 root；正文错误在前、审计错误在后。失败或 UNKNOWN 的共享源夹具也保留。审计 outcome 错误先转文本，避免序列化 `Exception` 对象覆盖原结果。

本轮 41 项包含保全源码原 26 项方法及参数组，以及新增 15 项；按原源码分组核对齐备。旧失败 TRX 不在新环境，未伪造历史 ID 比较或将父级移交的 1P/25F 历史回填为通过。

## 官方 SDK 和固定输入

实时微软发布元数据中的 SDK 10.0.402 Linux x64 URL、SHA512 与保全的官方记录完全一致。实际下载 240,279,509 B，SHA512：

`1904a98dda2db34ba78ba3c4fb1b224455329af91c341d83547843c1839e112b04ae699ee92fa2449a8f62249196a938723078c0c60b9f65c88539d5687729c2`

仅解包至工作区私有 SDK 目录；实际 SDK 为 10.0.402、Host 和 SDK 私有 XML/runtime 为 10.0.13。沿现有 `scripts/session-sdk.py` 使用工作区私有 CLI/XDG 目录，没有修改 HOME、系统证书、安全设置或凭据。

两个官方 10.0.13 runtime 包按原锁文件恢复；实际 archive SHA512、NuGet contentHash、官方漏洞审计、无警告绕过的 `nuget verify --all` 和 MIT/无需显式接受的原许可门均通过。固定 Core runtime 合同的 189 项共 **80,453,412 B**，每项大小和 SHA256 均匹配。

189 项是本次从相同受锁官方包重新取得的 runtime/notice 输入。为不改变原测试源码的目录合同，写入其现有兼容目录；没有恢复旧 Core、旧 Launcher、旧 catalog 或旧产物收据，不声称旧 `verification-b-source` 的 194 文件制品恢复完成。新的来源/成员映射清单与原包验签日志均已保留。

## 原件和运行绑定

八个正常角色、Installer 和测试项目共 10 次必要构建均为 0W/0E/exit 0。所有 restore 使用原锁与官方源，锁文件未变。正常 framework-dependent Launcher 合同未放宽。

构建和测试时 HEAD 均为 `2b357c9e1191f21a7fdc8bea499b142d59b12ed3`，工作树包含六份未提交源码候选。12,302 项受跟踪输入及新源码的完整投影在构建前/测试后相同；23 个实际主 DLL 本体已预采样留存，23 DLL 与 127 个测试运行文件前后字节一致。九个角色/Installer 输出中的 Core 与测试 Core 一致。实际 DLL 的基线修订如实记录，不将后续提交 SHA 冒充已编译产物的嵌入修订。

原 TRX 为 61,624 B，SHA256：

`b9d077900c1a4eca05523867def0faf6c2318ab740f3c099644710fd661350f1`

38 份正常审计原件、15 个应保留的审计 root 及两个故意审计失败的 root 均核对实际状态；保留 payload 的 4,575 个文件重新核 hash 一致。成功且已确认结算的夹具按既有测试 cleanup 结束；失败或 UNKNOWN 原件未删除，没有扩展到其他旧/未知目录清理。

随候选提交的[机器摘要](../test-evidence/2026-10-25-withdrawal-recovery.json)仅记录限定结果、来源资格、大小和哈希。实际命令、退出/时限、SDK 和包资格、完整输入投影、原 TRX、日志及审计文件的 101 成员原件包**仅在原执行环境保全，不纳入本次拟推送提交及祖先链、未公开传输或上传**。原件包 1,085,026 B，SHA256：

`e28d7c1c2e07b5a711c783ea44f3ab1158102526aa47c8d61345b3347b83aece`

原件归档保留原路径和原字节；曾包含它的未发布候选 `16bf914d1e10f6c13bb96972d6cc3b44713d527d` 仅由本地保全引用保留，不进入新候选的祖先链。原 DLL 本体、原 TRX 和保留的合成目录也仅在原执行环境保全；不假设其他环境共享路径。

## 交付边界

此结果只验证原 Core/适配器调用在 Linux 隔离文件存储与合成 registry 上的 41 项合同。生产 pins `Unavailable/null`、默认 false、普通 Launcher 资格门均未启用；没有新增公开卸载 CLI，也没有执行真实注册表、Windows 安装卸载、用户电脑或模型调用。全 Core 历史失败、真实 Windows、身份、界面/画质、250/600ms 和完整产品验收均不回填。六源码及 41 项名称/参数已获限定静态 ACK；这不代表父级独读了原 TRX。当前仅交六源码和三个文本的候选供范围审查，原件仍仅在原环境保全。审查后才按授权正常推送该开发分支，不推送本地保全引用；不写 main、不强推或启动 CI。

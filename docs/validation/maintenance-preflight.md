# P7-05 非破坏性卸载／修复预检

状态：本 slice `Pass`；原 P7-05 仍 `InProgress`。本项对应 SPEC 13.4、16.9/P7-05 的独立预检部分，没有实现或验证真实卸载、损坏修复及干净 Windows 虚拟机退出门。

现有 `InstallationRegistrationWithdrawal` 增加固定的 `ForMaintenancePreflight` 模式；两个 intent 为 Uninstall、Repair。它复用原 embedded pins、existing-only admission、只读 IO ledger、`Qualify` 与原 IO／registry／admission 结算，不从磁盘 receipt、路径、历史 ticket 或相邻 pin 文件创建另一套来源资格。第一次完整 pinned generation／self-contained bootstrap 核验后再读七引用，观察变化即拒绝；没有 current 也拒绝，不借历史撤回记录补成修复来源。

`StartPreflight` 在同一 owner 上始终返回同一 task；模式在构造时固定，预检 owner 调用 `Start` 拒绝，撤回 owner 也不能转成预检。结果为 `ObservedReadOnly`、`Refused`、`Cancelled` 或 `Unconfirmed`，含原 operation、intent、历史观察（仅成功且原资源关闭确认时给出）、原 cleanup 状态、剩余步骤及 errors。剩余动作全部 `NotAuthorized`；此结果没有执行、应用或 replay 方法。源关闭后的重复请求只读原结果；刷新必须用新的原 owner 重新核验，旧观察不能用作当前资格。

预检仅借原 admission 排除并行变更，保持 setup／host／upgrade 原 lease；没有创建 pending、plan、tombstone 或 publication channel，没有撤销七引用。取消只在 Closing 前生效；UNKNOWN 优先于取消或成功，不因事后读到匹配字节变绿。原 IO、已启动 registry 或 lease 关闭失败时保原 owner／资源，未确认的依赖关闭前保 admission；`SettleOriginalsAsync` 只结算这些原件，不重读来源、不补偿、不重放。未启动的非净 registry 不被挂靠或关闭。

卸载剩余动作列出七引用撤回、原进程停止、产品／缓存删除、Shell／身份包和设置／密钥移除；修复剩余动作列出原进程停止、payload 恢复、注册发布及 Shell／身份修复。每项均未授权。修复没有安排删除设置／密钥，所有测试中原数据 sentinel 保留。这里的列表用于说明阻塞范围，不能作为执行顺序或授权。

没有新增 public CLI；Installer 仍仅识别 `--install`，生产 pins 仍 `Unavailable/null`，普通 Launcher 资格门及真实 Windows、注册表、Shell、身份包、设置／密钥操作入口没有放开。不启动模型、CI、部署、外部设备或旧实例。

## 限定验证

复用本工作区先前已批准、已实际安装的官方 SDK 10.0.402 / Host 10.0.13 和既有锁定缓存，没有新下载或依赖变更。189 项 runtime 输入 80,453,412B 在本轮测试前后全部核符；输入是本工作区此前新准备的官方 runtime 成员，不是旧 artifact 恢复。

首次 test 编译 `Fail`：4 处同步拒绝断言选到 xUnit async 重载，各有 CS0619 和 xUnit2014，0W/8E、exit 1，未启动测试。改为显式同步 assertion lambda 后 test 构建 0W0E/exit 0；正常八角色及 Installer 构建也全部 0W0E/exit 0。没有放宽 analyzer 或删减断言，原失败日志保留。

只运行完整 `InstallationRegistrationWithdrawalTests` 类一次；该类变为 partial，以复用原 isolated Sources／Rig 和合成 registry，而非另造 fixture 来源。原 41 例名称／参数完整保留，新增 19 例（11 Fact + 8 InlineData），实际 60P/0F/0skip，exit 0，125.962 秒，600 秒预算未到，原进程 join、原日志 close 均确认。起止为 2026-10-25 03:56:35.382642 — 03:58:41.347483 UTC。

新增反例分别覆盖：两 intent 正常只读且全部动作未授权；资格前／后取消；重复请求与并行 owner 拒绝；源关闭历史结果与新 owner 重新核验；未知 pending 原样保留；registry、IO 原 stream、admission 独立 close UNKNOWN 与仅原结算；matching receipt/path 不能替代本次 pins；真实损坏的合成 payload 原字节保留；第二次观察改变；未启动非净 registry；Closing 前拒绝和之后 UNKNOWN；审计失败保 root；撤回 owner 不能改成预检。

12,306 项输入、23 个主 DLL、127 个测试运行文件在本次测试前后全部相同；9 个角色／Installer Core 副本与实际 test Core 相同。实际 Core SHA256 为 `cbc5bb2012395dcfd89a57258b7f72f99a28efad10f4ea4c152865390e928916`。实际构建／测试 HEAD 为 `a67e39cc7f5afab7b80b9d5be76c4ccb3878ddc6` 加四项源码差异，Core/test InformationalVersion 同为 `1.0.0+a67e39cc7f5afab7b80b9d5be76c4ccb3878ddc6`；不冒称后来候选提交 SHA 已被编译。机器报告逐项给出四源码 hash，与原测试前副本完全一致。

原 TRX 89,437B，SHA256 `b241901875f8d251ac38cc9d01d136a9e3645ee6a909c33f49472ee99662293a`；原日志 10,165B，SHA256 `1f6fbc2a4688b53086f383e43fec114759c11523e0d8ec6c5414c6e776d1910e`。56 个审计 JSON，30 个应保留审计 root 及 3 个故意审计失败 root 保全；9,150 个保留 payload 文件重核一致。当前 run 保留 2,934,098,828B，低于先定 8 GiB 上界；启动前可用 27,478,458,368B，核验后可用 24,511,631,360B。

完整 manifest、原 TRX／日志／测试前源码和 DLL 副本、审计与保留 root 仅在原执行环境 `artifacts/2026-10-25-maintenance-preflight` 及已有 runner 日志目录保全；没有归档或传输 binary。仓库仅含源码和可读文本报告，不声称读者已独立读取原 TRX。[机器报告](../test-evidence/2026-10-25-maintenance-preflight.json) 列出本轮 60 例的精确名称和结果、失败编译事实、命令及证据 hash。

不扩大旧验证结论：旧 1P25F／全 Core 历史保持，未重跑全 Core；本轮没有真实 Windows、注册表、安装卸载或修复、真实 Shell／身份包／DPAPI、普通 Launcher 来源资格验证。完整差异及文本范围交独审，ACK 后才正常 dev push；不 main／强推／发布／推保全引用。

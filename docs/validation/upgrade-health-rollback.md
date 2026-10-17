# P7 升级协调、实际自有角色结算与启动健康回退

基线 `e684fc3d3caa504186447faa25b2c7640b539688` 已获父独审有限准入；本片正常分支 `dev/upgrade-health-rollback`，只实现原 SPEC 13.4 / P7-05 的协调子项。不是 Windows 安装器交付，也不提升 P7/K 组整体状态。

## 实现与实际作用域

复用 `InstallationStore` 的完整版本核验、同根 `File.Replace` 指针和 exact previous/current 条件回滚，不通过破坏新版文件来触发 `RecoverAsync`。协调器把新版实际启动与健康端口放在原 writer lease 内；失败先结算自己启动的候选角色，再恢复本次确切旧指针并实际重新启动旧版。初装失败回无安装指针；旧版重启失败、候选 startup/exit/IO/取消任务不确定时保留 pending 并返回 `InstallationUpgradeUnconfirmedException`，不报告成功回退。普通 `ApplyAsync` 的原提交后取消语义保留；协调升级在新健康失败/取消时走回退，`RolledBack` 返回原失败供调用方处理。

所有 Apply、Recover、协调启动共用 `upgrade.lock`；正常启动持到所选完整版本的实际 Start 和健康完成，升级持到健康或可确认回退。增加 `execution-owner.lock` 和 actual Start 之前刷盘创建的 `execution-owner.json`：同 root 另一协调器、普通文件写入者或普通角色入口不能绕过仍存活的已拥有角色。OS 锁释放不清除持久 owner；新协调器不凭 PID、不凭拿到锁推断旧角色已结束。只有原协调器持 exact bytes，且所有自有角色已退出、原协议/IO/取消任务结算后，才归档自己的 owner。

`upgrade-pending.json` 单一有界记录含本次 ID、原指针、来源摘要与旧版；写在退出/替换前。未知、修改或遗留记录一律保留并阻止重入；仅同实例仍持原 owner、journal bytes 和实际子进程句柄时可 `RetryRollbackAsync`。不从磁盘重建进程所有权，不自动清理崩溃后未知 owner。完成记录、旧版和失败候选保留，不做 GC；单条元数据有界不等于累计磁盘使用有界。

固定允许的七个项目 EXE 角色只由本协调器的 `OwnedProcessSession.Start` 返回值拥有；不枚举、打开或停止别人的 PID。旧角色优雅退出是必需平台端口，随后独立核真实 exit、startup 及成功完成原 IO join。拒退不替换指针、不强杀旧角色以继续升级。候选失败清理及 `DisposeAsync` 可以终结本对象自己启动并持有的 child；Dispose 是整个自有运行时的结束，包含健康角色，当前没有安装器退出时把健康应用所有权移交给 Windows 产品启动器的实现。

平台协议原 Task（包括同步方法体）、每次唯一异步取消及 CTS 清理任务都保留在原角色下。控制调用逐段有界，阻塞取消回调不在调用方同步执行；未结算原任务阻止回退/旧版启动。重试复用尚未结束的原退出或 IO join，不另起同一端口操作。平台端口是可信应用适配接口，不是来自浏览器或任意进程的声明；当前没有可用 Windows 适配器。

## 实际测试与红绿

合成包包含真实构建的 SettingsStoreFixture DLL、deps、runtimeconfig、Core DLL 和公开行为文件。测试 starter 明确把所选 generation 的占位 EXE 映射为 dotnet + **该 generation 内**真实 driver DLL；不是从 tools 目录启动后只传版本标签。健康端口检查实际 `AppContext.BaseDirectory`、driver SHA、版本、本次 launch nonce 和 Healthy。占位 EXE 不是 Windows 产品 EXE；启动 GUID 是本次测试/端口关联字段，不是认证凭据，也不授予生产绕过入口门的权限。

所有子进程只使用项目自有公开合成数据、临时根与 stdio；成功及受控失败路径保留真实退出码、driver SHA、原 stdout/stderr 结算标记。晚到 Start 测试先实际创建 child 再延迟返回 owner；原 `OwnedProcessSession` 在 owner 返回后接回、停止并观察它，原 IO 未 join 前不重启旧版。所有未知名字的 sentinel 和链接目标均属于本次 fixture，库不删除它们，测试结束后仅清理自有临时树。

| 执行 | 实际结果和归类 |
| --- | --- |
| r01 | 新增测试 `var` 多声明构建失败，无 TRX；不算运行红测 |
| r02 → r03 | 首个候选 58P/1F，TRX 最终可见失败是 `DisposeAsync` 的结算异常；首个 Retry 异常被 await using Dispose 掩盖，未独立保留。源码路径及仅产品补原任务 join 后同一方法实际 1P，支持候选原任务等待缺口；不能把 TRX 当作首个 Retry 异常轨迹 |
| r04 | 中间候选 62P，不混入最终源码冻结 |
| r05 → r06 | 独审 P2 实际 3F：另一协调器 Start/Upgrade 越过仍活 A 两例；自然退出 A 未 join 原 stderr 就发布 B 一例。启动前持久 owner + 跨操作租约 + 独立 IoSettled 后，同三方法 3P |
| r07 → r08 | 同步 Cancel 阻塞回调实际 1F（调用方超过 10s）；原任务及异步取消持有后，同一方法 1P，先 Unconfirmed/阻塞，释放原回调后实际回旧版 |
| r09 | 最终候选相关 68P，含新完整闭环及原文件事务/安装角色相关回归 |

另实测：真实 OS Start 失败、完整但 unhealthy/错版本/错 nonce/无健康/提前退出、两自有 App/Broker 真退出后启动新版、拒退持久阻塞和原实例重试、真实独立 admission 子进程/其他 writer 拒绝、健康取消、旧版重启失败、unknown journal 保留、初装回无指针；真实控制器 `Environment.Exit(79)` 在 actual Start 前中断，确认 OS owner 锁已释放而记录仍拒绝 fresh。此 checkpoint 不产生子进程，避免孤儿进程实验。

追加四个真实 symlink：包文件、包目录、安装祖先、用户数据祖先；包链接指向相同摘要内容，拒绝来自链接检查，目标每个原字节和原指针/用户数据保持。另连续两次 `RecoverAsync` 比较 exact pointer、所有版本文件和原数据幂等。保留原 40 例全部断言；其中原 SecretFileStore 合成 AES 密文/解密保持覆盖仍执行，新协调测试另比较公开配置及合成密文 sentinel 原字节，不把 sentinel 说成 DPAPI 实验。

最终 r09/r10/r11/r12/r13 源码均一致，每轮 47 份、合计 235 份绑定：相关 68P，完整 Core **1770P = 父 1743 全名多重集 + 新 27**，完整 Provider **2325P** 与父名单完全相同，均无 filter/skip（相关专项明确有 filter）；App/Worker 实际构建各 **0 Warning / 0 Error**。新 27 = 升级协调 22 + symlink 4 + Recover 幂等 1，原测试未删未弱化。独立只读源码复审已无剩余 P1/P2，封存另审，详见[原件索引](../test-evidence/2026-10-17-upgrade-health-rollback.json)。各红绿只对应当前候选实现的缺陷，不归因到历史图片、idle、Core 或 Windows Job 问题。

## 原 P7 下一不可缺的实际对接链

1. **完整 payload → 已核版本树。** `packaging/text-alpha.json` 仍是六 managed role 的 Alpha 子集，installer=false、原生 DLL NotIncluded。复用 `stage-text-alpha.ps1` 的冲突拒绝、每文件 SHA、角色来源记录模式，但保留其 Alpha 合同，不能改标签充完整包。需要独立的完整 x64 自包含多文件清单、原生 Shell/OCR 等实际产物、身份与扩展分发文件。`InstalledRoleFiles.Required` 是 NativeHost 校验子集，不是完整产品安装清单。
2. **真实入口/退出/健康 → 本片协调端口。** 当前 `Program.Main` 不识别安装器退出/健康握手，单实例窗口消息只 Show；`ShellWindow.ClosingShell` 与 `OverlayPrivacyFlow.SettleExitContent` 已有门控取消、设置保存及各自有子模块结算，应接同一条退出路径。需要 App/各入口实际遵守 owner/admission、对实际用户/会话/持有映像确认退出、从选定版本启动、形成真实健康回执与健康后的所有权交接。本片只覆盖自己 Start 并持有的角色，不能替代“系统里所有快译角色已停”。`--diagnose` 只读身份，不能当红色浮钮/托盘/配置启动健康。
3. **确定安装位置 → 派生身份/宿主元数据 → 注册及回退。** `InstalledHostPolicy` 现要求相邻 host manifest、相邻 EXE 的绝对路径、真实进程路径和原角色摘要；NativeHost manifest 的最终绝对路径与 Store 内部随机 generation 目录需要在封存前统一设计。不能发布后补文件破坏 exact catalog，也不能放宽已有校验。可在本环境先编写并以临时目录验证：预留最终目录、验证不可变源 payload 后生成仅固定类型的本机元数据、分别绑定源摘要与派生完整目录摘要、复用 `IntegrationInstalledFiles`/`InstalledRoleFiles` 读取校验。当前尚未实施该片，未颁发 production trust receipt、未填示例/猜测扩展 ID。
4. **Windows 事务适配 → 安装器与 K 组。** 现 `IIntegrationRegistry` 只有 Read，开发身份脚本遇已有 own package 即拒绝，不能当升级/回滚。将来最小产品专用注册端口应快照精确自己 Chrome/Edge HKCU 项/既有身份外部位置，按预期原值更新并核读回；失败逆向条件恢复，foreign/new value 保留且 Unconfirmed，禁止更改 HKLM/策略或泛化删除。其状态机与合成 registry port 可在 Linux 编写测试；实际同身份升级、NativeHost 注册回退、真实扩展 ID、可选登录启动、卸载保留设置选项、干净 Windows VM 的 K01—K08 必须另有实际 Windows 证据。当前片不做永久卸载删除。

P7 集成还缺一处现有生产接线：`InstalledBrowserReceiver` 当前只在 `Program.cs` 的 P0 `ImageProbeWindow` 接收/结算，正常 `ShellWindow` 没有该实例。完成注册并不自动使正常产品浏览器图片链可用；应复用原接收/逐次确认/撤销合同，另接生产来源生命周期，不能把评审窗口当已交付入口。本片未扩这一功能。

## 保留边界

无获准可用 Windows 实测环境。真实角色退出控制、App visible readiness、原生身份/菜单/OCR、NativeHost/浏览器注册回退、DPAPI/ACL、完整截图链、物理 250/600ms、真实模型质量及全 P6/K 组仍 NotRun。Inno 完整安装工程/完整 payload 未交付；签名材料、开发证书信任须另行授权；Skia 4.153.1 四包许可待用户批准、源码 403 不绕过；未查 GitHub Actions 或换路重试先前拒绝。

同根原子替换与 `Flush(true)`/进程 Exit 证据不保证断电耐久或目录 fsync，不保证跨文件/OS 注册事务原子性；不是对抗同账户恶意换名的所有权安全边界。不会把磁盘校验成功等同“旧版可启动”；只给实际成功握手的自己子进程结论。Windows 用户数据迁移可逆性未验证，本片不迁移/解密生产用户设置或秘密。历史图片间歇失败等根因独立 Unknown，生产图片复用 Off，原来源/密码/完整性/取消/清理与发送门不变。无部署、真实安装/注册/签名、费用、凭据/权限变更、新许可接受或未知进程操作。

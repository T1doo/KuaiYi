# 固定 B 源码的 Windows CI 前置条件

固定产品源码 `92e3548845bd07ee7717b53df9588c412d128f9d`，证据父提交 `ab3bf269f956a7b19f6d8ead1383ff80495ad95c`。仓库实际只读查询为 Public，既有标准 `windows-2022`／固定 checkout action／contents:read 可以复用。SDK402 与原固定许可已获授权；本次前置检查不安装或执行 SDK、不恢复依赖、不调用产品或模型。

现有 Windows 工作流只构建 App，未验证 B。固定源码的 isolated App 在最前入口拒绝 `--prepare-image`，不能作为第二 CLI 正向验收包。正常 App 使用 KnownFolder 的 `LocalApplicationData/KuaiYi`，修改环境变量不能证明私有数据隔离。将来正常构建只能在此临时 runner、原目录不存在且专属的前提下运行；不接触用户电脑。

GitHub [官方 runner 说明](https://docs.github.com/en/actions/reference/runners/github-hosted-runners)注明标准公开 runner 免费、Windows VM 为 administrator/UAC disabled。生产 `WindowsInstallationImagePeer.Current` 则要求实际 medium8192、非 elevated、同 user/logon/authentication/session。因此不能假定默认 runner token 可运行正向链。镜像脚本仅设置 ConsentPromptBehaviorAdmin，不能据此断言不存在已有 linked token。

最小变更为一次标准 runner 的只读原生前置检查：原产品 src/tests/packaging/构建配置与固定源码无差异；交付为证据父的单子提交，严格五文件 scope、attempt1、public、job3分钟／step1分钟。通过原 `WindowsFacts` 与有界 `GetTokenInformation` 读取当前及 OS 原有 linked token，输出完整性/elevation/primary type、同身份关系及既有 SeImpersonate present/enabled 布尔值；所有返回 handle 均关闭。不打印 SID/LUID/handle、不创建用户、不以 token 创建进程、不启动产品、不使用 linked token、不调整 token/privilege/UAC/注册表、不安装 SDK或信任、不注册 Shell、不上传 artifact/cache。

检查完成仅记 `InventoryComplete_ProductNotRun`。即使 token shape 合格，也没有真实连接 peer、live session、安装包、同 WPF、Worker、ACL 或用户桌面验收；不把 prerequisite 当授权。若存在同 user/logon/AuthId/session 的原 medium linked primary token，后续才评估使用既有已启用权限的受限 driver 启动；必须另审真实 held-process 复核、原租约、KnownFolder 隔离和全部子角色同 token 链。`CreateProcessWithTokenW` 候选需显式使用已有访问权的 desktop，默认 lpDesktop 可能增加访问权，不能在本授权下默认使用；不临时启用权限、不造 token、不放宽生产门。此片不判断 linked handle 的启动所需访问权。

实际管道 DACL／错误 peer 拒绝可以在 Windows 中单独验证；分阶段取消／断线／owner 保留可运行原确定性合同，但不代表真实 WPF 完整链。正向第二 CLI→同 WPF→实际 Worker finalize→Off 预览还须上述真实 token、可用桌面、正常 App/Worker 完整部署与原安装 store 同时成立。Explorer 菜单／来源、用户焦点／多屏／250/600ms／画质仍需单独真实桌面证据，headless CI 不补签。

实际只投递一次：源码 `e36c92f8c978cd6871e3bbc30a99623d60ef9b64`，[run39559953411](https://github.com/T1doo/KuaiYi/actions/runs/39559953411)／[job119088533514](https://github.com/T1doo/KuaiYi/actions/runs/39559953411/job/119088533514)，2026-10-21 07:08:47—07:09:19 UTC，checkout／前置采集均 success。完整原始 job log 48,731字节、SHA256 `79e2923414321b6a1b132ec913854516d19813c2f2bf23cf99fe743e35c78516`，唯一结果 JSON 与提取件逐字段相同，独立原件复核 ACK。8项 checkout hash 全部精确等 Git 文件的 LF→CRLF 转换；不宣称物理 checkout 字节与 LF blob 相同。

实际当前 primary token：IL12288／High、Elevated=true、ElevationType=Default(1)、session2；SeImpersonate present/enabled 均 true。`TokenLinkedToken` 查询为 Unavailable／Win32 1312，没有取得 linked handle。SameUser/Logon/Authentication/Session 的 false 是未比较的默认值，不能解释为实际身份不匹配。桌面为 WinSta0／Default，UserInteractive、HasShell、DwmEnabled 均 true，1个 monitor，OS build20348、镜像20261012.333.1。前置采集完成不等于任何产品用例 Pass。

正向链的确切阻塞是：**本次当前 token 不合生产 medium/非elevated 门，且没有从其 linked 查询取得合格执行入口**。并非声称所有 medium 路线不可能。现有 SeImpersonate 不能凭空授予该入口；不自动读用其他进程 token、不创建用户或凭据、不调整 token/UAC/ACL、不放宽生产门。继续此方案最小条件是隔离 runner 已有可用的同用户/logon/AuthId/session medium非elevated primary 执行入口，并已具备原桌面访问及启动所需 handle rights；若必须新建受限会话或改变策略，须先明确另行授权的具体动作。无需用户个人电脑或新 SDK 许可。

可在原权限内独立准备下一次有界验证：固定默认 App 的本地文件打开→真实 ImageWorker 解码/退出→实际 WPF 图片/“使用首帧”标注及清零 owner；另可直接验证真实 Windows adapter 的 High 拒绝及原 first-instance/DACL 组件。需分别审定 harness 后再投递，不能把分段结果补签第二 CLI warm 或 Explorer。当前产品/SDK/认证/ACL/Worker/WPF/取消与 owner 验收全部 NotRun；没有追加 CI、重跑 Linux 旧全套或旧 job。

PackageHold、production reuseOff、P7-01 InProgress，生产代码与锁不改。[本轮命令、原始日志与源码索引](../test-evidence/2026-10-21-warm-image-windows.json)。原件 ZIP 180,932字节、SHA256 `6af86c0eaa0d34d873b73b6309558070b080451b127cf8bf3e91e2c1145ffe97`，33唯一成员／MANIFEST32项均核验；Library已成功保存 `libfile_46457f5852f08191a3edb4b5d6a794d4`，version0，不分享私有下载URL。旧 run39347646426 的本次 gh log 返回 Exit0 但原件0字节，不能称已回收或据此归因；历史状态不回填。

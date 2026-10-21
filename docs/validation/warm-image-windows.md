# 固定 B 源码的 Windows CI 前置条件

固定产品源码 `92e3548845bd07ee7717b53df9588c412d128f9d`，证据父提交 `ab3bf269f956a7b19f6d8ead1383ff80495ad95c`。仓库实际只读查询为 Public，既有标准 `windows-2022`／固定 checkout action／contents:read 可以复用。SDK402 与原固定许可已获授权；本次前置检查不安装或执行 SDK、不恢复依赖、不调用产品或模型。

现有 Windows 工作流只构建 App，未验证 B。固定源码的 isolated App 在最前入口拒绝 `--prepare-image`，不能作为第二 CLI 正向验收包。正常 App 使用 KnownFolder 的 `LocalApplicationData/KuaiYi`，修改环境变量不能证明私有数据隔离。将来正常构建只能在此临时 runner、原目录不存在且专属的前提下运行；不接触用户电脑。

GitHub [官方 runner 说明](https://docs.github.com/en/actions/reference/runners/github-hosted-runners)注明标准公开 runner 免费、Windows VM 为 administrator/UAC disabled。生产 `WindowsInstallationImagePeer.Current` 则要求实际 medium8192、非 elevated、同 user/logon/authentication/session。因此不能假定默认 runner token 可运行正向链。镜像脚本仅设置 ConsentPromptBehaviorAdmin，不能据此断言不存在已有 linked token。

最小变更为一次标准 runner 的只读原生前置检查：原产品 src/tests/packaging/构建配置与固定源码无差异；交付为证据父的单子提交，严格五文件 scope、attempt1、public、job3分钟／step1分钟。通过原 `WindowsFacts` 与有界 `GetTokenInformation` 读取当前及 OS 原有 linked token，输出完整性/elevation/primary type、同身份关系及既有 SeImpersonate present/enabled 布尔值；所有返回 handle 均关闭。不打印 SID/LUID/handle、不创建用户、不以 token 创建进程、不启动产品、不使用 linked token、不调整 token/privilege/UAC/注册表、不安装 SDK或信任、不注册 Shell、不上传 artifact/cache。

检查完成仅记 `InventoryComplete_ProductNotRun`。即使 token shape 合格，也没有真实连接 peer、live session、安装包、同 WPF、Worker、ACL 或用户桌面验收；不把 prerequisite 当授权。若存在同 user/logon/AuthId/session 的原 medium linked primary token，后续才评估使用既有已启用权限的受限 driver 启动；必须另审真实 held-process 复核、原租约、KnownFolder 隔离和全部子角色同 token 链。`CreateProcessWithTokenW` 候选需显式使用已有访问权的 desktop，默认 lpDesktop 可能增加访问权，不能在本授权下默认使用；不临时启用权限、不造 token、不放宽生产门。此片不判断 linked handle 的启动所需访问权。

实际管道 DACL／错误 peer 拒绝可以在 Windows 中单独验证；分阶段取消／断线／owner 保留可运行原确定性合同，但不代表真实 WPF 完整链。正向第二 CLI→同 WPF→实际 Worker finalize→Off 预览还须上述真实 token、可用桌面、正常 App/Worker 完整部署与原安装 store 同时成立。Explorer 菜单／来源、用户焦点／多屏／250/600ms／画质仍需单独真实桌面证据，headless CI 不补签。

准备状态：尚未运行 Windows；独审后只投递一次。PackageHold、production reuseOff、P7-01 InProgress，生产代码与锁不改。旧 run39347646426 的本次 gh log 返回 Exit0 但原件0字节，不能称已回收或据此归因；不重跑旧 job。

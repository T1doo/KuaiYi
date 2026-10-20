# P6 隔离解码：许可已批准，源码审查前置未完成

2026-10-20 从已推送的 `ef26c900b8fb5291a8ab1979803c240a34974086` 接续，分支 `dev/p6-codec-source-review`。实际 Linux x64、单 worktree/唯一 writer、开始时 clean，远端原开发分支等于 HEAD；main 仍为 `51d077481cf6303602fcf5d2718213da9cf05178`。本轮仅完成下述授权和官方元数据核对，未交付解码器。

## 本轮明确变化

用户已明确接受固定 **4.153.1** 的 `SkiaSharp`、`SkiaSharp.NativeAssets.Win32`、`SkiaSharp.NativeAssets.macOS`、`SkiaSharp.NativeAssets.Linux.NoDependencies` 四包及原附 MIT/原生第三方条款，限指定云开发环境安装、锁定、隔离图片解码及合成测试。许可门已解除，不再重复请求。此授权不包括其他版本或包、用户电脑、费用、发布、凭据/安全网络权限或绕过拒绝。原生包并非只有 MIT；所接受的[原许可](../test-evidence/2026-10-16-image-isolated-decode-audit-originals__LICENSE.txt)与[第三方声明](../test-evidence/2026-10-16-image-isolated-decode-audit-originals__THIRD-PARTY-NOTICES.txt)仍保持原字节。

四个此前成功且未被拒绝的官方 NuGet nuspec，本轮各一次有界 HTTPS 读取，全部 HTTP200/exit0，与原始已存元数据逐字节同 SHA256。四份均声明版本4.153.1、requireLicenseAcceptance=true、MIT 表达式、branch release/4.153.1、commit `4783f51448f9b070dda4f87b83e941c9599e466e`；net10.0 托管包依赖 Win32/macOS，两者均为相同固定版本。macOS 是声明的传递依赖，不表示产品支持 macOS。没有跟随源码重定向、下载/安装 nupkg、加载或执行 Skia。包声明的 commit 不等于已取得源码或验证了原生资产来源。

## 拒绝边界与当前判断

原明确拒绝目标是下面两项，原回执仅写 Proxy tunnel403 Forbidden，未保留足以比较代理端点/策略的原件：

- `https://api.github.com/repos/mono/SkiaSharp/git/trees/4783f51448f9b070dda4f87b83e941c9599e466e`
- `https://api.github.com/repos/mono/SkiaSharp/releases/tags/v4.153.1`

本轮实际网络 enabled，代理为既有 proxy:8080，普通产品 Git 远端和官方 NuGet 可读；没有观察到针对上述两目标的策略解除证据。旧记录缺少代理端点不能反推出配置已变。独立复核结论是，这些事实不足以构成原目标重试的实质新证据。

**本轮没有请求上述目标，也没有换域名、镜像、Git transport、凭据、代理或其他工具取得同一被拒源码。** 因而本轮不是新的403结果；当前 GitHub 目标实际响应仍未观测，不宣称整个 GitHub 或当前网络不可用。源码读取保持受原拒绝边界限制，许可批准不能解除该门。

## 精确最小缺失材料

源码及供应链审查目前未通过，至少缺：

1. 对应 commit `4783f51448f9b070dda4f87b83e941c9599e466e`、有可核来源/摘要的源码快照，包含所需子模块/固定第三方 revision；仅 nuspec、标签或许可汇总不能替代正文。
2. 该版本实际原生构建配置、组件/功能开关及其与 Win32 `libSkiaSharp.dll`、Linux `libSkiaSharp.so` 的对应证据，以判断第三方声明覆盖的实际资产和依赖；不将包元数据误作构建复现证明。
3. 合规取得这些材料的条件：由获准渠道提供原件，或访问策略管理方对原拒绝目标提供明确解除/允许读取的实质证据。不能以批准许可证、普通站点成功或换环境代替此项。

此前只读包清单、资产哈希与原声明仍可作为审查输入，本轮核其已存原件不变；这不是完整供应链审查通过。未通过前不 restore/install/改项目锁，不写依赖未知 API 的 codec 或提前接图片 UI。未发现可绕开该前置且直接交付本项的独立实现，故没有新增外围工具或重复 SDK 测试。

本轮没有运行 dotnet/测试/构建、触发CI、真实模型、安装/发布或用户电脑操作；生产代码、依赖锁、workflow、原SPEC与旧证据保持。P6-02 仍 InProgress/Blocked，完整 PNG/JPEG/WebP/BMP/GIF首帧、EXIF/一般缩放、P6-06图片打开/投放及 Windows 真实 Worker/OCR/WPF 验收均未完成，不以PNG子集冒充整体。250/600ms、frame/资源上限、来源/密码/完整性/取消/清理/隔离门、生产图片复用Off及PackageHold保持。旧CI拒绝日志未查询或绕路。

本轮官方响应摘要、原件哈希和独立复核见[证据索引](../test-evidence/2026-10-20-p6-codec-source-review.json)。

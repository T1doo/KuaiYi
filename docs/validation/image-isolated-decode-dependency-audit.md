# P6-02 隔离 PNG/JPEG 解码：依赖前置核验与许可阻塞

父版 `fcf4271efa8e1283e3c0b803662e37f54d0a692e`；分支 `dev/image-isolated-decode`。已核实际文件、干净 Git、单 worktree、默认远端父版及 main；唯一写入者 root，既有 inline_review 仅只读独审，无同目标运行实例。本批尚未交付 PNG/JPEG 解码，以下是可复核的前置检查。

## 固定候选包及明确许可门

只读核验官方 NuGet 的稳定候选 **4.153.1**。四个实际 nuspec 均声明 `<requireLicenseAcceptance>true</requireLicenseAcceptance>`，license expression 为 MIT；按用户“遇需明确接受新协议先报告”及 AGENTS 的显式新协议需另授权规则，未运行相关 restore、安装、加载或包代码，未改项目/版本/依赖锁。

| 包 | 用途 | 官方 nupkg SHA256 |
| --- | --- | --- |
| SkiaSharp | 仅拟用于一次性 worker 的托管绑定 | `9cf0fa681223d4956bfbd37995c9eaa8d259935ae3d195fac681ff9a4e9c4816` |
| SkiaSharp.NativeAssets.Win32 | Windows worker 原生库 | `c27b04338d1e4d400c20de534356310b9971e123e081c732af97e56f5572e1ae` |
| SkiaSharp.NativeAssets.macOS | net10.0 托管包实际声明的传递包；不表示支持 macOS | `da8967494eb11d881494d0fd2562893174e92056d41c22c907b2a36caf1032c9` |
| SkiaSharp.NativeAssets.Linux.NoDependencies | 仅拟用于 Linux 合成像素 codec 测试，非 Windows worker 验收 | `1678572dd5d1488c3f39892460bec16c2b0942489f3a5149abfbcbae3da53431` |

官方包地址分别为 [SkiaSharp](https://www.nuget.org/packages/SkiaSharp/4.153.1)、[Win32](https://www.nuget.org/packages/SkiaSharp.NativeAssets.Win32/4.153.1)、[macOS](https://www.nuget.org/packages/SkiaSharp.NativeAssets.macOS/4.153.1)、[Linux.NoDependencies](https://www.nuget.org/packages/SkiaSharp.NativeAssets.Linux.NoDependencies/4.153.1)。包内 nuspec 与官方 flat-container nuspec 的差异仅 BOM/换行，原件分别保留，不改写为相同字节。四包元数据均声明源码 commit `4783f51448f9b070dda4f87b83e941c9599e466e`、branch `release/4.153.1`；这是**包声明的来源**，尚非本批取得源码并验证构建的证明。

已只读检查 nupkg ZIP 内容、固定包 SHA256/SHA512 及全部 ZIP 条目 SHA256，并保留原始许可文本。四包 LICENSE.txt 相同（1129 bytes，SHA256 `89101e35a8c66fd4d6dffc1763259161d35cb564c169714ec227a768c89f2938`）；三原生包 THIRD-PARTY-NOTICES.txt 相同（139775 bytes，SHA256 `21504c46c4c58aa64c1055bd2dcbc5f9a136b4b8c412ed3cc6740e22c5b127f5`）。原生声明包括 DNG SDK 条款、libmicrohttpd 的 LGPL/eCos 文本及其他第三方声明，**不能把整个原生包概括为只有 MIT**。这些汇总声明不能单独证明各组件实际链接进指定资产，组件/构建配置及许可审查仍未完成；没有代用户执行协议接受操作。

关键资产已固定：`runtimes/win-x64/native/libSkiaSharp.dll` 12875576 bytes，SHA256 `935ef4a00462e6b0c4dadb870f734fa43679b4f561287fe0d28cbe2ba147e832`；`runtimes/linux-x64/native/libSkiaSharp.so` 12208760 bytes，SHA256 `b25029b255f3fcfdf758187826b3a802376ec9ec4e8cb17a1e7a41c8f5243d2a`。只解析文件头确认架构，不加载/执行库；签名信任、可重复原生构建及 Windows 实际加载均未验。

官方 GitHub API 的 tree/release 两个只读请求均获代理 `403 Forbidden`，已停止，不重试/绕拒绝。未取得该版本官方源码正文，因此源码级组件审查明确 Blocked。之前 web 工具打开 4.148.0 托管包失败、猜测无扩展名 LICENSE 为404，均不作为4.153.1源码或许可证据；没有改用旧版规避许可门。NuGet 元数据及包内容检查不冒充完整法律/供应链审查通过，也不把候选哈希冒充已 restore 的 packages.lock.json。

## 现有实现与下一步范围

实际 ImageWorker 仍为 `net10.0/win-x64`，private inherited stdio 单进程/单 job，输入上限20MiB；现有路径只做 PNG 头部检查后调用 NativeOcr。现有私有 OCR 回复是最多1MiB JSON，不能直接塞入24MP像素或提高 JSON/frame 限额。后续解码应在原一次性 worker 内，区分 OCR 与 decode 命令，有限分块返回受限像素及方向映射，父端校验 session/version/epoch/request、精确总长/块序与完整终止，取消/崩溃/迟到及真实退出未确认保持 owner，不能单靠 new client 跳过旧持有者。

Skia 引用只放 worker/明确隔离的 codec 项目，主 App/Imaging 不加载不可信原图 codec；生产仍不接打开/拖放 UI、不发送。保留文件来源、密码/完整性、Job/process 及全部取消清理门，不调整250/600ms或现有其他预算来适配 Linux。受限 PNG/JPEG 头部/完整解码、方向映射、实际缓冲清零、合成样本、坏签名/截断/损坏/超尺寸/取消/崩溃/晚回执均需实际代码与回执；本批这些项目全部 NotRun。不会写一个未装依赖、未验证 API 的空解码器来声称完成。

解码端二次验证 format/Info 后才分配固定 BGRA8888/明确 alpha 语义的栅格，只接受完整 Success，拒绝 partial/incomplete。父端在分配前校验 dimensions/stride/checked总长，不能提高既有1MiB frame限制。JPEG非正常EXIF方向、PNG动画/其他多帧若未实现，应有明确拒绝状态，不能写 ScreenshotIdentity 冒充方向处理。一次性进程用于故障隔离，不是已证明的安全沙箱或原生内存/600ms时限证据。

固定官方元数据、包/条目摘要、未完成项目与独审见 [证据](../test-evidence/2026-10-16-image-isolated-decode-dependency-audit.json)。大 nupkg 和原生二进制仅留 ignored artifacts 用于只读核验，不提交/部署。

可审阅原文：[实际 MIT 文本](../test-evidence/2026-10-16-image-isolated-decode-audit-originals__LICENSE.txt)、[实际原生第三方声明](../test-evidence/2026-10-16-image-isolated-decode-audit-originals__THIRD-PARTY-NOTICES.txt)。原始许可含上游空白/换行，不为使 whitespace 检查通过而改写；diff检查对这些不可改写原件单独保留原字节，其余变更执行检查。

## 运行与阶段

本批未运行 dotnet，未修改生产代码、依赖版本/锁、测试、工作流或原生门，故不重跑无变更的 SDK 构建；父版1379/App0W/E仅为历史证据，不能报成本批执行。P6-02保持 InProgress/Blocked，P6-04/05保持 InProgress/NotRun；完整PNG/JPEG解码、全格式样本、EXIF、OCR、一般缩图文字可读性与完整P2/P4/P6未完成，Windows/WPF/COM真实验收仍NotRun。

普通截图仍本地未翻译未发送、生产离线复用Off；250/600ms与全部安全门保持。无新权限/费用/凭据/真实模型/Windows CI/CMake实验，main不改、不强推、不部署。Actions不查询/调度/绕拒绝，run数Unknown；证书存储/信任Unknown，不读改配置/库、不trust/删除；若后续运行dotnet继续每次显式 `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`。需要对固定四包及所附原始第三方条款的明确授权，并解开源码审查阻塞后才能推进依赖与解码实现。

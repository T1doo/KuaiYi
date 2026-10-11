# 已核实的开发环境

日期：2026-10-11。仓库 `/workspace/KuaiYi`，远端 `https://github.com/T1doo/KuaiYi.git`，开发分支 `dev/p0-foundations`，启动基线 `51d077481cf6303602fcf5d2718213da9cf05178`。

- Debian GNU/Linux 13 x64；无 DISPLAY / WAYLAND_DISPLAY，无 Windows 交互桌面。
- .NET SDK 10.0.401、MSBuild 18.9.11、Runtime 10.0.12。SDK 已存在于 `/workspace/.kuaiyi-setup/dotnet`，本轮未重新安装 .NET。
- Node 24.19.0、npm 11.9.0、Python 3.12.14、GCC/G++。
- PowerShell 7.6.6 来自 Microsoft Debian trixie 正式包源；只解包到 `/workspace/.kuaiyi-setup/powershell-7.6.6`。
- PowerShell 包 SHA256：`efae8744e1e552c07ffd7d56782c71813c25d456f8edfb1cabb4f3bb2bfdc248`；已与仓库 Packages.gz 元数据核对。
- 未安装 Windows SDK、MSVC、MakeAppx、Microsoft SignTool、Inno Setup；未发现 CMake。
- Windows 交叉构建仅下载 WindowsDesktop.App.Ref 10.0.12 与 NETCore.App.Host.win-x64 10.0.12（MIT）。这些引用包不等同于原生 Windows SDK 或 C++ 工具链。
- Linux `/usr/bin/signtool` 属于 libnss3-tools，不是 Windows SDK 签名工具。
- 原 `/workspace/.agents` 为空；未发现适用 AGENTS.md，本轮已建立仓库工程约定。

此环境运行脚本的方式：

```bash
source /workspace/.kuaiyi-setup/activate.sh
export PATH=/workspace/.kuaiyi-setup/powershell-7.6.6/opt/microsoft/powershell/7:$PATH
export XDG_CACHE_HOME=/workspace/.kuaiyi-setup/cache
export XDG_CONFIG_HOME=/workspace/.kuaiyi-setup/config
export XDG_DATA_HOME=/workspace/.kuaiyi-setup/data
export POWERSHELL_TELEMETRY_OPTOUT=1
pwsh -NoLogo -NoProfile -File scripts/bootstrap.ps1
pwsh -NoLogo -NoProfile -File scripts/test.ps1 -Suite Offline
```

PowerShell 首次调用因尝试使用只读 `/home/agent/.cache` 失败；设置标准 XDG 目录后启动成功。没有修改 HOME，没有更改系统证书、网络策略或其他项目。

`bootstrap.ps1` 默认只检测，报告写到被 git 忽略的 `artifacts/environment/environment.json`。`-RequireWindows` 检查完整 Windows 条件，不满足时非零退出。

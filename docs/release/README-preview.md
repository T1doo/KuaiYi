# KuaiYi 0.1.0 preview — Windows x64 托管构建输入

这是供审查与后续 Windows 集中验收使用的候选，**不是已安装产品或正式 v1.0**。请先核对 GitHub Release 页面独立列出的 ZIP SHA256，再解压到全新目录，用 Windows 自带 PowerShell 5.1 或已安装的 PowerShell 7 运行：

```powershell
powershell.exe -NoProfile -File ./scripts/verify-preview.ps1 -Root .
```

该命令只核文件集合、长度和 SHA256，不启动程序、不安装、不注册任何组件。若系统执行策略拒绝脚本，停止并报告具体拒绝，不使用Bypass或改系统策略。包内 manifest 是完整性清单，不是可信安装来源、签名或授权；5.1兼容性仍须实际Windows验证，不用Linux语法检查冒充。

包内 `app/` 是本次锁定 .NET SDK 10.0.402、runtime 10.0.13 的七角色 x64 自包含开发闭包，保留App原相邻进程查找路径；`bootstrap-input/` 是独立Launcher输入，PDB另放 `symbols/`。原八角色完整清单及平铺来源均记录在manifest，只允许固定runtime包已定义的WindowsBase Desktop/Core stub冲突，其他异字节一律拒绝。系统无需为了这些输入额外安装 .NET，但本候选尚未在 Windows 上运行。App 有未安装开发入口，这不等于受审安装链；不要将其复制到已安装的 versions/GUID 或 bootstrap-bundles 目录，不要尝试据此启用安装资格。

包未包括四个产品 native DLL、正式或开发签名身份包、Installer 及已封存 G/B；当前扩展目录也不代表已注册或已验证的浏览器 origin。不要安装同名512B合成夹具。`Launcher.exe` 需要原 bootstrap 布局，不能直接用此目录代替；Installer 的原 `Unavailable/null` pins 仍拒绝安装。没有新建密钥、证书、权限、登录启动项或注册表数据。

扩展七个原始文件在 `extensions/chromium/`，不含真实 origin 配置、用户数据或浏览器凭据。来源许可仍要求实际安装所持有的文件/模块及原生命周期；路径、receipt、manifest 或哈希匹配都不会赋权。

完整 Windows 界面运行40472560874实际失败；本次新图包下载Forbidden后停止，实际场景、像素、失败首因与自主清理未知。离线回归及发布校验结果见 Release 附带的报告，不能当 Windows 通过。完整 v1.0 的 A–L、P7/P8及下载即用资格尚未闭合，详见 `docs/release/WINDOWS-TESTS.md`。此包不为真实模型、真实捕获、安装或系统权限动作提供自动入口。

源码版本、每文件哈希和构建状态记录在 manifest。公开包不含构建日志、用户配置、真实密钥、原文或截图。发布失败/未知原件保留原云环境，不能声称它们都已随本包交付。

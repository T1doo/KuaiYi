# 2026-10-23 下午：KuaiYi 最小 Windows 执行交接

只交给父级唯一电脑执行任务 `01a1673b-54ea-75a3-b515-03d6282e4028`。本云任务不连接/操作用户电脑，不另建本地写入者。

## 今日实际条件与交付目标（后续纠正优先）

父级实核用户电脑只有.NET7 runtime，无现代.NET SDK、MSVC、Windows SDK或CMake。用户要求普通用户下载即可使用；不在该电脑安装开发工具，不拿源码或help通过代替成品。四native编译方案仅为以后已有合法工具时的清单，今日不执行。

本轮云端真正解决了原runtime验签/还原条件和Launcher自包含publish；完整端用户包尚未完成：缺四真实Windows native产物（Capture/Ocr/OleNativeBridge/ShellExtension），七角色完整自包含集合及固定来源闭包、真实扩展origin和审定G/B pins。Installer维持Unavailable，不能把结构夹具或下述UI工具称为成品。

另准备一个无需安装运行时的公开WPF界面验收工具，**只用于父级可选界面实测，不代签成品**。见[运行交接](windows-public-overlay-handoff.md)。它使用固定已审源码的真实ShellWindow，限定公开合成内容自动播放，无模型请求、无截图、无选区、不启用OLE/Drop来源接收或取数、浏览器接收或安装注册。云端没有操作电脑或声称Windows通过。

## 固定源与可用包

固定源 `5afaa0c84091186da5f0e968a24936be6d733241`；对应产品代码 `583a8c31162599883f59423b3d9e44dd23f91e1c`，两者只差3证据报告。此前源码独审限定ACK，153是旧离线范围，本次不要重跑。

[固定源提交](https://github.com/T1doo/KuaiYi/commit/5afaa0c84091186da5f0e968a24936be6d733241)。**不是可安装的应用包**，Installer pins仍Unavailable。

为避免整仓历史证据ZIP超过100MiB，准备了完整足够构建这四项目的15文件源切片：`KuaiYi-native-sources-5afaa0c.zip`，16,034B，SHA256 `64b26f8d32e59ae3a0772c804d2ffc45ce8b6fb48e027f6086962cd268eaf12f`，Library `libfile_feb9c0ea0ed88191a067e9d6d2b270f3` v0。父级按Library ID取文件，不传播私有下载URL。包内SOURCE-MANIFEST逐项给SHA256及原Git blob，原字节来自固定提交；包含四native目录、两个原生测试输入和Shell.ico，没有修改CMake或C++，不是完整应用/发布包。

原源目录也可直接核：[Capture](https://github.com/T1doo/KuaiYi/tree/5afaa0c84091186da5f0e968a24936be6d733241/src/KuaiYi.CaptureNative)、[OCR](https://github.com/T1doo/KuaiYi/tree/5afaa0c84091186da5f0e968a24936be6d733241/src/KuaiYi.OcrNative)、[OLE](https://github.com/T1doo/KuaiYi/tree/5afaa0c84091186da5f0e968a24936be6d733241/src/KuaiYi.OleNativeBridge)、[Shell](https://github.com/T1doo/KuaiYi/tree/5afaa0c84091186da5f0e968a24936be6d733241/src/KuaiYi.ShellExtension)。

## 原生方案当前阻塞：缺既有开发工具，不在用户电脑安装

预计15—25分钟（已有工具前提），专属目录预留1GiB，源包仅16KiB。先用3分钟只读检查现有工具：Windows x64、已有CMake>=3.28、Visual Studio 2022 MSVC v143（原已核基线14.44）、Windows原生SDK10.0.26100.0的headers/libs、C++/WinRT及RC。它们不是.NET SDK。缺哪项就报告精确版本/组件，**不安装工具、不接受新协议、不提权、不改PATH全局设置**。其他版本不能冒称锁定基线，先交父级协调。

- Capture：C++20、C++/WinRT；windowsapp/ole32/d3d11/dxgi/user32/gdi32。
- OCR：C++20、C++/WinRT；windowsapp/ole32。
- OleBridge：MSVC/WinSDK；ole32/uuid。
- Shell：另需RC；ole32/shell32/shlwapi。

在全新、确认不为reparse/link的 `E:\temp\KuaiYi-20261023-<随机后缀>` 子目录工作，不复用或清空E:\temp本身。解包前核ZIP完整hash，解包后核SOURCE-MANIFEST所有文件。准备src/build/temp/logs/output目录均在该专属根。只使用已有x64开发工具环境；若需要调用已有vcvars64.bat，仅改变当前子进程环境，不安装组件。

以下只作用于该临时进程环境；不要改USERPROFILE/HOME/APPDATA/LOCALAPPDATA或注册表来伪造隔离：

```powershell
$env:TEMP = "$qaRoot\temp"
$env:TMP = "$qaRoot\temp"
$env:VSCMD_SKIP_SENDTELEMETRY = '1'
$env:DOTNET_CLI_HOME = "$qaRoot\dotnet-state"
$env:NUGET_PACKAGES = "$qaRoot\nuget-packages"
$env:NUGET_HTTP_CACHE_PATH = "$qaRoot\nuget-http"
$env:NUGET_PLUGINS_CACHE_PATH = "$qaRoot\nuget-plugins"
$env:NUGET_SCRATCH = "$qaRoot\nuget-scratch"
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
```

本批只需CMake/MSVC，不需要.NET SDK下载/还原。Windows/预装工具自身的系统维护缓存不能承诺通过这些变量全部迁移；不要删除系统缓存。若工具准备动作确实要新增项目数据到专属目录外，先停该动作交父级，不靠改KnownFolder规避。

分别对四项目执行（$source为解包根；$qaRoot为上述新建专属根）：

```powershell
cmake -S "$source\src\KuaiYi.CaptureNative" -B "$qaRoot\build\CaptureNative" -G "Visual Studio 17 2022" -A x64 -T v143 -DCMAKE_SYSTEM_VERSION=10.0.26100.0
cmake --build "$qaRoot\build\CaptureNative" --config Release --target KuaiYi.CaptureNative GdiReadContractTests --parallel 2
cmake -S "$source\src\KuaiYi.OcrNative" -B "$qaRoot\build\OcrNative" -G "Visual Studio 17 2022" -A x64 -T v143 -DCMAKE_SYSTEM_VERSION=10.0.26100.0
cmake --build "$qaRoot\build\OcrNative" --config Release --target KuaiYi.OcrNative --parallel 2
cmake -S "$source\src\KuaiYi.OleNativeBridge" -B "$qaRoot\build\OleNativeBridge" -G "Visual Studio 17 2022" -A x64 -T v143 -DCMAKE_SYSTEM_VERSION=10.0.26100.0
cmake --build "$qaRoot\build\OleNativeBridge" --config Release --target KuaiYi.OleNativeBridge --parallel 2
cmake -S "$source\src\KuaiYi.ShellExtension" -B "$qaRoot\build\ShellExtension" -G "Visual Studio 17 2022" -A x64 -T v143 -DCMAKE_SYSTEM_VERSION=10.0.26100.0
cmake --build "$qaRoot\build\ShellExtension" --config Release --target KuaiYi.ShellExtension KuaiYi.CommandPolicyTests --parallel 2
ctest --no-tests=error --test-dir "$qaRoot\build\CaptureNative" -C Release -R '^GdiReadOrderingContract$' --output-on-failure
ctest --no-tests=error --test-dir "$qaRoot\build\ShellExtension" -C Release -R '^CommandPolicy$' --output-on-failure
```

逐命令保存真实退出码与原日志；配置或构建失败不循环重试，不降低/W4 /WX /EHsc或换CRT。四项目原CMake均Release静态CRT。两个测试只用合成回调/路径：不截图、不读取用户选区、不启动模型。它们不能代签实际GDI、OCR、OLE、Shell菜单。

验收四个 `build\<项目>\Release\KuaiYi.<项目>.dll`：存在、非零、有界；只读核PE32+/AMD64/DLL标志，记录各自长度/SHA256。若已有dumpbin可只读核exports：Capture的KY_CaptureMonitor/KY_CaptureGdi，OCR的KY_OcrDiagnose/KY_OcrRecognize/KY_OcrFree，OLE的KY_CreateDropTarget，Shell的DllGetClassObject/DllCanUnloadNow。不要LoadLibrary调用/注册DLL来代替静态核验。

回传：固定源SHA、15输入manifest核验结果、工具/SDK准确版本、四构建各自exit、两个CTest结果、四DLL及日志hash。允许将仅本任务产物/日志封成私有Library原件供云端接回；此包不可冒称已签名/完整安装器/运行验收。

## 今日不默认执行的动作

不运行正常App/Installer/--prepare-image：当前正常入口使用真实KnownFolder LocalApplicationData\KuaiYi，不能仅靠TEMP或LOCALAPPDATA环境变量保证全在E:\temp。现有CI-only harness也不能伪造GITHUB_ACTIONS/身份绕守卫。

不注册sparse MSIX/信任开发证书、regsvr32、Shell或NativeHost HKCU项、自启动，不重启Explorer，不安装浏览器扩展或接触已有浏览器个人资料，不借/降token、改UAC/ACL/防火墙。它们有专属目录外系统副作用，今天的临时目录授权不能自动涵盖。已有完整临时模式和合成来源能力经父级另审后，才考虑实际UI/COM/身份链；当前清单不声称这些已准备。

实际Windows首次安装/升级恢复/READY/退出、同WPF warm链、画质及250/600ms仍未验；两原生契约测试不能代替它们，也不涉及任何真实模型验收。

## 退出与清理

结束所有本任务自己创建的cmake/MSBuild/cl/两个测试的原进程/子进程，确认实际退出；不按模糊进程名杀用户进程。若有未结算者，报告精确本任务owner/路径并保留，不假报清理完成。

先回传必要产物及原件并核hash，再删除仅本任务新建且归属已确认的 `E:\temp\KuaiYi-20261023-<随机后缀>` 整个子树（包含下载源包、src/build/temp/cache/log/output）；拒绝链接/越界，不删除E:\temp其他项目或用户数据。不能清理Windows自身缓存/全局NuGet/证书/预装SDK。退出当前临时终端恢复进程级环境；回报目录是否实际消失、剩余占用及失败原文。

## 云端今天已发生的新结果（不占电脑时间）

同固定5af源、SDK10.0.402：两runtime10.0.13原档完整SHA512匹配，正常在线nuget verify --all均exit0且通过既有严格门；随后官方源locked restore和恰两包的实际漏洞查询均通过，原锁未改。旧10月22日阻塞原件不回填。

r03全局显式-r把win-x64错误传播到便携Core，原locked门NU1004拒绝；随后按项目原有RID、去掉命令全局-r，原锁未变，Launcher官方locked restore/实际inventory与漏洞查询、自包含publish均exit0。不是必须改Core锁的产品缺陷。原日志均保留；真实完整B Read/完整七角色包尚未签，生产pins仍Unavailable。

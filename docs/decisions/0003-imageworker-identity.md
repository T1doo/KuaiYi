# ADR 0003：P0-05 独立图片进程与稀疏身份

P0-05 必须验证实际安装位置的双 EXE 包身份、Windows.Media.Ocr 和现代 Explorer 菜单。本轮只实现可审阅的实际调用源码、托管负路径和工程脚本；不注册包、导入/生成证书、不安装扩展，不接受新的 SDK 法律协议。

`KuaiYi.App.exe` 与 `KuaiYi.ImageWorker.exe` 各有独立 `<msix>` 清单，分别对应 Application Id `KuaiYi` / `ImageWorker`。开发 Name=`KuaiYi.Development`，Publisher=`CN=KuaiYi Development`，x64，Version=`0.1.0.0`。实际进程用 GetCurrentPackageFullName / GetCurrentPackageId / GetCurrentApplicationUserModelId 诊断，包括 Publisher；不从父进程或预计算 PFN 推断身份。[官方稀疏包步骤](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/grant-identity-to-nonpackaged-apps)

为了不恢复此前未获准的 Microsoft.Windows.SDK.NET.Ref，OCR 采用 Windows SDK C++/WinRT 桥 DLL，由独立 Worker 的 MTA 调用。源代码实际调用 OcrEngine 的语言/最大尺寸能力及 RecognizeAsync；最终仍须 Windows SDK/MSVC 原生编译与真实识别验证。当前 Linux 没有原生工具链，也没有执行 SDK 安装或显式接受协议。托管项目无新增 NuGet 依赖；CLR 不进入 Shell。[官方 OCR API](https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr.ocrengine?view=winrt-26100)、[桌面 WinRT 支持约束](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/winrt-api-desktop-app-support)

本轮的最小 OCR 输入是无 EXIF 的 PNG。托管和原生都在解码前检查签名/IHDR、20MiB、16384 单边、24MP（乘法使用64位）；解码后再次核对尺寸和设备 OcrEngine.MaxImageDimension。缺身份/语言/原生桥、超限、取消、原生失败都明确报错。返回行、词、边界与文本，不伪造置信度或图表理解。P6 的锁定 SkiaSharp、JPEG/WebP/BMP/GIF、白底透明合成、EXIF、重采样与视觉路由仍未完成；本 ADR 不变更该基线。

父进程持有自己启动的单次 Worker，以继承 stdio 接入，随机握手字节不进命令行；严格绑定 request/session/version/epoch、分片顺序及长度。全图像和结果只走内存；Worker 环境移除 provider secret 等非必要变量。关闭管道/取消或15秒总截止均走自有进程退出确认；失败保留所有权，UI 禁止重启和关闭直至确认。原生 WinRT cancellation 不视为保证终止，仍有进程截止。此私有启动通道是 P0 工程切片，生产命名管道 SID/session/role ACL 与全局协调器仍未实现。

Explorer 使用真正 IExplorerCommand / IClassFactory 的原生 DLL和 SurrogateServer STA 注册；只关联六种规格图片扩展，没有 `*`。菜单构造不打开图片、不解码、不访问模型；Invoke 只选单个本地路径，启动固定相邻主 EXE 的待确认命令，不经 shell 字符串执行。主程序 Off 时不读待处理图片；实际打开后验证磁盘句柄、目录/reparse 属性与最终本地路径再限量读取。PNG之外在本 P0实验界面仍明确不支持，不声明完整 R12。[官方现代菜单路线](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/integrate-packaged-app-with-file-explorer)

开发签名与正式发布分开。已有开发签名证书可由用户显式调用脚本签名；证书生成/信任及注册未获本轮授权，不执行。缺正式签名仅是正式发布条件；不能用它作为不准备开发身份工程的理由。当前未生成 MSIX 的实际原因是没有 Windows MakeAppx/SDK及原生 DLL。开发 EXE 审阅 ZIP 是框架依赖部分产物，不是安装包；P8仍须自包含多文件安装、升级与卸载验证。

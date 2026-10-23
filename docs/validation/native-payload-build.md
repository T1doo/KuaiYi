# 原生 payload：既有免费 Windows CI 一次构建

父级2026-10-23授权完整端用户包优先，允许既有明确免费CI内实际产物构建，不向用户电脑安装开发环境。本片复用原windows-warm-components.yml的既有public标准windows-2022入口，精确parent7312d1f630c1b5ad3697810460db8b4f37fe1bbc、干净源码、五文件差异、attempt1、job15/step14/内部12分钟，无matrix/retry/新权限。contents:read，checkout固定既有SHA，凭据不持久化，无artifact/cache存储、新工具下载或安装/法律接受。

只用预装CMake>=3.28/匹配CTest、唯一VS2022及实际14.44工具集、SDK10.0.26100.0 headers/libs/C++WinRT/RC。当前GitHub官方镜像清单20261012.333.1列CMake3.31.6、VS2022和SDK26100，但不把网页清单当实际runner事实；原Get-CiEnvironment门在job内记录实际版本/NativeBlockers，缺件停止，不换SDK/编译器绕门。既有预装工具使用与先前获准CI相同，不调用安装程序或同意新协议。

四原CMake Release x64：CaptureNative、OcrNative、OleNativeBridge、ShellExtension，原/W4 /WX /EHsc与静态CRT保留；按实际14.44完整版本指定v143。原两个合成CTest精确各执行1例（无零测试通过），不执行产品DLL/模型/注册/桌面。PE32+/AMD64/DLL静态门与全文件SHA256、实际sourcehead/toolset/SDK收据保留。编译失败停止余项，已产物/日志原件仍回收；超时明确原子树退出Unconfirmed，不签清理通过。

四DLL与只登记的命令日志/事实/CTest XML通过原emit_evidence输出到普通job日志；每件16MiB、raw合计32MiB/gzip合计4MiB，超限Blocked。没有GitHub artifact存储或Release。推送只确认请求投递，不代表runner启动或构建通过。沿既有拒绝边界，本任务不访问被拒Actions/runner API或更换通道绕拒；父级正常授权页面回收KUAIYI_NATIVE_PAYLOAD、KUAIYI_CI_FILE/CHUNK，逐件gzip/raw hash及全部分片核验后，才可将真实四DLL作为包输入。

七角色App/NativeHost/CaptureBroker/ClipboardHost/CaptureHost/OleReceiver/ImageWorker及Launcher自包含发布可在既有云端SDK402和严格验签runtime13条件下独立推进，不需用户SDK；真实G/B封存仍要求源闭包和实际资格门。四native构建/托管publish都不能自动生成合法扩展origin或审定pins，Installer保持Unavailable，完整安装、READY/恢复、真实Windows用户交互、250/600ms尚未由此验证。

官方依据：[镜像工具清单](https://github.com/actions/runner-images/blob/main/images/windows/Windows2022-Readme.md)、[public标准runner免费范围](https://docs.github.com/en/billing/concepts/product-billing/github-actions)。公开仓库metadata本次实际read成功（visibility public，非archived）；不将仓库admin metadata解释成未测的Actions endpoint权限。

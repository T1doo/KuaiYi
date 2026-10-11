# P0-05 图片 Worker、身份和 Explorer 工程

这是平台工程切片，P0-05 / R12 尚未验收。`KuaiYi.App` 当前是明确标识的 P0 OCR 审阅界面，默认 Off，无网络、自动取词、API Key 或译文持久化；不是已完成的 P1 主产品或浮窗。Explorer传来的路径待用户确认才读取。窗口内结果仅存内存，Off清空并取消。

## 工程与可复现离线检查

- Imaging：有界 PNG 头检查、语言/身份/引擎尺寸策略、严格私有 IPC、实际打开文件句柄验证、Worker 生命周期。
- ImageWorker：独立 EXE 和身份，MTA调用原生 OCR、内存分片、错误返回；不引用 Providers。
- OcrNative：真实 C++/WinRT OcrEngine/BitmapDecoder、词框 JSON、取消 event、CoTaskMem显式释放；当前尚未原生编译。
- ShellExtension：真正 COM class factory / IExplorerCommand、单文件本地图片、Windows argv转义、固定相邻EXE，现代菜单清单。
- packaging/identity：开发稀疏清单、两应用独立嵌入身份、中性自制图标。native两项目须在已授权安装的 Windows SDK/MSVC环境编译。

```powershell
./scripts/build.ps1 -Scope Managed
./scripts/test.ps1 -Suite Offline
./scripts/test-identity-source.ps1
./scripts/stage-identity-probes.ps1 -ManagedOnly
```

Linux另可运行 `g++ -std=c++20 -DNDEBUG -Wall -Wextra -Werror tests/native/command-policy-test.cpp -o /tmp/kuaiyi-command-policy-test` 后执行该文件；它只验证实际Shell共用的路径/argv策略，不含COM，不代表现代菜单已通过。断言在Release也有效。

审阅包 `artifacts/KuaiYi-P0-identity-managed-win-x64.zip` 包含两个已检查 PE32+ x64、各自内嵌 `<msix>` 身份的 EXE及托管依赖/图标，要求 .NET 10 Windows Desktop x64。缺 OcrNative/Shell DLL 时安装验证必须失败；绝不能拿它当可识别图片的完整包或安装器。

## Windows 下的后续步骤（本轮均未执行）

只使用本项目的 Windows 测试环境和已经授权的工具链；不默认操作用户连接的桌面。获得本轮未具备的环境/授权后：

1. `build.ps1 -Scope All` 构建两个原生 DLL，并执行原生共用策略 CTest；保留完整 MSVC 输出。随后 `stage-identity-probes.ps1` 将它们与双 EXE合并。先 `validate-dev-identity.ps1 -InstallRoot <真实测试安装目录>`。
2. `package-identity.ps1 -Mode Development -InstallRoot <目录>` 调用真正 Microsoft Windows SDK MakeAppx，生成未签名开发 MSIX。稀疏包 `/nv` 是官方外部内容打包步骤，会跳过文件引用验证；因此不把该命令独自当 schema/路径通过，需另留 Windows实际注册和安装位置诊断证据。
3. 开发证书须 Subject=`CN=KuaiYi Development`、有效代码签名用途、有私钥且仅供开发。用户自行批准生成与 CurrentUser TrustedPeople 信任；本轮没有创建/导入证书。已有证书时显式 `package-identity.ps1 ... -CertificateThumbprint <开发证书摘要>` 在当前用户 My 存储签名，脚本没有 PFX密码参数，不导出私钥。
4. `register-dev-identity.ps1 -Mode Validate -InstallRoot <目录> -PackagePath <已签MSIX>` 验证 Microsoft SignTool、可信开发签名、实际包XML及安装PE。`-Mode Register` 仅供后续用户明确批准后显式调用；不自动导入证书、不删任何已有包、不提升权限或绕过安全策略。已有同名开发身份时拒绝，让用户明确处理自身包的版本。
5. `test-installed-identity.ps1 -InstallRoot <目录>` 从目录直接启动两个EXE，分别比对实际注册 FullName/PFN!ApplicationId；Worker语言列表/最大尺寸只是能力诊断，OCR仍NotRun。加 `-RunPublicOcrFixture -Language en-US` 才建立自己CreateNew的公开中性PNG，主EXE→Worker→OCR应精确识别 `KUAIYI TEST`；结果只记计数、匹配布尔值和退出状态，无OCR原文日志。临时文件只包含仓库公开夹具，随后删除。
6. 实际Explorer单PNG/JPEG/WebP/BMP/GIF、多选、目录、伪扩展文件、受限/网络/重解析路径与菜单性能需要独立观察。必须记录 Windows 11现代第一层菜单“快译”和 Invoke→主程序Off待确认；“显示更多选项”不算通过。P0 PNG调用实验不能声称其它格式的生产解码完成。

脚本从不默认安装包、生成/信任证书、注册COM、重启Explorer或改ExecutionPolicy。Windows GUI/焦点、实际句柄重解析、WinRT取消/ABI、OCR文本质量、现代菜单、安装/升级/卸载均为NotRun。本轮不会以Linux实际进程或XML文本检查替代这些证据。

正式签名/安装器属于P8；`package-identity.ps1 -Mode Release` 当前明确失败，无未签名release替代。

窗口状态及真实启动线程已进一步接入共享Gate/队列接收器，见image-ui-review.md。启动期间取消保留未确认所有权并清理迟建进程；不会仅因取消等待就宣称没有创建Worker。

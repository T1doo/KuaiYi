# 原生 payload：既有免费 Windows CI 一次构建

父级2026-10-23授权完整端用户包优先，允许既有明确免费CI内实际产物构建，不向用户电脑安装开发环境。本片复用原windows-warm-components.yml的既有public标准windows-2022入口，精确parent7312d1f630c1b5ad3697810460db8b4f37fe1bbc、干净源码、五文件差异、attempt1、job15/step14/内部12分钟，无matrix/retry/新权限。contents:read，checkout固定既有SHA，凭据不持久化，无artifact/cache存储、新工具下载或安装/法律接受。

只用预装CMake>=3.28/匹配CTest、唯一VS2022及实际14.44工具集、SDK10.0.26100.0 headers/libs/C++WinRT/RC。当前GitHub官方镜像清单20261012.333.1列CMake3.31.6、VS2022和SDK26100，但不把网页清单当实际runner事实；原Get-CiEnvironment门在job内记录实际版本/NativeBlockers，缺件停止，不换SDK/编译器绕门。既有预装工具使用与先前获准CI相同，不调用安装程序或同意新协议。

四原CMake Release x64：CaptureNative、OcrNative、OleNativeBridge、ShellExtension，原/W4 /WX /EHsc与静态CRT保留；按实际14.44完整版本指定v143。原两个合成CTest精确各执行1例（无零测试通过），不执行产品DLL/模型/注册/桌面。PE32+/AMD64/DLL静态门与全文件SHA256、实际sourcehead/toolset/SDK收据保留。编译失败停止余项，已产物/日志原件仍回收；超时明确原子树退出Unconfirmed，不签清理通过。

四DLL与只登记的命令日志/事实/CTest XML通过原emit_evidence输出到普通job日志；每件16MiB、raw合计32MiB/gzip合计4MiB，超限Blocked。没有GitHub artifact存储或Release。推送只确认请求投递，不代表runner启动或构建通过。沿既有拒绝边界，本任务不访问被拒Actions/runner API或更换通道绕拒；父级正常授权页面回收KUAIYI_NATIVE_PAYLOAD、KUAIYI_CI_FILE/CHUNK，逐件gzip/raw hash及全部分片核验后，才可将真实四DLL作为包输入。

七角色App/NativeHost/CaptureBroker/ClipboardHost/CaptureHost/OleReceiver/ImageWorker及Launcher自包含发布可在既有云端SDK402和严格验签runtime13条件下独立推进，不需用户SDK；真实G/B封存仍要求源闭包和实际资格门。四native构建/托管publish都不能自动生成合法扩展origin或审定pins，Installer保持Unavailable，完整安装、READY/恢复、真实Windows用户交互、250/600ms尚未由此验证。

官方依据：[镜像工具清单](https://github.com/actions/runner-images/blob/main/images/windows/Windows2022-Readme.md)、[public标准runner免费范围](https://docs.github.com/en/billing/concepts/product-billing/github-actions)。公开仓库metadata本次实际read成功（visibility public，非archived）；不将仓库admin metadata解释成未测的Actions endpoint权限。

## 2026-10-23 正常日志正文回收及消费者独立核验

此前“原件未回收”为历史状态；这一具体阻塞现已关闭。父级使用已有登录的普通job页面正文取得原FILE/CHUNK，不使用被拒download/raw/API路径。本消费者环境独立通过Library材料化，未假设父级临时目录共享。

Library `libfile_832f9b13205c81919c08be209f93db84` v0，`kuaiyi-ci-39909049792-evidence.zip`，345446字节、3JSON；ZIP SHA256 `6587b905d2ca37f13bc92ae306d03fdb495dd7b73046507ec6f5ee5143c5cc62`。CRC、唯一成员及原正文SHA256 `69723d2eac65dbdd088b666c9a1a665738ffa5b75b4f81b9a0b59d4019c4ee85` 均核。原job为120267538766/run39909049792，source `147ab977b3725b15d35e55b0efb01110edb9d297`。

消费者自行重组20 FILE/71 CHUNK：原顺序/连续序号/总数、严格base64、gzip长度和SHA、单gzip完整结束、raw长度和SHA、summary完整清单和合计全部通过；raw710057字节、gzip335693字节。四DLL与native-products长度/hash/source/toolset/SDK逐项一致，PE头只读核AMD64/PE32+/DLL标志。原两个CTest XML各1通过仅为历史执行记录，本轮未重跑。原运行facts实际为Server2022/high IL/session2/WinSta0/Default/单屏96DPI、DesktopBlockers空；Sdk字段null，不能声称预装SDK402可用。

四个native源码目录从147ab到消费者HEAD `bf479ec6d2bb3c488e9fdf5bb1037caeb6df246b` 无diff。独立消费收据见[JSON](../test-evidence/2026-10-23-native-ci-consumer.json)，解码原件保存在当前workspace `artifacts/2026-10-23-native-ci-recovery/decoded/`，没有加载、执行或安装DLL。

本结果只解除原件缺失与回收通路未证明两项阻塞，不代表完整payload资格、origin/G/B pins、签名、真实安装、READY或WPF UI通过；ProductionPins仍Unavailable、WindowsAppAcceptance仍NotRun。没有新CI运行。后续生产UI证据可沿相同普通日志正文通道回收，须绑定新job/source、保持原有界分片/摘要门及独立消费者核验；当前已有workflow仍仅原生构建，不能直接当作UI入口重跑。

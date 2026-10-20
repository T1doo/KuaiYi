# 补齐既有 11 项浏览器宿主集成测试

源码与测试基线 `64745d23f689d4fc9507507fb580679d6a8de7f4`；从该提交创建正常开发分支 `dev/browser-host-integration`。本轮没有改产品代码、测试、锁、脚本或workflow，只建立已批准项目隔离工具链并补原缺口。

## 授权、许可与当前环境

指定环境 Linux x86_64 / Debian13，原工作区只有旧SDK401，未执行或替换它。`global.json`精确402/rollForward disable。历史[授权原件](../test-evidence/2026-10-18-session-runtime-security-originals__sdk-authorization-and-blocker.json)记录2026-10-18 09:22:58UTC批准官方Linux SDK402在快译云workspace独立下载/hash/extraction/build；[后续初始化结果](session-runtime-security.md)及progress的2026-10-19条目记录继续解决初始化授权。当前委派明确允许同范围常规工具链，因此并非仅WindowsCI许可。

从原精确官方URL取同一240,279,509B tar.gz，无redirect/retry/换源；完整SHA512等于既有批准原件 `1904a98dda2db34ba78ba3c4fb1b224455329af91c341d83547843c1839e112b04ae699ee92fa2449a8f62249196a938723078c0c60b9f65c88539d5687729c2`。归档MIT 1,116B和notices77,436B逐字节等于已提交原件，无新协议。预检5,631项/640,467,796B，拒绝非普通文件/目录、绝对路径、上跳、重复后解到原固定项目隔离SDK402路径。SDK/归档仅在ignored artifacts，不提交或系统安装。

复用未改的scripts/session-sdk.py：只对子进程设置精确SDKROOT/HOST_PATH/PATH、工作区0700 runtime/data/cli，默认用户作用域mutex、真实NuGet迁移及CLI首次使用正常执行；不伪造sentinel/跳first-use。HOME不变，HOME/.dotnet仍不存在，既有目录权限未改；生成开发证书false。静态运行时/私有XML13门先于dotnet，实际--info确认SDK402/Host13。

## 实际命令与结果

原BrowserHostFixture及Contracts/Core/Imaging/NativeHost五项目执行`restore --locked-mode -p:NuGetAudit=true -p:NuGetAuditMode=all -p:NuGetAuditLevel=low -p:TreatWarningsAsErrors=true`成功；随后`build --no-restore -c Release -nodeReuse:false -p:UseSharedCompilation=false`成功，0警告/0错误。五份实际assets均enableAudit=true/all/low、logs空、libraries仅Project；NativeHost额外SDK apphost下载为精确10.0.13。此路径无第三方PackageReference，不将无包审计说成Windows运行时包在线验签，也未调用App准备器/publish或解除Package Hold。

精确选择原遗漏11项（2个命名单例＋9个confirmation boundary情形），现有测试文件不改：

```sh
node --test --test-concurrency=1 --test-reporter=tap --test-name-pattern='^(actual extension sender to independent host loop commits ordered maximum chunks|actual extension/host/production receiver waits over five seconds for explicit synthetic desktop decision|actual ServiceWorker/native host/production receiver confirmation boundary:)' tests/browser/protocol.test.mjs tests/browser/confirmation.test.mjs
```

在launcher返回的环境设置`DOTNET_HOST_PATH`为精确402 muxer。2026-10-20 02:08:44UTC开始，实际8.109秒，Exit0；**11 Pass / 0 Fail / 0 Skipped / 0 Cancelled**。最终TAP名称集合逐项严格等于上轮regression.log中缺dotnet的11失败名称，不扩大纯Node测试数，不将先前64P/11F原件覆写，也未重跑完整75项。

实际一例最大分片发送经生产BrowserSession/DesktopPipeChannel/IPC codecs到合成PublicFixtureDesktop的真实stdio/local pipe；另10例经生产DesktopReceiverSession/LocalImageInbox/RuntimeGate及真实管道，覆盖6秒后确认、允许/不回答/拒绝/取消/Off/断开/迟到回答/未派timer的迟到回答/迟到Ready。Chrome Port、peer身份和用户decision均为明确合成，9边界情形控制clock，第一例6秒等待为真实elapsed。测试断言原子提交次数、摘要、原字节数、缓冲清零/收件箱空、子进程退出等，而非简单spawn成功。

137个相关源码/项目/锁/JS/夹具/启动器输入逐字节等基线，实际宿主目录16文件SHA封存，Contracts/Core/Imaging/NativeHost四DLL与各项目build输出相同。五份实际assets原件封存；原SDK和原所有锁不变。只读独核确认授权/许可/11名称及实际结果、真实宿主与合成端口边界，无剩余P1/P2；见[机器索引](../test-evidence/2026-10-20-browser-host-integration.json)。

真实Chrome/Edge注册和权限、Windows/WPF/身份/ACL/实际输入时限、UIA/OLE/OCR/完整P6P7、模型质量、签名安装及发布仍未验。原250/600ms、来源/密码/完整性/取消/清理隔离门及图片复用Off不改。CI39347646426第3步内部首因Unknown；没有新CI、Actions或credential_observation_restricted重试。未改系统/共享训练环境、安全权限/网络/凭据、用户电脑，没有模型、新费用或发布。

# P0-06 浏览器图片与 NativeHost 协议切片

基线 `cb54673dc9686059a5b5f632c23aebb42eb3e1ad` 的清理专项已获监督方放行。本轮按完整SPEC的11.3、12.4、13.2、14.1—14.4和P0-06推进。交付真实MV3扩展源码、生产NativeHost入口、实际stdio/内部IPC客户端及离线证据；不是已安装的浏览器入口或完整R13/P0通过。

## 当前修正与未完成截图路径

监督复核确认原中心命中不能证明完整像素归属。当前浏览器截图fallback全面拒绝，移除scripting权限，仅保留同源无凭据PNG原图；具体反例、43项测试和来源未认证状态见browser-ownership-review.md。后续ReadBounded竞态已修，最新.NET388/Node43全回归及新ZIP见browser-read-ownership-review.md；旧v1包还含该已知读取竞态。下文31项/v1包记录是历史协议切片，不能作为安全截图候选或实际来源认证。

## 历史用户意图、权限与图片取得

扩展只申请contextMenus、nativeMessaging、activeTab、scripting；没有永久host权限、history、cookies、storage、内容脚本、网页消息入口、externally_connectable或默认key。只响应Chrome提供的图片菜单onClicked；核对当前active tab、HTTPS页面、主frame、点击页面和操作ID。每次操作30秒、单次连接；换操作、导航、移除tab、切换目标窗口的active tab或用户点击扩展按钮均取消。不从postMessage接受来源；native Port的onMessage仅接收宿主回复。

先connectNative并发送OfferImage，等桌面实际授权Ready后才读图片或截图。原图仅在当前页面同源HTTPS范围fetch，credentials=omit、redirect=error、referrerPolicy=no-referrer、cache=no-store；拒绝认证信息、跨源原图和无界流。P0只接受PNG字节。取得失败时使用scripting隔离世界定位唯一同URL图片，核对可见矩形、页面、滚动、DPR、visualViewport和缩放；保守拒绝跨frame、重复目标、遮挡/裁切/transform不确定情况。captureVisibleTab前后重复测量，坐标变化即丢弃；按物理像素向内裁切并仅传裁后PNG，显示“使用图片可见部分”。tab切换/导航事件取消操作；实际浏览器事件时序与截图品质仍未验证。不能验证则提示区域截图，不猜目标、不给桌面srcUrl，也不发送整张网页截图。

当前截图几何和权限有源码及纯逻辑/模拟API拒绝测试，真实Chrome/Edge捕获、遮挡、缩放、导航竞态均NotRun。桌面区域截图后端、热键和完整多格式预处理仍未实现，提示不是已可运行的替代功能验收。

## 两层协议和授权

| 浏览器消息 | 行为 |
|---|---|
| OfferImage(operationId) | 已验证来源的宿主向已验证本机App发SubmitImage/OfferImage；未启用或未授权返回Error，不读图 |
| Ready(operationId, protocol=1) | 本次桌面授权已取得；不向扩展导出内部身份/nonce |
| BeginImage(transferId, totalLength, mime, sha256) | ID必须等于Offer；只接受有限PNG声明，分配有界传输 |
| Chunk(transferId, index, data) | 复用Contracts.ImageChunkWire真实解析，严格字段/ID/顺序/base64/字节上限，逐片确认 |
| CommitImage(transferId) | 全量和SHA256一致，PNG头/24MP/16384单边验证后才向桌面IPC传字节 |
| CancelImage(operationId) | 清除缓冲、通知该授权的桌面取消并结束连接；未知ID拒绝 |

Native Messaging使用原生uint长度和UTF-8 JSON，完整消息最多256KiB；原始分片190KiB、总量20MiB，绝对30秒截止。stdout只输出有限协议帧，错误不回显输入。JSON最大深度32，未知/重复字段、非法类型、负/超大长度、乱序、重复Begin、无Offer、摘要不符及URL/activation声明均拒绝。Commit只是向桌面交付图片，不调用OCR或模型。

内部IPC是独立KYY1版本1编码，控制64KiB、二进制1MiB（4字节索引包含在上限内）。连接ID由宿主生成；桌面发完整session/version/request/epoch与一次nonce，后续Begin/Commit/Cancel携带原grant；IPC Begin附长度、MIME、尺寸和SHA256，未确认Begun不发送二进制。回复需匹配连接ID、消息角色和operationId，未知/失联不宣称交付成功。JSON回复ParseValue持有独立副本，清零原帧后仍可校验。

## 生产与测试边界

生产Program没有--probe、fixture、可绕过平台或角色检查的开关。严格读取浏览器首个origin参数，须canonical chrome-extension://32个a-p字符/；Windows下须当前用户Chrome/Edge注册项指向固定相邻宿主清单，清单名称/stdio/EXE必须对应实际启动二进制，allowed_origins须为实际扩展ID且无通配符。没有预填任何示例ID。

还要求相邻native-host-trust.json的schemaVersion=1和files对象完整匹配11个已安装App/Host的EXE、DLL、deps/runtimeconfig及共享运行模块；缺失、重复、未知路径、修改DLL均拒绝。配置是P7安装事务输出，当前未生成。它不是签名/完全抵抗同账户恶意软件的声明。

通过当前用户专属、SID+Windows session+NativeHost角色命名的本机管道连接App；CurrentUserOnly、本机服务器地址、实际GetNamedPipeServerProcessId、进程session、固定App位置和文件完整性校验后才建立DesktopPipeChannel。App未运行/未提供该管道则拒绝，不拉起任意EXE。当前App还没有生产角色入口、NativeHost客户端完整性验证及授权发放端；这些P1/P7接线未冒充完成。即使手工注册也不能在当前App里得到Ready。扩展origin不是模型endpoint origin，图片授权不会授予HTTP服务商同意；模型网络仍必须复用RuntimeGate.CanSend/TryBeginSend。

KuaiYi.BrowserHostFixture位于tools，仅用于测试，绝不发布/注册。它运行同一BrowserSession和DesktopPipeChannel；独立宿主进程内通过实际本地命名管道连到控制的测试桌面authority，authority使用真实RuntimeGate发身份/nonce并校验每次交付。合成a字母origin只在这个测试工具和负路径测试中出现；无生产允许来源。正路径测试证明传输/请求合同，未证明Windows安装或实际授权来源。

## 本轮实际运行

- 19托管工程Release locked-mode交叉构建0警告/0错误。
- Core304 + Provider77 = **381/381**，0失败/0跳过；另有Node标准库执行实际扩展模块 **31/31**，两种运行器合计412个不同测试子项。
- 新.NET51项：44项独立宿主进程，其中4项启动正式NativeHost验证无配置/非法origin/--probe拒绝，其余40项运行确切生产循环与真实内部管道的测试authority；7项真实临时合成文件完整性验证。正路径含完整20MiB/多分片/摘要/尺寸/IPC；取消、EOF、绝对截止、Off/B/锁屏、伪grant/回复ID、超限/截断/UTF-8/重复字段均实测。
- Node31项含同一扩展sendImage/nativeConnection→实际子进程stdio→实际内部命名管道→测试authority的三分片传输，端到端字节SHA256与公开合成输入一致。Chrome Port适配器只在测试中模拟；不是浏览器安装或真实页面取图通过。其余为权限静态、fetch参数、来源/裁切边界及失联/取消测试。
- 新实现首轮40中39通过、1失败：实际stdin读取未及时响应token；给等待者加WaitAsync绝对截止，保留/观察迟到读取，不假称底层读取已取消。首次修复40中35通过、5失败因迟到观察重复stderr破坏测试计数格式，已用有限类型码记录并保留原始证据。首次真实IPC51中19通过、32失败：JsonDocument引用了被清零帧；改独立ParseValue副本后51全部通过。没有删除或降低原断言。

原始TRX、Node JUnit、源摘要、日志和审阅ZIP摘要见 [证据汇总](test-evidence/2026-10-11-browser-summary.json)。`scripts/test.ps1 -Suite Offline`现包含Node测试；另用`scripts/build.ps1 -Scope Managed`、`scripts/stage-browser-probes.ps1`构建审阅分发。Node24.19.0，无npm依赖或依赖安装。

审阅ZIP `artifacts/KuaiYi-P0-browser-protocol-v1-win-x64.zip`：SHA256 `0d00ab21af2d514516be4048c75da877a9432131fb4d1ca55a2c531c57a5ed04`，209309字节，18个文件摘要逐一核对，正式宿主EXE为实际PE32+ x64/Console静态检查通过。只含NativeHost托管框架依赖产物和七个真实扩展文件，不含fixture、测试、实际origin清单或信任receipt，不是安装包。没有扩展安装、NativeHost注册、OAuth、API Key、付费模型、证书或系统策略改动。全部106整体验收仍NotRun，P0-06总体Blocked；实际浏览器和Windows角色/注册/截图验证、P0其它平台门槛及E06同步浮钮冻结缺口不变。待监督独审后再推进下一授权切片。

依据：[Chrome Native Messaging](https://developer.chrome.com/docs/extensions/develop/concepts/native-messaging)、[activeTab](https://developer.chrome.com/docs/extensions/develop/concepts/activeTab)、[contextMenus](https://developer.chrome.com/docs/extensions/reference/api/contextMenus)、[Edge Native Messaging](https://learn.microsoft.com/en-us/microsoft-edge/extensions/developer-guide/native-messaging)，本轮2026-10-11核对，不复制官方示例ID到生产配置。

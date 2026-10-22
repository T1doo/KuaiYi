# 共享 scope 原 IO 的 G 候选准备

接续限定 ACK 的 a909c20981dd88fb31ecb42bf943071dbfec32f1 / e73e0539c9922951154bef8e32ec9d80d7e226f8，对应 SPEC13.3/13.4、P7-04/05。仅内部只准备 G，不接 B、Prepared、current 发布、注册或安装入口。

## 产品路径

原 scope 在启动前持有唯一 generation IO owner、固定的 source pin、Ticket/Before SHA 和预留随机路径，发布内层原 Task 后才执行。仅完整 Before 后可启动一次；取消和 Abort 封闭 producer、等待同一原任务，不能用新任务证明旧工作完成。私有构造候选只能由完整验证工厂产生，绑定 scope 引用和原 Task 结果；交付仲裁及 live 检查均检查原 token，取消不可恢复。

G 工厂依次通过源 catalog pin、全部文件/角色/PE/deps/runtime/XML/扩展合同，编码并预检有界 intent 和两种 terminal retention，然后 CreateNew/Flush/Close intent。完整 intent 才允许写本次 staging。逐块复制真实源字节及校验摘要，派生完整 source catalog/provenance/Host manifest/trust/v3 catalog；分别在 staging（使用最终路径）及随机 final 目录完整复验后才产生候选。源 SHA 与安装派生 SHA 明确区分，metadata 不从源包接受，candidate 没有发布/执行 API。

Package/Payload 的共享路径把原 FileStream、directory enumerator、PEReader 和 IncrementalHash 先登记到 scope 持有的 IO ledger，再开始可能抛错的工作；所有嵌套读取均传同一 owner。正文错与每个原关闭错分别收集，单错 EDI、多错 Aggregate。新目录副作用前必须确认原资源已结算。未调用旧 CopyTo/Materialize，也没有失败时删除 partial 的局部 using 封装。

metadata 的 CreateNew/Write/Flush/Close 任一尝试出错即永久 StorageUnconfirmed；保留原 bytes、pending、同一资源 owner，不以完整长度或事后关闭改判已知状态。payload partial 仅在 intent 完整、原 IO 确已结算时允许 AbortBeforePrepared，terminal 记 IncompleteInactive 与 intent/source SHA，候选目录保留。完整候选对应 VerifiedInactive，Prepared SHA 仍 null；两者都不是成功安装。未知 staging/final、foreign metadata 或 Move 结果不明仍 Unconfirmed，不领养、不删、不重放。

原 finalizer join G 后关闭原 ledger，再进行 Closing 的 metadata 精确核验；新开的读取句柄也进入同一 ledger 并由该预发布 finalizer 结算。失败时 CleanupConfirmed 为 false，同 pending SettleOriginalsAsync 只重试原资源，不重读、不重放、不归档。成功归档 Move 后无可失败的资源清理。

## 实际原件与定向红绿

artifacts/2026-10-22-generation-preparation 保留每轮完整 command/log/TRX、失败 UnitTestResult 原文、输入与产物摘要、对应完整测试源码和候选 diff。轮次不相加。

- r01 Core 普通编译 0W/E，零测试。
- r02 source 原读取与关闭双故障反例 1F：原 FileStream 未结算却错误归档 AbortedBeforePrepared；r03 完整测试文件相同 1P，修为 Unconfirmed 并保留同句柄，只在同 owner 上结算。
- r04 七普通托管构建 0W/E、无锁文件变更，为现有合成源夹具准备实际托管 DLL/apphost/deps。
- r05 最后校验后取消及交付后取消两个反例 2F；r06 完整测试文件相同 2P。BeforeCredential 是最后原校验后的纯故障注入阶段，测试不依赖 sleep 或真实调度猜测；生产默认无 hook。
- r07 G 专项 24P/0F/0skip，r08 新增嵌套 PEReader/enumerator 原关闭双错及未知 staging/final 保留 4P/0F/0skip。未用两轮相加冒称固定树签收。

## 验证边界

完整源夹具仅复制当前正常构建的托管角色、apphost、deps，原生 DLL/runtime/第三方缺件仍是明确的中性结构性 PE fixture。通过说明生产纯文件/协议验证接受该结构闭包，绝不说明可启动 Windows 自包含包、真实身份注册或安装成功。未运行真实 Windows/medium/browser/Explorer/WPF/OLE/OCR/画质/250/600ms；已存历史 Windows 分段证据保持原范围。本片不更改 Off、来源/密码/完整性、取消/清理、隔离与发送门、250/600 预算或 WebP odd 拒绝；无新依赖/许可/凭据/权限/费用/模型调用。

B 的共享准备、Prepared 与三方前向/逆序补偿仍未实施；本片没有 Committed、恢复/历史授权、fixed locator、CLI 注册/安装/自启动入口。旧独立入口沿默认原实现，不改变既有消费合同。

固定树签收与原件归档将在独审通过后记录于本报告，不借记旧586/310或完整Core/Codec。

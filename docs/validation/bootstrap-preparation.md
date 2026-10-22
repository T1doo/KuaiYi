# 同 scope 的 inactive bootstrap 准备工厂

接续根侧限定ACK的 G 产品 eaec97468fe2d47c89fde042f528ba002a6f3e65 / 证据01805238769af7d420d21dd3dca440664b993031，对应SPEC13.3/13.4、P7-04/05。本片只有 B 只准备，没有 Prepared、canonical bootstrap 发布、三方提交或安装入口。

## 同一原 scope 与 IO ledger

原 Ticket 保存的 B 源 SHA 是唯一 pin，调用方只能提供源路径与原 G 候选。原 G Task 必须成功、凭据必须属于同一仍存活 scope，才能登记唯一 B channel 与唯一内层 B Task；producer 开放前原 Task 已发布。交付及 live 检查都核 scope、原 G、原 token 与 Closing；取消不可恢复，错误不自动换源重试。finalizer join 原 B，cleanup 任务门包含原 B，Abort/取消不能遗弃原 IO。

B channel 与 G 共用同一个 Ledger，资源、metadata、不确定状态均共享；每条记录保留原 channel。原 FileStream、PEReader、hash 和 enumerator 先登记再执行，Close 故障从原 channel 分派；Closing 新开的 metadata 读取仍由预发布 finalizer 拥有，使用写入该 metadata 的原 channel 读取/关闭。未以新的 stream/独立 cleanup owner 假证原资源关闭，单错 EDI、多错 Aggregate 保留。

B channel 只写本次 intent/staging，额外允许只读原 G 目录。完整 G v3/source/installed SHA 与实际 payload 再验证；B 源、staging、最终候选分别通过同一 BootstrapPackage 资格门，比较实际 Core bytes、托管 revision/依赖、Windows x64 apphost/binding、精确 deps/runtimeconfig。所有上述文件 IO 均使用同 channel；没有把旧 Read/Hash 的 using 或 File.ReadAllBytes 包在外层冒充原 owner。普通独立入口仍走原默认语义，旧 Hash(string) overload 保留。

apphost 绑定采用有界流式查找，正确保留跨64KiB块的尾部，实际读完并检查长度；允许原32MiB上限、拒绝+1。metadata 读取的原4MiB通用上限及各消费限额未扩大。

## 归属、只准备与失败

预留 Ticket.Bundle 对应独立 deployment-bootstrap-<operation>.json；intent绑定源 pin、Ticket/Before SHA、原 G 源/派生 SHA、revision 及精确 staging/final。首次写前检查 actual intent≤16KiB 和 G+B 完整/不完整两种 terminal 实际≤16KiB。

路径为 root/bootstrap-staging-<Bundle> → root/bootstrap-candidates/<Bundle>。inactive 父目录只是容器；未知 peer 内容不领养、不删，预留 child 已存在则拒绝并保留。canonical bootstrap-bundles、bootstrap-store.json、stable-bootstrap.json、current、registry/Host receipt 均不写。本片避免因创建无 canonical owner 的 bundles 而在 Abort 后让旧入口永久拒绝；准备结果不是可启动稳定入口。后续 Prepared/发布需明确 canonical owner 与 bundle 归属事务，不能把本结果直接当 active receipt。

完整 B intent 后才复制五文件及写原 catalog；staging与final验证通过才能由 private 工厂产生 scope/G 绑定候选。metadata CreateNew/Write/Flush/Close 任一错误永久锁存全局未知，保留 pending/partial/同 owner；payload partial 仅在 intent 完整、原 IO 全部结算后可 AbortBeforePrepared，并保留 IncompleteInactive。完整候选记 VerifiedInactive，Prepared仍null。Move结果未知、foreign intent/catalog、未知 child 持续阻断，不重放、不降级激活。terminal 保留独立 G/B 归属，不以 clean Abort 冒称部署成功。

## 实际候选原件（不相加）

artifacts/2026-10-22-bootstrap-preparation 包含完整 command/log/TRX/失败XML、对应源码、输入和产物前后摘要。

- r01 共享IO/Bootstrap读取 Core 编译0W/E，零测试；r02 七普通managed构建0W/E。
- r03 新B原读取与关闭双错真实1F：finalizer尚未join新B Task，返回异常只剩PUBLIC_B_CLOSE，丢失PUBLIC_B_READ。r04 完整测试文件相同1P；补原B join及任务门，原错误对象和同一未关闭句柄均保留，后续仅原owner结算。
- r05 专项实际12P/18F，全部原件保留，不作为验收通过。r02 的 Launcher/Core副本是补join前构建，r04/test重编主Core后两者实际摘要不等；严格Core门拒绝，部分屏障因此未到。r05-core-copy-diagnostic.json 保存主Core7d6cfa9115e5ad08135b3f05b0f4ae472a1e705148c18ca4ef59e0f7d18ad75c和Launcher副本1d8a8d231ef559b0be8be174145d08e9f6ba959dd8c7bbe925af72a3f9dfff6b。
- r06 不改产品或测试，只同步七普通managed构建，均0W/E；r07 与r05完整测试文件相同，30P/0F/0skip。字节门未放宽，旧红不回填；r05的12P不借记为完整有效验证。
- r08 收紧4个资格反例为精确错误原因，4P：源pin、Core bytes、Launcher revision、apphost binding。避免不相关失败替代目标拒绝。

30例包含只准备成功及原绑定、Abort后真实旧Apply/Deploy纯文件兼容、foreign/retired/canceled G、G实际重验、metadata partial/flush/close、payload partial、取消交付与原read/copy/flush屏障、Move未知、Closing新B原读双错、foreign metadata/未知子目录、32MiB及跨块读取。

## 未签边界

B五文件来自现有正常框架依赖构建；G仍使用明确的原生/runtime结构夹具，不能据此证明Windows自包含包可运行。真实Windows/medium/browser/Explorer/WPF/OLE/OCR效果/画质/250/600ms未验；三native DLL及完整Windows runtime缺件继续保留。既有历史Windows分段只保留原口径。

无新许可、依赖、凭据、权限、费用、真实模型调用或用户电脑操作；不改main、不部署。Off、来源/密码/完整性、取消/清理/隔离/发送门、250/600预算和odd WebP拒绝保持。Prepared/Host准备/三方提交与补偿、canonical bootstrap发布、完整安装/注册/P7仍未实现。

内部独立只读源码和候选原件审查限定ACK，无确定新增P1/P2；固定树验证与Library原件另记，不借候选轮次作为固定签收。

## 固定树签收

产品 `f23bdcf387cfacb7d9c187c8c27739f1f7a97eb0` clean树执行r09七次普通managed构建，全部0W/E；r10同固定树一次 **141P/0F/0skip**，B30＋G28＋Scope25＋Before23＋旧BootstrapDeployment35，五类141唯一显示名称。旧BootstrapDeployment实际35，以TRX为准；未借记此前128/586/310或完整Core/Codec，所有轮次分列。

r09/r10的909输入before=after且逐项匹配产品Git；17目标AssemblyInfo均含完整产品SHA，14产品DLL从构建到测试摘要一致，测试及两fixture本轮普通编译，4实际Core副本一致。没有二进制本体，只保留产物摘要与版本源码，不冒称独立DLL重hash。

365原src仅修改BootstrapPackage/Scope/PreparationIO三文件，新增private B candidate；362其余src及335原tests全部字节不变，新30例单独文件。锁、项目、workflow、packaging、扩展、scripts、sln未改。audit另核r03/r04完整测试文件相同，r05/r07完整测试文件和所有产品输入相同，及同步构建前后Core副本不同→一致；这些失败不回填为产品通过。

固定树及原件独审限定ACK。Library `libfile_d4d833ce83fc8191b658d54379a20274`（v0，1,546,947B，179成员/178manifest），SHA256 `77c02c171d5341a48a9d5006bac48a81e5fc737f2ed08de9ed407296674a249e`；本地Library身份已核。ZIP CRC、178摘要、10份固定Git源码、raw141、红绿、混代Core诊断及17版本文件均独立核验，无DLL本体。证据提交仅三报告，产品f23bdcf不变；Prepared/canonical激活/Windows仍未签。

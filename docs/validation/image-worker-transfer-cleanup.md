# ImageWorker 私有传输、取消与缓冲所有权闭环

父版 `83c821f47937b076889c64f881db68034d0f925c`，正常分支 `dev/image-worker-transfer-cleanup`；核实际文件/HEAD、干净基线、单 worktree、默认远端/main与唯一写入者 root，worker_review 仅只读独审。四包许可确认与源码403阻塞保持，本批不依赖Skia，也未推进其解码能力。

## 已修复的实际缺口

ImageJobReceiver 的 Abort/Dispose 原来只清 data/Job，未封闭生命周期，失败后可再次 Begin，filled/next还可能残留。现终态锁存，清零实际已持有图像，重置 Job/RequestId/计数；之后 Begin/Chunk/Commit 均无法恢复。没有新增摘要/协议类型或放宽原来的 request、序号、精确总长与20MiB限制。

ImageWorkerClient 原来直接 await 管道读写，依靠底层协作取消；非协作读取会挡住 finally，无法及时执行已持有子进程的停止。现等待与原任务分开：取消可进入原 OwnedProcessSession.Stop；迟到读只观察并清实际 payload，不再交付。所有原读异常记录有界类型码。原写及 Flush 还持有数组时不清，真正结算后才清；nonce的发送副本与用于比较的独立32字节分开拥有。

原调用仍运行、原I/O/清理任务未结算或进程/启动结算未确认时，TryFinalize拒绝最终释放。进程已退但I/O仍持有时返回新的 `SettlementUnconfirmed`（枚举尾部追加，原值不变），保留同一client/Process所有权；后续结算完成才允许实际Dispose。ObserveExit只报告原有进程/启动退出事实，不等于全部I/O清理完成。原App入口持有ownedWorker，原timer/finally只有TryFinalize成功才清owner，未改这些门。二次Run不再重置active标志，仍单次使用。

原Worker hello校验/echo异常路径未必清nonce；另当控制帧/读取先失败，recognize可能仍借用原图时worker即可退出作用域并清图。现实际私有loop提取为ImageWorkerSession，Program仍直接绑定原NativeOcr.Recognize。hello payload有finally；原recognize与原control读取都在不可跳过的finally结算后才Dispose receiver，即使控制非法、回复写入失败或取消回调抛错也如此。控制帧实际payload始终清零，次要故障只输出有界异常类型，不输出消息/图像/路径。原生工作不响应取消时继续持有，由父原有进程门停止，不用新超时跳过借用任务。正常路径仍先回复再等父端EOF，避免互等。

## 实测与失败回执

所有dotnet在激活前后及命令前显式 `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`；测试启动的每个dotnet子进程也显式设置false。Release、后续no-restore，重复restore使用locked-mode。Provider测试以别名直接引用实际Imaging与Worker DLL，不复制Worker实现或另造decoder；现有测试项目锁仅增加3个Project节点，全部第三方版本/contentHash不变，其余42锁原字节不变。没有还原/安装/加载Skia或新的第三方依赖。

| 回执 | 实际结果 |
| --- | --- |
| r00 | 既有包图加入实际生产项目引用，restore成功，无新第三方包 |
| r01 | 编译失败7诊断，0执行/无TRX；新夹具的target-typed new、Stream派生类结构字段访问和阻塞Task.Result，保原源码/log后修夹具 |
| r02 | **0通过/9失败**：Receiver4、hello1、control/借图2、真实托管worker非协作读2，均为实际生产缺口 |
| r03 | 9/9 |
| r04 | 23/23 |
| r05/r06 | locked restore通过，专项35/35 |
| r07 | 1414/1414 |
| r08/r09 | Worker win-x64与App Windows目标交叉构建分别0W/0E |
| r10 | 最终 **1416/1416**，父版1379测试名多重集完整保留，新增37项，无跳过 |

37项覆盖终态/完整身份/序号/精确长度，实际IpcFrameCodec伪造uint.MaxValue长度在28字节处拒绝、截断后真实4字节payload清零；原操作借用图直到控制异常/取消回调结束才清；hello/回复/非空迟到控制真实数组清零；有效cancel/EOF顺序；真实托管worker hello/结果两个非协作读阶段、hello/Begin/图片块/Flush四个写阶段、晚读任意异常、清理未确认持有/再释放、重复Run和预取消。

真实子进程执行本次构建的win-x64目标**托管DLL**，测试按当前构建配置定位，并核child Worker DLL与测试引用DLL的SHA256相同；证据另绑定实际child目录和tests目录的Worker/Imaging/Core/Contracts四对DLL。Linux正常往返的结果是原有 `UnsupportedPlatform`，没有Windows NativeOCR、Job原生验收或PNG/JPEG codec执行。Session的识别委托、流延迟与8字节输入是明确合成夹具，仅证明生产协议/所有权；不宣称真实图片解码/视觉理解。

测试中的2/3/8秒是失败保护或合成调度屏障，不是产品取消时限验收。原15秒worker/client预算、OwnedProcessSession/真实exit和App门原样保持；250/600ms并未调整。17份SDK原log/TRX与10份父版/编译/红版源码按精确 `-text` 路径保全；父192原件及1777既有证据不改。一次只读ADR文件名通配未命中为工具路径错误，不是生产/SDK失败。详见[固定证据](../test-evidence/2026-10-16-image-worker-transfer-cleanup.json)。

## 未验与阶段

P6-02仍InProgress/Blocked，P6-04/05仍InProgress/NotRun。PNG/JPEG有界codec/EXIF/多格式样本/源码组件许可审查、OCR与一般缩图文字可读性未完成；Windows实际进程/Job/包身份/NativeOCR、WPF测量/选择/阅读位置、COM、真实取消时限和完整P2/P4/P6仍NotRun。未运行完整Core.Tests项目或CMake/Windows探针；本批未新增CI或查询/调度/绕拒绝Actions，run数Unknown。

普通截图仍本地未翻译未发送，生产离线复用Off；来源/密码/完整性/取消/清理与250/600ms门、Core/Contracts/全部App/Provider生产代码、8模板/8workflow保持。证书存储/信任Unknown，不读改配置/库、trust或删除。无新协议接受/权限/费用/凭据/真实模型/main/强推/部署；此前许可确认仍待用户明确答复，官方源码403未绕过。

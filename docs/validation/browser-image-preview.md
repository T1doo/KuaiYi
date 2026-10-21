# 正常主浮窗接收浏览器图片并准备本地预览

基线 `1eeb40444d718d8d92529207220cdc17ca0c076e`，正常开发分支 `dev/p7-browser-image-preview`，指定保存 Linux 环境、单 worktree、唯一 root writer。SPEC 13.2/P7-03 的既有接收器此前只用于 P0 探针；本轮把它接入已运行的正常主浮窗。先独立只读设计审查，再实现和源码复审；没有启动旧写入实例或删除未知锁。

## 实际产品路径

正常生产 Shell 在启动配置健康且已安装的接收策略核验成功后启动原接收器；离线实验、fixture 和关闭平台集成的窗口不启动。策略、当前用户管道、持有的 Windows peer、SID/session/PID/starttime/path/摘要、允许 origin 和原版本/分片协议不放宽。扩展声明不当作已证明的浏览器身份，逐次桌面确认默认拒绝。菜单显示接收状态或失败类型；失败不自动修复、注册或重试。

接收专用 RuntimeGate 只授权本次有限 PNG 内存传输，不授予主浮窗启用、profile、endpoint 或 HTTP 权限。原 Shell Off/On 状态保持。会话只有在 Commit 回执和原 finally 完成、原异步读取实际返回后，才回调带精确 connectionId/committed identity 的交接；Inbox 只能按同一 identity 一次转移。原任务未结束不会以“已取消”冒充清理完成。即使 Cancel 回调或 pipe.Dispose 抛异常，也继续等待原读取/确认/发布任务，失败的已提交收据清除，原异常对象保留。

接收前记录 Shell epoch/启用状态、settings version、image/result revision 与可见性；确认、传输、交接、原准备任务和采用来源继续核对。停用、设置变化、停止、清除、隐藏、锁屏和退出撤销。后台设置提交即使落在最后检查与安装图片 CTS 的间隙，也由持续 preparation permit 的原 token/revision 拒绝；获取 capture owner 时在 Gate 同一锁中比较原 epoch/Activation。`.NET CancelAsync` 同步标记 token、异步执行回调并返回其结算任务；本轮持有该原任务到结束，再清实际借用收据。[官方语义](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtokensource.cancelasync?view=net-10.0)

浏览器原 PNG 字节借用到原准备 Task 及取消回调实际结束，然后清零；不生成临时文件或文件路径，不强转截图。沿既有 ImageWorkerClient/native 解码、归一化、原 job/SHA、EOF/实际退出/清理门，保留 20MiB/24MP/16384 输入与 2560 长边/4MiB 输出界。历史类型名 `OwnedFileImage` 是共享的闭合编码栅格 owner，本轮可信 Kind=Browser 且只接受 PNG；没有文件 handle、桌面 anchor 或截图 proof。原打开/投放文件 API 保持。

本地预览明确显示“浏览器图片 · 本地接收 · 未翻译”；原图入口、图文确认、结果版本和失败恢复保留浏览器来源。Off 接收不启用、不自动识别或发送。需要用户沿原启用与图文确认后，原两阶段分类/生成逐次门控才可发送；图片复用仍 Off。已有 OCR-only 只延续可信 Browser 来源标签，未新增 OCR 算法或重复文件 OCR 验证。

## 真实验证及证据

命令、退出值、TRX、失败日志、预修复源码、最终源码/产物摘要及旧文件字节比对见[证据索引](../test-evidence/2026-10-21-browser-image-preview.json)及其原件 ZIP。未修改旧测试断言、依赖/锁/许可、扩展权限或安装身份策略。

- 接收回归实际使用 Linux portable named pipe、原 DesktopPipeChannel/Session/Inbox。peer 与确认端口明确合成；覆盖 Off/On 独立接收门、原 pending read 未返不交接、原准备/取消回调未返不清收据、旧来源/设置/锁屏不复活、最后安装间隙撤销、异常清理。不能把这些当 Windows peer/WPF 实测。
- Provider 新 9 项编译链接实际共享命令、来源、标签和 coordinator；worker/已准备 owner 端口明确合成。两协议使用真实本地 loopback HTTP，原确认拒绝时零 HTTP，确认后沿原分类和生成，不带 Cookie/路径/origin/SHA。实际失效提示正文保持浏览器标注。
- Codec 新 3 项用实际 Linux native/private Worker 解码合成 PNG，检验原 SHA/独立 Browser kind/无桌面 proof，JPEG 在 factory 前拒绝；原受控投影 Task 未返不 finalize，取消后实际数组清零。分段验证不冒充从浏览器到 Windows Shell 的完整端到端。

最终完整 Provider **2436P/0F/0skip**（原2427+新9），完整 Codec **481P/0F/0skip**（原478+新3）。完整 Core 首轮及一次同源码 `--no-build` 复核均 **2069P/1F/0skip**（2070总数，含新增14项均通过）；唯一最终报告失败为旧 `CaptureHostTests.KillFailureOrUnconfirmedWaitRetainsOwnerAndBlocksFinalizeWithoutBlockingUi(waitFailure:true)`，`Cleanup:69` 的 finalize 断言。两轮之间定向原方法 **2P**；这不证明根因已解决。finally 可能覆盖 body 异常，不能据最终栈声称此前所有断言通过。完整 Core 状态保留 **Fail / 根因 UNKNOWN**，不循环重跑凑绿，不改旧断言或退出门。

App、NativeHost、ImageWorker 最终 Release 交叉构建各 **0W/0E**；正确 App/ImageWorker 两次 locked restore + audit all/low 退出0。脚本 r17/r20 误写成不存在的 `KuaiYi.Worker` 项目（MSB1009，未编译/审计产品），r21/r22 用正确 `KuaiYi.ImageWorker` 补跑通过，错误日志保留。383项原测试/锁/依赖项目/许可/扩展/工作流/安装数据/SPEC字节保护比对全真；Provider测试项目仅新增实际命令的 Compile link。中间 75/77/79、41、3 的定向通过不累计。

保留失败：r02 新命令缺 System.IO 的编译失败；r04 新 fixture 未赋值字段 CS0649（0 执行）；r10 注入 pipe.Dispose 异常后失败断言实际是 Inbox 非 null（2034 byte 收据），不是“Running 已完成”断言失败。独审进一步发现两处 Cancel 抛异常可跳过原任务，修复后加入真实回调抛异常、未配合取消的原 decision Task 回归。全部原件保留。

## 未验及剩余缺口

当前只连接**已运行、正常安装策略可用**的主浮窗，未完成冷启动/Explorer `--prepare-image` 的原生产交接；该入口仍需独立的认证转交设计。既有扩展仍限已授权同源 HTTPS PNG；跨域、认证、blob、其他格式和可见部分裁切没有扩展。完整 P7-02/P7-03、K05/K06、P6/Beta 及 106 项总验收不标 Pass。

真实 Windows NativeHost/管道 peer 身份、Chrome/Edge/WPF/最终安装、OLE/COM/Windows OCR 与本机包身份、真实来源/密码/完整性、画质/字号、250/600ms、600MiB 仍未验。旧 BMP 469P/1F→定向2P→470P 历史根因仍未知。本轮旧 CaptureHost 全 Core 清理失败单独保留，不推断为已修复或与浏览器接入有确定因果。

WebP 奇数 payload 明确拒绝、PackageHold、生产复用 Off 及所有原发送/来源/密码/完整性/原 SHA/取消/实际退出门保持。仅合成中性数据与 loopback；无真实模型、用户内容或用户电脑、新依赖许可、费用、凭据、安全网络权限、CI 调度、main 修改、强推或发布部署。

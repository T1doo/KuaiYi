# 原客户端 GrantedAccess 与独立 peer 复核

2026-10-21：**客户端 bit4 实授已确认，安全红门保持；整轮仍 Fail／exit3**。独审限定 ACK 原件与独立测试结论，不能将此解释为无风险或已证明生产 DACL 漏洞。服务器实例操作性权限影响尚未确认。

实际执行源 `9d90a1f75f587ed20f2214a4b9efaf3f4bdff514`，唯一父 `a7f6aa57ca11c707203dfd5d8062bd88d3acd508`。固定 `92e3548845bd07ee7717b53df9588c412d128f9d` 的生产／测试／锁／打包均不改。旧 CreateFile 有效句柄及两轮红件保留，不回填历史唯一根因。

## 本轮实际观测

[run39567803017/job119114224216](https://github.com/T1doo/KuaiYi/actions/runs/39567803017/job/119114224216) 实际 job **failure**、wrapper **Blocked**、probe **Fail**、actual-pipe-access **exit3**。18条记录命令中其余17条均exit0；没有超时、重跑或全套凑绿。

| 独立 fixture | 原客户端请求 | NtQueryObject 实授 | 实际 endpoint | 结论 |
|---|---|---|---|---|
| bit4测量 | `0x0012019f` | `0x0012019f`，bit4=true | Client，flags=8 | **Fail：ClientBit4Granted_SecurityReviewRequired** |
| exact-mask／双向负例 | `0x0012019b` | `0x0012019b`，bit4=false | Client，flags=8 | 本段 Pass；不代签 DACL 操作性拒绝 |

两次都是原有效 SafeHandle；NTSTATUS `0x00000000`、ReturnLength=56，读取 ObjectBasicInformation offset4。没有复制或更换查询句柄。GetNamedPipeInfo 成功、flags位0为0，确认客户端endpoint；最大实例数1仅如实记录，不作 DACL 拒绝证明。两条原 CreateServer 句柄实际 DACL 结构均 protected、唯一原 logon ACE、精确0x12019b，这仍只是结构观测。

第二段实际双方 PID 都是当前进程1728、session2，四项均执行并拒绝：ServerElevatedRejected、ClientElevatedRejected、ServerWrongImagePlusElevatedRejected、ClientWrongImagePlusElevatedRejected。每项独立保留错误，前段安全红门没有遮住第二段。identity来自真实本进程OS读取，门前实测断言Elevated=true；**此次未查询实际 High RID**。同进程负例不代签跨进程正向认证；wrong-image+Elevated组合不独立证明image-path门。

两段原连接wait均RanToCompletion并join；原client DisposeAsync、原SafeHandle.Dispose、原server DisposeAsync各自成功，原两个句柄关闭，CleanupConfirmed=true，CleanupErrors=[]。首段唯一安全红错保留在正文与总Errors；第二段BodyErrors=[]。没有任何EXIT_READY、清除生产owner或假成功。

## 范围与来源

新 GrowthHarness `--ci-pipe-access` 仅调用原生产 ReadToken／CreateServer／peer构造器与新只读测量。原请求含bit4得到有效客户端句柄的历史事实保留；此次补充了确切实授值。用独立随机名字分隔两条原策略管道，原等待和所有清理确认后才开始第二段。client dispose抛错不能遮住原SafeHandle清理，任一dispose失败即使句柄closed也不能标结算成功。

系统 ntdll 动态加载限定System32；固定56B缓冲只读公开 GrantedAccess，不记录SID、句柄、内核保留字段。查询失败应为未确认，不能作为拒绝通过。此次两次查询均成功，不触发该未知分支。

只运行一次public标准windows-2022、push／attempt1、contents:read、固定checkout depth8。复用原批准SDK402 ZIP／许可／hash／安全解包、双runtime正常在线验签、runtime和harness locked还原／审计；只构建harness。**本轮App UI、Worker、图片、KnownFolder、OCR／模型均NotRun**，没有重复三条已过图片链，不借记其通过数。

没有修改生产ACL、UAC、桌面／网络安全设置或凭据，没有造／借／降token，没有新增SDK协议、依赖许可或费用，没有操作用户电脑。PackageHold、生产复用Off。额外服务器实例测试未实现；先前第二CreateServer带FIRST_PIPE_INSTANCE且max1返回5，不能证明DACL拒绝。

## 原件

20件共77,399原字节经独立chunk／base64／gzip／解压SHA核对，raw日志111,711B，SHA256 `425041b01021f802b8d16f0dc2c038932104045f6bccc3c2b726c47438cb7a2f`。369项实际Windows checkout输入匹配源：4原字节＋365正常CRLF，0差异。原始源码／日志ZIP245,205B、36成员，SHA256 `5b30de66edd8363cd3d5bd37557831581e127e01561ec246d588be074eecd23e`。

完整证据包包括该不可变原件ZIP、20件原件、实际命令／源码／产物摘要、本地必要构建、3项既有CI检查及独审记录：337,156B、43成员，SHA256 `c33aec83756a54a37c1480f8302e08002ffde63158ad56568fa3f1c299aa0c48`。已确认Library ID **`libfile_98055d2e0c7881919817c99b05a88e31`**，version0。见 [机器可读索引](../test-evidence/2026-10-21-warm-pipe-access-windows.json)。旧三图片链与红件保持在原Library `libfile_c32e11ee722c8191b4303512b346a51e`。

## 仍未验收

客户端bit4实授的服务器权限影响待操作性设计独审，必须排除FIRST_PIPE_INSTANCE与实例数量干扰；当前没有实施该额外测试，也没有宣告无风险。跨身份DACL拒绝、独立image-path门、正向warm／第二CLI／Explorer仍未验收；正向warm缺既有medium入口仍Blocked。原Windows安装／OLE／OCR／250/600ms／真人画质边界保留。

官方依据：[管道安全与访问权](https://learn.microsoft.com/en-us/windows/win32/ipc/named-pipe-security-and-access-rights)的额外实例检查针对服务器CreateNamedPipe；[NtQueryObject](https://learn.microsoft.com/en-us/windows/win32/api/winternl/nf-winternl-ntqueryobject)提供公开GrantedAccess结构；[GetNamedPipeInfo](https://learn.microsoft.com/en-us/windows/win32/api/namedpipeapi/nf-namedpipeapi-getnamedpipeinfo)提供endpoint标志。客户端实授事实与服务器实例操作性结论分别保留。

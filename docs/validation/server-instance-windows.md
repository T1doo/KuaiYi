# 额外服务器实例操作性复核

2026-10-21：**Pass_LimitedTwoDeniedAttempts，独审限定 ACK**。两个普通启动继承当前 token 的独立子进程，在本次指定 fixture 条件下都未取得额外 server 句柄；原始错误均为 Win32 5。此结论不回填旧客户端 bit4 实授红门，不宣告无风险或跨身份安全。

实际执行源 `ef51719275c5dd6ddef335fe2aa31a88466bff5e`，唯一父 `c49c308f52551f84fa9683523aca2f415189ab50`；固定生产源 `92e3548845bd07ee7717b53df9588c412d128f9d`、测试、锁与打包均未改。

## 实际操作与结果

[run39572086629/job119128509028](https://github.com/T1doo/KuaiYi/actions/runs/39572086629/job/119128509028) 实际 **success**，wrapper/probe均为上述限定Pass，actual-server-instance exit0。18条记录命令均exit0；没有循环重跑、超时或全套产品测试借记。

| 条件 | 实际 child | 第二CreateNamedPipe | 完整原任务／句柄清理 |
|---|---|---|---|
| 无client | 7416，普通Process.Start | 无效server句柄，Win32 **5** | exit0，所有原IO/exit/dispose/handle确认 |
| 调用child自己持有bit4请求client | 7764，普通Process.Start；原server侧实际clientPID=7764 | 无效server句柄，Win32 **5** | exit0，原client跨native调用持有且随后关闭；全部结算 |

两次第二调用都是原 `CreateNamedPipeW`，openMode `0x40000003`，只去 FIRST_PIPE_INSTANCE。相同随机fixture的首server全程持有原SafeHandle，首openMode `0x40080003`；两者mode8、maxInstances=2、buffers65536/65536、timeout0一致。仅测试fixture的max从生产1改2，不改生产代码。首server endpoint/max核验、before/GO/after原句柄持有检查均通过，未重建或重占名字。

实际第一phase在GO前无连接；第二phase由同一个调用第二server的child自己CreateFile请求 `0x0012019f`，原client为有效Client endpoint/flags8，持有跨原生创建调用。父先join原connection并从原server查实际clientPID=7764，再发单字节GO。**没有重测本次客户端GrantedAccess**；旧9d90a1f的bit4实授事实保留。此次两个server返回的都是无效句柄，因此没有可测server endpoint/GrantedAccess，不能伪填0作为实授值。若取得任何成功serverhandle，代码会测原handle endpoint/GrantedAccess并无条件保留红；其他错误，包括满额／参数不一致，都不支持拒绝结论。

## 身份与结算

父从原 Process.SafeHandle 只读查询生产ReadToken和TOKEN_QUERY owner，child在Ready及原生调用前自读核对。user/logon/owner/auth摘要、session2、Elevated=true、AppContainer=false、UiAccess=false均匹配；pipe owner与原TokenOwner、protected单logon ACE精确0x12019b均匹配。摘要只用于测试关联，不是生产授权；未完整审计所有token属性或原始High RID（ProductionIntegrity=-1为原生产的非Medium映射）。没有造／借／降token、凭据或runas。

两个child的原Ready/stdout尾部/stderr/GO write/flush/WaitForExit Task均RanToCompletion并join；原stdin关闭、stdout/stderr各自Dispose、原Process Dispose全部成功。child的原GO read及原server SafeHandle Dispose成功，第二child的原client SafeHandle也关闭。父原connection RanToCompletion、首server原Dispose成功且原SafeHandle关闭。两phase与总State CleanupConfirmed=true；Errors、BodyErrors、CleanupErrors均为空，未触发kill或保留失败fence。

失败路径仍保留：取消不等文本writer锁；原exit faulted／未确认不能用HasExited或新wait冒充join，而是保存Fail_CleanupUnconfirmed、持有原Process和首server，外围90秒只能失败。所有正文与原IO/dispose异常分别保全；GO原读观察超时后继续原Task fence。此路径本轮未触发，不把静态合同当故障注入验证。

## 证据与边界

只用现有public标准windows-2022／push／attempt1、固定checkout depth10、contents:read、15/14/13分钟预算。原批准SDK402 ZIP／许可／hash／安全解包、双runtime正常在线验签、runtime/harness locked还原和审计、harness必要构建均执行通过。本轮UI、Worker、图片、OCR／模型均NotRun；未重复客户端实授测量或三条图片链。没有修改现有对象ACL、UAC、系统／网络安全设置或用户电脑，没有新增许可、依赖、凭据或费用。

20件81,135原字节的chunk/base64/gzip/解压SHA经独立重组核验；raw job118,937B SHA256 `f2a4656ca43a254597143499f15604dc8d0d4a8146c1c87008de53a3c0c628a8`。370项实际Windows输入全部绑定源：4原字节＋366正常CRLF、0差异，执行前后摘要一致。不可变原件源码ZIP255,614B、36成员，SHA256 `ae1f641934fc5f09a10b747f93688ef775b036d91769b66ed05bf55d44b8be6b`。

完整原件包含上述ZIP、20件原件、真实命令／源码／产物摘要、必要交叉构建、三项既有CI检查、CS0136初始编译失败及独审记录：350,443B、44成员，SHA256 `632c0290faead78db853e0869f95e03de142df7ba4b508740d9a4e583abadff6`。Library已确认 **`libfile_6b161863e82481919114e39599f68fd9`**，version0。详见 [机器可读索引](../test-evidence/2026-10-21-server-instance-windows.json)。旧客户端bit4实授安全红门原件仍为Library `libfile_98055d2e0c7881919817c99b05a88e31`，不改其failure记录。

本次仅支持**这两次原token、精确fixture条件下未取得额外server能力**，不证明跨身份DACL安全、不完成生产安全总评或P7。正向medium warm因没有既有入口仍Blocked；独立App/第二CLI/Explorer/安装/OLE/OCR/250/600ms/真人画质边界保留。PackageHold、生产复用Off。

官方语义：[Named Pipe Security and Access Rights](https://learn.microsoft.com/en-us/windows/win32/ipc/named-pipe-security-and-access-rights)明确额外服务器实例的FILE_CREATE_PIPE_INSTANCE检查。此次去FIRST并保留max2空余，直接记录操作结果；没有把早先带FIRST/max1的第二bind 5当成DACL操作性证明。

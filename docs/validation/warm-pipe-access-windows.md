# 原客户端 GrantedAccess 与独立 peer 复核

准备中，Windows 尚未执行。唯一父 `a7f6aa57ca11c707203dfd5d8062bd88d3acd508`，精确六文件变更；固定 `92e3548845bd07ee7717b53df9588c412d128f9d` 的生产、测试、锁与打包均不改。历史 bit4 有效客户端句柄与两轮红件保留，三条已过图片链不重复。

新 GrowthHarness 显式 `--ci-pipe-access` 仅用本进程真实 Elevated token与原 CreateServer 的 logon DACL／first-instance。两条独立随机fixture：第一条请求原 rights|4，在持有的原有效客户端 SafeHandle 上动态查询系统 ntdll NtQueryObject(ObjectBasicInformation)；56B固定缓冲，只读offset4 GrantedAccess，NTSTATUS成功且返回精确长度才确认。另用 GetNamedPipeInfo 确认实际客户端endpoint，记录请求／实授／状态码／长度，不记录SID、原始句柄或内核保留字段。失败记未确认，不当拒绝通过；实授bit4保留安全红门，不能解释为无风险。

原客户端／原连接等待／原服务器分别join和释放，全部成功才进第二fixture；原实验的安全红门或查询失败不遮住独立测试。第二fixture用精确0x12019b打开并实测授予mask，核实际双方PID/session，再独立尝试双方真实Elevated peer构造器及wrong-image+Elevated组合负例；任一负例失败不能遮住另一方。组合负例不独立证明image-path门。没有正向medium peer、模拟身份、原生token创建／借用／降权、额外服务器实例、生产入口或ACL策略变化。

正文与每个取消／原wait／原dispose故障分别保留，不凭IsClosed认定抛错dispose成功。无App UI／Worker／KnownFolder／图片／OCR／模型执行。额外服务器实例权限禁止仍待另审操作性测试排除FIRST_PIPE_INSTANCE和max1干扰，当前不以第二bind Win32 5证明DACL操作性拒绝。

仅一次public windows-2022 push/attempt1，contents:read，15min job／14min step／13min总预算；固定checkout depth8供精确祖先门。复用原获准SDK402下载／许可／hash／安全解包、双runtime正常在线验签、runtime与harness locked还原／审计，只构建harness、不构建部署Worker。不新增依赖／许可／费用／凭据／安全权限，不操作用户电脑。

依据：[Named Pipe Security and Access Rights](https://learn.microsoft.com/en-us/windows/win32/ipc/named-pipe-security-and-access-rights) 的额外实例检查针对服务器CreateNamedPipe；[NtQueryObject](https://learn.microsoft.com/en-us/windows/win32/api/winternl/nf-winternl-ntqueryobject) 的PUBLIC_OBJECT_BASIC_INFORMATION提供原handle的GrantedAccess。本片只测并保全客户端实授，不能代签服务器实例权限或跨身份DACL拒绝、正向warm／Explorer／P7。

独审修正：原client DisposeAsync与原SafeHandle.Dispose分开尝试、保留各自异常；前者失败即使后者关闭也不能标清理成功。身份准确限定为原ReadToken已实测的Elevated布尔值，不借之前runner的High RID观测代签本次RID。

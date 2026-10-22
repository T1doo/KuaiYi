# 安装入口适配合同

接续 eaf6ed1，仅复用单一 InstallationOperation，不新增恢复算法。依据 SPEC13.3/13.4；所有实际执行测试仍是隔离根、fake registry 与合成受控子进程。

## 信任与入口

独立 KuaiYi.Installer 的精确 `--install` 命令，其他参数拒绝；不改 App 普通入口。Installer 产物不属于其所 pin 的 generation/bootstrap，采用先封存 payload，再单独构建带 pins 的 installer 两阶段过程，避免 App.dll/catalog/pin 摘要循环。包路径固定为入口目录下 `installer-payload/generation` 和 `installer-payload/bootstrap`，安装根固定为真实 LocalApplicationData 下 `KuaiYi.Application`，数据目录仍为 `KuaiYi`，无用户提供路径/hash/重试/恢复开关。

两个 source catalog SHA 必须来自 Installer 内嵌、随构建审定的精确 JSON 资源；不从包、外部相邻配置、CLI、环境变量或注册表取得 pin。当前没有完整 Windows 包，资源为明确 unavailable；生产命令在任何根创建/registry 读取/controller 工厂之前拒绝。合成测试仅注入内存编译调用的 pin 文档与实际 fixture 源摘要，不把它当发行 pin。配置后的 pins 仅表示包完整性信任，不能证明原生/运行时、签名或 Windows 安装已验。

## 首次根与单一 scope

Operation 可由入口显式请求首次初始化，旧调用默认 existing-only 不变。原 Scope 在任务执行前持有初始化 owner/IO ledger。仅当精确目标根不存在且父目录已存在时，使用原子排他 native mkdir（Windows CreateDirectoryW/Linux mkdir测试适配，无ACL参数修改）创建随机 sibling staging，写入固定 owner、三个空锁及绑定原 operation 的 initialization-pending；Flush/原读/Close 全由该 owner 记录。全部结算后原子 Move 到目标；任何 partial/foreign/Move 或原关闭未知保留，不清理、不接入现有入口。

普通 role/Host/setup 入场拒绝 initialization-pending。只有同原 Scope 的不可序列化初始化 owner 可在已确认 Move 后借用 setup 入场，且随后保持原 setup/host/upgrade admission，先完整持久写本 Scope Ticket，再归档本次初始化 marker；归档未知或关闭未知必须保留原 Ticket，不归档为 Refused/Abort。不可按目录 owner 文本或 marker 内容重建初始化资格。已存在根只用原 existing-only 门，不初始化空目录、不迁移缺锁旧根；普通 Store 原低层测试 API 不获得 bypass。

## 入口结果和后台生命周期

入口 owner 先发布原 Task，整个过程仅构造/Start 一个原 Operation，并持有 registry、CTS、stage result 和 original lifetime。前向取消送同一 token；不增加回滚机制。按原七种结果分别显示，READY 只表示本次阶段健康。入口在结果发布后仍 join Operation.OriginalWork，原子生命周期尚未结算不得返回成功/Dispose CTS；unknown 静态保留原对象，不重新认领/kill。Operation 原阶段任务结束后仍显式 join 封闭 producer 的原角色协议/startup/observer/stop/exitWait，持有 child 未退出时仅被动原 OS 观察，不调用 Stop/Finalize/恢复；无法证明退出则入口原Task永久保留，不以新timer作为退出证明。阶段消息仅固定状态，无 source path/hash/原文/异常信息。消息输出异常不得阻断原操作结算，也不得将已接受 READY 改写为失败部署；原输出错误单独保全，最终返回非零。

Ctrl+C 在 Start 前接 handler 并抑制默认终止，预发布/缓存同一 CancelAsync 原Task；封闭请求后仍保持 handler 抑制第二次 Ctrl+C，join 原取消任务后才解除订阅，取消错误独立保全，未知 CTS 不 Dispose。

实际 installer factory 每次新建 InstallationApplicationClient/原 controller，安装器持有的健康会话不启用 warm capability：原 peer 要求控制进程位于选中 generation，本分发入口不满足，绝不放松身份门/替换期望路径；这一会话不签 warm 功能可用。不借稳定 Launcher 冒充部署授权，generation-controller 私有 authority 转交不在本片范围。退出码只对 NewVersionReady 且原生命周期 clean 返回0；PreviousVersionReady、已恢复、NotCommitted、CommittedNotReady、Unconfirmed 互不冒称新版安装成功。不能在 READY 时退出留无人拥有的原 UI/session。

## 最小验证与边界

验证 unavailable/畸形/重复/额外 pins 零根和零 registry；参数拒绝；首次根真实创建→原 Scope Ticket→实际 G/B prepare/commit→合成 READY/退出；现有根升级；已有空/foreign 根、初始化写/close 双错、Move before/after effect、Ticket 失败和初始化归档未知保持阻断；原 IO held 时零资格；阶段输出失败仍 join；READY 不等于退出。少量原 Operation 代表对照，不重复266或204/完整Core。

未接 Inno/MSIX/Shell 注册、自启动/卸载；三原生 DLL、完整 self-contained runtime、可信发行 pins 仍缺。没有本轮许可执行真实用户注册/安装。真实 Windows/medium/Explorer/WPF、250/600ms/画质与完整 P7 不签收。

原子排他创建依据：[CreateDirectoryW](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createdirectoryw) 对既存目标返回 ERROR_ALREADY_EXISTS，且只创建最终目录；传空 SECURITY_ATTRIBUTES 保默认继承，不改系统安全配置。Linux mkdir 仅隔离测试适配。

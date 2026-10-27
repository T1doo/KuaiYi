# 启动前收证与原 owner 初始化修复（本地候选，未触发）

基线445a5d6415e52bb4ced8331540d8bc0a0bec32bb原样保留在提交祖先。原执行远端0a314a6eaa16ea580326a29ab7fc30032f12cada、准备远端f2b7c57acfedf21cd8d684e7b075e345c8da5597均未改。本轮原环境初核可用空间18,936,504,320B，无开发/测试或CI在途；没有删除旧数据或恢复旧线程。

旧run40338532720实际Fail、supervisor步骤两秒失败、artifact0的事实不变。只读取本地已保全元数据及源码，未重试Forbidden日志，未换API/浏览器/端点/凭据。不存在可读取的Windows runner工作树残留；当前Linux工作区与该runner不共享路径。具体原异常、runner默认编码与原事件内容仍Unknown，不把445编码修复或其5P当旧CI根因。

## 确定的独立错误与修复

原JobOwner已取得并assign后，supervise的mkdir、时钟和首save在try/finally之外：目录冲突、ENOSPC或时钟抛错会直接越过原handle结算/UNKNOWN持有。现在将其纳入原finally；未取得原工作根时禁止写入该根，原owner仅按原membership结算，未确认继续持有，不创建替代owner、不杀PID、不以runner orphan日志证明成功。

新增4个相关反例：既有根不覆盖且结算、首次写证失败但结算、首次写证失败且UNKNOWN持有、初始化时钟失败仍结算。原19例红为15P4E，修后19P；UTF8原5例回归5P。都是本地FakeAPI，不执行Windows进程树/原生动作。原红绿日志及原件SHA保全，未跑C#/全Core。该源码错误确定存在，但仍不证明它是旧403运行实际异常。

## 已准备的独立诊断

新diagnose_visual_prejob.py无参数默认拒绝；未来唯一显式命令是 python -I -B ./scripts/diagnose_visual_prejob.py --diagnose-prejob。它只做运行时版本/默认编码摘要、两份固定SHA源码的解析与非main模块导入、严格UTF8事件解码和原context门，并硬停在Job创建前。原产品、Popup、Full/Reduce、原epoch几何+fade、250/600门不改，不运行正常supervisor主入口。

独立exclusive根artifacts/ui-polish-bootstrap只写prejob.json，不能提前创建原supervisor必须exclusive的ui-polish-windows根。源码每份≤256KiB并核固定SHA，事件输入≤4MiB、输出单JSON≤4096字节。仅保留stage、异常类、Python版本/encoding、源码字节hash及格式校验后的DeclaredSourceSha；明确GitSourceCommitVerified=false，不把环境SHA当已验证Git来源。不输出事件正文、环境、异常message、用户名/绝对路径或签名URL。

默认编码对照只记录Accepted/UnicodeError，不用于放宽当前严格UTF8输入。模块源必须匹配固定hash后才导入，模块名不是__main__；不创建Job/API owner、不启动子命令、WPF、托盘、模型、采集、显式产品registry调用或SDK下载；这不声明Windows或解释器内部绝无注册表读取。10项离线测试覆盖原门、source/input/output界限、内容脱敏、ENOSPC/关闭失败、默认关闭，正例用WinDLL与subprocess禁止调用哨兵；原9P草稿保留，最终10P不累加。

原PowerShell parent-exit还依赖原work目录存在。未来候选改用另一exclusive根ui-polish-bootstrap-parent，在Python前先写parent.json；原前台返回后记exit，仅记异常类型，写证失败为Fail，不覆盖原失败；不依赖原native根。父收据用CreateNew取得并持有原FileStream，最后写入与原句柄关闭独立结算，写错仍一次close，任一未确认都不发布父收据；失败诊断只将本轮stdout的白名单stage/异常类放入父收据，不复制原始stdout或旧子文件。仅本轮取得且关闭确认、regular/nonreparse、≤4KiB的父收据可列入上传；子收据还必须本轮exit0及固定schema核符。原根已存在时不纳入该路径，不以always固定路径上传旧文件。父收据只证明当前Python前台是否返回，不证明原Job/机器树清理。超时未得到原返回证据仍Unknown，不按PID补杀或伪造已清理。

## 待批准的精确范围

已在本地原注册路径windows-ui-polish.yml准备**只诊断**工作流；固定原仓库、r03分支、自身path、公开push/attempt1，contents:read、checkout及upload action仍原固定SHA。作业≤3分钟；诊断步骤按GitHub分钟粒度≤1分钟。上传最多两份本轮owned JSON（分两个artifact）、每份≤4KiB、有效期1天，不上传源码/DLL/截图/原始日志。父pwsh与Python各自返回收证；标准runner退出不能称全机自主清理。

这是新免费Windows启动前诊断，与旧15分钟/6分钟WPF窗口不同：没有产品build、窗口/输入/托盘、HKLM EnableLUA读取、Job构造或四DLL测试。没有许可通过泛指批准，也不是重新获取受拒旧日志。即便新诊断通过，只能收窄当前启动前路径，不能回填旧异常、实际WPF或清理验收。

只做本地准备与独立静态审查，不push任何分支、不触CI。当前无pwsh，父模板只有静态/YAML核对，不能声称PowerShell原生通过。原精确native PARENT/FILES门未放宽，本提交不能直接作为产品视觉运行候选。若批准新诊断，应先重新核原run及无在途、完整候选SHA与source hash、正常单次推同执行分支；若仍失败或无run不自动重推/dispatch/重试。此次新3分钟/1分钟作业尚待具体批准。

最后离线核查发现新诊断的两份固定源码仍text=auto，使用Git命令级core.autocrlf=true过滤原739提交时，两份字节hash均改变。仅在.gitattributes为这两个精确脚本补-text；同过滤后两份均与原blob完全相同，不归一化后冒充原字节，不改全局或系统Git设置。红绿JSON原件保全。此为新诊断准备的确定检出兼容缺口，不称旧403失败原因。

## 一次实际执行结果（仅启动前诊断）

用户明确批准后，2026-10-27 03:55:12.542975–03:55:14.726397UTC仅一次正常push固定49c0bba0cc9ca172c01e59fc73dfacbba817d17d至原r03执行分支。唯一[run40446841918](https://github.com/T1doo/KuaiYi/actions/runs/40446841918)/job122000026629，push/attempt1、原注册workflow395782622，实际GitHub终态success。job03:55:20–03:55:44共24秒、诊断步骤03:55:37–39约2秒，未超过3分钟/1分钟；watch原命令exit0且已结束，没有自动重试或转原视觉模式。

GitHub直接记录父/子上传步骤成功、artifact2个。父ID12707874755、元数据400B/digest sha256:8f133d000f28d09350af266b0cc1b65eb19aeefa17ba8ca2f0b48eda4fbace11；子ID12707979525、元数据623B/digest sha256:0854a2d0d1e50a45db24933b7982e9facfa4b19596e29f163b610fe12d3d521a。**这些仅为服务端发布值，不是本地下载核包。**正常读取本次子zip经对象存储重定向返回Forbidden后立即停止；父未下载，不换工具/端点/凭据，不读旧403日志。原错误仅本地保留，含短期URL不进仓库。两份收据GitHub有效期至2026-10-28 03:55UTC附近，实际元数据精确时间见机器记录。

按已审源码和成功步骤可推论：当前来源字节检查、严格UTF8和原context门完成，并硬停Job前；原Python前台正常返回、仅当前owned且有界收据取得上传资格。这是控制流推论，**并未独读JSON**，实际Python版本/编码值与JSON字段不能回填；GitSourceCommitVerified并非该收据自证。元数据及其字节hash已独立只读核对，独审没有取包或绕拒绝。

此次成功不证明原403异常原因、原Job API/树结算、WPF像素/焦点/动效或完整机器清理。产品、模型与普通启动生产门没有开放。当前剩余可读证据缺口是本次两份JSON原文；应通过正常获权提供，不新增Windows作业、改输出路线或重试已受拒下载来追同份内容。

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

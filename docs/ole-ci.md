# P0-03/E06 hosted OLE public contracts

## P0-03/E06 有界恢复与真实 OLE CI 准备（尚未运行）

2026-10-12容量失败后按父最新指令只作一次有界接续。恢复核对HEAD/远端dev均9764c1d085b7ce4adf9bae401c9c004312a39afd，main仍51d077481cf6303602fcf5d2718213da9cf05178；工作区无未提交/未推送差异，无活动构建/测试/推送，仅root写入、独审只读。没有重复启动原命令或更改模型设置。

现有E06物理路线未通过：OlePresentationWindow的RawMouse拒绝injected，真实ShellWindow只有WPF pointer路径；前六轮CI不包含实际OLE业务。新增仅显式CI构建属性编入的App公开实验入口，普通App不含此命令或测试partial。用真正App.exe/ShellWindow和ProductionTextDrop、原父身份校验/原registrar/NativeBridge/receiver；temp无密钥配置，仅Drop接线，selection/剪贴板/热键/Provider不接。公开故障源沿原PreviewDown→DoDragDrop→真实同步GetData路径Sleep5000ms，live stdout元数据在Sleep前flush，不写原文。SendInput只指向自己持有的公开源和浮钮，前台/窗口/按键/capture不符标Blocked，不绕过RawMouse injected守卫。

两场景：GetData进入后且1500ms原截止前观察主UI心跳与真实合成WPF拖动；自身移动可能取消lease，明确分开。执行生产取消后必须持有原receiver OS handle实际HasExited、原Run/Control结算、TryFinalize/registrar撤销全确认，才显式恢复新实例；第二次实际GetData阻塞时正常Close并核相同退出/注册所有权。仅held Process可终止，源进程关闭8s/自有强杀确认3s；App内部60s/外部75s+5s确认，不延长原OLE截止。源和App均为既有locked项目普通build，不publish/不zip/不安装/不上传artifact。

当前仅源码准备，独立精确SHA审核后才执行本地/Windows测试；没有新的COM/OLE/输入通过声明。实体鼠标/源捕获/混合DPI/普通Win11与整体E06仍NotRun/Blocked，P0门槛不变。

源码自查补正（未测试、未push）：f616880准备提交发现Invoke-Checked会向success stream输出构建文本，helper改将它们Write-Host，保证返回唯一typed receipt；CI peer改捕获原peer最新Control task而非Ready时快照，恢复必须原实际控制链结算。先前准备commit保留，不把静态发现标为真实测试失败或Windows通过。

独审6e6a0a6提出证据阻断，尚未测试/推送：补Source原QPC timestamp/frequency和GetDataBlockReturned状态，响应/Close前同时复核原DropBegin接收起点、源阻塞起点<1500ms、原receiver持有句柄未退出及Run未结束；新增Ctrl/Alt/Shift/Win/Escape空闲检测并在新按下前复核。CI记录所有receiver Created/Ready顺序及每个旧原Run/最新Control、held OS process HasExited、Client.ObserveExit和registrar撤销，任何提前新建禁止隔离Pass。合成pointer与隔离生命周期分别报告，IsolationPass绝不推导E06物理或pointerPass。

最新恢复命令在当前已有HEAD6e6a0a6/worktree clean/无活动构建测试下执行；git远端命令失败为Failed to connect to proxy port8080，并非再次模型容量报错。既有GitHub连接器只读GET refs成功，远端dev仍9764c1d、main仍51d0774；未扩权、未改网络/账户配置，也未启动或重跑Windows CI。

按父最新“不重复之前成功Windows结果”，工作流本轮显式-OleOnly：只为新的临时host构建现有OleNativeBridge（前次host已回收且无artifact上传，不能假称重用其不可取得DLL），不重跑四组件/两CTest/Growth210帧旧合同。原default模式保持原四组件/HWND逻辑，OleOnly才运行新E06公开探针；新report Scope=HostedOleFixtureContractsOnly，CTests/Overlay检查为0/NotRun，单独打印隔离/合成输入/E06PhysicalNotRun。当前代码尚未测试；先对精确源码与模式做独审。

独审58f5fbc静态发现fresh checkout的App assets缺口：OleOnly跳过GrowthHarness restore，补现有App项目--locked-mode restore，仍不安装SDK/新版本/来源软件。未启动CI，不把该静态发现记成实际NETSDK1004失败。job summary补分列OLE隔离/合成pointer/E06PhysicalNotRun和旧合同未重跑说明。

本地执行证据（非Windows）：9ec5356通过独审后，CI App默认并行build退出1（汇总0warn/0error、未完成编译）；既有OLE tests在MSBuild本地NamedPipe socket bind被当前沙箱拒绝（MSB1025/SocketException13），随后no-build单进程VSTest明确SocketServer/TcpListener Permission denied、Test Run Aborted，0项测试执行，不记Pass/Fail断言数。未扩权；改执行参数-m:1/-nr:false/UseSharedCompilation=false后，CI App8工程、FaultSource6、Receiver6编译各0warning/error；45既有PS合同通过、OLE helper Parse通过、真实Linux-OleOnly入口exit2/全部Windows计数0/Blocked，普通App MSBuild items明确无CI文件/define。所有失败log保留，未削弱原测试。关闭动作再在实际UI调用点复核live原GetData/Drop，排除排队后才关闭的时序误认；此一行待精确增量审后重新编译App。Windows E06仍未启动，实体鼠标NotRun/P0Blocked保持。

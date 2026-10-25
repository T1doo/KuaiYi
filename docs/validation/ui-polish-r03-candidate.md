# 第三视觉窗口候选：仅离线准备

本候选直接基于 e12c1ed8ff351f8300dbdaca18585d7994493317，产品 before 固定 e4d57e77426ab235c7219b375debd76bc072e00b，after 固定 e12c1ed8ff351f8300dbdaca18585d7994493317。当前 harness 通过原 KuaiYiVisualProductRoot 对照编译上述原 Git blob；不把本次探针变动混入产品源。严格 NUL 树、逐原 blob/hash、锁定依赖和来源/大小断言保持。

11 文件仅新独立 workflow、两个 Python 实现、两个 Python 测试、三个 harness 源码及三文本。普通准备分支 dev/ui-polish-visual-r03-preparation 不匹配任何 Windows 入口。新入口仅 push dev/ui-polish-visual-r03 且修改自身 workflow、公开原仓库、attempt1，同时 Python/C# 再核 execution ref；没有 dispatch 或自动重试。当前不创建或推送 execution 分支。

设置页每侧浅/深各8状态共16张：宽页、窄页输入焦点、按钮焦点、输入禁用、原 motion ComboBox 的 Popup、Escape关闭、方向键/Enter选择后恢复、Combo禁用。Popup 用实际 PART_Popup.Child 的独立 HWND、原 DIP/实际 DPI 和原图像字节，不能用设置内容截图代替。程序 routed F4/Escape/Down或Up/Enter 仅交给原 Combo；校验 IsOpen、原 SelectedIndex、SelectionChanged、焦点及恢复原草稿/IsEnabled，不保存设置、不更改系统 motion/theme/DPI。这不是物理键盘输入验收。浮窗每侧原18张，Full仍必须同epoch真实中间几何和真实fade样本，Reduce保持原门。总计划68张，不是已取得68张。

自有 supervisor 在任何子命令前创建并绑定原 unnamed Windows Job，默认不允许 breakaway；关闭共享编译/节点复用/server。原 prepare/run Popen 先 await 原进程，再从原 Job 句柄查询成员；prepare后仍有孙辈就不启动visual。只有成员完整且仅supervisor自己可确认后代退出；成员容量512、数量不符、权限失败或末次 Query/CloseHandle失败都 UNKNOWN。原关闭只尝试一次，不采用 PID、不杀未知进程、不接管 runner Job。UNKNOWN 保持原 owner/句柄至外层预算，不以退出或runner orphan日志冒充自主清理。

收证只覆盖自有 supervisor＋继承的验证后代，**不覆盖** checkout、upload、父 pwsh 或 GitHub runner。父 pwsh 原前台命令返回另写 parent-exit.json，不能由 supervisor 自证自身已退出。窗口/tray/Flow/请求任务仍由原 owner分项结算。作业15分钟；同一supervisor步骤14分钟，prepare最多8分钟、visual共6分钟（不是每侧6分钟），原子进程内部45秒/外部60秒门保留。没有KillOnJobClose、自动重试或额外native DLL窗口。HKLM EnableLUA 原只读仍存在；无注册表写入、安装、抓取、模型、用户电脑或安全设置修改。

离线实际：Python原合同24例基础上新增4拒绝反例，28P；Job fake 15P，含末次查询失败、关闭单尝试、证据落盘/诊断管道失败仍持有、prepare原wait退出后孙辈仍存活、超时原wait持有。fake只校验状态，不运行Windows API/真实树探针。Harness及Provider交叉构建0W0E，相关C#观察11＋palette4共15P；888编译输入/130 DLL前后相同。C#构建在最终Python边界修正前完成，三个本次C#源仍逐hash同实际编译字节；Python按最终字节运行。旧10P初稿日志保留，不累加为25。原件仅本云工作区保留，不提交TRX/DLL/二进制归档。

目前真实 Popup键路由/深色可读性/中间帧/Windows Job ABI/自主整树清理均 NotRun，必须第三窗口具体批准后才执行。第二次 [run40217224688](https://github.com/T1doo/KuaiYi/actions/runs/40217224688) 仍Fail，只取得before设置8张图，after没有运行；不回填或降低标准。当前没有其他独立产品修复证据足以继续改生产UI，等待固定候选原生验证。

原生契约参考：[Job继承](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects)、[进程列表](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-jobobject_basic_process_id_list)。最终候选须独立静态审查后仅正常推准备分支；新窗口许可尚未取得。

# 第二次云端视觉窗口：仅准备候选

状态：Prepared / Pending explicit second-window approval。本轮仅来源与执行入口绑定，不推送、不创建CI、不启动Windows/native/WPF窗口，不重复已完成24项测试。一般鼓励“继续”不当作新窗口授权。第一次run40177638553/attempt1及18原件完整保留，失败结论不改。

## 精确候选与入口

准备基线为11f13f5ebc4fcb812d98586c5f594ca3a2633990，已审NUL固定树路径与原始blob修复。候选仅一个直接子提交、5个文件（workflow、Python绑定、C#诊断门、progress、本文件），不得附带产品源码/测试/pins/依赖变化。Python源门核父提交、完整变动集合、事件SHA=HEAD、clean；保持before=e4d57e77426ab235c7219b375debd76bc072e00b / after=419082f9863abf109a3d9ee7274dde59697bb85a。候选的完整SHA在本轮最终回报给出，不做自引用文件。

本地准备分支dev/ui-polish-visual-r02-preparation，不推送。拟议未来执行目标是dev/ui-polish-visual-r02，workflow push分支门、job if、Python hosted_context及C#诊断门四处一致；旧dev/ui-polish-visual不再匹配这个新候选。工作流无workflow_dispatch、schedule或rerun动作，job仅public T1doo/KuaiYi、push、首次run_attempt=1。只有未来明确批准第二窗口并再次核唯一writer/无重复run后，才可把这个精确候选单次推至目标分支；本轮不执行。

## 保持的预算与副作用合同

标准windows-2022，contents:read、pinned checkout不保留凭据；job≤15分钟、prepare≤8分钟、visual≤6分钟；四个顺序原进程各45秒内层、60秒原owner观察，且共享视觉预算。任何失败/UNKNOWN停止后续，保留原Popen/wait/双drain及状态/失败文件，不自动重跑、不把超时当清理成功。

仅公开合成WPF内容、原测试窗口/托盘/程序焦点；原DesktopOnly preflight会只读HKLM EnableLUA。没有产品安装、系统DPI/theme/motion变更、真实采集、模型调用、注册表写入或用户电脑操作。原窗口/原tray disposed且added=false与任务结算核验保持，全部失败/UNKNOWN原日志和PNG仍由always artifact直接保全，排除源码、SDK和构建二进制。

官方SDK10.0.402/runtime10.0.13的原签名/hash、locked restore/audit以及Git原始blob hash、路径/数量/大小门保持。真实源码导出/Windows构建/WPF像素与实际清理仍NotRun；本轮静态检查及独审不能替代它们。

## 本轮定向静态核查

核查三层分支门和workflow push入口一致、直接parent与5文件集合精确、before/after/SHA与原对象读取不变、预算/permissions/actions/原owner与证据目录未放宽。只做Python AST及声明/差异静态检查，不运行旧24用例、编译、渲染或CI。原历史24P仍仅历史结果。

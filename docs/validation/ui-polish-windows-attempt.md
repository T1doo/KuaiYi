# 一次云端 WPF 视觉验证

用户已明确批准：标准免费 windows-2022，单 job≤15分钟，视觉阶段≤6分钟，4个原窗口进程各45秒内层/60秒外层；不自动重跑。仅公开合成内容与程序焦点，原托盘/HWND生命周期；原 DesktopOnly preflight 会只读 HKLM EnableLUA。无产品安装、系统DPI/主题/动作变更、真实采集、模型、注册表写入、用户电脑或新安全权限。

精确准备基线 b787149d5c50c3a6a9f940d0cd361db409c2090d；新入口只在 dev/ui-polish-visual 初次push运行。before=e4d57e77426ab235c7219b375debd76bc072e00b，after=419082f9863abf109a3d9ee7274dde59697bb85a；逐Git blob来源核验，单独构建目录、实际DLL与运行前后文件哈希。delivery与product SHA区分。

复用原Popen/wait/两drain/60秒观察owner，单独校验新结果，不调用旧window/source入口，不把保留state冒充已删除。原Tray对象disposed与added字段只读观测，要求确实退出删除；stderr清理错误也使本次失败。任何失败/UNKNOWN停止后续进程；UNKNOWN原owner仍保留，外层超时不冒充结算。

成功与失败PNG/JSON/日志全部收集，证据总预算192MiB，官方pinned upload-artifact保存一天；不上传SDK、runtime nupkg、构建DLL或原源码副本。完整manifest逐hash。上传只是保全，不等像素评审通过；真实结果在本次执行前均NotRun。

本地准备检查：13个Python环境/结果门用例全部通过；新托盘只读观测的Harness交叉编译0W0E、两命令exit0且进程组空，原件在 artifacts/2026-10-25-ui-visual-probe/approved-tray-build-r01/。这不是Windows/WPF运行结果。独审发现的两阶段日志重名与UNKNOWN finally前保全遗漏均已修正；always artifact直接采集原owned日志/报告与四个场景目录，即使步骤取消也保留已产生的部分原件。真实执行尚未开始。

## 本次实际终态（唯一 attempt1）

运行40177638553/job121121683762，实际提交e40e1f7a8810ae6abe5adc987b043dd38f9d1821；北京时间16:41:49–16:43:00。准备阶段FAIL/Blocked，错误`RuntimeError: Git blob differs`；视觉step skipped，0个WPF测试进程/0张PNG，像素/窗口/托盘清理验收均NotRun。没有重跑。

原DesktopOnly preflight通过，WinSta0/Default、96 DPI，工作区1024×728；HKLM EnableLUA只读值1。job内官方SDK10.0.402安装校验/版本命令通过，两项runtime包签名、locked restore与audit命令均exit0。随后原before归档与tree命令exit0，逐blob对照失败，before/after产品构建尚未发生。不能把准备工具通过写成前端通过。

Artifact12593187305为235280B，ZIP SHA256 `3e98ea49bfc8a9af17e52100728c902225e58bc9292958a966532e90374f02f4`；已下载核符。18实际原件=17项manifest+manifest本身，逐长度/SHA256全符；原ZIP/报告/日志保全在artifacts/2026-10-25-ui-polish-ci/，不提交二进制。独审亦逐项核实。

本地只读反例说明archive不能作为原blob字节搬运：固定before的.gitattributes原blob1164333B，命令内core.autocrlf=true时archive成员1173593B，多9260个CRLF而hash不符；false/input则匹配。原云端archive和core.autocrlf/core.eol实测未上传，因此不声称具体首个文件或唯一原因。另有已独审的中文路径quote解析缺口，它不能冒充此次实际首因。

后续最小修复为NUL路径解析及直接读取Git对象原字节，保留原hash要求，不改全局Git设置；应先本地修复回归再独审。此次一次Windows许可已消费，后续窗口必须另获明确批准，不自动重跑。详细收据见../test-evidence/2026-10-25-ui-polish-windows-attempt1.json。

## 后续独立本地源码修复（未开启新窗口）

移除源码搬运中的archive：固定SHA的`git ls-tree -rz`以NUL分帧、仅首TAB区分meta/path，严格UTF-8；普通blob允许集合来自该固定树。随后仅把验证过的对象ID送入`cat-file --batch-check/--batch`，按原始大小读取二进制正文、核对象/type/size及完整Git blob SHA1，再记录SHA256。正文NUL、CRLF与任意binary保持原字节；不改任何全局Git配置，不放宽Git blob differs。

每树≤20,000文件、单blob≤64MiB、总量≤1GiB，先核尺寸再读取。Windows目标拒控制字符（含换行/TAB/DEL）、穿越/drive/UNC/反斜杠/ADS、非法字符/尾点空格、DOS名（含上标COM¹/²/³、LPT¹/²/³）、大小写或file/dir前缀冲突、symlink/submodule。root及祖先不得链接，独占root/逐文件xb；异常保留partial，不删除或覆盖。大块源数据以`.bin`工作数据保存，既有artifact白名单不上传它；stderr仍单独保全。

实际本地证据：旧算法2红例均FAIL（LF→CRLF与Unicode C引号路径）；修复首轮23P；独审补六个DOS保留名并增加非零git命令的明确mock后最终24P=原13+新增11，不累加两轮。覆盖空格/Unicode、正文NUL/CRLF/0xff、Windows不支持路径的NUL完整解析后拒绝、错源/类型/数量/大小、截断/多余/损坏、已有root/symlink和原命令exit7立即停止。红例没有单独记录进程退出码，以实际2个失败断言和原日志为准。原件在artifacts/2026-10-25-raw-git-fix/，只提交文本收据。

固定before/after仅做只读tree+batch-check元数据核：12,315/12,317文件，总548,845,391/548,871,698B，最大blob16,700,907B，均满足路径/限额。**这不是完整实际导出或Windows构建验证**。本次工作流及窗口许可门未改；不会触发新视觉CI。已消费的attempt1和失败原件保留，WPF像素及清理仍NotRun。

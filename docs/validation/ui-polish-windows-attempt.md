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

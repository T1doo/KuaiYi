# supervisor启动前事件编码：离线确定缺陷，实际CI首因未知

基线 0a314a6eaa16ea580326a29ab7fc30032f12cada；远端执行分支仍绑定该提交。唯一run40338532720/job121626530482的checkout成功，supervisor步骤两秒内Fail、exit1，artifact数0。原日志下载经重定向返回Forbidden后已停止，不换API/浏览器/凭据或端点重取；临时签名URL仅原本地错误原件保全，不进入本报告。不能断言WPF启动或未启动，像素/焦点/动效/原资源树清理均无证据。

只读检查了原Python入口、解析/路径、环境门、0a父f2与四文件绑定及pwsh命令文本。命令所指脚本在0a中存在，入口原branch/attempt1/public/platform门与候选绑定相符。没有本机pwsh，未执行PowerShell或Windows，静态检查不是实际Windows兼容验收。Job ABI/原异常仍待原日志，未凭两秒时间猜测异常。

发现一个独立可复现错误：supervisor原 GITHUB_EVENT_PATH.read_text() 未指定encoding；默认非UTF8代码页时，合法UTF8中文JSON可能UnicodeDecodeError，或把非法UTF8字节当本地字符接受。原verify-ui-polish-windows.py入口已显式UTF8，此次仅对supervisor同样补 encoding='utf-8'，不改gate、owner、Job、异常结算、预算、产品/探针或系统设置。标准库文档说明文本默认编码依赖平台：[Path.read_text](https://docs.python.org/3.12/library/pathlib.html#pathlib.Path.read_text)、[open](https://docs.python.org/3.12/library/functions.html#open)。

回归执行原supervisor main中创建Job之前的AST前缀，用合成事件文件、仅对其默认解码注入cp1252；不改系统locale，不执行native owner、子进程、输入或网络。原代码实际3P/1F/1E（exit1）：中文丁的UTF8字节0x81解码报错、非法UTF8未拒绝。单行修复后5P/0F/0E（exit0），覆盖中文原字保持、ASCII正常、非法UTF8拒绝、错误分支拒绝、private拒绝。原红绿log长度/hash及source/test hash见test-evidence/2026-10-26-ui-bootstrap-encoding.json，原件在原云环境artifacts/2026-10-26-ui-polish-r03/bootstrap-encoding-fix-1保全；未重跑旧28/15或C#。

**这不证明本次CI因编码失败**：实际runner默认编码与原事件payload均Unknown，原始失败日志仍不可读。离线反例只证明源码存在独立的确定输入处理缺陷。Python3.9/3.10/3.11/3.12语法解析均通过，其余workflow、wrapper、产品与探针无diff。

本地工程修复候选不推送任何分支、不触发窗口。原wrapper精确PARENT/FILES保持0a当时绑定，不放宽来接受本新提交；本候选不是可直接运行的触发包装。后续如需实际验证，应先取得原异常证据，并重新形成精确来源绑定/触发候选交独审及新窗口批准。当前原生/像素/清理未验，旧失败与全部原件保留。

# 今日公开 WPF 界面工具交接（不是端用户成品）

只交父级唯一Windows执行任务01a1673b-54ea-75a3-b515-03d6282e4028；云端未运行Windows。用户要求下载即可使用，本工具自包含，不安装SDK/运行时/编译器/Python。完整应用包仍缺四native/七角色实际闭包/真实扩展origin/审定pins，不可用该工具代签完整产品。

固定已审源：`5afaa0c84091186da5f0e968a24936be6d733241`。工具正常默认构建，没有CI环境伪装、isolated或WindowsCiProbe编译开关。使用真实ShellWindow，明确提供临时SettingsCoordinator，选择平台关闭，只喂固定公开合成文字，ModelCalls=0。界面测试仍需要真实Windows桌面。

## 可直接取得的包

- Library `libfile_cdb25340273c8191b330204309c3b2d2`，v1，已确认保存；旧v0缺第三方声明，不使用；不传播私有下载URL。
- `KuaiYi-public-overlay-5afaa0c-win-x64.zip`，64,897,322 bytes。
- SHA256 `29c489593b65a7be6c364160c825453eafb6f14ce7680d6b2106cdba6b2b6e12`。
- 440 ZIP成员；逐文件TOOL-MANIFEST（439 payload文件，150,655,751 bytes）。Core/Desktop10.0.13包含在包内，apphost为PE32+/AMD64。两runtime原档严格在线验签、实际官方漏洞查询、完整SHA512核验；409 runtime文件逐项绑定，14 SDK未复制的WindowsFormsIntegration及卫星补原已验证RuntimeList字节，无源码改动。原包许可随包notices。
- 不运行包内其他App/NativeHost入口；保留其SDK正常输出不等于允许执行那些入口。

## 有界执行

建议总计3分钟，预留1GiB；正常动画数秒，20秒请求关闭，不代表20秒已退出。父级在已归属根 `E:\temp\codex-win-tests-20261023-1329-01a14dbf` 下另建KuaiYi专属随机子目录；确认非链接且原不存在。下载、解包、输出、TEMP/TMP全部在该子目录。先核ZIP hash，再核每个TOOL-MANIFEST文件长度/hash；拒绝越界路径/链接，不共享其他项目目录。

仅为本次子进程设置TEMP/TMP到其专属temp，不改APPDATA/LOCALAPPDATA/USERPROFILE或注册表，不安装任何依赖。设置工作目录为解包目录；父级应保留原Process对象，并重定向stdout/stderr到专属logs。唯一运行命令：

```text
KuaiYi.GrowthHarness.exe --ci-actual-overlay E:\temp\<本次KuaiYi专属根>\output\overlay.csv
```

这个既有CLI名称的ci表示有界工具场景，无需设置CI/GITHUB_ACTIONS环境。不要点菜单、F5、拖放、截图、复制或发送，不触碰用户窗口内容。不要同时运行其他会改变前台窗口的测试，以免ForegroundUnchanged断言受真实干扰；若实际发生，保留失败，不隐藏。

等待原Process实际退出，记录退出码。工具20秒会请求关闭；父级等待30秒后若仍未退出，只报告原进程未结算及证据，不假报清理或直接删除。后续关闭/终止只能针对本次确切原进程并由父级按既有授权处理；强制退出不算正常退出通过。不能按模糊进程名终止用户程序。

验收必须同时有：原进程真实exit0；CSV、summary JSON、phases原件；StreamCompleted=true，ModelCalls=0，Errors空，RetainedFrames>=10、DiscardedFrames=0，Checks全部true，关闭/RunReturned阶段及输出保存成功。summary字段不能替代原进程实际退出。可选人工看公开浮窗，不采集用户桌面图；不能把单场景当DPI多屏/高对比/focus完整验收或性能250/600ms验收。

## 退出、证据与清理

先保存仅工具公开内容的CSV/summary/phases/stdout/stderr和包hash、原进程退出信息到父级证据渠道。确认原进程实际退出后，核其temp中`kuaiyi-overlay-probe-*`是否已由工具正常删除；清理仅本次专属KuaiYi子目录（包、解压runtime、temp、output、logs），不删除父级根、其他项目或系统缓存。报告实际剩余占用及失败原文。

正常限定路径没有KnownFolder配置写入、捕获/热键、浏览器接收、模型HTTP、Shell/Host注册或自启动；Windows自身维护日志/缓存不能冒称完全可重定向，也不清理系统缓存。发现本任务新增文件要落在专属目录外时停该动作并报告，不通过改KnownFolder伪造隔离。

## 实际构建证据边界

原locked restore成功。GrowthHarness package-list CLI两次`Sequence contains no matching element`为Fail，未改生产gate或伪填通过；只对本公开工具经只读独审使用原assets两个target、8个project libraries+根项目、0 package和全部原锁未改的替代证明，两runtime实际独立审计/签名均通过。实际正常no-restore publish exit0；不是完整Installer资格，生产pins保持Unavailable。Launcher自包含publish也已在云端成功，但未因此签完整B Read或安装成功。

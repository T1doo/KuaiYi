# 浏览器截图归属拒绝与来源边界

监督方对基线 `c83dde56b58039d337bcb3ba9b15379bd7a71a58` 指出的截图风险属实。实际measureImage仅检查中心elementFromPoint，矩形边缘的覆盖内容或CSS非图片区域可能被一起裁切。上一轮报告对“经验证可见部分”的描述不能作为安全完成证据。

先在原源码上执行8项合成DOM负例：角落、边缘、相反角落覆盖、clip-path、border、padding、object-fit和mask；8项全部错误返回可截图矩形，负断言8/8失败。使用的是实际measureImage实现，DOM/API是明确测试fixture；未运行真实浏览器或截图，不宣称8次真实内容外传。原源码字节、测试、日志和JUnit均保留。

当前完整关闭浏览器截图fallback。在任何目标上，都于DOM注入、captureVisibleTab、图像解码或传输前返回ScreenshotOwnershipUnverified，包括表面无遮挡的简单图片。移除不再使用的scripting权限。保留同源HTTPS、credentials=omit、拒绝重定向和有限PNG原图获取；Service Worker实际使用acquireImage，原图失败会明确拒绝并取消本次授权，不发送Begin/Chunk/Commit。cropPixels只保留为数字矩形边界工具，不具有可见性或截图授权含义。没有用更多检测点或样本推断任意像素归属。

扩展43/43项通过，包含新增9项实际acquireImage的拒绝用例（上述8类加表面无遮挡）和3项导入实际Service Worker处理函数的完整操作：覆盖/clip-path失败不截图、不注入、不发送图片帧；授权后同源无凭据原图仍交付相同PNG字节。API/DOM/Port仅作为测试输入，未重写采集实现。旧数字边界测试和实际宿主/管道传输测试保留。此次JS修复未重跑.NET381，未变更其生产源；原已核证据保留，后续资源竞态修复将重新执行全回归。

说明页现在明确“浏览器截图路径尚未完成”，也不暗示当前P0已有桌面区域截图快捷键。截图功能、真实浏览器内容/权限/坐标/安装仍NotRun/Blocked，106整体仍NotRun，不因43项协议/拒绝测试变成可用图片功能。审阅包只含原图路径和严格拒绝策略；原v1包保留历史用途，含已知不安全截图代码，不应作为可用候选安装。最新包和证据见 [本轮汇总](test-evidence/2026-10-11-browser-ownership-summary.json)。

## 来源边界准确状态

argv origin、已注册allowed_origins及已安装文件摘要只证明配置与文件匹配，不能独立证明启动者真是浏览器。普通本机进程可以手工带相同参数启动宿主。当前生产缺真实ID、注册、receipt和App receiver，仍默认拒绝；这不是已开放的生产漏洞，也不是已完成来源认证。

接入生产receiver前，需要从操作系统校验客户端进程、当前用户/session、安装位置、完整模块与已知角色；不能仅信任进程自报origin。即使确认NativeHost二进制角色，浏览器来源和用户手势的因果关系仍是独立问题，同账户恶意进程属于明确有限威胁模型。缺可信浏览器来源证据时，设计应采用桌面逐次明确确认（不因自报origin或Activation=On自动向模型发送）；必要的局部预览和内存接收也必须有有限生命周期和独立许可。确认后每次网络发送仍过完整request identity和endpoint origin同意，不能把来源配置匹配当服务商授权。

本轮只记录这条待验证设计，不开放receiver、不安装注册、不发放持续权限。操作系统验证、授权UI及真实Windows证据完成并经监督复核前，生产保持拒绝。ReadBounded的取消/完成竞态按监督要求另行修复，截图修复不改变其保护或来源判断。

依据：[Chrome可见tab截图API](https://developer.chrome.com/docs/extensions/reference/api/tabs#method-captureVisibleTab)提供可见tab图像；[getBoundingClientRect](https://developer.mozilla.org/en-US/docs/Web/API/Element/getBoundingClientRect)的矩形包括border/padding。两者加中心命中不能证明矩形内像素全部属于图片。本轮以明确拒绝处理缺口，不宣称已设计出完整证明。

后续独立资源修复已完成：ReadBounded统一迟到观察/释放与截止交付复核，7确定性用例及完整.NET388/Node43通过，详见browser-read-ownership-review.md。请使用同时含两项修复的新审阅包，前一fallback-closed包仍为历史读取代码。

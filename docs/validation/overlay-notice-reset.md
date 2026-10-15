# P2-04：版本切换后清除旧的新内容提示

接续afa883f5582dd8d4fab548b06a8f0cd441b1118d，唯一集成写入者持锁，独审只读。当前指定环境是Linux，没有真实Windows/WPF桌面能力；没有安装模拟层、连接其他桌面或新增权限。现有批准入口GrowthHarness的`--actual-overlay`构造正式ShellWindow并注入公开长文本，但需要Windows；其自动观察只覆盖几何/锚点等，不包含轮鼠、键盘、滚动条交错或50次收放反向。因此本轮B06/B07实际交互均为 **NotRun**，不把端口测试或交叉编译计为验收。

## 确认的产品接线遗漏

已有上读路径把follow设为false；继续追加后FlushResult显示“有新内容”。版本菜单成功SelectVersion后递增resultRevision、设置follow=true并OpenBody，但原FlushResult只有显示提示的分支，没有清除分支。窗口仍展开时，PrepareVisuals也不会清除它，因而旧提示可以跨版本残留。这是源码事件路径发现，尚未在Windows复现。

只改FlushResult的一处属性同步：有正文且暂停跟随时显示提示，其他情况设为Collapsed。正常上读追加仍显示；版本切换、恢复跟随或无正文时清除旧提示。原“有新内容”点击回底、阅读意图/修订守卫、隐藏及结果切换、收起与动画均不改，没有新状态、队列、适配器或计时器。

独审同时核B07：现有OverlayFrames从Current/当前opacity反向，wantsExpanded记录最后操作，已有反向连续性及交错序列测试。本轮未发现需要独立改动的新Core缺口，不重新实施已有功能。以下仍需要真正WPF：长文本达到上限后的阅读位置、所有输入和50ms刷新交错、提示按钮实际可达性（尤其向上生长）、回底后持续跟随、50次收起/回看和动画反向、实际锚点/闪烁/帧时间。

## 本轮验证范围

执行App Release、Windows targeting、locked/no-restore交叉构建；结果与日志hash见[固定证据](../test-evidence/2026-10-15-overlay-notice-reset.json)。独立源码审查覆盖原显示路径、版本切换后的清除路径以及提示点击原行为。此次是单处属性同步，不增加镜像表达式测试或重复原队列夹具；前批538/538回执保持原字节，本轮未重跑，不能称为本次动态验证。Windows B06/B07、完整B组及生产验收均未通过，P2-04仍InProgress。

每次dotnet前显式DOTNET_GENERATE_ASPNET_CERTIFICATE=false并由子进程继承，复用既有CLI home。前批证书提示、原日志/TRX保全；证书存储及信任状态仍Unknown，未读库、trust或delete。原CMake根因Unknown、不重开实验；250/600ms及来源/密码/完整性/取消/清理门保持。不改main、workflow或原CI分支，不强推、部署、真实模型调用或新增费用/凭据/权限。正常推送仍使用不匹配八个保留workflow的产品分支dev/overlay-reading-input。

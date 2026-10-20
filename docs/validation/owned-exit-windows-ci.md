# 已批准WindowsCI的投递与实际读取阻断

生产修复[98c3f27d07ee392bf98e647861596812c2d63a39](https://github.com/T1doo/KuaiYi/commit/98c3f27d07ee392bf98e647861596812c2d63a39)及其红绿原件已独审保存。另审四文件CI增量后，单次普通push投递[00ecaae6def945cf39f3bdacf5fca7f8dc2d69e9](https://github.com/T1doo/KuaiYi/commit/00ecaae6def945cf39f3bdacf5fca7f8dc2d69e9)，精确PARENT=98c3f27。原工作流仅名称改变以请求新run，runner仅更新parent／精确四文件scope与文案；既有源门测试补拒绝dirty checkout及错误event SHA。离线29P；提交后真实本地Git源门Pass，不冒充Windows。

用户已明确批准同范围必要免费CI，接受原WindowsSDK10.0.402与原隔离方式。原固定ZIP／许可证／双正常在线签名／runtime audit／public标准windows-2022／job15min step14min内总13min attempt1全部保持；SDK安装器／其测试／其他8workflow／锁及生产门不变。没有重跑旧job或连续触发新CI。

投递后按新授权对确切新SHA做一次Actions只读查询，命令`gh api repos/T1doo/KuaiYi/actions/runs?head_sha=00ecaae6def945cf39f3bdacf5fca7f8dc2d69e9&per_page=5`实际Exit1，错误为`Get https://api.github.com/...: Forbidden`，未返回数据。此前读取已知未执行，本次为收到相关新授权后的唯一重试；随后停止，无换接口／connector／浏览器路由绕过。此错误是读取路径的访问阻断，不能认定CI失败或任务未启动。

目前仅确认源码push成功、远端准确指向00ecaae；实际run/job ID、直接CI链接、签名／SDK／audit／完整测试／三专门构建的执行阶段、计数与cleanup均Unknown。新完整原始日志及TRX尚未读取，不能拿旧日志补签。需要父级正常登录桥接此确切SHA的run/job链接、终态、最终JSON、完整raw／gzip与原始失败／TRX（若生成），先核现有运行，**不要再触发**。拿到失败日志后再定位同范围最有价值修复。

生产修复的实际Linux4F→10P、r03的205P1F及最终相关两类34P保持原证据边界；不把Linux通过当Windows验收，也不把新P2当历史25次Crash已实证的原因。包Hold；图片间歇底因Unknown，生产复用Off、250/600ms／来源／密码／完整性／取消／清理门保持，真实App／WPF／键盘／模型／完整P6P7未验收。

[源码、独审、真实本地源门、投递与被拒读取原件](../test-evidence/2026-10-20-owned-exit-windows-ci.json)。

# 图片取消与清理：受界诊断闭环

基线 `9a38249db5bc69f49601de3e97f93e97671d9c77`，正常开发分支 `dev/p6-image-settlement`，唯一root writer与只读独审。父侧有限ACK后优先诊断，未改格式、解码、截图/file来源、Provider或发送链。

## 确定事实与最小修复

前批r13旧BMP取消记录469P/1F，失败栈为Dispose→SettlementUnconfirmed；随后同构建定向2P与完整470P。Dispose异常也可能遮盖调用者先前异常，因此旧记录不能证明该方法内全部断言已过，不能证明具体哪条IO或其根因。本批不篡改旧BMP或旧断言，不用复跑覆盖失败。

新确定性测试使实际进程管道返回EOF，但以TCS保留同一个原Tail Task；取消先产生OperationCanceledException，原实现finally及Dispose的SettlementUnconfirmed均没有首异常inner。r02真实0P/1F证明的是**首异常诊断丢失**，不是BMP根因。r01为测试误引用private helper导致编译失败、0执行，已保留；改用自有合成PNG。

`ImageJobException`只增加可选inner，原Code保持。`ImageWorkerClient`在channel finally前、RunOnce外层及总预算转换前只保存首次PrimaryFailure；返回/Dispose的结算包装仍是原失败Code，inner保留同一个首异常对象。真实CleanupFailure单独保存，不能被生成的“结算未确认”占位CAS覆盖；原IO的迟到fault仍经IoFailureCode观察。异常对象仅当前client内存持有，不加入日志、JSON、用户提示或历史；不能自动捕获客户端外调用者作用域中的断言异常。

清理task边界显式观察真实异常并原样rethrow，实际清零在finally执行；faulted cleanup不算成功结算。全部原IO和cleanup task继续参与门控，展示快照最多64项（pending优先、总数/截断显式）、事件ring32。安全快照只有固定enum kind/status、类型名/错误码、计数和观察序号，无message/stack/Data/内容/path/token。序号是观察顺序，不是任务真实完成时间、OS退出时间或性能测量。历史SettlementFailureCode成功后也保留；当前状态应看active/pending/fault/exit等字段。

没有增加sleep、宽限轮询、吞异常或将exit等同IO结算。TryFinalize遇到原Task/IO未完成仍返回false，owner保留；原task真实结算且清理成功后可对同一client显式retry。注入cleanup fault时，即使实际buffer finally已清零、进程已退出，仍保持false-finalize，不把fault标为成功。只读独审发现“实际cleanup异常未保存/未显式观察”P2后，新增ClearOwnedIo边界及fault回归，已闭合。

## 验证范围

[证据索引](../test-evidence/2026-10-20-image-settlement.json)及原件ZIP包含红绿TRX、真实安全快照输出、命令和最终源摘要。最终完整Codec含原470+新增5，旧470源字节不改；Provider只测原传输37+文件来源20=57，Core只测相关97，均不是全Provider/Core。Worker/App仍仅Linux环境的Windows交叉构建。

新增5例：取消后原IO待结算→原IO完成但cleanup TCS待续→实际cleanup成功且sameowner retry；迟到原IO IOException不覆盖首取消；真实投影IOException对象与channel外验证首异常保留；真实cleanup IOException单独保存/显式观察/保持阻止finalize。两阶段屏障不是时延凑绿。实际raster在失败时清零，file投影次数0，未交付可发送owner。快照和合成私密标记隔离断言通过。原byte/code/来源/完整性/预算/EOF/真实退出/发送/PackageHold/复用Off门保持。

这里只确认这些有界调度下暂未结算可后续完成，未发现这些场景的实际泄漏或失活；不据此证明全部调度永不泄漏，也不宣称修复旧BMP根因。Windows native/Worker/WPF/OCR/实机字体画质、250/600ms、600MiB及完整P6仍NotRun。没有新依赖/许可/安全设置、真实用户图片/模型、费用/凭据/CI/用户电脑/发布部署/main修改或强推。完成此闭环后才接原P6-06单图投放，复用既有file owner与发送确认路径。

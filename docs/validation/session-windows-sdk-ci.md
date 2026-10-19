# 已批准的 Windows SDK402 临时 CI

后续真实结果：本次run39226078160已Fail，SDK／在线签名／审计已通过，Core进入测试后失败并超时。下文保留投递时Unknown状态，最新诊断、有限修复及完整回归失败见[后续记录](session-windows-core-diagnosis.md)。

用户于2026-10-19 16:31北京明确“接受”固定Windows归档LICENSE（Sentinel_e978556d48a48191aa940cf146bdb928），此前这一份具体条款的Pending已解除。源码 `32116577a34d9f914f93dc715261e99da7d33868` 经独立审查Pass后，已一次普通推送 `dev/session-runtime-signature-windows`，精确父为 `6df5f910e7429fde02c8e02c9ce53c953f668094`。远端63heads仅该分支前进，其余62含main `51d077481cf6303602fcf5d2718213da9cf05178` 不变。

安装器固定官方WindowsSDK10.0.402 ZIP300755826B、完整SHA512及已接受LICENSE/notices SHA256，HTTP层拒redirect，无重试／换源；全归档Windows名称／路径／类型／重名／symlink-reparse／加密／条目数／展开大小检查及许可／13metadata核对，均先于创建fresh SDK目录。现有官方ZIP已实际只读通过5577条目与807617264B展开量预检，没有本机解包或执行WindowsSDK。原双runtime正常在线验签先行，安装后原安全SDK静态门先于任何dotnet；仅子进程SDK／缓存／CLI环境，继续原locked restore／audit-all-low／warnings-errors、完整Core／Provider及默认App／隔离App／ImageWorker构建，失败即停。

实际离线旧实现39例1失败，最终51P，红绿日志、各自源码ZIP和19份最终输入hash均保留。最终主反例另加三项断言，不能称同方法字节红绿；全部是合成SDK／端口合同，不能替代Windows运行。独审核实六文件／源码原件绑定、八原安全函数及15保护输入不变、真实ZIP只读预检和失败关闭，无剩余P1/P2。

这次普通源推送仅确认CI请求投递；新run/job链接、SDK安装、在线验签／审计、实际Core／Provider计数、三项构建及远端原件回收仍Unknown，Package Hold。父级可通过正常登录页面取得最终 `KUAIYI_SESSION_RUNTIME_WINDOWS` JSON，以及全部 `KUAIYI_CI_FILE`／`KUAIYI_CI_CHUNK` 行；核对每件1..N分片、gzip大小／SHA256和解压后原件大小／SHA256后才确认回收。脚本仅登记命令日志和实际TRX，限定每件16MiB／raw总32MiB／gzip总4MiB，不完整导出不能报整体成功。标准Public windows-2022、attempt1、job15／step14／内部13分钟，无artifact或cache上传、旧job重跑、本机Actions读取或被拒路线替代。

本次后续提交仅封存docs和精确原件属性，不再修改该workflow路径，不投递第二次本专用CI。生产C#／锁／原八workflow未改；不操作main／系统／安全／凭据，不发布／新增费用／引入Skia四包／调用真实模型。历史图片间歇等根因保持Unknown，生产图片复用Off和250/600ms／来源／密码／完整性／取消／清理门保持；真实WPF／键盘可见性／Windows App／模型及完整P6P7验收NotRun。

[原件与交付索引](../test-evidence/2026-10-19-session-windows-sdk-ci.json)包括明确批准、实际红绿、真实归档只读预检、独审消息、源码快照及正常推送／63refs差异；早先Windows在线验签Pass属于另一源码1662f65／run39221162999，不能套用到本次新run。

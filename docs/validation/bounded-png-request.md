# P6-02：4MiB 请求 PNG 编码前置组件

2026-10-20，从已独审、clean 且远端一致的 `9edc5bb2fd2e84a02449ca8144319fb14d137fc0` 接续，单 worktree、唯一 root writer，正常分支 `dev/p6-bounded-png-request`。选中原 SPEC7.3/P6-02 的默认编码后不超过4MiB及更严格API限制子项。完整P6仍InProgress，生产截图一般缩小路由尚未接入。

## 实际行为

`ImageWorkerClient.PreparePngAsync` 在已有隔离Worker方向修正、默认长边2560的面积缩小及白底合成之后，生成一次lossless RGB PNG。输入及Worker支持范围沿用上一批：PNG identity、有限baseline JPEG/EXIF1–8，PNG eXIf仍明确Unsupported。只有原通道完整EOF、实际exit0及原IO结算后才能开始编码；不增加Worker协议或父进程Skia依赖。

编码上限默认4,194,304字节，可配置65,536–4,194,304字节，更低值明确拒绝且不启动子进程。上限计入签名、全部chunk和CRC。只输出IHDR/IDAT/IEND，原EXIF/GPS等元数据不复制。超过上限返回现有需要截取局部的 `ScreenshotNormalizationException`，没有降低质量、另缩尺寸、重试或静默漏掉局部。该异常尚未从本新接口接入产品UI；文字可读性评审亦未完成。

`PreparedImagePng` 持有实际编码数组，记录精确总长、SHA256、MIME、上限及原prepared数值元数据和 `ImageTransform`。JPEG方向、白底及连续pixel-edge映射语义保持；encoded输入没有桌面锚点。转移PNG前清理栅格，Dispose清理实际PNG数组。父端再次校验栅格摘要、总长及实际alpha255；不是仅凭一个PNG头宣称任意输入已完整验证。

原RunOnce active状态贯穿同一个编码Task、全部输出和栅格清理。默认编码通过Task.Run执行；取消及总15s协作预算触发后仍等待原任务真实结算，迟到结果清零且不能交付，不能靠WaitAsync放弃CPU任务。15s不是硬终止保证；原Worker超时和进程终结门保持。没有250/600ms或600MiB实测结论。

共享截图PNG编码器增加无额外栅格复制的内部BGRA入口，保留原公共1–20MiB参数范围。row、每次实际MemoryStream容量和最终ToArray均纳入自有buffer观察及清理；CRC每64KiB检查取消，压缩flush恰逢取消不能错误提示截小图。正常、超限、observer抛错、取消及晚返均测试真实数组，不以复制副本代替清零证据。

## 独立审查发现和修复

最小设计及最终五文件源码由独立只读reviewer审查。发现派生MemoryStream调用base.Write(span)会走Stream/ArrayPool的额外副本路径；改为有界扩容后直接写入已观察的自有buffer，先SetLength再复制以避免清零新内容。

为核实此P2，仅一次请求当前runtime commit `4099f83977f4d29604a9aeb971fb0a7bc1d5f699` 的官方MemoryStream源码URL，HTTP404正文为该ref不存在；没有取得该官方源码、没有重试或切换路线。随后读取当前已批准runtime10.0.13的公开方法IL并运行仅使用自有合成slice的探针，确认派生span→Stream.Write→ArrayPool.Rent/Copy/virtual arrayWrite→Return(clearArray:false)。实际CoreLib及四个方法IL摘要保全。没有读取池尾或已经归还的池内容，不把这个证据说成窥看池证实泄漏。此次修复消除上述可避免副本路径；ZLib、框架或native内部缓冲清零没有得到验证。

探针首次自写IL解析器错误使用FF而非FE前缀，KeyNotFound退出134；修正后一次因果复跑成功。这是探针解析器错误，不是SDK/native/sandbox崩溃；首源码和stderr保留，无新增工具包或依赖。

## 实际验证

| 最终本批运行 | 结果 |
| --- | --- |
| codec Release构建测试 | 129P/0F/0skip：新增26及原103，旧两个测试文件字节不改 |
| Core相关限定filter | 74P：Worker/startup/finalization/IPC，非全Core |
| Provider相关限定filter | 154P：传输、截图归一化和坐标边界，非全Provider |
| Worker/App Release交叉构建 | 各0警告0错误；不是Windows运行 |
| 五个项目locked restore | 全部exit0，NuGet audit all/low及warnings-as-errors，锁文件不改 |

新增测试实际通过生产源码fixture子进程，把5×3不对称图缩到3×2并独立核23/103/183/37/117/197灰度，核JPEG方向6、透明白底、请求摘要/映射和只含三个chunk的CRC/像素。1024高熵图实际编码在3MB–4MiB内；1280高熵图明确超限且尺寸不改。另核精确总字节cap与cap-1、全部真实工作buffer、原child额外字节/exit7阻止编码、实际15s协作预算以及忽略CT的受控原task晚返清理。合成端口验证所有权与晚返，不冒称Windows或native硬时限。

首编译CS8602为0测试执行。首轮23P/3F：两条高熵输入由Skia生成超过原256 PNG chunk上限，尚未进入新编码；改用原截图PNG编码器生成同一噪声输入，保留产品256界。另一条buffer观察用例错误假定异常后ZLib.Dispose不会再分配，改为核指定触发确实发生且所有后续实际数组也清零，未降低清理断言。修复后11项受影响专项11P，最终整组129P；中间回执不与最终累计。首源码、失败TRX、专项及最终原件均保全，见[证据索引](../test-evidence/2026-10-20-bounded-png-request.json)。

## 下一缺项和边界

下一核心项是将一般缩小及4MiB结果通过显式桌面锚点/坐标适配接入生产截图路由，并补可读性拒绝行为。当前生产截图入口仍保留原常量图缩小限制；本组件没有暗中切换路由。PNG eXIf及其他完整格式、打开/投放UI、Windows真实native/Worker/OCR/WPF及全P6验收仍未完成。

沿用获批四个SkiaSharp4.153.1包，锁/许可证/原声明及Worker输出声明字节不改；App与Imaging实际assets/output无Skia。来源/密码/完整性/取消/清理/隔离及frame/资源限额、250/600ms门保持，PackageHold及生产图片复用Off保持。无用户图片/真实模型/CI调度/费用/凭据或安全权限/发布/用户电脑操作；main保持 `51d077481cf6303602fcf5d2718213da9cf05178`，旧CI39347646426 step3被拒日志没有请求或绕路。Linux结果不替代Windows桌面验收。

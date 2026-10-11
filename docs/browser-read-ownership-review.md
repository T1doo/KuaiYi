# NativeHost 截止与读取完成的资源所有权

截图拒绝修复提交 `7d176397d035f4409d105d1764683c6062d7dae8` 之后，本轮独立修复监督方发现的ReadBounded竞态。来源配置仍不等于浏览器认证，当前没有生产receiver/授权来源，继续拒绝；设计边界见browser-ownership-review.md，本轮未接入授权端。

旧catch只在pending.IsCompleted=false时观察迟到任务。WaitAsync因截止失败后，底层若在catch检查前完成，成功返回的JsonDocument无人Dispose；失败也可能跳过统一观察。现所有失败等待都把同一个pending任务交给ObserveLateRead，成功文档由该唯一观察者using释放，失败存有限类型码，EOF统一结束。成功等待则交给RunAsync的using所有者，两条路径互斥。不会再交付迟到内容、不重复注册观察者、不重读帧，也不把底层stdin宣称成已被取消。

另补截止状态的交付检查：已经完成的Task可能走WaitAsync成功快路径，即使token刚好已过期。实际RunAsync取得文档后再次检查linked token，过期不交付Offer，正常using仍释放文档。这不放宽任何origin/activation/identity限制。

## 确定性验证

新增7项执行实际BrowserSession.ReadBounded与NativeMessagingCodec：手动TimeProvider触发真实deadline CTS/linked token，TaskCompletionSource保持底层读取或catch观察槽位；成功、实际截断帧EndOfStreamException、EOF × catch观察前已完成或观察后才完成，共6种。读解析的是公开合成Native Messaging帧，文档由实际JSON解析器分配，直接检查ObjectDisposedException、观察者次数1、读取次数1、late failure类型、桌面Offer/Submit次数0、单个Timeout帧。第7项在读取已完成时触发截止，验证不交付Offer且正常所有者释放文档，不注册额外观察者。

没有Sleep或猜测时序。内部friend测试钩子和受控读取任务仅由Core.Tests配置，生产Program和浏览器没有开启路径；生产默认TimeProvider.System、30秒截止不变。测试使用真实编解码器/文档生命周期，底层不可取消的等待由测试任务控制，不能写成这7项是实际Windows stdin测试；原独立Linux宿主stdin、JSON生命周期和内部命名管道测试保留且全回归重新执行。

先保持旧完成检查，仅加内部屏障/手动时间依赖：首批6项3失败/3通过；加强实际文档Dispose断言后仍3失败/3通过。加第7项实际交付边界后，最终修复前7项**4失败/3通过**：成功文档未释放、成功/失败/EOF漏观察，以及过期仍调用Offer。相关精确编译源、测试源和各轮TRX保留。修复后7/7；完整Core311 + Provider77 = **388/388**，Node扩展 **43/43**，0失败/跳过。两种运行器共431项。19托管locked Release构建0警告/0错误。

原始失败日志/JUnit的空白按工具原字节保留；源码/脚本/测试/文档执行范围内的git diff --check通过，未为了格式检查改写失败证据。完整源摘要、原始TRX/JUnit、构建/打包日志、最新18文件ZIP见 [证据汇总](test-evidence/2026-10-11-browser-read-ownership-summary.json)。

最新合并审阅包为 `artifacts/KuaiYi-P0-browser-read-ownership-win-x64.zip`，209228字节，SHA-256 `ef458b50e8e3d2ff708f8fa4c234c88bec6278b3b3444d63b04d961075c4ea1b`。包内18个运行/扩展文件及审阅清单已核验；框架依赖、缺实际注册和授权端，仅供审阅，不是安装包。此前各独立ZIP保留为历史证据。

当前浏览器截图仍完整拒绝、原图仅同源无凭据PNG；origin参数/注册/摘要只证明配置匹配，生产缺真实注册/receipt/receiver，不认证浏览器来源。没有真实页面获取/外传、模型调用、扩展安装或注册。Windows原生角色/浏览器/截图/安装与106整体用例仍NotRun，E06不变。本轮两项交监督独审后再讨论原核心的生产来源验证，未直接开放。

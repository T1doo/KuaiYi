# 流式文字显示节奏

新增纯显示用 ReadingReveal：只持有版本、显示位置、时间与节奏余量，不保留来源字符串，不修改 OverlayResults、请求、复制正文或来源快照。渲染 Markdown 使用已接受文本的前缀，显示调度从 50 ms 调整为 33 ms；布局测量与正文共用这一前缀。已有 220/180/80 ms 动画及高度跟随曲线未修改。

小批输出约按每秒 42 个 UTF-16 位置推进，突发积压按剩余量提高追赶速度；使用 StringInfo 文本元素边界，避免当前已收到字符串内拆分 emoji/组合字符。网络片段本身截断未完成 Unicode 序列的情况未新增专门验收，不由此声称任意网络分片均完整。

减少动画、用户暂停跟读、隐藏／预览状态、旧版本或停止状态直接显示已接受内容。ClearMarkdown 重置显示状态。采样终帧增加显示已追平检查，避免“请求完成但文字尚未显示完”提前截断动图。

## 验证

- SDK 10.0.402 构建 0W0E：`artifacts/reading-reveal-build.log`。
- 4 项纯节奏测试通过，覆盖 Unicode 边界、停止/减少动画立即显示、版本/reset、空闲不积累信用：`artifacts/reading-reveal-tests.log`。
- 569 项 Markdown/Overlay 托管回归通过：`artifacts/reading-reveal-regression.log`。测试端口未实现实际 WPF pacing hook，不能将此计数当成完整动效验收。
- 最新真实公开窗口 `reading-reveal-01`：完整输出、Errors 空、零模型调用、AnchorStable=true、Grew=true；正常关闭。非激活样式仍为测试适配，不签非激活验收。
- 40 张真实 WPF 样本生成 `docs/test-evidence/frontend-desktop-2026-11-04/reading-reveal/motion/opening.gif`，采样3813ms，额外终帧停留1200ms，无插值、非桌面录屏／FPS验收。

仍需覆盖真实网络的大块输出、长代码/表格、流中停止与切换版本、系统减少动画及高DPI实际性能。未宣称所有路径完成。

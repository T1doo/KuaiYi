# 所选本地图片来源的折叠预览

唯一父为已接收 `2a51a5ec06f24948cb412dcd5eb06e11cf8caaf3`，正常新分支 `dev/overlay-image-source`。起始HEAD/远端一致、工作树干净、单worktree、root唯一写入者、独审只读；未知锁和进程不动，main不改。

## 交付范围与选中项

按SPEC4.2/7.2/14.1接现有生产截图快捷键：完整桌面帧仍在裁剪后立即释放，只将最终裁剪图的所有权转给本地图片来源卡。此前Shell只有文字结果版本，图片模型/分类/OCR调度尚未接入；本批明确显示“本地图片 · 未翻译 · 宽×高”，不伪造文字快照、正文、RequestIdentity、Provider结果或请求，不启用、不上传。Off/On保持。文件/浏览器/图片拖入入口、图像规范化、图片模型及再生成仍属后续P6，不以本地预览冒充交付。

SelectedSource统一返回所选VersionId及Text/Image；本地图片具有独立版本ID和SourceId，正文/测量/原文守卫/标签/菜单/复制/回看/重新生成与风格共同消费。只读原生Expander/Image默认折叠，图片来源卡不提前占满窗口；“查看原文”显式展开并沿原阅读守卫定位。图卡不复制文字、不提供图片重新生成或风格选项。文字原始复制/缓存/Markdown继续走原链路。

有效图片提交替换文字显示并清其快照；新文字Begin选中真实文字版本，原图只保留一个有界fallback，待新文字成功Complete/明确保留可读停止结果或实际来源替换再释放。失败时已有可读文字rollback优先；没有文字回退才恢复原本地图片版本，折叠且不自动定位。取消新框选/取词Prepare拒绝按SPEC7.2/4.7保留已提交会话，只释放本次暂存图和旧显示副本；旧展开/Loaded/接纳回调不能自动恢复旧图。

## 所有权、边界与清理

OwnedSourceImage独占ScreenshotPixels最终裁剪BGRA缓冲，调用方显式转移后不再Dispose该crop。构造时按原ImagePolicy校验16384单边/24MP，拒绝也消费并归零；仅一个原图owner，最多96MB有界像素，不保存路径、EXIF或磁盘历史。对象不暴露原图buffer，受锁保护的CreatePreview只生成独立、最长1024且最多1024²像素/4MiB的等比例预览，中间失败/取消归零；原图的受控缓冲在Dispose归零。

界面和测量都借用同一原图。折叠状态没有native bitmap；实际展开才由裁剪像素生成本地WriteableBitmap，不加载URI、资源或远程图片。测量仅用尺寸/相同有限宽高，绝不生成第二份bitmap。图片视口宽度共享正文预算，高度为min(180,max(1,H/2))DIP（H为正文高度预算）；折叠/解绑/替换释放显示副本，detach后逐行尝试写零，部分失败保留bitmap供Clear重试。填充与擦除双故障聚合；点击/命令/Loaded边界报告失败而不抛出dispatcher，待擦除bitmap不能静默复用为空白成功。

图片接纳检查截图CTS owner、input/result修订、epoch、当前ScreenshotIntent、锁屏/Hidden/退出与取消；Intent.Dispose自身epoch+1单独核验，不调用会启用本地请求的TryCommit。原生CaptureHost结算先沿原流程确认；不确认仍保held owner。新捕获开始先解绑旧显示；任何旧展开或Loaded还受版本/owner存活、阅读修订、Capture、Hidden、锁屏、退出保护。

清除、锁屏、退出及等待设置结束退出均独立尝试源/显示/原图释放，即使native擦除失败也继续清原图、隐藏或请求草稿关闭。等待草稿时保父窗可见，不改草稿/保存事务；正常退出仍逐一结算旧startup/position/Drop/Selection/Screenshot/Hotkeys/Translation，任何未确认都聚合并拒绝Close。StopSelection只增强独立清理尝试，hook释放未确认时保原owner；未用令牌宣称COM中断。所有这些调用的原生执行/时限仍NotRun。

## 实际回执与失败分类

最终Provider **923/923**，精确保留父884测试名多重集合，新增39（ImageSourcePreview28、OwnedSourceImage11），相关402；App Release交叉构建两次均 **0警告/0错误**。新增用例使用合成像素、实际OwnedSourceImage/截图意图/Core/Coordinator及链接的生产构造/绑定/事件/清理方法；WPF/receiver等为明确数据调用端口。完整Provider仍含既有MockProvider本地HTTP测试；新增图片用例不创建socket、不调用模型。

- r01：26/28，两个Clear相关失败。新夹具Accept漏掉生产finally中的EndScreenshotRestore，先Dispose CTS但留下截图stop引用；补齐实际生命周期调用，断言保留。r02同28/28。
- r03：920/920；r04 App0W/E。独审发现并修复可读文字回退被图fallback覆盖、UI事件异常逃逸、填充错误遮蔽、待擦除bitmap空白复用、锁屏/退出提前中断及新增清理位于截图主try外。
- r05：922/923，唯一hide:true用例只改Gate.Hidden、未真正隐藏窗口，又忽略TryReopen拒绝。新Hidden守卫正确拒绝展开；补齐现有窗口端口对实际Hide/Show（含ShellLifecycle.Show的Gate.Disable）映射，不弱化断言。r07最终923/923；r06 App0W/E。

12份原始log/TRX均唯一预检/noclobber，原字节复制到精确-text新路径；父69份已提交原始回执保持。独审只读，无SDK/Actions/Windows执行。源码manifest、计数、保全与原失败详见[固定证据](../test-evidence/2026-10-15-overlay-image-source.json)。

真实WPF展开/测量/布局/像素释放/选择/阅读锚点/路由、截图/CaptureHost、OLE/COM/broker与所有权结算、UI/HTTP取消时限、B06/B12/B07和完整P2/P6均 **NotRun**。数据端口的内存数组归零不能证明WPF或系统副本已物理擦除；.NET字符串/系统缓冲/GC副本也不作绝对擦除承诺。

250/600ms、来源/密码/完整性/取消/清理门和生产离线复用Off保持；Core既有文件/工作流/锁文件/既有证据不改。所有dotnet前先export且命令前显式DOTNET_GENERATE_ASPNET_CERTIFICATE=false。证书存储/信任与旧覆盖前字节仍Unknown，不读改/恢复；Actions不查询不绕Forbidden、远端run数Unknown。无新依赖、模型、费用凭据权限协议、Windows/CMake实验、main/强推/部署。

# 分片 JSON 外壳与默认取消专项修复

监督方在精确提交 `838377b466424eef28027f1890b17dcaf5943af8` 指出：ChunkLimit 原先 192KiB，base64 后恰好 262144 字节，尚未计 type/transferId/index/data JSON 外壳。原 assembler/codec 分测没有证明最大合法分片可穿过完整消息管道。

以 `d898f097a89a547da5835ac988057c62bea03f57` 当前基线补完整帧→JSON→base64→assembler→Commit 回归，**修复前 1/1 失败**。证据在 `test-evidence/2026-10-11-chunk-envelope-before-fix.trx/json`；不删除原失败。

原始 chunk 降为 **190KiB = 194560 字节**，base64 最多 **259416 字节**，在 **256KiB = 262144 字节** 中余 **2728 字节**。当前规范外壳只有固定 type、36 字符 GUID、int 索引及 data，最长合法头可容纳；完整序列化后的实际 UTF-8 消息仍再次限界。总输入 **20MiB** 原上限不变。

新增 ImageChunkWire 写入与读取闭环，严格校验规范 ID/索引、未知/重复字段、base64 和解码后大小；失败即清除当前未完成重组。Native Messaging 是 stdio 二进制 UTF-8 JSON，不用于 HTML；编码器避免把 base64 '+' 转成 '\u002B' 导致膨胀。UTF-8 中文、合法 emoji 转义和实际负载字节长度均测试，协议不要求特定 Unicode 转义风格。

新增 12 项端到端/边界测试：原最大值闭环；零/加号密集/random 最大值；+1 发送零输出及接收拒绝；最长合法 GUID/max int 索引可穿过帧但非法顺序不 Commit；长 ID/索引溢出/多字节 type/额外字段拒绝；多字节额外头超实际消息上限；完整 20MiB 经108个有界 JSON chunk 后摘要 Commit。这里只验证协议字节，不宣称随机 fixture 是有效 PNG 或图像预处理已交付。

同时收紧新生产信封入口默认取消：在 Gate 身份锁内关联任务令牌与可选 caller token，避免取令牌后源被替换/释放。四项真实 HTTP 测试保持服务端响应屏障不释放，用 Off/Stop/Lock/换文实际取消等待中的 HTTP。低层传输仍可在测试中故意不传令牌，以强制旧 HTTP 错误真的到达并检查身份隔离。

最新本地全量 **134/134**（Core57、Provider77），11 项托管交叉构建/三探针打包与源摘要见 `test-evidence/2026-10-11-protocol-boundaries-summary.json`。保留旧故障、118项历史证据以及新测试 Unicode 转义假设失败。供监督方独审；不把开发者自查称作外部审查通过。

兼容矩阵按 SPEC 17.2 六核心应用/18视图恢复，包含 Obsidian 三视图，全部未运行；SumatraPDF 扩展不能替代核心。Windows 实测、生产手势/epoch/熔断/ACL、真实 NativeHost/扩展与 GitHub CI 仍未验收。

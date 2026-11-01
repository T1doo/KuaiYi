namespace KuaiYi.App;

// Fixed product labels only: never index configuration values or user content.
internal readonly record struct SettingsSearchHit(int PageIndex,string Title);
internal static class SettingsSearchCatalog
{
    internal const int MaximumQueryLength=96;
    private static readonly (string Title,string Keywords)[] entries=
    [
        ("常规","启用 开关 默认 Off On 启动 登录 语言"),
        ("API 与模型","服务 连接 地址 URL HTTP HTTPS API Key 密钥 认证 模型 流式 超时 代理 发送"),
        ("翻译风格","表达 模板 预设 领域 读者 忠实 通俗 专业 解释 提示词"),
        ("交互与浮窗","字号 字体 宽度 主题 深色 浅色 动画 减少动画 浮窗 位置 原文 显示 导入 导出"),
        ("快捷键","键盘 热键 截图 组合 冲突"),
        ("图片与集成","图片 图像 截图 OCR 视觉 浏览器 扩展 集成 发送 预览"),
        ("隐私与诊断","隐私 诊断 清理 日志 内容 权限 授权 数据")
    ];
    internal static string[] Titles()=>entries.Select(entry=>entry.Title).ToArray();
    internal static IReadOnlyList<SettingsSearchHit> Search(string? query)
    {
        if(string.IsNullOrWhiteSpace(query)||query.Length>MaximumQueryLength)return [];
        string[] terms=query.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
        var hits=new List<SettingsSearchHit>();
        for(int i=0;i<entries.Length;i++)
        {
            string labels=entries[i].Title+" "+entries[i].Keywords;
            if(terms.All(term=>labels.Contains(term,StringComparison.OrdinalIgnoreCase)))hits.Add(new(i,entries[i].Title));
        }
        return hits;
    }
}

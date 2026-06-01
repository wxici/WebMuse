using System.Text;
using System.Text.Json;
using WebRebuildRecorder.App.Core.Serialization;
using WebRebuildRecorder.App.Models;

namespace WebRebuildRecorder.App.Services;

public sealed class DomInteractionTargetCollector
{
    private static readonly JsonSerializerOptions JsonOptions = WrbJsonOptions.Default;

    private static readonly string[] DangerousKeywords =
    [
        "login", "signin", "sign-in", "register", "checkout", "cart", "payment", "pay", "buy", "purchase",
        "delete", "remove", "logout", "submit", "subscribe",
        "登录", "注册", "购物车", "结账", "支付", "购买", "删除", "移除", "退出", "提交", "订阅"
    ];

    private static readonly string[] HighValueKeywords =
    [
        "menu", "nav", "work", "works", "project", "projects", "case", "services", "approach", "about",
        "contact", "let's work together", "learn more", "view", "play", "theme", "dark", "light", "assistant", "chat",
        "菜单", "导航", "作品", "项目", "案例", "服务", "方法", "关于", "联系", "咨询", "了解更多", "播放", "主题", "切换", "助手", "聊天"
    ];

    private readonly AppLogger _logger;

    public DomInteractionTargetCollector(AppLogger logger)
    {
        _logger = logger;
    }

    public async Task<InteractionTargetCollectionResult> CollectAsync(
        IBrowserController? browser,
        Uri baseUri,
        CancellationToken cancellationToken = default)
    {
        if (browser is not IDomInteractionProvider provider)
        {
            return CreateUnavailableResult();
        }

        try
        {
            var rawTargets = await provider.CollectInteractionTargetsAsync(baseUri, cancellationToken);
            var targets = rawTargets
                .Where(IsUsableTarget)
                .Select((target, index) => NormalizeTarget(target, baseUri, index + 1))
                .OrderByDescending(target => target.Priority)
                .ThenBy(target => target.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new InteractionTargetCollectionResult
            {
                Status = InteractionTargetCollectionStatus.Available,
                Reason = "已采集网页 DOM 交互目标。",
                Targets = targets
            };
        }
        catch (Exception ex)
        {
            _logger.Error("采集网页交互目标失败。", ex);
            return new InteractionTargetCollectionResult
            {
                Status = InteractionTargetCollectionStatus.Failed,
                Reason = "采集网页交互目标失败，已降级为热点坐标观察。",
                Fallback = "hotspot-coordinate-observe"
            };
        }
    }

    public async Task SaveAsync(RebuildProject project, InteractionTargetCollectionResult result)
    {
        Directory.CreateDirectory(project.ActionsDirectory);

        await File.WriteAllTextAsync(
            Path.Combine(project.ActionsDirectory, "dom-summary.json"),
            JsonSerializer.Serialize(BuildDomSummary(result), JsonOptions),
            Encoding.UTF8);

        await File.WriteAllTextAsync(
            Path.Combine(project.ActionsDirectory, "interactive-targets.json"),
            JsonSerializer.Serialize(result, JsonOptions),
            Encoding.UTF8);

        await File.WriteAllTextAsync(
            Path.Combine(project.ActionsDirectory, "interactive-targets.md"),
            BuildMarkdown(result),
            Encoding.UTF8);
    }

    public static InteractionTargetCollectionResult CreateUnavailableResult()
    {
        return new InteractionTargetCollectionResult
        {
            Status = InteractionTargetCollectionStatus.Unavailable,
            Reason = "当前外部浏览器模式无法读取 DOM，请切换 Playwright/CDP/WebView2 模式以启用元素级交互采集。",
            Fallback = "hotspot-coordinate-observe"
        };
    }

    private static object BuildDomSummary(InteractionTargetCollectionResult result)
    {
        return new
        {
            status = result.Status,
            reason = result.Reason,
            fallback = result.Fallback,
            interactiveTargetCount = result.Targets.Count,
            generatedAt = DateTime.Now,
            targetsFile = "interactive-targets.json"
        };
    }

    private static bool IsUsableTarget(InteractionTarget target)
    {
        if (!target.Visible || !target.InViewport)
        {
            return false;
        }

        if (target.Rect.Width < 8 || target.Rect.Height < 8)
        {
            return false;
        }

        return true;
    }

    private static InteractionTarget NormalizeTarget(InteractionTarget target, Uri baseUri, int index)
    {
        target.Id = string.IsNullOrWhiteSpace(target.Id) ? $"target-{index:000}" : target.Id;
        target.Text = NormalizeText(target.Text);
        target.AriaLabel = NormalizeNullableText(target.AriaLabel);
        target.Role = NormalizeNullableText(target.Role);
        target.Title = NormalizeNullableText(target.Title);
        target.ClassName = NormalizeNullableText(target.ClassName);
        target.UrlKind = ClassifyUrl(target.Href, baseUri, out var sameOrigin);
        target.SameOrigin = sameOrigin;
        target.Priority = ScoreTarget(target);
        target.RecommendedAction = GetRecommendedAction(target);
        target.Reason = BuildReason(target);
        return target;
    }

    private static string ClassifyUrl(string? href, Uri baseUri, out bool sameOrigin)
    {
        sameOrigin = false;
        if (string.IsNullOrWhiteSpace(href))
        {
            return "unknown";
        }

        var text = href.Trim();
        var lower = text.ToLowerInvariant();
        if (lower.StartsWith("mailto:", StringComparison.Ordinal))
        {
            return "mailto";
        }

        if (lower.StartsWith("tel:", StringComparison.Ordinal))
        {
            return "tel";
        }

        if (text.StartsWith('#'))
        {
            sameOrigin = true;
            return "same-page-hash";
        }

        if (!Uri.TryCreate(baseUri, text, out var absolute))
        {
            return "unknown";
        }

        sameOrigin = string.Equals(absolute.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase);
        var targetText = $"{absolute.AbsolutePath} {absolute.Query} {absolute.Fragment}";

        if (ContainsDangerousKeyword(targetText))
        {
            return "danger";
        }

        if (PathHasDownloadExtension(absolute.AbsolutePath))
        {
            return "download";
        }

        if (ContainsKeyword(targetText, ["login", "signin", "sign-in", "register", "登录", "注册"]))
        {
            return "login";
        }

        if (ContainsKeyword(targetText, ["checkout", "payment", "pay", "cart", "purchase", "结账", "支付", "购物车", "购买"]))
        {
            return "payment";
        }

        return sameOrigin ? "same-origin-internal" : "external";
    }

    private static bool PathHasDownloadExtension(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension is ".zip" or ".rar" or ".7z" or ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx";
    }

    private static int ScoreTarget(InteractionTarget target)
    {
        var source = BuildSearchText(target);
        if (ContainsDangerousKeyword(source) || target.UrlKind is "danger" or "login" or "payment")
        {
            return -100;
        }

        var score = 0;
        if (IsCta(source))
        {
            score += 40;
        }

        if (ContainsKeyword(source, ["menu", "nav", "菜单", "导航"]))
        {
            score += 35;
        }

        if (ContainsKeyword(source, ["theme", "toggle", "dark", "light", "主题", "切换"]))
        {
            score += 30;
        }

        if (ContainsKeyword(source, ["project", "work", "works", "case", "项目", "作品", "案例"]))
        {
            score += 25;
        }

        if (target.SameOrigin)
        {
            score += 20;
        }

        if (target.Visible && target.InViewport)
        {
            score += 20;
        }

        if (target.Rect.Width >= 32 && target.Rect.Height >= 24)
        {
            score += 10;
        }

        if (target.UrlKind is "external" or "download" or "mailto" or "tel")
        {
            score -= 30;
        }

        if (target.TagName.Equals("button", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(target.Role, "button", StringComparison.OrdinalIgnoreCase))
        {
            score += 12;
        }

        if (ContainsKeyword(source, HighValueKeywords))
        {
            score += 15;
        }

        return score;
    }

    private static string GetRecommendedAction(InteractionTarget target)
    {
        if (target.Priority < 0)
        {
            return "skip";
        }

        if (target.UrlKind is "same-origin-internal" or "same-page-hash" &&
            !ContainsDangerousKeyword(BuildSearchText(target)))
        {
            return "hover-click";
        }

        if (string.Equals(target.Role, "button", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(target.TagName, "button", StringComparison.OrdinalIgnoreCase) ||
            BuildSearchText(target).Contains("aria-expanded", StringComparison.OrdinalIgnoreCase))
        {
            return "hover-click";
        }

        return "hover";
    }

    private static string BuildReason(InteractionTarget target)
    {
        var parts = new List<string>();
        if (IsCta(BuildSearchText(target))) parts.Add("CTA");
        if (target.SameOrigin) parts.Add("站内链接");
        if (target.UrlKind is "external") parts.Add("站外链接，仅悬停");
        if (ContainsDangerousKeyword(BuildSearchText(target))) parts.Add("包含危险动作关键词");
        if (string.Equals(target.Role, "button", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(target.TagName, "button", StringComparison.OrdinalIgnoreCase)) parts.Add("按钮");
        if (parts.Count == 0) parts.Add("可见交互元素");
        return string.Join(" / ", parts);
    }

    private static string BuildMarkdown(InteractionTargetCollectionResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# 网页交互目标清单");
        builder.AppendLine();
        builder.AppendLine("本文件记录程序在页面中识别到的链接、按钮、菜单、CTA、弹层入口等可交互元素。");
        builder.AppendLine();
        builder.AppendLine($"- 采集状态：{result.Status}");
        if (!string.IsNullOrWhiteSpace(result.Reason))
        {
            builder.AppendLine($"- 说明：{result.Reason}");
        }

        if (!string.IsNullOrWhiteSpace(result.Fallback))
        {
            builder.AppendLine($"- 降级方式：{result.Fallback}");
        }

        builder.AppendLine();

        if (result.Targets.Count == 0)
        {
            builder.AppendLine("本次未采集到可用交互目标。");
            return builder.ToString();
        }

        builder.AppendLine("| 序号 | 类型 | 文本 | 链接 | 可见 | 优先级 | 推荐动作 | 原因 |");
        builder.AppendLine("|---|---|---|---|---|---:|---|---|");
        for (var i = 0; i < result.Targets.Count; i++)
        {
            var target = result.Targets[i];
            builder.AppendLine($"| {i + 1} | {Escape(target.TagName)} | {Escape(GetDisplayText(target))} | {Escape(target.Href ?? "")} | {(target.Visible ? "是" : "否")} | {target.Priority} | {Escape(target.RecommendedAction)} | {Escape(target.Reason)} |");
        }

        return builder.ToString();
    }

    private static string BuildSearchText(InteractionTarget target)
    {
        return string.Join(' ', target.Text, target.AriaLabel, target.Role, target.Title, target.ClassName, target.Href, target.Cursor)
            .ToLowerInvariant();
    }

    private static string GetDisplayText(InteractionTarget target)
    {
        return string.IsNullOrWhiteSpace(target.Text)
            ? target.AriaLabel ?? target.Title ?? target.Role ?? target.TagName
            : target.Text;
    }

    private static bool IsCta(string text)
    {
        return ContainsKeyword(text, ["contact", "learn more", "let's work", "view", "start", "咨询", "联系", "了解更多", "开始"]);
    }

    private static bool ContainsDangerousKeyword(string text)
    {
        return ContainsKeyword(text, DangerousKeywords);
    }

    private static bool ContainsKeyword(string text, IEnumerable<string> keywords)
    {
        return keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeText(string? text)
    {
        return string.Join(' ', (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();
    }

    private static string? NormalizeNullableText(string? text)
    {
        var normalized = NormalizeText(text);
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string Escape(string text)
    {
        return text.Replace("|", "\\|", StringComparison.Ordinal);
    }
}

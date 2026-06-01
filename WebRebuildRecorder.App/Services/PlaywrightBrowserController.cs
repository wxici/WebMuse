using WebRebuildRecorder.App.Models;

namespace WebRebuildRecorder.App.Services;

// This class keeps the future Playwright controller name for interface compatibility.
// The current implementation does not inspect DOM. It only uses SendInput for visible
// mouse movement, hover pauses, optional safe hotspot clicks, and wheel scrolling.
public sealed class PlaywrightBrowserController : IBrowserController
{
    private readonly AppLogger _logger;
    private readonly ActionLogger _actionLogger;
    private readonly ExternalBrowserController _externalBrowserController;
    private readonly Random _random = new();
    private bool _paused;
    private bool _stopped;

    public PlaywrightBrowserController(AppLogger logger, ActionLogger actionLogger)
    {
        _logger = logger;
        _actionLogger = actionLogger;
        _externalBrowserController = new ExternalBrowserController(logger);
    }

    public async Task OpenAsync(string url, BrowserProfile profile)
    {
        await _externalBrowserController.OpenAsync(url, profile);
        _logger.Info("当前自动观察仅使用 SendInput 鼠标移动、hover 和滚轮滚动。");
    }

    public async Task StartAutoObserveAsync(ActionProfile profile, CancellationToken token)
    {
        _paused = false;
        _stopped = false;

        await _actionLogger.LogAsync("auto-observe-profile", "", "自动观察配置。", new
        {
            speedPreset = profile.SpeedPreset,
            observeMode = profile.ObserveMode,
            maxDurationSeconds = profile.AutoObserveDurationSeconds
        });

        if (string.Equals(profile.ObserveMode, ObserveModes.ManualLed, StringComparison.OrdinalIgnoreCase))
        {
            await _actionLogger.LogAsync("manual-led-observe", "", "人工主导模式：录屏保持运行，自动化不移动鼠标。");
            return;
        }

        await _actionLogger.LogAsync("page-load-wait", "", $"等待 {profile.PageLoadWaitMs}ms 让首屏加载。");
        await Task.Delay(profile.PageLoadWaitMs, token);
        await _actionLogger.LogAsync("auto-observe-start", "", $"自动观察开始：{profile.SpeedPreset}/{profile.ObserveMode}");

        var startedAt = DateTime.Now;
        if (profile.EnableDomTargetCollection && profile.RuntimeInteractionTargets.Count > 0)
        {
            await ObserveInteractionTargetsAsync(profile, startedAt, token);
        }
        else if (profile.EnableDomTargetCollection)
        {
            await _actionLogger.LogAsync(
                "dom-target-fallback",
                profile.RuntimeInteractionCollectionStatus,
                "DOM 交互目标不可用或为空，自动观察降级为热点坐标观察。",
                new
                {
                    fallback = "hotspot-coordinate-observe",
                    observeMode = profile.ObserveMode
                });
        }

        if (string.Equals(profile.ObserveMode, ObserveModes.HotspotPriority, StringComparison.OrdinalIgnoreCase))
        {
            await ObserveHotspotsAsync(profile, startedAt, token);
        }

        if (string.Equals(profile.ObserveMode, ObserveModes.QuickScan, StringComparison.OrdinalIgnoreCase)
            || string.Equals(profile.ObserveMode, ObserveModes.Standard, StringComparison.OrdinalIgnoreCase)
            || string.Equals(profile.ObserveMode, ObserveModes.HotspotPriority, StringComparison.OrdinalIgnoreCase))
        {
            await ScrollObserveAsync(profile, startedAt, token);
        }

        await _actionLogger.LogAsync("auto-observe-complete", "", "自动观察完成。");
    }

    public async Task PauseAutoObserveAsync()
    {
        _paused = true;
        await _actionLogger.LogAsync("manual-pause", "", "自动观察已暂停，进入人工控制。");
    }

    public async Task ResumeAutoObserveAsync()
    {
        _paused = false;
        await _actionLogger.LogAsync("manual-resume", "", "自动观察已恢复。");
    }

    public async Task StopAutoObserveAsync()
    {
        _stopped = true;
        await _actionLogger.LogAsync("automation-stop", "", "自动观察已停止。");
    }

    public Task<string?> GetCurrentUrlAsync() => Task.FromResult<string?>(null);

    public Task<string?> GetPageTitleAsync() => Task.FromResult<string?>(null);

    private async Task ObserveHotspotsAsync(ActionProfile profile, DateTime startedAt, CancellationToken token)
    {
        await Task.Delay(2000, token);

        foreach (var target in GetHotspotTargets())
        {
            if (ShouldStop(profile, startedAt, token))
            {
                return;
            }

            await WaitIfPausedAsync(token);
            await MoveAndHoverAsync(target, profile, token);

            if (profile.TryClickHotspots && IsSafeClickHotspot(target.Name))
            {
                NativeInputService.LeftClick();
                var wait = _random.Next(profile.ClickWaitMsMin, profile.ClickWaitMsMax + 1);
                await _actionLogger.LogAsync("hotspot-click", target.Name, "可选热点点击。", new
                {
                    waitMs = wait,
                    speedPreset = profile.SpeedPreset,
                    observeMode = profile.ObserveMode
                });
                await Task.Delay(wait, token);
            }
        }
    }

    private async Task ObserveInteractionTargetsAsync(ActionProfile profile, DateTime startedAt, CancellationToken token)
    {
        var targets = profile.RuntimeInteractionTargets
            .Where(target => target.Visible && target.InViewport && target.Priority > 0)
            .OrderByDescending(target => target.Priority)
            .Take(Math.Clamp(profile.MaxTargetsPerViewport, 1, 20))
            .ToList();

        foreach (var target in targets)
        {
            if (ShouldStop(profile, startedAt, token))
            {
                return;
            }

            await WaitIfPausedAsync(token);
            await MoveAndHoverAsync(TargetToScreenPoint(target), profile, token);
            await _actionLogger.LogAsync("hover-target", target.Id, "悬停网页交互目标。", new
            {
                targetId = target.Id,
                text = target.Text,
                href = target.Href,
                ariaLabel = target.AriaLabel,
                role = target.Role,
                priority = target.Priority,
                reason = target.Reason,
                speedPreset = profile.SpeedPreset,
                observeMode = profile.ObserveMode
            });

            if (profile.EnableSafeClick && string.Equals(target.RecommendedAction, "hover-click", StringComparison.OrdinalIgnoreCase))
            {
                NativeInputService.LeftClick();
                await _actionLogger.LogAsync("click-target", target.Id, "执行安全交互目标点击。", new
                {
                    targetId = target.Id,
                    text = target.Text,
                    href = target.Href,
                    safeClick = true
                });
                await Task.Delay(Math.Max(300, profile.AfterClickWaitMs), token);
            }
        }
    }

    private async Task ScrollObserveAsync(ActionProfile profile, DateTime startedAt, CancellationToken token)
    {
        var maxSteps = Math.Max(1, Math.Min(profile.MaxAutoScrollSteps, profile.MaxSamePageInteractions));
        var targets = GetObserveTargets(profile.ObserveMode);

        for (var i = 0; i < maxSteps && !ShouldStop(profile, startedAt, token); i++)
        {
            await WaitIfPausedAsync(token);
            await MoveAndHoverAsync(ApplyRandomOffset(targets[i % targets.Count]), profile, token);

            var distance = _random.Next(profile.ScrollDistancePxMin, profile.ScrollDistancePxMax + 1);
            var wheelEvents = _random.Next(2, 5);
            var wheelDelta = Math.Max(1, distance / 120) * -120;
            var perEventDelta = Math.Min(-120, wheelDelta / wheelEvents);
            for (var eventIndex = 0; eventIndex < wheelEvents; eventIndex++)
            {
                token.ThrowIfCancellationRequested();
                NativeInputService.ScrollWheel(perEventDelta);
                await Task.Delay(_random.Next(35, 110), token);
            }

            var pause = _random.Next(profile.ScrollPauseMsMin, profile.ScrollPauseMsMax + 1);
            await _actionLogger.LogAsync("scroll", "page", "滚轮滚动。", new
            {
                deltaY = distance,
                pauseMs = pause,
                wheelEvents,
                speedPreset = profile.SpeedPreset,
                observeMode = profile.ObserveMode
            });
            await Task.Delay(pause, token);
        }
    }

    private async Task MoveAndHoverAsync(ObserveTarget target, ActionProfile profile, CancellationToken token)
    {
        var moveDuration = _random.Next(profile.MouseMoveDurationMsMin, profile.MouseMoveDurationMsMax + 1);
        await NativeInputService.MoveMouseSmoothAsync(target.X, target.Y, moveDuration, token);
        await _actionLogger.LogAsync("mouse-move", target.Name, "移动鼠标观察目标区域。", new
        {
            x = target.X,
            y = target.Y,
            durationMs = moveDuration,
            speedPreset = profile.SpeedPreset,
            observeMode = profile.ObserveMode
        });

        var hoverDuration = _random.Next(profile.HoverDurationMsMin, profile.HoverDurationMsMax + 1);
        await _actionLogger.LogAsync("hover", target.Name, $"悬停 {hoverDuration}ms。", new
        {
            durationMs = hoverDuration,
            speedPreset = profile.SpeedPreset,
            observeMode = profile.ObserveMode
        });
        await Task.Delay(hoverDuration, token);
    }

    private async Task WaitIfPausedAsync(CancellationToken token)
    {
        while (_paused && !token.IsCancellationRequested && !_stopped)
        {
            await Task.Delay(250, token);
        }
    }

    private bool ShouldStop(ActionProfile profile, DateTime startedAt, CancellationToken token)
    {
        return token.IsCancellationRequested
            || _stopped
            || (DateTime.Now - startedAt).TotalSeconds >= profile.AutoObserveDurationSeconds;
    }

    private static List<ObserveTarget> GetHotspotTargets()
    {
        return
        [
            FromRatio("left-bottom-cta", 0.12, 0.88),
            FromRatio("right-top-dot", 0.92, 0.16),
            FromRatio("right-top-grid", 0.96, 0.16),
            FromRatio("right-floating-1", 0.97, 0.55),
            FromRatio("center-hero", 0.50, 0.50)
        ];
    }

    private static List<ObserveTarget> GetObserveTargets(string observeMode)
    {
        if (string.Equals(observeMode, ObserveModes.QuickScan, StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                FromRatio("center", 0.50, 0.52),
                FromRatio("right-top-menu", 0.92, 0.16),
                FromRatio("middle-image", 0.50, 0.62),
                FromRatio("bottom-action", 0.50, 0.82)
            ];
        }

        return
        [
            FromRatio("center", 0.50, 0.50),
            FromRatio("left-top-nav", 0.20, 0.16),
            FromRatio("right-top-menu", 0.90, 0.16),
            FromRatio("middle-image", 0.50, 0.62),
            FromRatio("bottom-action", 0.50, 0.84)
        ];
    }

    private static ObserveTarget FromRatio(string name, double xRatio, double yRatio)
    {
        var width = (int)System.Windows.SystemParameters.PrimaryScreenWidth;
        var height = (int)System.Windows.SystemParameters.PrimaryScreenHeight;
        return new ObserveTarget(
            name,
            Math.Clamp((int)Math.Round(width * xRatio), 20, Math.Max(20, width - 20)),
            Math.Clamp((int)Math.Round(height * yRatio), 20, Math.Max(20, height - 20)));
    }

    private ObserveTarget ApplyRandomOffset(ObserveTarget target)
    {
        var width = (int)System.Windows.SystemParameters.PrimaryScreenWidth;
        var height = (int)System.Windows.SystemParameters.PrimaryScreenHeight;
        var offsetRange = _random.Next(24, 64);
        var x = Math.Clamp(target.X + _random.Next(-offsetRange, offsetRange + 1), 20, Math.Max(20, width - 20));
        var y = Math.Clamp(target.Y + _random.Next(-offsetRange, offsetRange + 1), 20, Math.Max(20, height - 20));
        return target with { X = x, Y = y };
    }

    private static ObserveTarget TargetToScreenPoint(InteractionTarget target)
    {
        var width = (int)System.Windows.SystemParameters.PrimaryScreenWidth;
        var height = (int)System.Windows.SystemParameters.PrimaryScreenHeight;
        var x = Math.Clamp((int)Math.Round(target.Rect.CenterX), 20, Math.Max(20, width - 20));
        var y = Math.Clamp((int)Math.Round(target.Rect.CenterY), 20, Math.Max(20, height - 20));
        return new ObserveTarget(target.Id, x, y);
    }

    private static bool IsSafeClickHotspot(string name)
    {
        return name is "left-bottom-cta" or "right-top-dot" or "right-top-grid" or "right-floating-1";
    }

    private sealed record ObserveTarget(string Name, int X, int Y);
}

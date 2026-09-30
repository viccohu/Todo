using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace Memo;

internal static class PinnedWindowAnimation
{
    private sealed record Entry(Func<int> MinimumHeight, Action Adjusted);
    private static readonly Dictionary<Window, Entry> Windows = new();
    private static CancellationTokenSource? _animation;
    public static bool IsAnimating => _animation != null;

    public static void Register(Window window, Func<int> minimumHeight, Action adjusted)
    {
        Windows[window] = new Entry(minimumHeight, adjusted);
        window.Closed += (_, _) =>
        {
            _animation?.Cancel();
            Windows.Remove(window);
        };
    }

    private static Rectangle Bounds(Window window) => new(window.AppWindow.Position.X,
        window.AppWindow.Position.Y, window.AppWindow.Size.Width, window.AppWindow.Size.Height);

    private static Rectangle WorkArea(Window window)
    {
        var area = DisplayArea.GetFromWindowId(window.AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        return new Rectangle(area.X, area.Y, area.Width, area.Height);
    }

    private static void Apply(Window window, Rectangle bounds)
    {
        window.AppWindow.MoveAndResize(new RectInt32(bounds.X, bounds.Y, bounds.Width, bounds.Height));
        window.UpdatePinnedWindowGuard();
    }

    private static Rectangle Between(Rectangle start, Rectangle end, double progress) => new(
        start.X, (int)Math.Ceiling(start.Y + (end.Y - start.Y) * progress), start.Width,
        (int)Math.Floor(start.Height + (end.Height - start.Height) * progress));

    public static async Task<int?> ChangeHeightAsync(Window window, int height)
    {
        if (IsAnimating || !Windows.ContainsKey(window)) return null;
        using var cancellation = new CancellationTokenSource();
        _animation = cancellation;
        Window? lowerWindow = null;
        try
        {
            var start = Bounds(window);
            var lower = Rectangle.Empty;
            foreach (var candidate in Windows.Keys)
            {
                if (candidate == window) continue;
                var bounds = Bounds(candidate);
                if (bounds.Top >= start.Bottom && start.Right + PinnedWindowLayout.Gap > bounds.Left
                    && bounds.Right + PinnedWindowLayout.Gap > start.Left)
                {
                    lowerWindow = candidate;
                    lower = bounds;
                    break;
                }
            }
            var area = WorkArea(window);
            var target = PinnedWindowLayout.Expand(start, height, lower, area,
                lowerWindow == null ? area : WorkArea(lowerWindow),
                lowerWindow == null ? 40 : Windows[lowerWindow].MinimumHeight());
            var stopwatch = Stopwatch.StartNew();
            const double duration = 180;
            while (stopwatch.Elapsed.TotalMilliseconds < duration)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var fraction = Math.Min(1, stopwatch.Elapsed.TotalMilliseconds / duration);
                var eased = 1 - Math.Pow(1 - fraction, 3);
                // Move the lower window first, so expansion never paints over it.
                if (lowerWindow != null && lower != target.Lower)
                    Apply(lowerWindow, Between(lower, target.Lower, eased));
                Apply(window, Between(start, target.Upper, eased));
                await Task.Delay(16, cancellation.Token);
            }
            cancellation.Token.ThrowIfCancellationRequested();
            if (lowerWindow != null && lower != target.Lower)
            {
                Apply(lowerWindow, target.Lower);
                Windows[lowerWindow].Adjusted();
            }
            Apply(window, target.Upper);
            return target.Upper.Height;
        }
        catch (OperationCanceledException)
        {
            // A close can interrupt either participant; keep surviving windows'
            // saved size and collapsed state consistent with the displayed frame.
            if (lowerWindow != null && Windows.TryGetValue(lowerWindow, out var entry))
                entry.Adjusted();
            return Windows.ContainsKey(window) ? Bounds(window).Height : null;
        }
        finally { _animation = null; }
    }
}

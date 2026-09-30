using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Memo;

internal static class PinnedWindowLayout
{
    public const int Gap = 12;
    public const int SnapDistance = 10;
    public const int ResizeSnapReleaseDistance = 24;

    public static (Rectangle Upper, Rectangle Lower) Expand(Rectangle current, int height,
        Rectangle lower, Rectangle area, Rectangle lowerArea, int lowerMinimum)
    {
        var upper = new Rectangle(current.X, current.Y, current.Width,
            Math.Clamp(height, 40, Math.Max(40, area.Bottom - current.Top)));
        if (height <= current.Height || lower.IsEmpty || lower.Top < current.Bottom
            || current.Right + Gap <= lower.Left || lower.Right + Gap <= current.Left
            || upper.Bottom + Gap <= lower.Top)
            return (upper, lower);
        var minimum = Math.Min(lower.Height, lowerMinimum);
        var available = lowerArea.Bottom - current.Top - Gap - minimum;
        if (available < current.Height) return (current, lower);
        upper.Height = Math.Min(upper.Height, available);
        var y = upper.Bottom + Gap;
        return (upper, new Rectangle(lower.X, y, lower.Width,
            Math.Min(lower.Height, lowerArea.Bottom - y)));
    }

    private static bool CanSnapResize(int target, int requested, int previous)
    {
        // Keep an acquired edge stable, and catch small event-to-event jumps
        // across it. Absolute pointer intent still releases the edge naturally.
        var crossed = (previous < target && requested > target)
            || (previous > target && requested < target);
        var distance = previous == target || crossed ? ResizeSnapReleaseDistance : SnapDistance;
        return Math.Abs((long)target - requested) <= distance;
    }

    private static Rectangle Snap(Rectangle bounds, Rectangle area, IReadOnlyList<Rectangle> obstacles)
    {
        var xs = new List<int>();
        var ys = new List<int>();
        foreach (var other in obstacles)
        {
            xs.AddRange(new[] { other.Left, other.Right - bounds.Width,
                other.Left - bounds.Width - Gap, other.Right + Gap });
            ys.AddRange(new[] { other.Top, other.Bottom - bounds.Height,
                other.Top - bounds.Height - Gap, other.Bottom + Gap });
        }
        bool Fits(Rectangle candidate) => area.Contains(candidate) && obstacles.All(other =>
            candidate.Right + Gap <= other.Left || other.Right + Gap <= candidate.Left
            || candidate.Bottom + Gap <= other.Top || other.Bottom + Gap <= candidate.Top);
        var originalX = bounds.X;
        var originalY = bounds.Y;
        foreach (var x in xs.Distinct().Where(x => Math.Abs((long)x - originalX) <= SnapDistance)
                     .OrderBy(x => Math.Abs((long)x - originalX)))
        {
            var candidate = new Rectangle(x, bounds.Y, bounds.Width, bounds.Height);
            if (!Fits(candidate)) continue;
            bounds = candidate;
            break;
        }
        foreach (var y in ys.Distinct().Where(y => Math.Abs((long)y - originalY) <= SnapDistance)
                     .OrderBy(y => Math.Abs((long)y - originalY)))
        {
            var candidate = new Rectangle(bounds.X, y, bounds.Width, bounds.Height);
            if (!Fits(candidate)) continue;
            bounds = candidate;
            break;
        }
        return bounds;
    }

    public static Rectangle Resize(Rectangle proposed, Rectangle previous, Rectangle area,
        IReadOnlyList<Rectangle> obstacles, int minimumWidth, int minimumHeight, bool snap = false)
    {
        // Resizing never moves the origin. Resolve width first, then height;
        // an obstacle limits growth instead of moving the window around it.
        var maxWidth = area.Right - previous.X;
        var maxHeight = area.Bottom - previous.Y;
        foreach (var other in obstacles)
            if (other.Left >= previous.Right && previous.Top < other.Bottom + Gap
                && previous.Bottom + Gap > other.Top)
                maxWidth = Math.Min(maxWidth, other.Left - Gap - previous.Left);
        var minWidth = Math.Min(minimumWidth, previous.Width);
        var minHeight = Math.Min(minimumHeight, previous.Height);
        if (maxWidth < minWidth || maxHeight < minHeight) return previous;
        var width = Math.Clamp(proposed.Width, minWidth, maxWidth);
        foreach (var other in obstacles)
            if (other.Top >= previous.Bottom && previous.Left < other.Right + Gap
                && previous.Left + width + Gap > other.Left)
                maxHeight = Math.Min(maxHeight, other.Top - Gap - previous.Top);
        if (maxHeight < minHeight) return previous;
        var result = new Rectangle(previous.X, previous.Y, width,
            Math.Clamp(proposed.Height, minHeight, maxHeight));
        if (!snap) return result;
        bool Fits(Rectangle candidate) => area.Contains(candidate) && obstacles.All(other =>
            candidate.Right + Gap <= other.Left || other.Right + Gap <= candidate.Left
            || candidate.Bottom + Gap <= other.Top || other.Bottom + Gap <= candidate.Top);
        var widths = obstacles.SelectMany(other => new[] {
            other.Right - previous.Left, other.Left - previous.Left, other.Left - Gap - previous.Left });
        foreach (var target in widths.Distinct()
                     .Where(value => value >= minWidth && CanSnapResize(value, proposed.Width, previous.Width))
                     .OrderBy(value => Math.Abs((long)value - proposed.Width)))
        {
            var candidate = new Rectangle(result.X, result.Y, target, result.Height);
            if (!Fits(candidate)) continue;
            result = candidate;
            break;
        }
        var heights = obstacles.SelectMany(other => new[] {
            other.Bottom - previous.Top, other.Top - previous.Top, other.Top - Gap - previous.Top });
        foreach (var target in heights.Distinct()
                     .Where(value => value >= minHeight && CanSnapResize(value, proposed.Height, previous.Height))
                     .OrderBy(value => Math.Abs((long)value - proposed.Height)))
        {
            var candidate = new Rectangle(result.X, result.Y, result.Width, target);
            if (!Fits(candidate)) continue;
            result = candidate;
            break;
        }
        return result;
    }

    public static Rectangle Resolve(Rectangle proposed, Rectangle previous, Rectangle area,
        IReadOnlyList<Rectangle> obstacles)
    {
        var maxX = area.Right - proposed.Width;
        var maxY = area.Bottom - proposed.Height;
        if (maxX < area.Left || maxY < area.Top) return previous;
        proposed.X = Math.Clamp(proposed.X, area.Left, maxX);
        proposed.Y = Math.Clamp(proposed.Y, area.Top, maxY);
        bool Fits(Rectangle candidate) => obstacles.All(other =>
            (long)candidate.Right + Gap <= other.Left || (long)other.Right + Gap <= candidate.Left
            || (long)candidate.Bottom + Gap <= other.Top || (long)other.Bottom + Gap <= candidate.Top);
        if (Fits(proposed)) return Snap(proposed, area, obstacles);

        // A nearest legal position lies at a work-area or obstacle boundary.
        var xs = new List<int> { proposed.X, area.Left, maxX };
        var ys = new List<int> { proposed.Y, area.Top, maxY };
        foreach (var other in obstacles)
        {
            xs.Add(other.Left - proposed.Width - Gap);
            xs.Add(other.Right + Gap);
            ys.Add(other.Top - proposed.Height - Gap);
            ys.Add(other.Bottom + Gap);
        }
        var best = previous;
        var distance = long.MaxValue;
        foreach (var x in xs)
        foreach (var y in ys)
        {
            if (x < area.Left || x > maxX || y < area.Top || y > maxY) continue;
            var candidate = new Rectangle(x, y, proposed.Width, proposed.Height);
            if (!Fits(candidate)) continue;
            var dx = (long)x - proposed.X;
            var dy = (long)y - proposed.Y;
            var score = dx * dx + dy * dy;
            if (score >= distance) continue;
            best = candidate;
            distance = score;
        }
        return distance == long.MaxValue ? previous : Snap(best, area, obstacles);
    }
}

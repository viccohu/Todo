using System.Drawing;
using Memo;

internal static class PinnedWindowLayoutTests
{
    public static void Run()
    {
        var area = new Rectangle(0, 0, 1000, 800);
        var obstacle = new Rectangle(400, 300, 200, 200);
        var previous = new Rectangle(0, 0, 100, 100);
        void Check(Rectangle proposed, Rectangle expected, Rectangle? workArea = null)
        {
            var actual = PinnedWindowLayout.Resolve(proposed, previous, workArea ?? area, new[] { obstacle });
            if (actual != expected) throw new Exception($"Collision: {proposed} -> {actual}, expected {expected}");
        }
        Check(new Rectangle(100, 100, 100, 100), new Rectangle(100, 100, 100, 100));
        Check(new Rectangle(300, 320, 100, 100), new Rectangle(288, 320, 100, 100));
        Check(new Rectangle(590, 320, 100, 100), new Rectangle(612, 320, 100, 100));
        Check(new Rectangle(440, 205, 100, 100), new Rectangle(440, 188, 100, 100));
        Check(new Rectangle(440, 490, 100, 100), new Rectangle(440, 512, 100, 100));
        Check(new Rectangle(288, 320, 100, 100), new Rectangle(288, 320, 100, 100));
        // Edge alignment and adjacent spacing; outside the magnetic range releases.
        Check(new Rectangle(406, 600, 100, 100), new Rectangle(400, 600, 100, 100));
        Check(new Rectangle(494, 600, 100, 100), new Rectangle(500, 600, 100, 100));
        Check(new Rectangle(700, 306, 100, 100), new Rectangle(700, 300, 100, 100));
        Check(new Rectangle(700, 394, 100, 100), new Rectangle(700, 400, 100, 100));
        Check(new Rectangle(440, 519, 100, 100), new Rectangle(440, 512, 100, 100));
        Check(new Rectangle(411, 600, 100, 100), new Rectangle(411, 600, 100, 100));
        Check(new Rectangle(279, 320, 100, 100), new Rectangle(288, 320, 100, 100));
        // An off-screen alignment target must be ignored.
        Check(new Rectangle(495, 600, 100, 100), new Rectangle(495, 600, 100, 100),
            new Rectangle(0, 0, 595, 800));
        Check(new Rectangle(-30, 0, 100, 100), previous);
        // No room at the requested size: reject rather than overlap or go off-screen.
        Check(new Rectangle(0, 0, 1000, 800), previous);
        Check(new Rectangle(600, 320, 100, 100), new Rectangle(500, 188, 100, 100),
            new Rectangle(0, 0, 600, 800));
        var negativeArea = new Rectangle(-1000, -800, 1000, 800);
        var moved = new Rectangle(-900, -700, 100, 100);
        if (PinnedWindowLayout.Resolve(moved, previous, negativeArea, new[] { obstacle }) != moved)
            throw new Exception("Negative monitor coordinates changed");

        void CheckResize(Rectangle current, Rectangle requested, Rectangle expected, params Rectangle[] blockers)
        {
            var actual = PinnedWindowLayout.Resize(requested, current, area, blockers, 100, 100);
            if (actual != expected) throw new Exception($"Resize: {actual}, expected {expected}");
            if (actual.Location != current.Location) throw new Exception("Resize moved the window");
        }
        // Bottom/right screen edges limit size, never shift the origin.
        CheckResize(new(800, 600, 100, 100), new(800, 600, 500, 500), new(800, 600, 200, 200));
        // Stacked windows preserve their 12px gap without pushing either window.
        CheckResize(new(400, 100, 200, 100), new(400, 100, 200, 500), new(400, 100, 200, 188), obstacle);
        CheckResize(new(100, 300, 200, 200), new(100, 300, 600, 200), new(100, 300, 288, 200), obstacle);
        CheckResize(new(400, 100, 200, 188), new(400, 100, 200, 150), new(400, 100, 200, 150), obstacle);
        // Simultaneously grow towards a diagonal obstacle without jumping over it.
        CheckResize(new(100, 100, 100, 100), new(100, 100, 500, 500), new(100, 100, 500, 188), obstacle);
        void CheckResizeSnap(Rectangle current, Rectangle requested, Rectangle expected)
        {
            var actual = PinnedWindowLayout.Resize(requested, current, area, new[] { obstacle }, 100, 100, snap: true);
            if (actual != expected) throw new Exception($"Resize snap: {actual}, expected {expected}");
        }
        // Width/bottom edge alignment while stacked or side-by-side.
        CheckResizeSnap(new(400, 600, 150, 100), new(400, 600, 194, 100), new(400, 600, 200, 100));
        CheckResizeSnap(new(700, 300, 100, 150), new(700, 300, 100, 194), new(700, 300, 100, 200));
        // Snap to the gap before contact; never overlap or move the origin.
        CheckResizeSnap(new(400, 100, 200, 100), new(400, 100, 200, 182), new(400, 100, 200, 188));
        CheckResizeSnap(new(100, 300, 200, 200), new(100, 300, 282, 200), new(100, 300, 288, 200));
        // Absolute pointer intent leaves the snap even after repeated small events.
        CheckResizeSnap(new(400, 600, 200, 100), new(400, 600, 211, 100), new(400, 600, 200, 100));
        CheckResizeSnap(new(700, 300, 100, 200), new(700, 300, 100, 211), new(700, 300, 100, 200));
        CheckResizeSnap(new(400, 600, 200, 100), new(400, 600, 225, 100), new(400, 600, 225, 100));
        CheckResizeSnap(new(700, 300, 100, 200), new(700, 300, 100, 225), new(700, 300, 100, 225));
        // Fast pointer events skip the 10px capture band on either side.
        CheckResizeSnap(new(400, 600, 187, 100), new(400, 600, 213, 100), new(400, 600, 200, 100));
        CheckResizeSnap(new(400, 600, 213, 100), new(400, 600, 187, 100), new(400, 600, 200, 100));
        CheckResizeSnap(new(700, 300, 100, 187), new(700, 300, 100, 213), new(700, 300, 100, 200));
        // Do not capture a far-away edge just because a large jump crossed it.
        CheckResizeSnap(new(400, 600, 150, 100), new(400, 600, 250, 100), new(400, 600, 250, 100));

        var folded = new Rectangle(100, 100, 300, 40);
        void CheckExpand(int height, Rectangle lower, int minimum, Rectangle expectedUpper, Rectangle expectedLower)
        {
            var actual = PinnedWindowLayout.Expand(folded, height, lower, area, area, minimum);
            if (actual.Upper != expectedUpper || actual.Lower != expectedLower)
                throw new Exception($"Expansion layout: {actual}, expected {expectedUpper}, {expectedLower}");
        }
        CheckExpand(300, new(100, 180, 300, 250), 160, new(100, 100, 300, 300), new(100, 412, 300, 250));
        CheckExpand(300, new(100, 180, 300, 500), 160, new(100, 100, 300, 300), new(100, 412, 300, 388));
        CheckExpand(700, new(100, 180, 300, 500), 160, new(100, 100, 300, 528), new(100, 640, 300, 160));
        CheckExpand(700, new(100, 180, 300, 40), 40, new(100, 100, 300, 648), new(100, 760, 300, 40));
        CheckExpand(300, new(100, 600, 300, 160), 160, new(100, 100, 300, 300), new(100, 600, 300, 160));
        CheckExpand(300, new(500, 180, 300, 500), 160, new(100, 100, 300, 300), new(500, 180, 300, 500));
        CheckExpand(300, Rectangle.Empty, 40, new(100, 100, 300, 300), Rectangle.Empty);
        var collapsed = PinnedWindowLayout.Expand(new(100, 100, 300, 300), 40,
            new(100, 412, 300, 250), area, area, 160);
        if (collapsed.Upper != folded || collapsed.Lower != new Rectangle(100, 412, 300, 250))
            throw new Exception("Collapse must leave the lower window unchanged");
    }
}

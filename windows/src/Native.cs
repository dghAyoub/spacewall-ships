using System.Runtime.InteropServices;
using System.Text;
namespace SpaceWall;

[StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
[StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }

static class N
{
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    public const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
    public const long WS_POPUP = 0x80000000L, WS_CHILD = 0x40000000L, WS_VISIBLE = 0x10000000L, WS_OVERLAPPEDWINDOW = 0x00CF0000L,
        WS_CLIPSIBLINGS = 0x04000000L, WS_CLIPCHILDREN = 0x02000000L;
    public const long WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000, WS_EX_APPWINDOW = 0x40000;
    public const uint SWP_NOSIZE = 1, SWP_NOMOVE = 2, SWP_NOZORDER = 4, SWP_NOACTIVATE = 0x10, SWP_FRAMECHANGED = 0x20, SWP_SHOWWINDOW = 0x40;
    public static readonly IntPtr HWND_BOTTOM = (IntPtr)1;

    [DllImport("user32", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
    [DllImport("user32")] public static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32")] public static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32")] public static extern IntPtr GetParent(IntPtr h);
    [DllImport("user32")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int w, int hh, uint flags);
    [DllImport("user32", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr h, int idx);
    [DllImport("user32", EntryPoint = "SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr h, int idx, IntPtr v);
    [DllImport("user32")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
    [DllImport("user32", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32")] public static extern bool IsWindow(IntPtr h);
    [DllImport("user32")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32")] public static extern int MapWindowPoints(IntPtr from, IntPtr to, ref POINT p, int n);
    [DllImport("user32")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32")] public static extern bool RedrawWindow(IntPtr h, IntPtr rect, IntPtr rgn, uint flags);
    [DllImport("shell32")] public static extern int SHQueryUserNotificationState(out int state);

    public static string Cls(IntPtr h) { var sb = new StringBuilder(256); GetClassName(h, sb, 256); return sb.ToString(); }
    public static long Style(IntPtr h) => GetWindowLongPtr(h, GWL_STYLE).ToInt64();
    public static void SetStyle(IntPtr h, long v) => SetWindowLongPtr(h, GWL_STYLE, (IntPtr)unchecked((int)v));
    public static long ExStyle(IntPtr h) => GetWindowLongPtr(h, GWL_EXSTYLE).ToInt64();
    public static void SetExStyle(IntPtr h, long v) => SetWindowLongPtr(h, GWL_EXSTYLE, (IntPtr)v);
    public static string Rc(IntPtr h) { GetWindowRect(h, out var r); return $"[{r.L},{r.T} {r.R - r.L}x{r.B - r.T}]"; }

    // Window tree text (used in diagnostics so a failed attach still tells us what Explorer looks like)
    public static string Tree(IntPtr h, int depth, int max)
    {
        var sb = new StringBuilder();
        sb.Append(' ', depth * 2).Append($"{h.ToInt64():X} {Cls(h)} vis={IsWindowVisible(h)} {Rc(h)}\n");
        if (depth >= max) return sb.ToString();
        for (var c = GetWindow(h, 5); c != IntPtr.Zero; c = GetWindow(c, 2)) sb.Append(Tree(c, depth + 1, max));
        return sb.ToString();
    }
    public static string DesktopTree()
    {
        var sb = new StringBuilder();
        var progman = FindWindow("Progman", null);
        sb.Append(Tree(progman, 0, 3));
        EnumWindows((t, _) => { if (Cls(t) == "WorkerW") sb.Append(Tree(t, 0, 3)); return true; }, IntPtr.Zero);
        return sb.ToString();
    }
}

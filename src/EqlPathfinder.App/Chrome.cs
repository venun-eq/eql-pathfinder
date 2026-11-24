using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace EqlPathfinder.App;

/// <summary>
/// Pathfinder's own window frame: the app draws its title bar (WindowChrome), and Windows 11 is asked for a
/// dark frame, rounded corners and a quiet border to match.
/// </summary>
internal static class Chrome
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private const int DarkMode = 20, CornerPreference = 33, BorderColor = 34, Round = 2;

    public static void Apply(Window w)
    {
        w.SourceInitialized += (_, _) =>
        {
            var h = new WindowInteropHelper(w).Handle;
            int dark = 1, round = Round, border = 0x003C322A; // COLORREF 0x00BBGGRR for #2A323C
            // Older Windows ignores the attributes it doesn't know.
            DwmSetWindowAttribute(h, DarkMode, ref dark, sizeof(int));
            DwmSetWindowAttribute(h, CornerPreference, ref round, sizeof(int));
            DwmSetWindowAttribute(h, BorderColor, ref border, sizeof(int));
        };
    }

    /// <summary>"EQL PATHFINDER" in Cinzel with the letter spacing WPF can't do on its own.</summary>
    public static void FillWordmark(Panel host, double size = 13.5)
    {
        var font = new FontFamily(new Uri("pack://application:,,,/"), "./Assets/Fonts/#Cinzel, Palatino Linotype");
        var gold = (Brush)Application.Current.Resources["Gold"];
        foreach (char ch in "EQL PATHFINDER")
            host.Children.Add(new TextBlock
            {
                Text = ch == ' ' ? "" : ch.ToString(),
                Width = ch == ' ' ? size * 0.45 : double.NaN,
                FontFamily = font,
                FontWeight = FontWeights.Bold,
                FontSize = size,
                Foreground = gold,
                Margin = new Thickness(0, 0, size * 0.14, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
    }

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPLACEMENT { public int length, flags, showCmd; public POINT minPosition, maxPosition; public RECT normalPosition; }

    [DllImport("user32.dll")] private static extern bool GetWindowPlacement(IntPtr hwnd, ref WINDOWPLACEMENT placement);
    [DllImport("user32.dll")] private static extern bool SetWindowPlacement(IntPtr hwnd, ref WINDOWPLACEMENT placement);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromRect(ref RECT rect, uint flags);
    private const int ShowNormal = 1, ShowMinimized = 2, ShowMaximized = 3, ShowMinNoActive = 7, RestoreToMaximized = 2;
    private const uint NoZOrder = 0x0004, NoActivate = 0x0010, MonitorDefaultToNull = 0;

    /// <summary>
    /// Where the window is: its restored size and position and whether it's maximized, as Windows records them, plus
    /// where it actually sits on screen. A window Windows Snap placed (dragged to an edge or corner) reports its
    /// pre-snap size as "restored", so the on-screen rectangle is what brings it back to the snapped spot.
    /// </summary>
    public static WindowPlace? GetPlacement(Window w, WindowPlace? previous)
    {
        var h = new WindowInteropHelper(w).Handle;
        if (h == IntPtr.Zero) return null;
        var p = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
        if (!GetWindowPlacement(h, ref p)) return null;
        var r = p.normalPosition;
        if (r.Right - r.Left < 100 || r.Bottom - r.Top < 100) return null;
        bool max = p.showCmd == ShowMaximized || (p.showCmd == ShowMinimized && (p.flags & RestoreToMaximized) != 0);
        var place = new WindowPlace { Left = r.Left, Top = r.Top, Right = r.Right, Bottom = r.Bottom, Maximized = max };
        if (p.showCmd == ShowMinimized)
        {
            // Minimized windows sit off-screen; keep where it was before.
            place.ScreenLeft = previous?.ScreenLeft; place.ScreenTop = previous?.ScreenTop;
            place.ScreenRight = previous?.ScreenRight; place.ScreenBottom = previous?.ScreenBottom;
        }
        else if (!max && GetWindowRect(h, out var s) && s.Right - s.Left >= 100 && s.Bottom - s.Top >= 100)
        {
            place.ScreenLeft = s.Left; place.ScreenTop = s.Top; place.ScreenRight = s.Right; place.ScreenBottom = s.Bottom;
        }
        return place;
    }

    /// <summary>
    /// Puts the window back where it was. Call from SourceInitialized, before it shows. Windows moves it onto a
    /// screen if that monitor is gone, and keeps it on the monitor it was on, at that monitor's scaling.
    /// </summary>
    public static void SetPlacement(Window w, WindowPlace place, bool minimized)
    {
        var h = new WindowInteropHelper(w).Handle;
        if (h == IntPtr.Zero || place.Right - place.Left < 100 || place.Bottom - place.Top < 100) return;
        var p = new WINDOWPLACEMENT
        {
            length = Marshal.SizeOf<WINDOWPLACEMENT>(),
            flags = minimized && place.Maximized ? RestoreToMaximized : 0,
            showCmd = minimized ? ShowMinNoActive : place.Maximized ? ShowMaximized : ShowNormal,
            normalPosition = new RECT { Left = place.Left, Top = place.Top, Right = place.Right, Bottom = place.Bottom },
        };
        SetWindowPlacement(h, ref p);

        // Back to the exact spot it occupied (a snapped half or corner included), if that spot is still on a monitor.
        if (minimized || place.Maximized || place.ScreenLeft is not int sl || place.ScreenTop is not int st ||
            place.ScreenRight is not int sr || place.ScreenBottom is not int sb) return;
        var screen = new RECT { Left = sl, Top = st, Right = sr, Bottom = sb };
        if (sr - sl < 100 || sb - st < 100 || MonitorFromRect(ref screen, MonitorDefaultToNull) == IntPtr.Zero) return;
        SetWindowPos(h, IntPtr.Zero, sl, st, sr - sl, sb - st, NoZOrder | NoActivate);
    }

    /// <summary>A maximized custom-chrome window spills past the screen edge by the resize border; pull the content back in.</summary>
    public static Thickness MaximizedInset()
    {
        var b = SystemParameters.WindowResizeBorderThickness;
        return new Thickness(b.Left + 3, b.Top + 3, b.Right + 3, b.Bottom + 3);
    }
}

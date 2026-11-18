using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace MusicPlayer
{
    /// <summary>
    /// Windows taskbar button status: progress bar (green = playing, yellow = paused)
    /// and a small play / pause overlay icon on the program icon.
    /// Uses the ITaskbarList3 COM interface of Windows (Windows 7+), no extra package needed.
    /// All calls are ignored if the taskbar is not available.
    /// </summary>
    public sealed class TaskbarStatus : IDisposable
    {
        public enum State { None, Playing, Paused }

        private readonly IntPtr hwnd;
        private ITaskbarList3? taskbar;
        private readonly Icon playIcon;
        private readonly Icon pauseIcon;

        private State lastState = (State)(-1);
        private int lastPermille = -1;

        public TaskbarStatus(IntPtr windowHandle)
        {
            hwnd = windowHandle;
            playIcon = CreatePlayIcon();
            pauseIcon = CreatePauseIcon();
            try
            {
                taskbar = (ITaskbarList3)new TaskbarInstance();
                taskbar.HrInit();
            }
            catch
            {
                taskbar = null; // no taskbar (e.g. unusual shell) -> just do nothing
            }
        }

        /// <summary>Set state and progress (0..1). Calls Windows only when something visible changed.</summary>
        public void Update(State state, double progress)
        {
            if (taskbar == null)
                return;

            int permille = state == State.None ? 0 : (int)(Math.Clamp(progress, 0.0, 1.0) * 1000);
            if (state == lastState && permille == lastPermille)
                return;

            try
            {
                if (state != lastState)
                {
                    switch (state)
                    {
                        case State.Playing:
                            taskbar.SetProgressState(hwnd, TBPFLAG.TBPF_NORMAL);   // green
                            taskbar.SetOverlayIcon(hwnd, playIcon.Handle, "Playing");
                            break;
                        case State.Paused:
                            taskbar.SetProgressState(hwnd, TBPFLAG.TBPF_PAUSED);   // yellow
                            taskbar.SetOverlayIcon(hwnd, pauseIcon.Handle, "Paused");
                            break;
                        default:
                            taskbar.SetProgressState(hwnd, TBPFLAG.TBPF_NOPROGRESS);
                            taskbar.SetOverlayIcon(hwnd, IntPtr.Zero, null);
                            break;
                    }
                }

                if (state != State.None && (permille != lastPermille || state != lastState))
                    taskbar.SetProgressValue(hwnd, (ulong)permille, 1000);
            }
            catch
            {
                // the taskbar button may not exist yet / anymore: try again on the next call
                lastState = (State)(-1);
                lastPermille = -1;
                return;
            }

            lastState = state;
            lastPermille = permille;
        }

        public void Dispose()
        {
            try { Update(State.None, 0); } catch { }
            playIcon.Dispose();
            pauseIcon.Dispose();
            if (taskbar != null)
            {
                Marshal.ReleaseComObject(taskbar);
                taskbar = null;
            }
        }

        // ---------- overlay icons (drawn in code, 16x16) ----------

        private static Icon CreatePlayIcon()
        {
            return DrawIcon(g =>
            {
                using var brush = new SolidBrush(Color.White);
                g.FillPolygon(brush, new[] { new PointF(4.5f, 2.5f), new PointF(13.5f, 8f), new PointF(4.5f, 13.5f) });
            }, Color.FromArgb(0, 120, 215));
        }

        private static Icon CreatePauseIcon()
        {
            return DrawIcon(g =>
            {
                using var brush = new SolidBrush(Color.White);
                g.FillRectangle(brush, 4, 3, 3, 10);
                g.FillRectangle(brush, 9, 3, 3, 10);
            }, Color.FromArgb(220, 150, 0));
        }

        private static Icon DrawIcon(Action<Graphics> drawSymbol, Color background)
        {
            using var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (var bg = new SolidBrush(background))
                    g.FillEllipse(bg, 0, 0, 15, 15);
                drawSymbol(g);
            }
            IntPtr hIcon = bmp.GetHicon();
            // Icon.FromHandle does not own the handle: clone it, then free the original
            using var tmp = Icon.FromHandle(hIcon);
            var icon = (Icon)tmp.Clone();
            DestroyIcon(hIcon);
            return icon;
        }

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        // ---------- COM interop: ITaskbarList3 ----------

        private enum TBPFLAG
        {
            TBPF_NOPROGRESS = 0,
            TBPF_INDETERMINATE = 0x1,
            TBPF_NORMAL = 0x2,
            TBPF_ERROR = 0x4,
            TBPF_PAUSED = 0x8
        }

        [ComImport, Guid("56FDF344-FD6D-11d0-958A-006097C9A090"), ClassInterface(ClassInterfaceType.None)]
        private class TaskbarInstance { }

        [ComImport, Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ITaskbarList3
        {
            // ITaskbarList
            void HrInit();
            void AddTab(IntPtr hwnd);
            void DeleteTab(IntPtr hwnd);
            void ActivateTab(IntPtr hwnd);
            void SetActiveAlt(IntPtr hwnd);
            // ITaskbarList2
            void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);
            // ITaskbarList3
            void SetProgressValue(IntPtr hwnd, ulong ullCompleted, ulong ullTotal);
            void SetProgressState(IntPtr hwnd, TBPFLAG tbpFlags);
            void RegisterTab(IntPtr hwndTab, IntPtr hwndMDI);
            void UnregisterTab(IntPtr hwndTab);
            void SetTabOrder(IntPtr hwndTab, IntPtr hwndInsertBefore);
            void SetTabActive(IntPtr hwndTab, IntPtr hwndMDI, uint dwReserved);
            void ThumbBarAddButtons(IntPtr hwnd, uint cButtons, IntPtr pButton);
            void ThumbBarUpdateButtons(IntPtr hwnd, uint cButtons, IntPtr pButton);
            void ThumbBarSetImageList(IntPtr hwnd, IntPtr himl);
            void SetOverlayIcon(IntPtr hwnd, IntPtr hIcon, [MarshalAs(UnmanagedType.LPWStr)] string? pszDescription);
            void SetThumbnailTooltip(IntPtr hwnd, [MarshalAs(UnmanagedType.LPWStr)] string? pszTip);
            void SetThumbnailClip(IntPtr hwnd, IntPtr prcClip);
        }
    }
}

using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using Windows.UI.WindowManagement;
using WinRT;

namespace RainmeterWebhookMonitor
{
    public class SystemTray
    {
        // This struct tells the system how to display the tray icon and its settings
        // See: https://learn.microsoft.com/en-us/windows/win32/api/shellapi/ns-shellapi-notifyicondataw
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct NOTIFYICONDATAW
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public uint uVersion;  // Changed from union to direct field
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATAW lpData);

        [DllImport("user32.dll")]
        static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern IntPtr CreateIconFromResource(byte[] presbits, uint dwResSize, bool fIcon, uint dwVer);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr CreateWindowEx(
            uint dwExStyle,
            string lpClassName,
            string lpWindowName,
            uint dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam
        );

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        private const uint NIM_ADD = 0x00000000;
        private const uint NIF_MESSAGE = 0x00000001;
        private const uint NIF_ICON = 0x00000002;
        private const uint NIF_TIP = 0x00000004;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_TRAYICON = 0x800;
        private const int GWLP_WNDPROC = -4;
        private const uint NOTIFYICON_VERSION = 3;
        private const uint NIM_SETVERSION = 4;

        private NOTIFYICONDATAW notifyIcon;

        private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
        private WndProcDelegate newWndProc;
        IntPtr previousWndProc;

        // Default constructor
        public SystemTray(IntPtr hwnd, IntPtr? hIcon = null)
        {
            IntPtr trayHwnd = InitializeNotifyIcon(hwnd, hIcon);
            newWndProc = InitializeWndProc(trayHwnd);
        }

        private WndProcDelegate InitializeWndProc(IntPtr hwnd)
        {
            //Set up window message handling
            newWndProc = new WndProcDelegate(WndProc);

            // Clear the last error before calling GetWindowLongPtr. See why: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowlongptra
            Marshal.SetLastSystemError(0); 

            // Get the previous window procedure
            previousWndProc = SetWindowLongPtr(hwnd, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(newWndProc));

            // Check for errors. previousWndProc may have legitimately been zero previously (not just returned zero as error), so also need to check for non-zero last Win32 error
            if (previousWndProc == IntPtr.Zero && Marshal.GetLastWin32Error() != 0)
            {
                int errorCode = Marshal.GetLastWin32Error();

                Logging.WriteCrashLog(new Win32Exception(errorCode), "Occurred trying to call SetWindowLongPtr, with error code: " + errorCode);
                throw new Win32Exception(errorCode);
            }

            return newWndProc;
        }

        public IntPtr InitializeNotifyIcon(IntPtr hwnd, IntPtr? hIcon_Optional = null)
        {
            // Convert signature's default value of null to IntPtr.Zero
            // Can't simply set the default value in the signature as IntPtr.Zero because it is not a compile time constant
            IntPtr hIcon;
            if (hIcon_Optional == null)
                hIcon = IntPtr.Zero;
            else
                hIcon = (IntPtr)hIcon_Optional;

            notifyIcon = new NOTIFYICONDATAW
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATAW)),
                hWnd = hwnd,
                uID = 1,
                uFlags = NIF_ICON | NIF_MESSAGE | NIF_TIP,
                uCallbackMessage = WM_TRAYICON
            };

            // If hwnd is IntPtr.Zero, create a hidden 0x0 window to receive tray icon messages
            if (hwnd == IntPtr.Zero)
            {
                hwnd = CreateWindowEx(0, "STATIC", "RainmeterWebhookMonitor_SystemTray", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                notifyIcon.hWnd = hwnd;
            }

            // If no icon handle is provided, try to load it from the current process, otherwise use default app icon
            if (hIcon == IntPtr.Zero)
            {
                // Gets the path to the current process
                string? exePath = Environment.ProcessPath;
                if (exePath != null)
                {
                    Icon? appIcon = Icon.ExtractAssociatedIcon(exePath);
                    if (appIcon != null)
                    {
                        hIcon = appIcon.Handle;
                    }
                }
                // If it's still null, load the default icon
                if (hIcon == IntPtr.Zero)
                {
                    hIcon = LoadIcon(IntPtr.Zero, (IntPtr)32512); // IDI_APPLICATION
                }
            }

            // Set properties of the tray icon
            notifyIcon.szTip = "Rainmeter Webhook Monitor"; // Tooltip
            notifyIcon.hIcon = hIcon; // Icon

            // Add the icon
            if (!Shell_NotifyIcon(NIM_ADD, ref notifyIcon))
            {
                // Handle error
                var error = Marshal.GetLastWin32Error();
                Trace.WriteLine($"Failed to add tray icon. Error: {error}");
            }

            // Set version (required for reliable operation)
            notifyIcon.uVersion = NOTIFYICON_VERSION;
            Shell_NotifyIcon(NIM_SETVERSION, ref notifyIcon);

            return hwnd;
        }

        private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (msg == WM_TRAYICON)
                {
                    uint lparam = (uint)lParam.ToInt64();
                    // On left clicking the tray icon
                    if (lparam == WM_LBUTTONUP)
                    {
                        //RestoreFromTray();
                        return IntPtr.Zero;
                    }
                    // On right clicking the tray icon
                    else if (lparam == WM_RBUTTONUP)
                    {
                        CustomContextMenu.CreateAndShowMenu(hwnd);
                        return IntPtr.Zero;
                    }
                }
                return DefWindowProc(hwnd, msg, wParam, lParam);
            }
            catch(Exception ex)
            {
                // TODO: If it keeps happening, try having it re-register the window procedure
                Logging.WriteCrashLog(ex);

                if (Program.debugMode == true)
                {
                    Trace.WriteLine($"Error in WndProc: {ex.Message}");
                    NativeMessageBox.ShowErrorMessage("An error occurred. Please check the log for details.", "Error");
                }

                // For now throw exception to see what's going on
                throw;
            }
        }
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("EECDBF0E-BAE9-4CB6-A68E-9598E1CB57BB")]
    internal interface IWindowNative
    {
        IntPtr WindowHandle { get; }
    }
}
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
//using Windows.UI.WindowManagement;
//using WinRT;

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
            public NOTIFYICONDATAA_uFlags uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public uVersion uVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern bool Shell_NotifyIcon(NotifyIcon_dwMessage dwMessage, ref NOTIFYICONDATAW lpData);

        [DllImport("user32.dll")]
        static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, UIntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern IntPtr CreateIconFromResource(byte[] presbits, uint dwResSize, bool fIcon, uint dwVer);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern uint RegisterWindowMessage(string lpString);

        // Using this to pass on messages to the original default window procedure if not being processed by the custom one
        [DllImport("user32.dll")]
        static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint uMsg, UIntPtr wParam, IntPtr lParam);

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

        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_TRAYICON = 0x800;
        private const int GWLP_WNDPROC = -4;

        private NOTIFYICONDATAW notifyIcon;

        private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, UIntPtr wParam, IntPtr lParam);
        private WndProcDelegate newWndProc;
        IntPtr previousWndProc;

        // For re-creation of the tray icon after a taskbar restart
        private uint _taskbarCreatedMessageId;

        // Default constructor
        public SystemTray(IntPtr hwnd, IntPtr? hIcon = null)
        {
            RegisterTaskbarCreatedMessage();
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

        private void RegisterTaskbarCreatedMessage()
        {
            // Register the TaskbarCreated message
            _taskbarCreatedMessageId = RegisterWindowMessage("TaskbarCreated");
            if (_taskbarCreatedMessageId == 0)
            {
                // Handle error: Could not register the message
                int error = Marshal.GetLastWin32Error();
                Debug.WriteLine($"Failed to register TaskbarCreated message. Error: {error}");
                // Depending on requirements, you might want to throw or log more severely
            }
            else
            {
                Debug.WriteLine($"TaskbarCreated message registered with ID: {_taskbarCreatedMessageId}");
            }
        }

        public IntPtr InitializeNotifyIcon(IntPtr? hwndInput = null, IntPtr? hIcon_Optional = null)
        {
            // Convert signature's default value of null to IntPtr.Zero
            // Can't simply set the default value in the signature as IntPtr.Zero because it is not a compile time constant
            IntPtr hIcon;
            if (hIcon_Optional == null)
                hIcon = IntPtr.Zero;
            else
                hIcon = (IntPtr)hIcon_Optional;

            // If hwnd is IntPtr.Zero, create a hidden 0x0 window to receive tray icon messages
            IntPtr hwnd = hwndInput ?? IntPtr.Zero;
            if (hwndInput == IntPtr.Zero)
            {
                hwnd = CreateWindowEx(0, "STATIC", "RainmeterWebhookMonitor_SystemTray", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                notifyIcon.hWnd = hwnd;
            }

            notifyIcon = new NOTIFYICONDATAW
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATAW)),
                hWnd = hwnd,
                uID = 1,
                uFlags = NOTIFYICONDATAA_uFlags.NIF_ICON | NOTIFYICONDATAA_uFlags.NIF_MESSAGE | NOTIFYICONDATAA_uFlags.NIF_TIP | NOTIFYICONDATAA_uFlags.NIF_SHOWTIP,
                uCallbackMessage = WM_TRAYICON
            };

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

            // Add or Modify the icon
            // Use NIM_MODIFY if the icon might already exist (e.g., after TaskbarCreated), otherwise use NIM_ADD. NIM_ADD fails if the icon already exists.
            // A simple strategy is to try Add, and if it fails, try Modify. However, for the initial add, NIM_ADD is correct.
            if (Shell_NotifyIcon(NotifyIcon_dwMessage.NIM_ADD, ref notifyIcon))
            {
                notifyIcon.uVersion = uVersion.NOTIFYICON_VERSION_4; // Done after adding the icon
                if (!Shell_NotifyIcon(NotifyIcon_dwMessage.NIM_SETVERSION, ref notifyIcon))
                {
                    Debug.WriteLine($"Failed to set tray icon version. Error: {Marshal.GetLastWin32Error()}");
                }
            }
            else
            {
                int error = Marshal.GetLastWin32Error();
                // ERROR_TIMEOUT (1460) can occur if the taskbar isn't ready.
                // ERROR_OBJECT_NOT_FOUND (4312) might occur on NIM_MODIFY if icon doesn't exist.
                Trace.WriteLine($"Failed to add tray icon. Error: {error}");
                // Optionally try NIM_MODIFY here if add failed, though it shouldn't be needed on first run.
                // if (!Shell_NotifyIcon(WinEnums.NotifyIcon_dwMessage.NIM_MODIFY, ref notifyIcon)) { ... }
            }

            return hwnd;
        }

        private void RecreateNotifyIcon(IntPtr hwnd)
        {
            Debug.WriteLine("Taskbar created/restarted. Attempting to recreate tray icon.");

            // If the icon was previously visible (or should be), try adding it again.
            // The InitializeAndAddNotifyIcon handles the setup and NIM_ADD/NIM_SETVERSION logic.
            // We might need to NIM_DELETE first if NIM_ADD fails consistently after restart,
            //      but often just NIM_ADD/NIM_MODIFY after TaskbarCreated is sufficient. Let's retry the Add/SetVersion flow.

            // Remove the old icon first (best practice) - ignore errors as it might already be gone
            Shell_NotifyIcon(NotifyIcon_dwMessage.NIM_DELETE, ref notifyIcon);

            // Re-initialize and add the icon
            InitializeNotifyIcon(hwnd);
        }

        private IntPtr WndProc(IntPtr hwnd, uint msg, UIntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_TRAYICON)
            {

                uint mouseMessage = (uint)lParam.ToInt64() & 0xFFFF; // Extract lower word

                // On left clicking the tray icon
                if (mouseMessage == WM_LBUTTONUP)
                {
                    //RestoreFromTray();
                    return IntPtr.Zero;
                }
                // On right clicking the tray icon
                else if (mouseMessage == WM_RBUTTONUP)
                {
                    CustomContextMenu.CreateAndShowMenu(hwnd);
                    return IntPtr.Zero;
                }
            }
            else if (_taskbarCreatedMessageId != 0 && msg == _taskbarCreatedMessageId)
            {
                RecreateNotifyIcon(hwnd);
                // We handle this message, but it's often broadcast, so returning DefWindowProc might be safer than Zero to allow other apps to receive it too.
                // However, for handling *our* icon recreation, Zero is technically correct. Let's pass it on just in case.
                return CallWindowProc(previousWndProc, hwnd, (uint)msg, wParam, lParam); // Pass it on
            }
            return DefWindowProc(hwnd, msg, wParam, lParam);

        }
    }


    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("EECDBF0E-BAE9-4CB6-A68E-9598E1CB57BB")]
    internal interface IWindowNative
    {
        IntPtr WindowHandle { get; }
    }

    [Flags]
    public enum NOTIFYICONDATAA_uFlags : uint
    {
        NIF_MESSAGE = 0x00000001,
        NIF_ICON = 0x00000002,
        NIF_TIP = 0x00000004,
        NIF_STATE = 0x00000008,
        NIF_INFO = 0x00000010,
        NIF_GUID = 0x00000020,
        NIF_REALTIME = 0x00000040,
        NIF_SHOWTIP = 0x00000080
    }

    // For use in the NOTIFYICONDATA structure
    // See: https://learn.microsoft.com/en-us/windows/win32/api/shellapi/ns-shellapi-notifyicondataa
    // Values from shellapi.h
    public enum uVersion : uint
    {
        _zero = 0, // Not named but a possible value
        NOTIFYICON_VERSION = 3, // Use this or else the context menu will not work
        NOTIFYICON_VERSION_4 = 4
    }

    // See: https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shell_notifyiconw
    public enum NotifyIcon_dwMessage
    {
        NIM_ADD = 0x00000000,
        NIM_MODIFY = 0x00000001,
        NIM_DELETE = 0x00000002,
        NIM_SETFOCUS = 0x00000003,
        NIM_SETVERSION = 0x00000004
    }
}
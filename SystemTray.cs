using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using Windows.Foundation;
using System;
using WinRT;
using Microsoft.UI;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using Windows.UI.WindowManagement;
using System.Windows;

namespace RainmeterWebhookMonitor
{
    public class SysytemTray
    {
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

        [DllImport("user32.dll", SetLastError = true)]
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
        private const uint NIM_MODIFY = 0x00000001;
        private const uint NIM_DELETE = 0x00000002;
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
        private IntPtr hwnd;
        //private WindowId windowId;
        private AppWindow appWindow;
        private bool isMinimizedToTray = false;
        private WndProcDelegate newWndProc;
        private IntPtr defaultWndProc;

        public void InitializeContextMenu()
        {
            //this.InitializeComponent();

            //// Get the window handle
            //hwnd = GetWindowHandle();
            //windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            //appWindow = AppWindow.GetFromWindowId(windowId);

            // Initialize tray icon
            //InitializeNotifyIcon();

            // Set up window message handling
            //Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(() =>
            //{
            //    newWndProc = new WndProcDelegate(WndProc);
            //    defaultWndProc = GetWindowLongPtr(hwnd, GWLP_WNDPROC);
            //    SetWindowLongPtr(hwnd, GWLP_WNDPROC,
            //        Marshal.GetFunctionPointerForDelegate(newWndProc));
            //});
        }

        public IntPtr InitializeNotifyIcon(IntPtr hwnd)
        {
            notifyIcon = new NOTIFYICONDATAW();
            notifyIcon.cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATAW));
            notifyIcon.hWnd = hwnd;
            notifyIcon.uID = 1;
            notifyIcon.uFlags = NIF_ICON | NIF_MESSAGE | NIF_TIP;
            notifyIcon.uCallbackMessage = WM_TRAYICON;

            // If hwnd is IntPtr.Zero, create a hidden 0x0 window to receive tray icon messages
            if (hwnd == IntPtr.Zero)
            {
                hwnd = CreateWindowEx(0, "STATIC", "RainmeterWebhookMonitor_SystemTray", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                notifyIcon.hWnd = hwnd;
            }

            // Load default application icon
            IntPtr hIcon = LoadIcon(IntPtr.Zero, (IntPtr)32512); // IDI_APPLICATION
            notifyIcon.hIcon = hIcon;

            // Set tooltip
            notifyIcon.szTip = "My WinUI3 App";

            // Add the icon
            if (!Shell_NotifyIcon(NIM_ADD, ref notifyIcon))
            {
                // Handle error
                var error = Marshal.GetLastWin32Error();
                System.Diagnostics.Debug.WriteLine($"Failed to add tray icon. Error: {error}");
            }

            // Set version (required for reliable operation)
            notifyIcon.uVersion = NOTIFYICON_VERSION;
            Shell_NotifyIcon(NIM_SETVERSION, ref notifyIcon);

            return hwnd;
        }


        private void ExitApplication()
        {
            Shell_NotifyIcon(NIM_DELETE, ref notifyIcon);

            if (defaultWndProc != IntPtr.Zero)
            {
                SetWindowLongPtr(hwnd, GWLP_WNDPROC, defaultWndProc);
            }
            //Application.Current.Exit();
        }

        private delegate IntPtr WndProcDelegate(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        private const int WM_CLOSE = 0x0010;  // Add this to the constants


        //private void ShowTrayContextMenu()
        //{
        //    var menu = new MenuFlyout();
        //    var restoreItem = new MenuFlyoutItem { Text = "Restore" };
        //    restoreItem.Click += (s, e) => RestoreFromTray();
        //    var exitItem = new MenuFlyoutItem { Text = "Exit" };
        //    exitItem.Click += (s, e) => ExitApplication();

        //    menu.Items.Add(restoreItem);
        //    menu.Items.Add(exitItem);

        //    // Get cursor position
        //    GetCursorPos(out POINT mousePoint);

        //    // Get the window's content as XamlRoot
        //    var rootElement = Content as UIElement;
        //    if (rootElement != null)
        //    {
        //        menu.XamlRoot = rootElement.XamlRoot;

        //        // Convert screen coordinates to XamlRoot coordinates
        //        var transform = rootElement.TransformToVisual(null);
        //        var pointInApp = transform.TransformPoint(new Point(mousePoint.X, mousePoint.Y));

        //        menu.ShowAt(null, pointInApp);
        //        menu.ShouldConstrainToRootBounds = false;
        //        menu.Placement = FlyoutPlacementMode.Bottom;
        //    }
        //}


        private IntPtr GetWindowHandle()
        {
            var windowNative = this.As<IWindowNative>();
            return windowNative.WindowHandle;
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
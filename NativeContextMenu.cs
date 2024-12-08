using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RainmeterWebhookMonitor
{
    public class NativeContextMenu
    {
        // Win32 API constants
        private const uint TPM_LEFTALIGN = 0x0000;
        private const uint TPM_RETURNCMD = 0x0100;
        private const uint MF_STRING = 0x00000000;
        private const uint MF_SEPARATOR = 0x00000800;

        // Win32 API structures
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        // Win32 API functions
        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll")]
        private static extern bool InsertMenu(IntPtr hMenu, uint uPosition, uint uFlags, uint uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern uint TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y,
            int nReserved, IntPtr hwnd, IntPtr lprc);

        public static uint ShowContextMenu(IntPtr hwnd, MenuItem[] menuItems)
        {
            // Create the popup menu
            IntPtr hMenu = CreatePopupMenu();

            // Add menu items
            uint itemId = 1;
            foreach (var item in menuItems)
            {
                if (item.IsSeparator)
                {
                    InsertMenu(hMenu, itemId, MF_SEPARATOR, itemId, string.Empty);
                }
                else
                {
                    InsertMenu(hMenu, itemId, MF_STRING, itemId, item.Text);
                }
                itemId++;
            }

            // Get the current cursor position
            POINT pt;
            GetCursorPos(out pt);

            // Show the menu and get the clicked item
            uint clickedItem = TrackPopupMenu(hMenu, TPM_RETURNCMD | TPM_LEFTALIGN,
                pt.X, pt.Y, 0, hwnd, IntPtr.Zero);

            // Clean up
            DestroyMenu(hMenu);

            return clickedItem;
        }

        public class MenuItem
        {
            public string Text { get; set; }
            public bool IsSeparator { get; set; }

            public MenuItem(string text)
            {
                Text = text;
                IsSeparator = false;
            }

            public static MenuItem Separator()
            {
                return new MenuItem(string.Empty) { IsSeparator = true };
            }
        }
    }

    //public class TestMenu
    //{
    //    public static void Test()
    //    {
    //        var menuItems = new NativeContextMenu.MenuItem[]
    //        {
    //            new NativeContextMenu.MenuItem("Option 1"),
    //            new NativeContextMenu.MenuItem("Option 2"),
    //            NativeContextMenu.MenuItem.Separator(),
    //            new NativeContextMenu.MenuItem("Option 3")
    //        };

    //        // Show menu and get selection
    //        uint selected = NativeContextMenu.ShowContextMenu(yourWindowHandle, menuItems);
    //    }
    //}

    // Example usage:
    //public class Program
    //{
    //    [STAThread]
    //    public static void Main()
    //    {
    //        // Create a basic window to host the context menu
    //        var form = new NativeWindow();
    //        form.CreateHandle(new CreateParams());

    //        // Define menu items
    //        var menuItems = new NativeContextMenu.MenuItem[]
    //        {
    //        new NativeContextMenu.MenuItem("Copy"),
    //        new NativeContextMenu.MenuItem("Cut"),
    //        new NativeContextMenu.MenuItem("Paste"),
    //        NativeContextMenu.MenuItem.Separator(),
    //        new NativeContextMenu.MenuItem("Delete")
    //        };

    //        // Show the context menu and get the selected item
    //        uint selectedItem = NativeContextMenu.ShowContextMenu(form.Handle, menuItems);

    //        // Handle the selected item
    //        if (selectedItem > 0)
    //        {
    //            string selectedText = menuItems[selectedItem - 1].Text;
    //            Console.WriteLine($"Selected: {selectedText}");
    //        }
    //    }
    //}
}

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Forms;
using static RainmeterWebhookMonitor.NativeContextMenu;

namespace RainmeterWebhookMonitor
{
    public class NativeContextMenu
    {
        // Win32 API constants
        private const uint TPM_LEFTALIGN = 0x0000;
        private const uint TPM_RETURNCMD = 0x0100;
        private const uint MF_STRING = 0x00000000;
        private const uint MF_SEPARATOR = 0x00000800;
        private const uint TPM_RIGHTBUTTON = 0x0002;
        private const uint TPM_LEFTBUTTON = 0x0000;

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
        private static extern uint TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hwnd, IntPtr lprc);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

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

            // Get the current cursor position to display the menu at that location
            POINT pt;
            GetCursorPos(out pt);

            // This is necessary to ensure the menu will close when the user clicks elsewhere
            SetForegroundWindow(hwnd);

            // Tells the OS to show the context menu and wait for a selection. But if the user clicks elsewhere, it will return 0.
            uint flags = TPM_RIGHTBUTTON | TPM_LEFTBUTTON | TPM_RETURNCMD;
            uint clickedItem = TrackPopupMenu(hMenu, flags, pt.X, pt.Y, 0, hwnd, IntPtr.Zero);

            // Clean up
            DestroyMenu(hMenu);

            return clickedItem;
        }

        public class MenuItem
        {
            public string Text { get; set; }
            public bool IsSeparator { get; set; }
            public int Index { get; set; } // The index should be 1-based

            public MenuItem(string text, int index)
            {
                Text = text;
                IsSeparator = false;
                Index = index;
            }

            public static MenuItem Separator(int index)
            {
                return new MenuItem(string.Empty, index) { IsSeparator = true };
            }
        }

        public class MenuItemSet
        {
            private List<MenuItem> _menuItems = new List<MenuItem>();

            public void AddMenuItem(string text)
            {
                _menuItems.Add(new MenuItem(text, _menuItems.Count + 1)); // 1-based index because 0 is reserved for no selection
            }

            public void AddSeparator()
            {
                _menuItems.Add(MenuItem.Separator(_menuItems.Count + 1)); // 1-based index because 0 is reserved for no selection
            }

            public MenuItem[] GetMenuItems()
            {
                return _menuItems.ToArray();
            }

            public int GetMenuItemIndex_ByText(string text)
            {
                return _menuItems.FindIndex(x => x.Text == text);
            }

            public string? GetMenuItemText_ByIndex(int index)
            {
                return _menuItems.Find(x => x.Index == index)?.Text;
            }
        }
    }

    public class CustomContextMenu
    {
        private static class MenuItemNames
        {
            public const string OpenConfigFile = "Open Config File";
            public const string ReloadConfig = "Reload Config";
            public const string Help = "Help";
            public const string Exit = "Exit";
        }

        public static void CreateAndShowMenu(IntPtr hwnd)
        {
            var menuItemSet = new NativeContextMenu.MenuItemSet();
            menuItemSet.AddMenuItem(MenuItemNames.OpenConfigFile);
            menuItemSet.AddMenuItem(MenuItemNames.ReloadConfig);
            menuItemSet.AddMenuItem(MenuItemNames.Help);
            menuItemSet.AddSeparator();
            menuItemSet.AddMenuItem(MenuItemNames.Exit);

            // Show menu and get selection
            uint selected = NativeContextMenu.ShowContextMenu(hwnd, menuItemSet.GetMenuItems());

            // Handle the selected item
            if (selected > 0)
            {
                string? selectedText = menuItemSet.GetMenuItemText_ByIndex((int)selected);
                Console.WriteLine($"Selected: {selectedText}");

                //Call the appropriate function based on the selected menu item
                switch (selectedText)
                {
                    case MenuItemNames.OpenConfigFile:
                        OpenConfigFile();
                        break;
                    case MenuItemNames.ReloadConfig:
                        RestartApplication();
                        break;
                    case MenuItemNames.Exit:
                        ExitApplication();
                        break;
                    case null:
                        Console.WriteLine("Error: Selected item not found.");
                        break;
                    default:
                        Console.WriteLine("Error: Selected item not handled.");
                        break;
                }
            }
        }

        private static void ShowHelpWindowMessage()
        {
            MessageBox.Show("This is a help message.", "Help", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private static void OpenConfigFile()
        {
            string configFilePath = Path.Combine(Directory.GetCurrentDirectory(), Program.appConfigJsonName);
            try
            {
                Process.Start(new ProcessStartInfo(configFilePath) { UseShellExecute = true });
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // If there is no association, try opening with Notepad
                if (ex.NativeErrorCode == 1155) // ERROR_NO_ASSOCIATION
                {
                    Process.Start(new ProcessStartInfo("notepad.exe", configFilePath) { UseShellExecute = true });
                }
                else
                {
                    Console.WriteLine($"Error opening config file: {ex.Message}");
                }
            }
        }

        // General universal functions
        private static void RestartApplication()
        {
            // Restart the application
            string? executablePath = Environment.ProcessPath;
            if (executablePath == null)
            {
                Console.WriteLine("Error: Executable path not found.");
                return;
            }
            Process.Start(executablePath);
            Environment.Exit(0);
        }

        private static void ExitApplication()
        {
            // Logic to exit the application
            Console.WriteLine("Exiting application...");
            Environment.Exit(0);
        }

    }
}

using System;
using System.Windows.Forms;
using static System.Net.Mime.MediaTypeNames;

namespace FluxWebhookMonitor
{
    public class SystrayApplicationContext : ApplicationContext
    {
        private NotifyIcon trayIcon;

        public SystrayApplicationContext()
        {
            // Initialize Tray Icon
            trayIcon = new NotifyIcon()
            {
                Icon = System.Drawing.SystemIcons.Application,
                ContextMenuStrip = new ContextMenuStrip(),
                Visible = true
            };

            // Adding a basic menu item to the context menu
            trayIcon.ContextMenuStrip.Items.Add("Exit", null, OnExitClicked);
        }

        private void OnExitClicked(object sender, EventArgs e)
        {
            // Clean up the icon before the application exits.
            trayIcon.Visible = false;
            Application.Exit();
        }
    }
}

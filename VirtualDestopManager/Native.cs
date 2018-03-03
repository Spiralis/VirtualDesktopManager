using System.Runtime.InteropServices;

// ReSharper disable InconsistentNaming

namespace VirtualDesktopManager
{
    internal static class Native
    {
        private const uint SPI_SETDESKWALLPAPER = 0x0014;
        private const uint SPIF_SENDWININICHANGE = 2;
        private const uint SPIF_UPDATEINIFILE = 1;

        public static void SetBackground(string pictureFile)
        {
            SystemParametersInfo(
                SPI_SETDESKWALLPAPER,
                0,
                pictureFile,
                SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern int SystemParametersInfo(
            uint uAction,
            uint uParam,
            string lpvParam,
            uint fuWinIni);
    }
}
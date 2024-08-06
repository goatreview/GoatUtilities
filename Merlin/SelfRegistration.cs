using System.Runtime.InteropServices;

namespace Merlin
{


    public static class SelfRegistration
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessageTimeout(
            IntPtr hWnd,
            uint Msg,
            UIntPtr wParam,
            string lParam,
            uint fuFlags,
            uint uTimeout,
            out UIntPtr lpdwResult
        );

        private const int HWND_BROADCAST = 0xffff;
        private const uint WM_SETTINGCHANGE = 0x001A;
        private const uint SMTO_ABORTIFHUNG = 0x0002;

        public static void RegisterInPath()
        {
            string path = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? string.Empty;
            string appDir = AppDomain.CurrentDomain.BaseDirectory;

            if (!path.Contains(appDir))
            {
                path = path.TrimEnd(';') + ";" + appDir;
                Environment.SetEnvironmentVariable("PATH", path, EnvironmentVariableTarget.User);

                // Notify other processes of environment change
                UIntPtr result;
                SendMessageTimeout(
                    (IntPtr)HWND_BROADCAST,
                    WM_SETTINGCHANGE,
                    UIntPtr.Zero,
                    "Environment",
                    SMTO_ABORTIFHUNG,
                    5000,
                    out result
                );
            }
        }

        public static void UnregisterFromPath()
        {
            string path = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? string.Empty;
            string appDir = AppDomain.CurrentDomain.BaseDirectory;

            if (path.Contains(appDir))
            {
                path = path.Replace(appDir, "").Replace(";;", ";").TrimEnd(';');
                Environment.SetEnvironmentVariable("PATH", path, EnvironmentVariableTarget.User);

                // Notify other processes of environment change
                UIntPtr result;
                SendMessageTimeout(
                    (IntPtr)HWND_BROADCAST,
                    WM_SETTINGCHANGE,
                    UIntPtr.Zero,
                    "Environment",
                    SMTO_ABORTIFHUNG,
                    5000,
                    out result
                );
            }
        }
    }
}

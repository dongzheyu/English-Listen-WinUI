using System;

namespace English_Listen_WinUI.Helpers
{
    public static class OsVersionHelper
    {
        public static bool IsWindows11_24H2_OrLater()
        {
            try
            {
                var osVersion = Environment.OSVersion;
                if (osVersion.Platform == PlatformID.Win32NT && osVersion.Version.Major >= 10)
                {
                    return osVersion.Version.Build >= 26100;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsWindows11OrLater()
        {
            try
            {
                var osVersion = Environment.OSVersion;
                if (osVersion.Platform == PlatformID.Win32NT && osVersion.Version.Major >= 10)
                {
                    return osVersion.Version.Build >= 22000;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static string GetWindowsVersionInfo()
        {
            try
            {
                var osVersion = Environment.OSVersion;
                var build = osVersion.Version.Build;
                var major = osVersion.Version.Major;
                var minor = osVersion.Version.Minor;

                if (build >= 22000)
                {
                    var version = $"Windows 11 (Build {build})";
                    if (build >= 26100)
                        version += " 24H2+";
                    else if (build >= 22621)
                        version += " 22H2/23H2";
                    else
                        version += " 21H2";
                    return version;
                }
                else if (build >= 19041)
                {
                    return $"Windows 10 (Build {build})";
                }
                else
                {
                    return $"Windows {major}.{minor} (Build {build})";
                }
            }
            catch (Exception ex)
            {
                return $"Unknown ({ex.Message})";
            }
        }
    }
}
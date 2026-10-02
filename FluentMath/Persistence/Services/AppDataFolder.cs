using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Storage;

namespace FluentMath.Persistence.Services
{
    // where the json files live:
    // %LocalAppData%\FluentMath for the installer build, the package LocalState for the store build
    //
    // the store build asks the app model rather than building the path: msix redirects a write to
    // %LocalAppData% into the package, so the literal path works for the app but sends "Open" to an empty
    // folder in an Explorer outside it; LocalState is the one both agree on, and goes with an uninstall
    public static class AppDataFolder
    {
        // === win32 api imports ===

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder? packageFullName);

        private const int AppmodelErrorNoPackage = 15700; // no package identity


        // === fields ===

        private const string LocalFolderName = "FluentMath";


        // === public api ===

        public static string Resolve()
        {
            if (IsPackaged())
            {
                try
                {
                    return ApplicationData.Current.LocalFolder.Path;
                }
                catch { /* no app data for this identity; the per user folder below still keeps the state */ }
            }

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), LocalFolderName);
        }


        // === private helpers ===

        private static bool IsPackaged()
        {
            try
            {
                int length = 0;
                return GetCurrentPackageFullName(ref length, null) != AppmodelErrorNoPackage;
            }
            catch
            {
                // an api that cannot be reached at all means no package identity either
                return false;
            }
        }
    }
}

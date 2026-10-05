using System;
using System.IO;
using System.Runtime.InteropServices;

namespace FluentMath.Distribution
{
    // the distribution channel:
    // installer, portable or store, the one place to ask; installer and portable are the GitHub downloads and
    // differ only in where the state lives
    public static class AppDistribution
    {
        // === win32 api imports ===

        // DllImport, not LibraryImport; the generator cannot marshal the optional null buffer the probe needs
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);

        private const int AppmodelErrorNoPackage = 15700; // no package identity


        // === fields ===

        // portable mode: this marker next to the exe moves the state from %LocalAppData% into the app
        // folder; (only in the portable zip)
        public const string PortableMarkerFileName = "portable.txt";

        private static readonly Lazy<bool> _isPackaged = new Lazy<bool>(DetectPackaged);
        private static readonly Lazy<bool> _isPortableBuild = new Lazy<bool>(DetectPortableBuild);


        // === public api ===

        public static bool IsPackaged => _isPackaged.Value;

        public static bool IsPortableBuild => _isPortableBuild.Value;


        // === private helpers ===

        // a zero length buffer probes the identity: ERROR_INSUFFICIENT_BUFFER when packaged, APPMODEL_ERROR_NO_PACKAGE
        // otherwise; (Package.Current would throw on every unpackaged start)
        private static bool DetectPackaged()
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

        // a packaged build never counts, its install folder is read only
        private static bool DetectPortableBuild()
        {
            if (IsPackaged) return false;

            try
            {
                string? appFolder = Path.GetDirectoryName(Environment.ProcessPath);
                if (string.IsNullOrEmpty(appFolder)) return false;

                return File.Exists(Path.Combine(appFolder, PortableMarkerFileName));
            }
            catch
            {
                // an unreadable app folder counts as installed (%LocalAppData%)
                return false;
            }
        }
    }
}
